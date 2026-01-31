using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
#if OCULUS_SDK_AVAILABLE
    /**
     * Replacement of OVRSkeleton to handle an issue with ShouldInitialize: cannot be used for remote hands in the editor, as there is a check on local finger tracking
     * See https://communityforums.atmeta.com/t5/Unity-VR-Development/Using-OVRHand-for-remote-hands-in-editor-issue-in-OVRSkeleton/td-p/1101040 
     */
    public class RemoteOVRSkeleton : MonoBehaviour
    {

        [SerializeField]
        protected OVRSkeleton.SkeletonType _skeletonType = OVRSkeleton.SkeletonType.None;

        [SerializeField]
        private OVRSkeleton.IOVRSkeletonDataProvider _dataProvider;

        [SerializeField]
        private bool _updateRootPose = false;

        [SerializeField]
        private bool _updateRootScale = false;

        [SerializeField]
        private bool _enablePhysicsCapsules = false;

        [SerializeField]
        private bool _applyBoneTranslations = true;

        private GameObject _bonesGO;
        private GameObject _bindPosesGO;
        private GameObject _capsulesGO;

        protected List<OVRBone> _bones;
        private List<OVRBone> _bindPoses;
        private List<OVRBoneCapsule> _capsules;

        protected OVRPlugin.Skeleton2 _skeleton = new OVRPlugin.Skeleton2();
        private readonly Quaternion wristFixupRotation = new Quaternion(0.0f, 1.0f, 0.0f, 0.0f);

        public bool IsInitialized { get; private set; }
        public bool IsDataValid { get; private set; }
        public bool IsDataHighConfidence { get; private set; }
        public IList<OVRBone> Bones { get; protected set; }
        public IList<OVRBone> BindPoses { get; private set; }
        public IList<OVRBoneCapsule> Capsules { get; private set; }

        public OVRSkeleton.SkeletonType GetSkeletonType()
        {
            return _skeletonType;
        }

        internal virtual void SetSkeletonType(OVRSkeleton.SkeletonType type)
        {
            _skeletonType = type;
        }


        public bool IsValidBone(OVRSkeleton.BoneId bone)
        {
            return OVRPlugin.IsValidBone((OVRPlugin.BoneId)bone, (OVRPlugin.SkeletonType)_skeletonType);
        }

        public int SkeletonChangedCount { get; private set; }

        protected virtual void Awake()
        {
            if (_dataProvider == null)
            {
                var foundDataProvider = SearchSkeletonDataProvider();
                if (foundDataProvider != null)
                {
                    _dataProvider = foundDataProvider;
                    if (_dataProvider is MonoBehaviour mb)
                    {
                        Debug.Log($"Found IOVRSkeletonDataProvider reference in {mb.name} due to unassigned field.");
                    }
                }
            }

            _bones = new List<OVRBone>();
            Bones = _bones.AsReadOnly();

            _bindPoses = new List<OVRBone>();
            BindPoses = _bindPoses.AsReadOnly();

            _capsules = new List<OVRBoneCapsule>();
            Capsules = _capsules.AsReadOnly();
        }

        internal OVRSkeleton.IOVRSkeletonDataProvider SearchSkeletonDataProvider()
        {
            var dataProviders = gameObject.GetComponentsInParent<OVRSkeleton.IOVRSkeletonDataProvider>();
            foreach (var dataProvider in dataProviders)
            {
                if (dataProvider.GetSkeletonType() == _skeletonType)
                {
                    return dataProvider;
                }
            }

            return null;
        }

        /// <summary>
        /// Start this instance.
        /// Initialize data structures.
        /// </summary>
        protected virtual void Start()
        {
            if (_dataProvider == null && _skeletonType == OVRSkeleton.SkeletonType.Body)
            {
                Debug.LogWarning("OVRSkeleton and its subclasses requires OVRBody to function.");
            }

            if (ShouldInitialize())
            {
                Initialize();
            }
        }

        private bool ShouldInitialize()
        {
            if (IsInitialized)
            {
                return false;
            }

            if (_dataProvider != null && !_dataProvider.enabled)
            {
                return false;
            }

            if (_skeletonType == OVRSkeleton.SkeletonType.None)
            {
                return false;
            }
            else if (IsHandSkeleton(_skeletonType))
            {
                return true;
            }
            else
            {
                return true;
            }
        }

        private void Initialize()
        {
            if (OVRPlugin.GetSkeleton2((OVRPlugin.SkeletonType)_skeletonType, ref _skeleton))
            {
                InitializeBones();
                InitializeBindPose();
                InitializeCapsules();

                IsInitialized = true;
            }
        }

        protected virtual Transform GetBoneTransform(OVRSkeleton.BoneId boneId) => null;

        protected virtual void InitializeBones()
        {
            bool flipX = IsHandSkeleton(_skeletonType);

            if (!_bonesGO)
            {
                _bonesGO = new GameObject("Bones");
                _bonesGO.transform.SetParent(transform, false);
                _bonesGO.transform.localPosition = Vector3.zero;
                _bonesGO.transform.localRotation = Quaternion.identity;
            }

            if (_bones == null || _bones.Count != _skeleton.NumBones)
            {
                _bones = new List<OVRBone>(new OVRBone[_skeleton.NumBones]);
                Bones = _bones.AsReadOnly();
            }

            bool newBonesCreated = false;
            // pre-populate bones list before attempting to apply bone hierarchy
            for (int i = 0; i < _bones.Count; ++i)
            {
                OVRBone bone = _bones[i] ?? (_bones[i] = new OVRBone());
                bone.Id = (OVRSkeleton.BoneId)_skeleton.Bones[i].Id;
                bone.ParentBoneIndex = _skeleton.Bones[i].ParentBoneIndex;
                UnityEngine.Assertions.Assert.IsTrue((int)bone.Id >= 0 && bone.Id <= OVRSkeleton.BoneId.Max);

                // don't create new bones each time; rely on
                // pre-existing bone transforms.
                if (bone.Transform == null)
                {
                    newBonesCreated = true;
                    bone.Transform = GetBoneTransform(bone.Id);
                    if (bone.Transform == null)
                    {
                        bone.Transform = new GameObject(BoneLabelFromBoneId(_skeletonType, bone.Id)).transform;
                    }
                }

                // if allocated bone here before, make sure the name is correct.
                if (GetBoneTransform(bone.Id) == null)
                {
                    bone.Transform.name = BoneLabelFromBoneId(_skeletonType, bone.Id);
                }

                var pose = _skeleton.Bones[i].Pose;

                if (_applyBoneTranslations)
                {
                    bone.Transform.localPosition = flipX
                        ? pose.Position.FromFlippedXVector3f()
                        : pose.Position.FromFlippedZVector3f();
                }

                bone.Transform.localRotation = flipX
                    ? pose.Orientation.FromFlippedXQuatf()
                    : pose.Orientation.FromFlippedZQuatf();
            }

            if (newBonesCreated)
            {
                for (int i = 0; i < _bones.Count; ++i)
                {
                    if (!IsValidBone((OVRSkeleton.BoneId)_bones[i].ParentBoneIndex) ||
                        IsBodySkeleton(_skeletonType))  // Body bones are always in tracking space
                    {
                        _bones[i].Transform.SetParent(_bonesGO.transform, false);
                    }
                    else
                    {
                        _bones[i].Transform.SetParent(_bones[_bones[i].ParentBoneIndex].Transform, false);
                    }
                }
            }
        }

        private void InitializeBindPose()
        {
            if (!_bindPosesGO)
            {
                _bindPosesGO = new GameObject("BindPoses");
                _bindPosesGO.transform.SetParent(transform, false);
                _bindPosesGO.transform.localPosition = Vector3.zero;
                _bindPosesGO.transform.localRotation = Quaternion.identity;
            }

            if (_bindPoses == null || _bindPoses.Count != _bones.Count)
            {
                _bindPoses = new List<OVRBone>(new OVRBone[_bones.Count]);
                BindPoses = _bindPoses.AsReadOnly();
            }

            // pre-populate bones list before attempting to apply bone hierarchy
            for (int i = 0; i < _bindPoses.Count; ++i)
            {
                OVRBone bone = _bones[i];
                OVRBone bindPoseBone = _bindPoses[i] ?? (_bindPoses[i] = new OVRBone());
                bindPoseBone.Id = bone.Id;
                bindPoseBone.ParentBoneIndex = bone.ParentBoneIndex;

                Transform trans = bindPoseBone.Transform
                    ? bindPoseBone.Transform
                    : (bindPoseBone.Transform =
                        new GameObject(BoneLabelFromBoneId(_skeletonType, bindPoseBone.Id)).transform);
                trans.localPosition = bone.Transform.localPosition;
                trans.localRotation = bone.Transform.localRotation;
            }

            for (int i = 0; i < _bindPoses.Count; ++i)
            {
                if (!IsValidBone((OVRSkeleton.BoneId)_bindPoses[i].ParentBoneIndex) ||
                    IsBodySkeleton(_skeletonType)) // Body bones are always in tracking space
                {
                    _bindPoses[i].Transform.SetParent(_bindPosesGO.transform, false);
                }
                else
                {
                    _bindPoses[i].Transform.SetParent(_bindPoses[_bindPoses[i].ParentBoneIndex].Transform, false);
                }
            }
        }

        private void InitializeCapsules()
        {
            bool flipX = IsHandSkeleton(_skeletonType);

            if (_enablePhysicsCapsules)
            {
                if (!_capsulesGO)
                {
                    _capsulesGO = new GameObject("Capsules");
                    _capsulesGO.transform.SetParent(transform, false);
                    _capsulesGO.transform.localPosition = Vector3.zero;
                    _capsulesGO.transform.localRotation = Quaternion.identity;
                }

                if (_capsules == null || _capsules.Count != _skeleton.NumBoneCapsules)
                {
                    _capsules = new List<OVRBoneCapsule>(new OVRBoneCapsule[_skeleton.NumBoneCapsules]);
                    Capsules = _capsules.AsReadOnly();
                }

                for (int i = 0; i < _capsules.Count; ++i)
                {
                    OVRBone bone = _bones[_skeleton.BoneCapsules[i].BoneIndex];
                    OVRBoneCapsule capsule = _capsules[i] ?? (_capsules[i] = new OVRBoneCapsule());
                    capsule.BoneIndex = _skeleton.BoneCapsules[i].BoneIndex;

                    if (capsule.CapsuleRigidbody == null)
                    {
                        capsule.CapsuleRigidbody =
                            new GameObject(BoneLabelFromBoneId(_skeletonType, bone.Id) + "_CapsuleRigidbody")
                                .AddComponent<Rigidbody>();
                        capsule.CapsuleRigidbody.mass = 1.0f;
                        capsule.CapsuleRigidbody.isKinematic = true;
                        capsule.CapsuleRigidbody.useGravity = false;
                        capsule.CapsuleRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    }

                    GameObject rbGO = capsule.CapsuleRigidbody.gameObject;
                    rbGO.transform.SetParent(_capsulesGO.transform, false);
                    rbGO.transform.position = bone.Transform.position;
                    rbGO.transform.rotation = bone.Transform.rotation;

                    if (capsule.CapsuleCollider == null)
                    {
                        capsule.CapsuleCollider =
                            new GameObject(BoneLabelFromBoneId(_skeletonType, bone.Id) + "_CapsuleCollider")
                                .AddComponent<CapsuleCollider>();
                        capsule.CapsuleCollider.isTrigger = false;
                    }

                    var p0 = flipX
                        ? _skeleton.BoneCapsules[i].StartPoint.FromFlippedXVector3f()
                        : _skeleton.BoneCapsules[i].StartPoint.FromFlippedZVector3f();
                    var p1 = flipX
                        ? _skeleton.BoneCapsules[i].EndPoint.FromFlippedXVector3f()
                        : _skeleton.BoneCapsules[i].EndPoint.FromFlippedZVector3f();
                    var delta = p1 - p0;
                    var mag = delta.magnitude;
                    var rot = Quaternion.FromToRotation(Vector3.right, delta);
                    capsule.CapsuleCollider.radius = _skeleton.BoneCapsules[i].Radius;
                    capsule.CapsuleCollider.height = mag + _skeleton.BoneCapsules[i].Radius * 2.0f;
                    capsule.CapsuleCollider.direction = 0;
                    capsule.CapsuleCollider.center = Vector3.right * mag * 0.5f;

                    GameObject ccGO = capsule.CapsuleCollider.gameObject;
                    ccGO.transform.SetParent(rbGO.transform, false);
                    ccGO.transform.localPosition = p0;
                    ccGO.transform.localRotation = rot;
                }
            }
        }

        protected virtual void Update()
        {
            UpdateSkeleton();
        }

        protected void UpdateSkeleton()
        {
            if (ShouldInitialize())
            {
                Initialize();
            }

            if (!IsInitialized || _dataProvider == null)
            {
                IsDataValid = false;
                IsDataHighConfidence = false;
                return;
            }

            var data = _dataProvider.GetSkeletonPoseData();

            IsDataValid = data.IsDataValid;

            if (!data.IsDataValid)
            {
                return;
            }

            if (SkeletonChangedCount != data.SkeletonChangedCount)
            {
                SkeletonChangedCount = data.SkeletonChangedCount;
                IsInitialized = false;
                Initialize();
            }

            IsDataHighConfidence = data.IsDataHighConfidence;

            if (_updateRootPose)
            {
                transform.localPosition = data.RootPose.Position.FromFlippedZVector3f();
                transform.localRotation = data.RootPose.Orientation.FromFlippedZQuatf();
            }

            if (_updateRootScale)
            {
                transform.localScale = new Vector3(data.RootScale, data.RootScale, data.RootScale);
            }

            for (var i = 0; i < _bones.Count; ++i)
            {
                var boneTransform = _bones[i].Transform;
                if (boneTransform == null) continue;

                if (IsBodySkeleton(_skeletonType))
                {
                    boneTransform.localPosition = data.BoneTranslations[i].FromFlippedZVector3f();
                    boneTransform.localRotation = data.BoneRotations[i].FromFlippedZQuatf();
                }
                else if (IsHandSkeleton(_skeletonType))
                {
                    boneTransform.localRotation = data.BoneRotations[i].FromFlippedXQuatf();

                    if (_bones[i].Id == OVRSkeleton.BoneId.Hand_WristRoot)
                    {
                        boneTransform.localRotation *= wristFixupRotation;
                    }
                }
                else
                {
                    boneTransform.localRotation = data.BoneRotations[i].FromFlippedZQuatf();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsInitialized || _dataProvider == null)
            {
                IsDataValid = false;
                IsDataHighConfidence = false;

                return;
            }

            Update();

            if (_enablePhysicsCapsules)
            {
                var data = _dataProvider.GetSkeletonPoseData();

                IsDataValid = data.IsDataValid;
                IsDataHighConfidence = data.IsDataHighConfidence;

                for (int i = 0; i < _capsules.Count; ++i)
                {
                    OVRBoneCapsule capsule = _capsules[i];
                    var capsuleGO = capsule.CapsuleRigidbody.gameObject;

                    if (data.IsDataValid && data.IsDataHighConfidence)
                    {
                        Transform bone = _bones[(int)capsule.BoneIndex].Transform;

                        if (capsuleGO.activeSelf)
                        {
                            capsule.CapsuleRigidbody.MovePosition(bone.position);
                            capsule.CapsuleRigidbody.MoveRotation(bone.rotation);
                        }
                        else
                        {
                            capsuleGO.SetActive(true);
                            capsule.CapsuleRigidbody.position = bone.position;
                            capsule.CapsuleRigidbody.rotation = bone.rotation;
                        }
                    }
                    else
                    {
                        if (capsuleGO.activeSelf)
                        {
                            capsuleGO.SetActive(false);
                        }
                    }
                }
            }
        }

        public OVRSkeleton.BoneId GetCurrentStartBoneId()
        {
            switch (_skeletonType)
            {
                case OVRSkeleton.SkeletonType.HandLeft:
                case OVRSkeleton.SkeletonType.HandRight:
                    return OVRSkeleton.BoneId.Hand_Start;
                case OVRSkeleton.SkeletonType.Body:
                    return OVRSkeleton.BoneId.Body_Start;
                case OVRSkeleton.SkeletonType.None:
                default:
                    return OVRSkeleton.BoneId.Invalid;
            }
        }

        public OVRSkeleton.BoneId GetCurrentEndBoneId()
        {
            switch (_skeletonType)
            {
                case OVRSkeleton.SkeletonType.HandLeft:
                case OVRSkeleton.SkeletonType.HandRight:
                    return OVRSkeleton.BoneId.Hand_End;
                case OVRSkeleton.SkeletonType.Body:
                    return OVRSkeleton.BoneId.Body_End;
                case OVRSkeleton.SkeletonType.None:
                default:
                    return OVRSkeleton.BoneId.Invalid;
            }
        }

        private OVRSkeleton.BoneId GetCurrentMaxSkinnableBoneId()
        {
            switch (_skeletonType)
            {
                case OVRSkeleton.SkeletonType.HandLeft:
                case OVRSkeleton.SkeletonType.HandRight:
                    return OVRSkeleton.BoneId.Hand_MaxSkinnable;
                case OVRSkeleton.SkeletonType.Body:
                    return OVRSkeleton.BoneId.Body_End;
                case OVRSkeleton.SkeletonType.None:
                default:
                    return OVRSkeleton.BoneId.Invalid;
            }
        }

        public int GetCurrentNumBones()
        {
            switch (_skeletonType)
            {
                case OVRSkeleton.SkeletonType.HandLeft:
                case OVRSkeleton.SkeletonType.HandRight:
                case OVRSkeleton.SkeletonType.Body:
                    return GetCurrentEndBoneId() - GetCurrentStartBoneId();
                case OVRSkeleton.SkeletonType.None:
                default:
                    return 0;
            }
        }

        public int GetCurrentNumSkinnableBones()
        {
            switch (_skeletonType)
            {
                case OVRSkeleton.SkeletonType.HandLeft:
                case OVRSkeleton.SkeletonType.HandRight:
                case OVRSkeleton.SkeletonType.Body:
                    return GetCurrentMaxSkinnableBoneId() - GetCurrentStartBoneId();
                case OVRSkeleton.SkeletonType.None:
                default:
                    return 0;
            }
        }

        // force aliased enum values to the more appropriate value
        public static string BoneLabelFromBoneId(OVRSkeleton.SkeletonType skeletonType, OVRSkeleton.BoneId boneId)
        {
            if (skeletonType == OVRSkeleton.SkeletonType.Body)
            {
                switch (boneId)
                {
                    case OVRSkeleton.BoneId.Body_Root:
                        return "Body_Root";
                    case OVRSkeleton.BoneId.Body_Hips:
                        return "Body_Hips";
                    case OVRSkeleton.BoneId.Body_SpineLower:
                        return "Body_SpineLower";
                    case OVRSkeleton.BoneId.Body_SpineMiddle:
                        return "Body_SpineMiddle";
                    case OVRSkeleton.BoneId.Body_SpineUpper:
                        return "Body_SpineUpper";
                    case OVRSkeleton.BoneId.Body_Chest:
                        return "Body_Chest";
                    case OVRSkeleton.BoneId.Body_Neck:
                        return "Body_Neck";
                    case OVRSkeleton.BoneId.Body_Head:
                        return "Body_Head";
                    case OVRSkeleton.BoneId.Body_LeftShoulder:
                        return "Body_LeftShoulder";
                    case OVRSkeleton.BoneId.Body_LeftScapula:
                        return "Body_LeftScapula";
                    case OVRSkeleton.BoneId.Body_LeftArmUpper:
                        return "Body_LeftArmUpper";
                    case OVRSkeleton.BoneId.Body_LeftArmLower:
                        return "Body_LeftArmLower";
                    case OVRSkeleton.BoneId.Body_LeftHandWristTwist:
                        return "Body_LeftHandWristTwist";
                    case OVRSkeleton.BoneId.Body_RightShoulder:
                        return "Body_RightShoulder";
                    case OVRSkeleton.BoneId.Body_RightScapula:
                        return "Body_RightScapula";
                    case OVRSkeleton.BoneId.Body_RightArmUpper:
                        return "Body_RightArmUpper";
                    case OVRSkeleton.BoneId.Body_RightArmLower:
                        return "Body_RightArmLower";
                    case OVRSkeleton.BoneId.Body_RightHandWristTwist:
                        return "Body_RightHandWristTwist";
                    case OVRSkeleton.BoneId.Body_LeftHandPalm:
                        return "Body_LeftHandPalm";
                    case OVRSkeleton.BoneId.Body_LeftHandWrist:
                        return "Body_LeftHandWrist";
                    case OVRSkeleton.BoneId.Body_LeftHandThumbMetacarpal:
                        return "Body_LeftHandThumbMetacarpal";
                    case OVRSkeleton.BoneId.Body_LeftHandThumbProximal:
                        return "Body_LeftHandThumbProximal";
                    case OVRSkeleton.BoneId.Body_LeftHandThumbDistal:
                        return "Body_LeftHandThumbDistal";
                    case OVRSkeleton.BoneId.Body_LeftHandThumbTip:
                        return "Body_LeftHandThumbTip";
                    case OVRSkeleton.BoneId.Body_LeftHandIndexMetacarpal:
                        return "Body_LeftHandIndexMetacarpal";
                    case OVRSkeleton.BoneId.Body_LeftHandIndexProximal:
                        return "Body_LeftHandIndexProximal";
                    case OVRSkeleton.BoneId.Body_LeftHandIndexIntermediate:
                        return "Body_LeftHandIndexIntermediate";
                    case OVRSkeleton.BoneId.Body_LeftHandIndexDistal:
                        return "Body_LeftHandIndexDistal";
                    case OVRSkeleton.BoneId.Body_LeftHandIndexTip:
                        return "Body_LeftHandIndexTip";
                    case OVRSkeleton.BoneId.Body_LeftHandMiddleMetacarpal:
                        return "Body_LeftHandMiddleMetacarpal";
                    case OVRSkeleton.BoneId.Body_LeftHandMiddleProximal:
                        return "Body_LeftHandMiddleProximal";
                    case OVRSkeleton.BoneId.Body_LeftHandMiddleIntermediate:
                        return "Body_LeftHandMiddleIntermediate";
                    case OVRSkeleton.BoneId.Body_LeftHandMiddleDistal:
                        return "Body_LeftHandMiddleDistal";
                    case OVRSkeleton.BoneId.Body_LeftHandMiddleTip:
                        return "Body_LeftHandMiddleTip";
                    case OVRSkeleton.BoneId.Body_LeftHandRingMetacarpal:
                        return "Body_LeftHandRingMetacarpal";
                    case OVRSkeleton.BoneId.Body_LeftHandRingProximal:
                        return "Body_LeftHandRingProximal";
                    case OVRSkeleton.BoneId.Body_LeftHandRingIntermediate:
                        return "Body_LeftHandRingIntermediate";
                    case OVRSkeleton.BoneId.Body_LeftHandRingDistal:
                        return "Body_LeftHandRingDistal";
                    case OVRSkeleton.BoneId.Body_LeftHandRingTip:
                        return "Body_LeftHandRingTip";
                    case OVRSkeleton.BoneId.Body_LeftHandLittleMetacarpal:
                        return "Body_LeftHandLittleMetacarpal";
                    case OVRSkeleton.BoneId.Body_LeftHandLittleProximal:
                        return "Body_LeftHandLittleProximal";
                    case OVRSkeleton.BoneId.Body_LeftHandLittleIntermediate:
                        return "Body_LeftHandLittleIntermediate";
                    case OVRSkeleton.BoneId.Body_LeftHandLittleDistal:
                        return "Body_LeftHandLittleDistal";
                    case OVRSkeleton.BoneId.Body_LeftHandLittleTip:
                        return "Body_LeftHandLittleTip";
                    case OVRSkeleton.BoneId.Body_RightHandPalm:
                        return "Body_RightHandPalm";
                    case OVRSkeleton.BoneId.Body_RightHandWrist:
                        return "Body_RightHandWrist";
                    case OVRSkeleton.BoneId.Body_RightHandThumbMetacarpal:
                        return "Body_RightHandThumbMetacarpal";
                    case OVRSkeleton.BoneId.Body_RightHandThumbProximal:
                        return "Body_RightHandThumbProximal";
                    case OVRSkeleton.BoneId.Body_RightHandThumbDistal:
                        return "Body_RightHandThumbDistal";
                    case OVRSkeleton.BoneId.Body_RightHandThumbTip:
                        return "Body_RightHandThumbTip";
                    case OVRSkeleton.BoneId.Body_RightHandIndexMetacarpal:
                        return "Body_RightHandIndexMetacarpal";
                    case OVRSkeleton.BoneId.Body_RightHandIndexProximal:
                        return "Body_RightHandIndexProximal";
                    case OVRSkeleton.BoneId.Body_RightHandIndexIntermediate:
                        return "Body_RightHandIndexIntermediate";
                    case OVRSkeleton.BoneId.Body_RightHandIndexDistal:
                        return "Body_RightHandIndexDistal";
                    case OVRSkeleton.BoneId.Body_RightHandIndexTip:
                        return "Body_RightHandIndexTip";
                    case OVRSkeleton.BoneId.Body_RightHandMiddleMetacarpal:
                        return "Body_RightHandMiddleMetacarpal";
                    case OVRSkeleton.BoneId.Body_RightHandMiddleProximal:
                        return "Body_RightHandMiddleProximal";
                    case OVRSkeleton.BoneId.Body_RightHandMiddleIntermediate:
                        return "Body_RightHandMiddleIntermediate";
                    case OVRSkeleton.BoneId.Body_RightHandMiddleDistal:
                        return "Body_RightHandMiddleDistal";
                    case OVRSkeleton.BoneId.Body_RightHandMiddleTip:
                        return "Body_RightHandMiddleTip";
                    case OVRSkeleton.BoneId.Body_RightHandRingMetacarpal:
                        return "Body_RightHandRingMetacarpal";
                    case OVRSkeleton.BoneId.Body_RightHandRingProximal:
                        return "Body_RightHandRingProximal";
                    case OVRSkeleton.BoneId.Body_RightHandRingIntermediate:
                        return "Body_RightHandRingIntermediate";
                    case OVRSkeleton.BoneId.Body_RightHandRingDistal:
                        return "Body_RightHandRingDistal";
                    case OVRSkeleton.BoneId.Body_RightHandRingTip:
                        return "Body_RightHandRingTip";
                    case OVRSkeleton.BoneId.Body_RightHandLittleMetacarpal:
                        return "Body_RightHandLittleMetacarpal";
                    case OVRSkeleton.BoneId.Body_RightHandLittleProximal:
                        return "Body_RightHandLittleProximal";
                    case OVRSkeleton.BoneId.Body_RightHandLittleIntermediate:
                        return "Body_RightHandLittleIntermediate";
                    case OVRSkeleton.BoneId.Body_RightHandLittleDistal:
                        return "Body_RightHandLittleDistal";
                    case OVRSkeleton.BoneId.Body_RightHandLittleTip:
                        return "Body_RightHandLittleTip";
                    default:
                        return "Body_Unknown";
                }
            }
            else if (IsHandSkeleton(skeletonType))
            {
                switch (boneId)
                {
                    case OVRSkeleton.BoneId.Hand_WristRoot:
                        return "Hand_WristRoot";
                    case OVRSkeleton.BoneId.Hand_ForearmStub:
                        return "Hand_ForearmStub";
                    case OVRSkeleton.BoneId.Hand_Thumb0:
                        return "Hand_Thumb0";
                    case OVRSkeleton.BoneId.Hand_Thumb1:
                        return "Hand_Thumb1";
                    case OVRSkeleton.BoneId.Hand_Thumb2:
                        return "Hand_Thumb2";
                    case OVRSkeleton.BoneId.Hand_Thumb3:
                        return "Hand_Thumb3";
                    case OVRSkeleton.BoneId.Hand_Index1:
                        return "Hand_Index1";
                    case OVRSkeleton.BoneId.Hand_Index2:
                        return "Hand_Index2";
                    case OVRSkeleton.BoneId.Hand_Index3:
                        return "Hand_Index3";
                    case OVRSkeleton.BoneId.Hand_Middle1:
                        return "Hand_Middle1";
                    case OVRSkeleton.BoneId.Hand_Middle2:
                        return "Hand_Middle2";
                    case OVRSkeleton.BoneId.Hand_Middle3:
                        return "Hand_Middle3";
                    case OVRSkeleton.BoneId.Hand_Ring1:
                        return "Hand_Ring1";
                    case OVRSkeleton.BoneId.Hand_Ring2:
                        return "Hand_Ring2";
                    case OVRSkeleton.BoneId.Hand_Ring3:
                        return "Hand_Ring3";
                    case OVRSkeleton.BoneId.Hand_Pinky0:
                        return "Hand_Pinky0";
                    case OVRSkeleton.BoneId.Hand_Pinky1:
                        return "Hand_Pinky1";
                    case OVRSkeleton.BoneId.Hand_Pinky2:
                        return "Hand_Pinky2";
                    case OVRSkeleton.BoneId.Hand_Pinky3:
                        return "Hand_Pinky3";
                    case OVRSkeleton.BoneId.Hand_ThumbTip:
                        return "Hand_ThumbTip";
                    case OVRSkeleton.BoneId.Hand_IndexTip:
                        return "Hand_IndexTip";
                    case OVRSkeleton.BoneId.Hand_MiddleTip:
                        return "Hand_MiddleTip";
                    case OVRSkeleton.BoneId.Hand_RingTip:
                        return "Hand_RingTip";
                    case OVRSkeleton.BoneId.Hand_PinkyTip:
                        return "Hand_PinkyTip";
                    default:
                        return "Hand_Unknown";
                }
            }
            else
            {
                return "Skeleton_Unknown";
            }
        }

        internal static bool IsBodySkeleton(OVRSkeleton.SkeletonType type) => type == OVRSkeleton.SkeletonType.Body;
        private static bool IsHandSkeleton(OVRSkeleton.SkeletonType type) =>
            type == OVRSkeleton.SkeletonType.HandLeft || type == OVRSkeleton.SkeletonType.HandRight;
    }
#else
    public class RemoteOVRSkeleton : MonoBehaviour {}
#endif
}
