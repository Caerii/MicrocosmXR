using Fusion;
using UnityEngine;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
#if OCULUS_SDK_AVAILABLE

    /**
     * Use networked var to share an HandState obtained from the local user HardwareOVRHandCollecter
     * 
     * Implements various OVRMesh and OVRSkeleton interfaces to animate the skin mesh based on the hand state
     */
    public class UncrompressedRemoteOVRHand : NetworkBehaviour, OVRSkeleton.IOVRSkeletonDataProvider,
    OVRSkeletonRenderer.IOVRSkeletonRendererDataProvider,
    OVRMesh.IOVRMeshDataProvider,
    OVRMeshRenderer.IOVRMeshRendererDataProvider
    {
        [Networked]
        NetworkBool IsDataValid { get; set; }
        [Networked]
        NetworkBool IsDataHighConfidence { get; set; }
        [Networked]
        float HandScale { get; set; }
        // Can be replaced by short, short, short
        [Networked, Capacity(24)]
        NetworkArray<Quaternion> BoneRotations { get; }

        public OVRHand.Hand handType = OVRHand.Hand.None;

        public HandState handState = new HandState();
        public bool IsLocalHand => Object && Object.HasStateAuthority;
        HardwareOVRHandCollecter localHardwareHandStateCollecter;

        void FindLocalHardwareHand()
        {
            foreach (var hand in FindObjectsOfType<HardwareOVRHandCollecter>())
            {
                if (hand.OVRSkeletonType == GetSkeletonType())
                {
                    localHardwareHandStateCollecter = hand;
                    break;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            StoreHandStateInNetworkData();
        }

        public override void Render()
        {
            base.Render();
            if (IsLocalHand)
            {
                if (localHardwareHandStateCollecter == null && handType != OVRHand.Hand.None)
                {
                    FindLocalHardwareHand();
                }

                if (localHardwareHandStateCollecter)
                {
                    localHardwareHandStateCollecter.CollectHardwareHandState(OVRPlugin.Step.Render, ref handState);
                }
            }
            else
            {
                ConvertNetworkedDataToHandState(ref handState);
            }
        }

#region Local hand state save
        // Store the latest hand state (obtained from the localHardwareHandStateCollecter during Render) in networked vars
        void StoreHandStateInNetworkData()
        {
            if (IsLocalHand)
            {
                // Fill network var with collected data 
                IsDataValid = handState.isDataValid;
                IsDataHighConfidence = handState.isDataHighConfidence;
                HandScale = handState.handScale;

                if (handState.boneRotations != null)
                {
                    for (int i = 0; i < handState.boneRotations.Length && i < BoneRotations.Length; i++)
                    {
                        //TODO Test difference (to limit data transfer) or refresh not everytime
                        BoneRotations.Set(i, handState.boneRotations[i]);
                    }
                }
            }
        }
#endregion

#region Remote hand state loading
        // Read the network vars current state to update the hand state (using in the various interfaces used by the OVRMesh / OVRSkeleton components)
        void ConvertNetworkedDataToHandState(ref HandState handState)
        {
            handState.isDataValid = IsDataValid;
            handState.isDataHighConfidence = IsDataHighConfidence;
            handState.handScale = HandScale;

            if (handState.boneRotations == null || handState.boneRotations.Length != BoneRotations.Length)
            {
                handState.boneRotations = new Quaternion[BoneRotations.Length];
            }
            for (int i = 0; i < BoneRotations.Length; i++)
            {
                handState.boneRotations[i] = BoneRotations[i];
            }
        }
#endregion

#region OVRSkeleton.IOVRSkeletonDataProvider
        public OVRSkeleton.SkeletonPoseData GetSkeletonPoseData()
        {
            var data = new OVRSkeleton.SkeletonPoseData();
            data.BoneRotations = new OVRPlugin.Quatf[handState.boneRotations == null ? 0 : handState.boneRotations.Length];
            data.IsDataValid = handState.isDataValid;
            if (handState.isDataValid)
            {
                //  data.RootPose = new OVRPlugin.Posef { Position = handState.rootPosePosition.ToVector3f(), Orientation = handState.rootPoseRotation.ToQuatf() };
                data.RootPose = new OVRPlugin.Posef { Position = Vector3.zero.ToVector3f(), Orientation = Quaternion.identity.ToQuatf() };
                data.RootScale = handState.handScale;
                for (int i = 0; i < data.BoneRotations.Length; i++) data.BoneRotations[i] = handState.boneRotations[i].ToQuatf();
                data.IsDataHighConfidence = handState.isDataHighConfidence;
            }

            return data;
        }

        public OVRSkeleton.SkeletonType GetSkeletonType()
        {
            switch (handType)
            {
                case OVRHand.Hand.HandLeft:
                    return OVRSkeleton.SkeletonType.HandLeft;
                case OVRHand.Hand.HandRight:
                    return OVRSkeleton.SkeletonType.HandRight;
                case OVRHand.Hand.None:
                default:
                    return OVRSkeleton.SkeletonType.None;
            }
        }
#endregion

#region OVRSkeletonRenderer.IOVRSkeletonRendererDataProvider
        public OVRSkeletonRenderer.SkeletonRendererData GetSkeletonRendererData()
        {
            var data = new OVRSkeletonRenderer.SkeletonRendererData();

            data.IsDataValid = handState.isDataValid;
            if (handState.isDataValid)
            {
                data.RootScale = handState.handScale;
                data.IsDataHighConfidence = handState.isDataHighConfidence;
                data.ShouldUseSystemGestureMaterial = false;
            }

            return data;
        }
#endregion


#region OVRMesh.IOVRMeshDataProvider
        public OVRMesh.MeshType GetMeshType()
        {
            switch (handType)
            {
                case OVRHand.Hand.None:
                    return OVRMesh.MeshType.None;
                case OVRHand.Hand.HandLeft:
                    return OVRMesh.MeshType.HandLeft;
                case OVRHand.Hand.HandRight:
                    return OVRMesh.MeshType.HandRight;
                default:
                    return OVRMesh.MeshType.None;
            }
        }
#endregion

#region OVRMeshRenderer.IOVRMeshRendererDataProvider
        public OVRMeshRenderer.MeshRendererData GetMeshRendererData()
        {
            var data = new OVRMeshRenderer.MeshRendererData();

            data.IsDataValid = handState.isDataValid;
            if (handState.isDataValid)
            {
                data.IsDataHighConfidence = handState.isDataHighConfidence;
                data.ShouldUseSystemGestureMaterial = false;
            }
            return data;
        }
#endregion
    }
#else
    public class UncrompressedRemoteOVRHand : NetworkBehaviour {}
#endif
}
