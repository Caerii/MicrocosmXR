using Fusion;
using Fusion.Tools;
using UnityEngine;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
#if OCULUS_SDK_AVAILABLE
    /**
     * Use networked var to share an HandState obtained from the local user HardwareOVRHandCollecter
     * 
     * Implements various OVRMesh and OVRSkeleton interfaces to animate the skin mesh based on the hand state
     */
    public class RemoteOVRHand : NetworkBehaviour, OVRSkeleton.IOVRSkeletonDataProvider,
    OVRSkeletonRenderer.IOVRSkeletonRendererDataProvider,
    OVRMesh.IOVRMeshDataProvider,
    OVRMeshRenderer.IOVRMeshRendererDataProvider
    {
        [Networked]
        public NetworkBool IsDataValid { get; set; }

        [Networked]
        public NetworkBool IsDataHighConfidence { get; set; }

        [Networked]
        public float HandScale { get; set; }


        const int BONE_DATA_SIZE = 16 * 24;          // the size should match handSynchronizationScriptable.BoneInfoByteSize, that you can see in the scriptable inspector in the total byte size field (only existing in the Editor)

        [Networked, Capacity(BONE_DATA_SIZE)]
        NetworkArray<byte> CompressedBonesRotations { get; }

        public OVRHand.Hand handType = OVRHand.Hand.None;

        public bool IsLocalHand => Object && Object.HasStateAuthority;
        HardwareOVRHandCollecter _localHardwareHandStateCollecter;

        [SerializeField] private HandSynchronizationScriptable handSynchronizationScriptable;

        ChangeDetector changeDetector;

        HardwareOVRHandCollecter LocalHardwareHandStateCollecter
        {
            get
            {
                if (_localHardwareHandStateCollecter == null && IsLocalHand && handType != OVRHand.Hand.None)
                {
                    _localHardwareHandStateCollecter = FindLocalHardwareHand();
                }
                return _localHardwareHandStateCollecter;
            }
        }

        /*
         * Full precision hand state (bone rotations are quaternion) that is used to answer the various hand representation callbacks to display the hand
         * On the local user, it is read from the local HardwareOVRHandCollecter.
         * On the remote user, it is recovered from the compressed network data through ConvertNetworkedDataToHandState 
         */
        public HandState handState = new HandState();

        /*
         * On local user, used to receive the HardwareOVRHandCollecter compressed hand state with its CollectCompressedHandState method, before sharing it to the network variables (which store this compressed version).
         * On the remote user, used to map the network data to a compressed hand state, which will be uncompressed to the uncompressed (full precision quaternion) handState
         */
        public CompressedHandState compressedHandState = new CompressedHandState();

        public TimedRingbuffer<HandState> handStateInterpolationRingBuffer;
        HardwareOVRHandCollecter FindLocalHardwareHand()
        {
            foreach (var hand in FindObjectsOfType<HardwareOVRHandCollecter>())
            {
                if (hand.OVRSkeletonType == GetSkeletonType())
                {
                    return hand;
                }
            }
            return null;
        }

        private void Awake()
        {
            if (handSynchronizationScriptable == null) Debug.LogError("handSynchronizationScriptable has not been set");
            handStateInterpolationRingBuffer = new TimedRingbuffer<HandState>(handSynchronizationScriptable == null? 0: handSynchronizationScriptable.storedInterpolationStates);
        }

        public override void Spawned()
        {
            base.Spawned();
            changeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            // The local player save the local HandState into the Networked variables (bones rotation are compressed)
            StoreHandStateInNetworkData();
        }

        public override void Render()
        {
            base.Render();
            if (IsLocalHand)
            {
                CollectLocalHandState();
            }
            else
            {
                // Hands of remote players are updated with compressed Networked data
                ParseNetworkData();
            }
        }

        void CollectLocalHandState()
        {
            if (LocalHardwareHandStateCollecter)
            {
                // Save the actual OVR hand skeleton datas into the local HandState (no compression)
                LocalHardwareHandStateCollecter.CollectHardwareHandState(OVRPlugin.Step.Render, ref handState);

                // Debug mode (to see locally the effect of the compression used
                if (handSynchronizationScriptable && handSynchronizationScriptable.debugRemoteRenderingLocally)
                {
                    StoreHandStateInNetworkData();
                    ParseNetworkData();
                }
            }
        }

        void ParseNetworkData(bool forceRefresh = false)
        {
            bool handRefreshNeeded = forceRefresh || (handSynchronizationScriptable && handSynchronizationScriptable.uncompressingBoneDataOnNetworkChangesOnly == false);
            foreach (var _ in changeDetector.DetectChanges(this))
            {
                // If any of the network var changed, we have to update the hand
                handRefreshNeeded = true;
                break;
            }
            if (handRefreshNeeded)
            {
                // Store network data into compressed hand state and then uncompress it to handState
                ConvertNetworkedDataToHandState(ref handState);
                if (IsInterpolationDataNeeded())
                {
                    handStateInterpolationRingBuffer.Add(handState, Time.time);
                }
            }
            InterpolateBonesRotations();           
        }

        bool IsInterpolationDataNeeded()
        {
            if (handSynchronizationScriptable == null) return false;
            return handSynchronizationScriptable.interpolateBoneRotations;
        }

        // return the eventual lerping progress value
        void InterpolateBonesRotations()
        {
            if (IsInterpolationDataNeeded() == false) return;

            InterpolationInfo<HandState> interpolationInfo = handStateInterpolationRingBuffer.InterpolateInfo(Time.time - handSynchronizationScriptable.interpolationDelay);

            switch (interpolationInfo.status)
            {
                case InterpolationStatus.InvalidBoundaries:
                    // No data, no adaptation
                    return;
                case InterpolationStatus.ValidFrom:
                    // No end boundary (time requested too recent, no data received)
                    handState.CopyValuesFrom(interpolationInfo.from);
                    return;
                case InterpolationStatus.ValidTo:
                    // No start boundary (time requested too far in the past)
                    handState.CopyValuesFrom(interpolationInfo.to);
                    return;
            }

            // Here, status == InterpolationStatus.ValidFromTo: from and to are valid hand states

            var previousHandState = interpolationInfo.from;
            var latestReceivedHandState = interpolationInfo.to;

            if (latestReceivedHandState.isDataValid && previousHandState.isDataValid && latestReceivedHandState.boneRotations != null && previousHandState.boneRotations != null && latestReceivedHandState.boneRotations.Length == previousHandState.boneRotations.Length)
            {
                handState.CopyValuesFrom(latestReceivedHandState);

                for (int i = 0; i < handState.boneRotations.Length; i++)
                {
                    var ovrBoneId = (OVRSkeleton.BoneId)i;
                    var handStateBoneId = ovrBoneId.AsHandSynchronizationBoneId();
                    if (handSynchronizationScriptable.boneInfoByBoneId.ContainsKey(handStateBoneId))
                    {
                        // Slerp the rotation change between from / to
                        handState.boneRotations[i] = Quaternion.Slerp(previousHandState.boneRotations[i], latestReceivedHandState.boneRotations[i], interpolationInfo.alpha);
                    }
                }
            }
        }

#region Local hand state save

        bool sizeInformationDisplayed = false;

        float lastEmissionTime = 0;

        // Store the latest hand state (obtained from the localHardwareHandStateCollecter during the FixedUpdateNetwork) in networked vars
        void StoreHandStateInNetworkData()
        {
            if (LocalHardwareHandStateCollecter)
            {
                if (handSynchronizationScriptable && handSynchronizationScriptable.limitHandChangeEmissionPersecond && lastEmissionTime != 0 && (Time.time - lastEmissionTime) < handSynchronizationScriptable.handChangeEmissionMinDelay)
                {
                    // Limiting emission change
                    return;
                }
                lastEmissionTime = Time.time;

                // save a compressed HandState according the OVR hand skeleton datas and the bone configuration specified into the HandSynchronizationScriptable scriptable object
                LocalHardwareHandStateCollecter.CollectCompressedHandState(ref compressedHandState);

                // Update the simple Network variables
                IsDataValid = compressedHandState.isDataValid;
                IsDataHighConfidence = compressedHandState.isDataHighConfidence;
                HandScale = compressedHandState.handScale;


                // Update the NetworkArray of bones' rotation
                if (compressedHandState.bonesRotations.Length > CompressedBonesRotations.Length)
                {
                    Debug.LogError("The CompressedBonesRotations capacity has to be greater than the total byte size requested by the compress bone structure described in the scritable !");
                    return;
                }

                if (compressedHandState.bonesRotations.Length != CompressedBonesRotations.Length && sizeInformationDisplayed == false)
                {
                    Debug.LogWarning("To avoid to waste data, the CompressedBonesRotations capacity has to be equal to the total byte size requested by the compress bone structure described in the scritable !");
                    sizeInformationDisplayed = true;
                }

                for (int i = 0; i < compressedHandState.bonesRotations.Length; i++)
                {
                    CompressedBonesRotations.Set(i, compressedHandState.bonesRotations[i]);
                }
            }
        }
#endregion


#region Remote hand state loading
        // Read the network vars current state to update the hand state (using in the various interfaces used by the OVRMesh / OVRSkeleton components)
        void ConvertNetworkedDataToHandState(ref HandState handState)
        {
            compressedHandState.isDataValid = IsDataValid;
            compressedHandState.isDataHighConfidence = IsDataHighConfidence;
            compressedHandState.handScale = HandScale;

            if (handSynchronizationScriptable && IsDataValid)
            {
                if (compressedHandState.bonesRotations == null || compressedHandState.bonesRotations.Length != CompressedBonesRotations.Length)
                {
                    compressedHandState.bonesRotations = new byte[CompressedBonesRotations.Length];
                }

                for (int i = 0; i < CompressedBonesRotations.Length; i++)
                {
                    compressedHandState.bonesRotations[i] = CompressedBonesRotations[i];
                }
            }
            compressedHandState.UncompressToHandState(ref handState, handSynchronizationScriptable.bonesInfo);
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
                //data.RootPose = new OVRPlugin.Posef { Position = handState.rootPosePosition.ToVector3f(), Orientation = handState.rootPoseRotation.ToQuatf() };
                data.RootPose = new OVRPlugin.Posef { Position = Vector3.zero.ToVector3f(), Orientation = Quaternion.identity.ToQuatf() };
                data.RootScale = handState.handScale;
                for (int i = 0; i < handState.boneRotations.Length; i++) data.BoneRotations[i] = handState.boneRotations[i].ToQuatf();
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
    public class RemoteOVRHand : NetworkBehaviour {}
#endif
}
