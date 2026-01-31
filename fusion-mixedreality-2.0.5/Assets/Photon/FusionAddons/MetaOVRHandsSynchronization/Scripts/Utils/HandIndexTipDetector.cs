using UnityEngine;
#if OCULUS_SDK_AVAILABLE
using Fusion.Addons.MetaOVRHandsSynchronization;
using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;

/**
 * Detect finger tracking hand tip and controller tracking hand tip
 */
public class HandIndexTipDetector : MonoBehaviour
{
    public OVRHandRepresentationManager ovrHandRepresentationManager;
    public BoxCollider indexFingerTrackingModeCollider;
    public BoxCollider indexControllerModeCollider;

    [Header("Controller model bone names")]
    [SerializeField] 
    string controllerLeftIndexBoneName = "b_l_index_ignore";
    [SerializeField]
    string controllerRightIndexBoneName = "b_r_index_ignore";

    private void Awake()
    {
        if (ovrHandRepresentationManager == null) ovrHandRepresentationManager = GetComponentInChildren<OVRHandRepresentationManager>();
    }

    private void Update()
    {
        CreatedIndexCollider();
    }

    void CreatedIndexCollider()
    {
        if (indexFingerTrackingModeCollider == null && ovrHandRepresentationManager.IsFingerDataValid)
        {
            OVRSkeleton.SkeletonType skeletonType = OVRSkeleton.SkeletonType.HandLeft;
            OVRSkeleton.BoneId indexBone = OVRSkeleton.BoneId.Hand_IndexTip;
            if (ovrHandRepresentationManager.Side == RigPart.RightController)
            {
                skeletonType = OVRSkeleton.SkeletonType.HandRight;
            }
            var indexName = OVRSkeleton.BoneLabelFromBoneId(skeletonType, indexBone);
            indexFingerTrackingModeCollider = CreateIndexCollider(ovrHandRepresentationManager.FingerTrackingHandSkeletonParentGameObject, indexName);
        }

        if (indexControllerModeCollider == null && ovrHandRepresentationManager.IsFingerDataValid == false)
        {
            var indexName = controllerLeftIndexBoneName;
            if (ovrHandRepresentationManager.Side == RigPart.RightController)
            {
                indexName = controllerRightIndexBoneName;
            }
            indexControllerModeCollider = CreateIndexCollider(ovrHandRepresentationManager.ControllerTrackingHandSkeletonParentGameObject, indexName);
        }
    }

    BoxCollider CreateIndexCollider(GameObject root, string indexName)
    {
        var index = root.transform.FindChildRecursive(indexName);
        if (index)
        {
            var collider = index.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = index.gameObject.AddComponent<BoxCollider>();
                collider.size = 0.02f * Vector3.one;
                collider.center = new Vector3(0.01f, 0f, 0f);
                collider.isTrigger = true;
            }
            return collider;
        }
        else
        {
            return null;
        }
    }
}
#else
public class HandIndexTipDetector : MonoBehaviour {}
#endif