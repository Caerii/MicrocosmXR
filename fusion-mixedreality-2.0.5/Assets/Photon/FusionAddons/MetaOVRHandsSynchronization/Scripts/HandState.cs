//#define FINGER_TRACKING_FINE_TUNING
#if OCULUS_SDK_AVAILABLE

using Fusion.Addons.DataSyncHelpers;
using Fusion.Tools;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{

    [System.Serializable]
    public struct HandState:ICopiable<HandState>
    {
        public const int BONE_COUNT = 24;
        public bool isDataValid;
        public bool isDataHighConfidence;
        public float handScale;
        public Quaternion[] boneRotations;

        public void CopyValuesFrom(HandState source)
        {
            isDataValid = source.isDataValid;
            isDataHighConfidence = source.isDataHighConfidence;
            handScale = source.handScale;
            if (source.boneRotations == null)
            {
                boneRotations = null;
            }
            else
            {
                if (boneRotations == null || source.boneRotations.Length != boneRotations.Length)
                {
                    boneRotations = new Quaternion[source.boneRotations.Length];
                }
                for (int i = 0; i < boneRotations.Length; i++)
                {
                    boneRotations[i] = source.boneRotations[i];
                }
            }
        }

    }

    public enum HandSynchronizationBoneId
    {
        Invalid,
        Hand_WristRoot,
        Hand_ForearmStub,
        Hand_Thumb0, Hand_Thumb1, Hand_Thumb2, Hand_Thumb3,
        Hand_Index1, Hand_Index2, Hand_Index3,
        Hand_Middle1, Hand_Middle2, Hand_Middle3,
        Hand_Ring1, Hand_Ring2, Hand_Ring3,
        Hand_Pinky0, Hand_Pinky1, Hand_Pinky2, Hand_Pinky3,
        Hand_ThumbTip,
        Hand_IndexTip,
        Hand_MiddleTip,
        Hand_RingTip,
        Hand_PinkyTip,
    }
    public enum BoneAxisCompressionMode
    {
        HardcodedValue,
        X, Y, Z,
        XY, YZ, XZ,
        XYLowPrecision, YZLowPrecision, XZLowResolution,
        XYZ,
        Quaternion,
        FollowAnotherBone
    }

    [System.Serializable]
    public struct FollowBoneDetails
    {
        public HandSynchronizationBoneId followedBone;
        public CompressedHandState.AxisRot boneAxis;
        public float minInputRot;
        public float maxInputRot;
    }

    [System.Serializable]
    public struct HandBoneInfo
    {
        public HandSynchronizationBoneId boneId;
        public BoneAxisCompressionMode axisCompressionMode;
        public bool applyOffset;
        public Vector3 offsetValue;
        public float XminRange;
        public float XmaxRange;
        public float YminRange;
        public float YmaxRange;
        public float ZminRange;
        public float ZmaxRange;
        public FollowBoneDetails followBoneDetails;
 
        public int ByteSize
        {
            get
            {
                switch (axisCompressionMode)
                {
                    case BoneAxisCompressionMode.X: case BoneAxisCompressionMode.Y: case BoneAxisCompressionMode.Z: return 1;
                    case BoneAxisCompressionMode.XYLowPrecision: case BoneAxisCompressionMode.YZLowPrecision: case BoneAxisCompressionMode.XZLowResolution: return 1;
                    case BoneAxisCompressionMode.XY: case BoneAxisCompressionMode.YZ: case BoneAxisCompressionMode.XZ: return 2;
                    case BoneAxisCompressionMode.XYZ: return 3; // Might be 2 if we choose to use 1 2-axis and 1 1-axis compression instead of 3 1-axis compression
                    case BoneAxisCompressionMode.Quaternion: return 4 * sizeof(float);
                }
                return 0;
            }
        }
    }

    [System.Serializable]
    public struct CompressedHandState
    {
        public bool isDataValid;
        public bool isDataHighConfidence;
        public float handScale;
        public byte[] bonesRotations;


        public enum AxisRot { X, Y, Z }

        public static float ClampBoneAngle(float angle, float minAngle, float maxAngle, AxisRot axis, OVRSkeleton.BoneId id)
        {
            var initialAngle = angle;
            // Try to quickly adapt the angle if it seems to be out of range.
            if (angle < minAngle)
            {
                var newFormat = angle + 360;
                bool isNewFormatImproved = newFormat < maxAngle;// New value is in accepted range
                isNewFormatImproved = isNewFormatImproved || Mathf.Abs(angle - minAngle) > Mathf.Abs(newFormat - maxAngle);// New value is not in range, but closer to the other boundary
                if (isNewFormatImproved)
                    angle = newFormat;
            }
            if (angle > maxAngle)
            {
                var newFormat = angle - 360;
                bool isNewFormatImproved = newFormat > minAngle;// New value is in accepted range
                isNewFormatImproved = isNewFormatImproved || Mathf.Abs(angle - maxAngle) > Mathf.Abs(newFormat - minAngle);// New value is not in range, but closer to the other boundary
                if (isNewFormatImproved)
                    angle = newFormat;
            }

            var clampedAngle = Mathf.Clamp(angle, minAngle, maxAngle);
#if UNITY_EDITOR
#if FINGER_TRACKING_FINE_TUNING
            if (clampedAngle != initialAngle && minAngle != maxAngle)
            {
                int resolution = 1;
                var debugAngle = resolution * (int)(angle / resolution);
                var debugClampedAngle = resolution * (int)(clampedAngle / resolution);
                Debug.LogError($"Min/max values for bone clamp the input values: initialAngle={initialAngle} angle={debugAngle} campledAngle={debugClampedAngle} ({minAngle}/{maxAngle}) on axis:{axis} of {OVRSkeleton.BoneLabelFromBoneId(OVRSkeleton.SkeletonType.HandLeft, id)}.");
            }
#endif
#endif
            return clampedAngle;
        }

        // CompressOneBoneRotationIntoOneByte computes the rotation of a bone into a byte.
        public static Byte CompressOneBoneRotationIntoOneByte(OVRSkeleton.BoneId id, ref OVRSkeleton.SkeletonPoseData ovrSkeletonPoseData, AxisRot axis, float minAngle, float maxAngle)
        {
            if (id == OVRSkeleton.BoneId.Invalid)
                return default;

            var angle = GetBoneAxisAngle(ref ovrSkeletonPoseData, id, axis);
            angle = ClampBoneAngle(angle, minAngle, maxAngle, axis, id);

            var byteRotation = (byte)RemapFloat(angle, minAngle, maxAngle, 0, 255);

            return byteRotation;

        }

        // CompressTwoBonesRotationIntoOneByte computes the rotation of two OVRSkeleton bones into a byte.
        public static Byte CompressTwoBonesRotationIntoOneByte(OVRSkeleton.BoneId id, ref OVRSkeleton.SkeletonPoseData ovrSkeletonPoseData,
       AxisRot axis1, AxisRot axis2, float minAngle1, float maxAngle1, float minAngle2, float maxAngle2)
        {
            if (id == OVRSkeleton.BoneId.Invalid)
                return default;

            var angle1 = ClampBoneAngle(GetBoneAxisAngle(ref ovrSkeletonPoseData, id, axis1), minAngle1, maxAngle1, axis1, id);
            var angle2 = ClampBoneAngle(GetBoneAxisAngle(ref ovrSkeletonPoseData, id, axis2), minAngle2, maxAngle2, axis2, id);

            var byteRotation1 = (byte)RemapFloat(angle1, minAngle1, maxAngle1, 0, 15);
            var byteRotation2 = (byte)RemapFloat(angle2, minAngle2, maxAngle2, 0, 15);

            byteRotation1 = (byte)(byteRotation1 << 4);


            return (byte)(byteRotation1 | byteRotation2);
        }

        // DecompressTwoBonesRotationFromOneByte computes the rotation of two bones (in a single byte) into a Quaternion.
        public static Quaternion DecompressTwoBonesRotationFromOneByte(Byte compressedValue,
            AxisRot axis1, AxisRot axis2, float minAngle1, float maxAngle1, float minAngle2, float maxAngle2)
        {
            Vector3 euler1 = Vector3.zero;
            Vector3 euler2 = Vector3.zero;

            switch (axis1)
            {
                case AxisRot.X:
                    euler1 = Vector3.right;
                    break;
                case AxisRot.Y:
                    euler1 = Vector3.up;
                    break;
                case AxisRot.Z:
                    euler1 = Vector3.forward;
                    break;
            }

            switch (axis2)
            {
                case AxisRot.X:
                    euler2 = Vector3.right;
                    break;
                case AxisRot.Y:
                    euler2 = Vector3.up;
                    break;
                case AxisRot.Z:
                    euler2 = Vector3.forward;
                    break;
            }

            var decompressed2 = (compressedValue & 15);
            var decompressed1 = compressedValue >> 4;

            var amount1 = RemapFloat(decompressed1, 0, 15, minAngle1, maxAngle1);
            var amount2 = RemapFloat(decompressed2, 0, 15, minAngle2, maxAngle2);

            euler1 *= amount1;
            euler2 *= amount2;
            var eulerFinal = euler1 + euler2;

            var result = Quaternion.Euler(eulerFinal.x, eulerFinal.y, eulerFinal.z);

            return result;
        }


        // DecompressOneBoneRotationFromOneByte computes the rotation of one bone (byte) into a Quaternion.
        public static Quaternion DecompressOneBoneRotationFromOneByte(Byte compressedValue, AxisRot axis, float minAngle, float maxAngle)
        {
            Vector3 euler = Vector3.zero;
            switch (axis)
            {
                case AxisRot.X:
                    euler = Vector3.right;
                    break;
                case AxisRot.Y:
                    euler = Vector3.up;
                    break;
                case AxisRot.Z:
                    euler = Vector3.forward;
                    break;
            }

            var amount = RemapFloat(compressedValue, 0, 255, minAngle, maxAngle);
            euler *= amount;
            var result = Quaternion.Euler(euler.x, euler.y, euler.z);
            return result;
        }

        // GetBoneAxisAngle computes the angle of an OVRSkeleton bone on a specific axis
        private static float GetBoneAxisAngle(ref OVRSkeleton.SkeletonPoseData skeletonPoseData, OVRSkeleton.BoneId boneId, AxisRot axis)
        {
            if (skeletonPoseData.BoneRotations == null || skeletonPoseData.BoneRotations.Length <= (int)boneId)
            {
                Debug.LogError($"Missing bone");
                return 0;
            }
            var boneLocalRot = skeletonPoseData.BoneRotations[(int)boneId].FromFlippedXQuatf();
            var boneLocalEuler = boneLocalRot.eulerAngles;
            switch (axis)
            {
                case AxisRot.X:
                    return Quaternion.Angle(boneLocalRot, boneLocalRot * Quaternion.Inverse(Quaternion.Euler(boneLocalEuler.x, 0, 0))) * (boneLocalEuler.x > 180 ? 1 : -1);
                case AxisRot.Y:
                    return Quaternion.Angle(boneLocalRot, boneLocalRot * Quaternion.Inverse(Quaternion.Euler(0, boneLocalEuler.y, 0))) * (boneLocalEuler.y > 180 ? 1 : -1);
                case AxisRot.Z:
                    return Quaternion.Angle(boneLocalRot, boneLocalRot * Quaternion.Inverse(Quaternion.Euler(0, 0, boneLocalEuler.z))) * (boneLocalEuler.z > 180 ? 1 : -1);
                default:
                    return default;
            }
        }

        // ComputeLastPhalanx computes the axis rotation of a bone based on another bone rotation.
        // It is used to reduce the number of byte to synchronize on the network. Indeed, most of time, the angle of the last phalanx is proportional to the other phalanx.
        // The calculation follows a parabolic law similar to: followingBoneRot = a * followedBoneRot²
        // "a" being computed so that:
        // If input angle=10°, output angle=1°
        // If input angle=45°, output angle=20°
        // If input angle=90°, output angle=81°
        public static Quaternion ComputeLastPhalanx(Quaternion inputQuaternion, AxisRot axisRot, float minInputRot, float maxInputRot)
        {
            Vector3 euler = inputQuaternion.eulerAngles;


            // Ensure values are within -180 to 180 range
            if (euler.x > 180)
                euler.x -= 360;
            if (euler.y > 180)
                euler.y -= 360;
            if (euler.z > 180)
                euler.z -= 360;

            if (minInputRot != 0 || maxInputRot != 0)
            {
                switch (axisRot)
                {
                    case AxisRot.X: euler.x = Mathf.Clamp(euler.x, minInputRot, maxInputRot); break;
                    case AxisRot.Y: euler.y = Mathf.Clamp(euler.y, minInputRot, maxInputRot); break;
                    case AxisRot.Z: euler.z = Mathf.Clamp(euler.z, minInputRot, maxInputRot); break;
                }

            }

            Vector3 newEuler = euler;

            // Parrabola adaptation of value: 
            float a = 0.01f;

            switch (axisRot)
            {
                case AxisRot.X:
                    newEuler.x = a * euler.x * euler.x * Mathf.Sign(euler.x);
                    break;

                case AxisRot.Y:
                    newEuler.y = a * euler.x * euler.y * Mathf.Sign(euler.y);
                    break;

                case AxisRot.Z:
                    newEuler.z = a * euler.z * euler.z * Mathf.Sign(euler.z);
                    break;
            }
            Quaternion modifiedQuaternion = Quaternion.Euler(newEuler);
            return modifiedQuaternion;
        }

        //  RemapFloat is used to remap a value from one range to another while maintaining proportionality between the two ranges
        public static float RemapFloat(float from, float fromMin, float fromMax, float toMin, float toMax)
        {
            var fromAbs = from - fromMin;
            var fromMaxAbs = fromMax - fromMin;

            var normal = fromAbs / fromMaxAbs;

            var toMaxAbs = toMax - toMin;
            var toAbs = toMaxAbs * normal;

            var to = toAbs + toMin;

            return to;
        }


        // FillRotationsWithOVRSkeleton updates the HandState bonesRotations byte array based on the OVR skeleton actual data
        // The HandBoneInfo list parameter defined how each bones must be compressed.
        public void FillRotationsWithOVRSkeleton(OVRSkeleton.SkeletonPoseData skeletonData, List<HandBoneInfo> bonesInfo)
        {
            if (isDataValid && skeletonData.IsDataValid)
            {
                int index = 0;
                foreach (var handBoneInfo in bonesInfo)
                {
                    HandSynchronizationBoneId boneId = handBoneInfo.boneId;
                    OVRSkeleton.BoneId ovrSkeletonBoneId = boneId.AsOVRSkeletonBoneId();

                    if (boneId != HandSynchronizationBoneId.Invalid)
                    {
                        switch (handBoneInfo.axisCompressionMode)
                        {
                            case BoneAxisCompressionMode.HardcodedValue:
                                // no need to sync harcoded value
                                break;

                            case BoneAxisCompressionMode.X:
                                bonesRotations[index] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                                break;

                            case BoneAxisCompressionMode.Y:
                                bonesRotations[index] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                                break;

                            case BoneAxisCompressionMode.Z:
                                bonesRotations[index] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                                break;

                            case BoneAxisCompressionMode.XYLowPrecision:
                                bonesRotations[index] = CompressTwoBonesRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.X, AxisRot.Y, handBoneInfo.XminRange, handBoneInfo.XmaxRange, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                                break;

                            case BoneAxisCompressionMode.YZLowPrecision:
                                bonesRotations[index] = CompressTwoBonesRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Y, AxisRot.Z, handBoneInfo.YminRange, handBoneInfo.YmaxRange, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                                break;

                            case BoneAxisCompressionMode.XZLowResolution:
                                bonesRotations[index] = CompressTwoBonesRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.X, AxisRot.Z, handBoneInfo.XminRange, handBoneInfo.XmaxRange, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                                break;

                            case BoneAxisCompressionMode.XY:
                                bonesRotations[index] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                                bonesRotations[index + 1] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                                break;

                            case BoneAxisCompressionMode.YZ:
                                bonesRotations[index] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                                bonesRotations[index + 1] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                                break;

                            case BoneAxisCompressionMode.XZ:
                                bonesRotations[index] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                                bonesRotations[index + 1] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                                break;

                            case BoneAxisCompressionMode.XYZ:
                                bonesRotations[index] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                                bonesRotations[index + 1] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                                bonesRotations[index + 2] = CompressOneBoneRotationIntoOneByte(ovrSkeletonBoneId, ref skeletonData, AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                                break;

                            case BoneAxisCompressionMode.Quaternion:

                                var rotation = Quaternion.identity;
                                if (skeletonData.BoneRotations == null || skeletonData.BoneRotations.Length <= (int)ovrSkeletonBoneId)
                                {
                                    Debug.LogError($"Missing bone");
                                }
                                else
                                {
                                    rotation = skeletonData.BoneRotations[(int)ovrSkeletonBoneId].FromQuatf();
                                }
                                byte[] quaternionArray = SerializationTools.AsByteArray(rotation);
                                Array.Copy(quaternionArray, 0, bonesRotations, index, quaternionArray.Length);
                                break;

                            case BoneAxisCompressionMode.FollowAnotherBone:
                                // no need to sync
                                break;
                        }
                        index += handBoneInfo.ByteSize;
                    }
                }
            }
        }

        // UncompressToHandState updates the HandState with and the HandBoneInfo list received in parameter
        public void UncompressToHandState(ref HandState handState, List<HandBoneInfo> bonesInfo)
        {
            handState.isDataValid = isDataValid;
            handState.isDataHighConfidence = isDataHighConfidence;
            handState.handScale = handScale;

            if (handState.boneRotations == null || handState.boneRotations.Length != HandState.BONE_COUNT)
            {
                handState.boneRotations = new Quaternion[HandState.BONE_COUNT];
            }

            if (isDataValid == false)
            {
                // No need to change the bone rotations: no valid finger tracking data
                return;
            }

            int index = 0;

            foreach (var handBoneInfo in bonesInfo)
            {
                HandSynchronizationBoneId boneId = handBoneInfo.boneId;
                OVRSkeleton.BoneId ovrBoneID = boneId.AsOVRSkeletonBoneId();
                bool applyOffset = handBoneInfo.applyOffset;

                if (applyOffset)
                {
                    handState.boneRotations[(int)ovrBoneID] = Quaternion.Euler(handBoneInfo.offsetValue);
                }

                if (boneId != HandSynchronizationBoneId.Invalid)
                {
                    Quaternion rotation = Quaternion.identity;

                    switch (handBoneInfo.axisCompressionMode)
                    {
                        case BoneAxisCompressionMode.HardcodedValue:
                            break;

                        case BoneAxisCompressionMode.X:
                            rotation = DecompressOneBoneRotationFromOneByte(bonesRotations[index], AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                            break;

                        case BoneAxisCompressionMode.Y:
                            rotation = DecompressOneBoneRotationFromOneByte(bonesRotations[index], AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                            break;

                        case BoneAxisCompressionMode.Z:
                            rotation = DecompressOneBoneRotationFromOneByte(bonesRotations[index], AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                            break;

                        case BoneAxisCompressionMode.XYLowPrecision:
                            rotation = DecompressTwoBonesRotationFromOneByte(bonesRotations[index], AxisRot.X, AxisRot.Y, handBoneInfo.XminRange, handBoneInfo.XmaxRange, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                            break;

                        case BoneAxisCompressionMode.YZLowPrecision:
                            rotation = DecompressTwoBonesRotationFromOneByte(bonesRotations[index], AxisRot.Y, AxisRot.Z, handBoneInfo.YminRange, handBoneInfo.YmaxRange, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                            break;

                        case BoneAxisCompressionMode.XZLowResolution:
                            rotation = DecompressTwoBonesRotationFromOneByte(bonesRotations[index], AxisRot.X, AxisRot.Z, handBoneInfo.XminRange, handBoneInfo.XmaxRange, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                            break;

                        case BoneAxisCompressionMode.XY:
                            rotation = DecompressOneBoneRotationFromOneByte(bonesRotations[index], AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                            rotation *= DecompressOneBoneRotationFromOneByte(bonesRotations[index + 1], AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                            break;
                        case BoneAxisCompressionMode.YZ:
                            rotation = DecompressOneBoneRotationFromOneByte(bonesRotations[index], AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                            rotation *= DecompressOneBoneRotationFromOneByte(bonesRotations[index + 1], AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                            break;
                        case BoneAxisCompressionMode.XZ:
                            rotation = DecompressOneBoneRotationFromOneByte(bonesRotations[index], AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                            rotation *= DecompressOneBoneRotationFromOneByte(bonesRotations[index + 1], AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                            break;
                        case BoneAxisCompressionMode.XYZ:
                            rotation = DecompressOneBoneRotationFromOneByte(bonesRotations[index], AxisRot.X, handBoneInfo.XminRange, handBoneInfo.XmaxRange);
                            rotation *= DecompressOneBoneRotationFromOneByte(bonesRotations[index + 1], AxisRot.Y, handBoneInfo.YminRange, handBoneInfo.YmaxRange);
                            rotation *= DecompressOneBoneRotationFromOneByte(bonesRotations[index + 2], AxisRot.Z, handBoneInfo.ZminRange, handBoneInfo.ZmaxRange);
                            break;

                        case BoneAxisCompressionMode.Quaternion:
                            if (handState.boneRotations.Length <= (int)ovrBoneID)
                            {
                                throw new Exception($"Unexpected bone index ({handState.boneRotations.Length}) ovrBoneID:{OVRSkeleton.BoneLabelFromBoneId(OVRSkeleton.SkeletonType.HandLeft, ovrBoneID)} ({(int)ovrBoneID})");
                            }
                            SerializationTools.Unserialize(bonesRotations, ref index, out rotation);
                            break;

                        case BoneAxisCompressionMode.FollowAnotherBone:
                            var followedBoneRotation = handState.boneRotations[(int)handBoneInfo.followBoneDetails.followedBone.AsOVRSkeletonBoneId()];
                            rotation = ComputeLastPhalanx(followedBoneRotation, handBoneInfo.followBoneDetails.boneAxis, handBoneInfo.followBoneDetails.minInputRot, handBoneInfo.followBoneDetails.maxInputRot);
                            break;
                    }

                    if (applyOffset)
                        handState.boneRotations[(int)ovrBoneID] *= rotation;
                    else
                        handState.boneRotations[(int)ovrBoneID] = rotation;

                    if (handBoneInfo.axisCompressionMode != BoneAxisCompressionMode.Quaternion)
                    {
                        // for Quaternion, the SerializationTools.Unserialize already handle the index incrementation
                        index += handBoneInfo.ByteSize;
                    }
                }
            }
        }
    }
}
#endif