#if OCULUS_SDK_AVAILABLE

namespace Fusion.Addons.MetaOVRHandsSynchronization
{

    public static class HandSStateMetaExtensions
    {
        // AsHandSynchronizationBoneId returns the HandSynchronizationBoneId corresponding to the OVRSkeleton.BoneId parameter
        public static HandSynchronizationBoneId AsHandSynchronizationBoneId(this OVRSkeleton.BoneId source)
        {
            switch (source)
            {
                case OVRSkeleton.BoneId.Hand_WristRoot: return HandSynchronizationBoneId.Hand_WristRoot;
                case OVRSkeleton.BoneId.Hand_ForearmStub: return HandSynchronizationBoneId.Hand_ForearmStub;
                case OVRSkeleton.BoneId.Hand_Thumb0: return HandSynchronizationBoneId.Hand_Thumb0;
                case OVRSkeleton.BoneId.Hand_Thumb1: return HandSynchronizationBoneId.Hand_Thumb1;
                case OVRSkeleton.BoneId.Hand_Thumb2: return HandSynchronizationBoneId.Hand_Thumb2;
                case OVRSkeleton.BoneId.Hand_Thumb3: return HandSynchronizationBoneId.Hand_Thumb3;
                case OVRSkeleton.BoneId.Hand_Index1: return HandSynchronizationBoneId.Hand_Index1;
                case OVRSkeleton.BoneId.Hand_Index2: return HandSynchronizationBoneId.Hand_Index2;
                case OVRSkeleton.BoneId.Hand_Index3: return HandSynchronizationBoneId.Hand_Index3;
                case OVRSkeleton.BoneId.Hand_Middle1: return HandSynchronizationBoneId.Hand_Middle1;
                case OVRSkeleton.BoneId.Hand_Middle2: return HandSynchronizationBoneId.Hand_Middle2;
                case OVRSkeleton.BoneId.Hand_Middle3: return HandSynchronizationBoneId.Hand_Middle3;
                case OVRSkeleton.BoneId.Hand_Ring1: return HandSynchronizationBoneId.Hand_Ring1;
                case OVRSkeleton.BoneId.Hand_Ring2: return HandSynchronizationBoneId.Hand_Ring2;
                case OVRSkeleton.BoneId.Hand_Ring3: return HandSynchronizationBoneId.Hand_Ring3;
                case OVRSkeleton.BoneId.Hand_Pinky0: return HandSynchronizationBoneId.Hand_Pinky0;
                case OVRSkeleton.BoneId.Hand_Pinky1: return HandSynchronizationBoneId.Hand_Pinky1;
                case OVRSkeleton.BoneId.Hand_Pinky2: return HandSynchronizationBoneId.Hand_Pinky2;
                case OVRSkeleton.BoneId.Hand_Pinky3: return HandSynchronizationBoneId.Hand_Pinky3;
                case OVRSkeleton.BoneId.Hand_ThumbTip: return HandSynchronizationBoneId.Hand_ThumbTip;
                case OVRSkeleton.BoneId.Hand_IndexTip: return HandSynchronizationBoneId.Hand_IndexTip;
                case OVRSkeleton.BoneId.Hand_MiddleTip: return HandSynchronizationBoneId.Hand_MiddleTip;
                case OVRSkeleton.BoneId.Hand_RingTip: return HandSynchronizationBoneId.Hand_RingTip;
                case OVRSkeleton.BoneId.Hand_PinkyTip: return HandSynchronizationBoneId.Hand_PinkyTip;
            }
            return HandSynchronizationBoneId.Invalid;
        }

        // AsOVRSkeletonBoneId returns the OVRSkeleton.BoneId corresponding to the HandSynchronizationBoneId parameter
        public static OVRSkeleton.BoneId AsOVRSkeletonBoneId(this HandSynchronizationBoneId source)
        {
            switch (source)
            {
                case HandSynchronizationBoneId.Hand_WristRoot: return OVRSkeleton.BoneId.Hand_WristRoot;
                case HandSynchronizationBoneId.Hand_ForearmStub: return OVRSkeleton.BoneId.Hand_ForearmStub;
                case HandSynchronizationBoneId.Hand_Thumb0: return OVRSkeleton.BoneId.Hand_Thumb0;
                case HandSynchronizationBoneId.Hand_Thumb1: return OVRSkeleton.BoneId.Hand_Thumb1;
                case HandSynchronizationBoneId.Hand_Thumb2: return OVRSkeleton.BoneId.Hand_Thumb2;
                case HandSynchronizationBoneId.Hand_Thumb3: return OVRSkeleton.BoneId.Hand_Thumb3;
                case HandSynchronizationBoneId.Hand_Index1: return OVRSkeleton.BoneId.Hand_Index1;
                case HandSynchronizationBoneId.Hand_Index2: return OVRSkeleton.BoneId.Hand_Index2;
                case HandSynchronizationBoneId.Hand_Index3: return OVRSkeleton.BoneId.Hand_Index3;
                case HandSynchronizationBoneId.Hand_Middle1: return OVRSkeleton.BoneId.Hand_Middle1;
                case HandSynchronizationBoneId.Hand_Middle2: return OVRSkeleton.BoneId.Hand_Middle2;
                case HandSynchronizationBoneId.Hand_Middle3: return OVRSkeleton.BoneId.Hand_Middle3;
                case HandSynchronizationBoneId.Hand_Ring1: return OVRSkeleton.BoneId.Hand_Ring1;
                case HandSynchronizationBoneId.Hand_Ring2: return OVRSkeleton.BoneId.Hand_Ring2;
                case HandSynchronizationBoneId.Hand_Ring3: return OVRSkeleton.BoneId.Hand_Ring3;
                case HandSynchronizationBoneId.Hand_Pinky0: return OVRSkeleton.BoneId.Hand_Pinky0;
                case HandSynchronizationBoneId.Hand_Pinky1: return OVRSkeleton.BoneId.Hand_Pinky1;
                case HandSynchronizationBoneId.Hand_Pinky2: return OVRSkeleton.BoneId.Hand_Pinky2;
                case HandSynchronizationBoneId.Hand_Pinky3: return OVRSkeleton.BoneId.Hand_Pinky3;
                case HandSynchronizationBoneId.Hand_ThumbTip: return OVRSkeleton.BoneId.Hand_ThumbTip;
                case HandSynchronizationBoneId.Hand_IndexTip: return OVRSkeleton.BoneId.Hand_IndexTip;
                case HandSynchronizationBoneId.Hand_MiddleTip: return OVRSkeleton.BoneId.Hand_MiddleTip;
                case HandSynchronizationBoneId.Hand_RingTip: return OVRSkeleton.BoneId.Hand_RingTip;
                case HandSynchronizationBoneId.Hand_PinkyTip: return OVRSkeleton.BoneId.Hand_PinkyTip;
            }
            return OVRSkeleton.BoneId.Invalid;
        }
    }
}

#endif