using UnityEngine;
using Fusion.XR.Shared.Grabbing;
using Fusion.XR.Shared.Rig;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
#if OCULUS_SDK_AVAILABLE

    /**
     * Centralize finger/controller input collecting
     * 
     * Collect OVR data to fill an HandState on request
     * 
     * Forwards to the HardwareHand (and so the the Grabber) finger pinching grabbing
     */
    public class HardwareOVRHandCollecter : MonoBehaviour
    {
        public OVRHand ovrHand;
        public HardwareHand hand;
        public Grabber grabber;
        public OVRSkeleton ovrSkeleton;

        #region Hand input status API (wraps data from several sources)
        public bool IsUsingFingerTracking => ovrHand.IsDataValid;
        public bool IsPinchingIndex => IsUsingFingerTracking && ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        public bool IsHoveringGrabbable => grabber.hoveredGrabbables.Count > 0;
        public bool IsUsingTrigger => hand.handCommand.triggerCommand >= 0.5f;
        public RigPart Side => hand.side;
        public bool IsControllerInHand => OVRPlugin.GetControllerIsInHand(OVRPlugin.Step.Render, Side == RigPart.LeftController ? OVRPlugin.Node.ControllerLeft : OVRPlugin.Node.ControllerRight);
        #endregion

        #region OVR info
        public OVRSkeleton.SkeletonType OVRSkeletonType => ((OVRSkeleton.IOVRSkeletonDataProvider)ovrHand).GetSkeletonType();
        public OVRHand.Hand OVRHandType => OVRSkeletonType == OVRSkeleton.SkeletonType.HandLeft ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight;
        OVRSkeleton.SkeletonPoseData _skeletonData;
        public OVRSkeleton.SkeletonPoseData SkeletonData
        {
            get
            {
                RefreshSkeletonData();
                return _skeletonData;
            }
        }
        #endregion

        void RefreshSkeletonData(bool forceRefresh = false)
        {
            if (skeletonDataCollectedForThisFrame == false || forceRefresh || ovrHand.IsDataValid != _skeletonData.IsDataValid)
            {
                var skeletonDataProvider = ((OVRSkeleton.IOVRSkeletonDataProvider)ovrHand);
                _skeletonData = skeletonDataProvider.GetSkeletonPoseData();
                skeletonDataCollectedForThisFrame = true;
            }
        }

        bool fingerGrabbingDisableForThisFrame = false;
        bool skeletonDataCollectedForThisFrame = false;

        [SerializeField] private HandSynchronizationScriptable handSynchronizationScriptable;

        private void Awake()
        {
            if (hand == null) hand = GetComponentInChildren<HardwareHand>();
            if (grabber == null) grabber = GetComponentInChildren<Grabber>();
            if (ovrHand == null) ovrHand = GetComponentInChildren<OVRHand>();
            if (ovrSkeleton == null) ovrSkeleton = GetComponentInChildren<OVRSkeleton>();
            if (handSynchronizationScriptable == null) Debug.LogError("handSynchronizationScriptable has not been set");
        }

        private void Update()
        {
            if (hand == null || ovrHand == null) return;

            if (IsUsingFingerTracking)
            {
                // Finger tracking
                hand.updateGrabWithAction = false;
                hand.isGrabbing = IsPinchingIndex && fingerGrabbingDisableForThisFrame == false;
            }
            else
            {
                hand.updateGrabWithAction = true;
            }
        }

        private void LateUpdate()
        {
            fingerGrabbingDisableForThisFrame = false;
            skeletonDataCollectedForThisFrame = false;
        }

        // Call this to warn the hand collecter that finger tracking is used for something else than grabboing, and so that finger-pinch grabbing should be disabled
        public void FingerPinchUsedThisFrame()
        {
            fingerGrabbingDisableForThisFrame = true;
        }


        #region LocalizationAsset hand state capture

        // Return an HandState based on the actual OVR hand skeleton datas
        public void CollectHardwareHandState(OVRPlugin.Step step, ref HandState handState)
        {
            var skeletonData = SkeletonData;
            handState.isDataValid = ovrHand.IsDataValid;
            handState.isDataHighConfidence = ovrHand.IsDataHighConfidence;
            handState.handScale = ovrHand.HandScale;

            if (skeletonData.BoneRotations != null)
            {
                if (handState.boneRotations == null || handState.boneRotations.Length != skeletonData.BoneRotations.Length)
                {
                    handState.boneRotations = new Quaternion[skeletonData.BoneRotations.Length];
                }
                for (int i = 0; i < skeletonData.BoneRotations.Length; i++)
                {
                    handState.boneRotations[i] = skeletonData.BoneRotations[i].FromQuatf();
                }
            }
        }

        // CollectCompressedHandState returns a compressed HandState computed according the OVR hand skeleton datas and the bone configuration specified into the HandSynchronizationScriptable scriptable object.
        public void CollectCompressedHandState(ref CompressedHandState compressHandState)
        {
            if (handSynchronizationScriptable && compressHandState.bonesRotations.Length != handSynchronizationScriptable.BoneInfoByteSize)
            {
                compressHandState.bonesRotations = new byte[handSynchronizationScriptable.BoneInfoByteSize];
            }

            var skeletonData = SkeletonData;
            compressHandState.isDataValid = ovrHand.IsDataValid;
            compressHandState.isDataHighConfidence = ovrHand.IsDataHighConfidence;
            compressHandState.handScale = ovrHand.HandScale;

            if (handSynchronizationScriptable)
            {
                compressHandState.FillRotationsWithOVRSkeleton(skeletonData, handSynchronizationScriptable.bonesInfo);
            }
            else
            {
                throw new System.Exception("handSynchronizationScriptable is required to provide compressed hand state");
            }
        }


        #endregion
    }
#else
    public class HardwareOVRHandCollecter : MonoBehaviour{}
#endif
}
