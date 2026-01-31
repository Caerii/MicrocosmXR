using UnityEngine;
using Fusion.XR.Shared.Rig;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
#if OCULUS_SDK_AVAILABLE
    /**
     * In addition to parent OVRHandRepresentationManager tasks, HardwareOVRHandRepresentationManager handles:
     * * - handles which collider is used for grabbing based on hand mode
     */
    public class HardwareOVRHandRepresentationManager : OVRHandRepresentationManager
    {

        OVRHand ovrHand;
        HardwareHand hardwareHand;

        [Header("Grabbing Colliders")]
        [SerializeField]
        Collider controllerModeGrabCollider;
        [SerializeField]
        Collider fingerTrackingModeGrabCollider;

#region OVRHandRepresentationManager implementations
        public override bool IsFingerDataValid => ovrHand && ovrHand.IsDataValid;

        public override RigPart Side => hardwareHand.side;
        public override GameObject FingerTrackingHandSkeletonParentGameObject => ovrHand.gameObject;
        public override GameObject ControllerTrackingHandSkeletonParentGameObject => gameObject;
#endregion

        protected override void Awake()
        {
            base.Awake();
            if (fingerTrackingModeGrabCollider == null)
            {
                Debug.LogError("fingerTrackingModeGrabCollider should be manually specified");
                // Fallback attempt
                var go = transform.FindChildRecursive("OVRHandCollider");
                if (go == null) go = transform.FindChildRecursive("FingerTrackingHandCollider");
                if (go) fingerTrackingModeGrabCollider = go.GetComponentInChildren<Collider>();
            }
            if (controllerModeGrabCollider == null)
            {
                Debug.LogError("controllerModeGrabCollider should be manually specified");
                // Fallback attempt
                var go = transform.FindChildRecursive("ControllerCollider");
                if (go == null) go = transform.FindChildRecursive("ControllerTrackingCollider");
                if (go) controllerModeGrabCollider = go.GetComponentInChildren<Collider>();
            }

            if (hardwareHand == null) hardwareHand = GetComponentInChildren<HardwareHand>();
            if (ovrHand == null) ovrHand = GetComponentInChildren<OVRHand>();
            if (fingerTrackingHandMeshRenderer == null && ovrHand) fingerTrackingHandMeshRenderer = ovrHand.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        protected override void Update()
        {
            base.Update();
            UpdateHandCollidersEnabled();
        }

        public void UpdateHandCollidersEnabled()
        {
            if (fingerTrackingModeGrabCollider && controllerModeGrabCollider)
            {
                if (fingerTrackingModeGrabCollider) 
                    fingerTrackingModeGrabCollider.enabled = IsFingerDataValid;

                if (controllerModeGrabCollider)
                    controllerModeGrabCollider.enabled = IsFingerDataValid == false;
            }
        }
    }
#else
    public class HardwareOVRHandRepresentationManager : MonoBehaviour {}
#endif
}
