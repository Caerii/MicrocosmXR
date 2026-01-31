using UnityEngine;
#if OCULUS_SDK_AVAILABLE
using System.Collections;
using System.Collections.Generic;

public class DebugOVRBones : MonoBehaviour
{
    public OVRSkeleton.SkeletonType skeletonType = OVRSkeleton.SkeletonType.HandLeft;

    public List<OVRSkeleton.BoneId> bonesToDebug = new List<OVRSkeleton.BoneId>();
    Dictionary<OVRSkeleton.BoneId, Transform> gizmoByBoneId = new Dictionary<OVRSkeleton.BoneId, Transform>();

    public GameObject gizmoPrefab;
    public float gizmoScale = 0.1f;

    public bool createPrimitives = false;
    public bool createPrimitivesOnScriptDemand = false;
    public bool createPrimitiveUseLocalPosition = true;

    public void CreatePrimitives(string namePrefix = "")
    {
        if (createPrimitives == false && createPrimitivesOnScriptDemand == false) return;
        foreach (var debugBone in bonesToDebug)
        {
            Transform boneTransform = transform.FindChildRecursive(OVRSkeleton.BoneLabelFromBoneId(OVRSkeleton.SkeletonType.HandLeft, debugBone));
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (createPrimitiveUseLocalPosition)
            {
                c.transform.SetPositionAndRotation(transform.InverseTransformPoint(boneTransform.position), Quaternion.Inverse(transform.rotation) * boneTransform.rotation);
            }
            else
            {
                c.transform.SetPositionAndRotation(boneTransform.position, boneTransform.rotation);
            }
            c.transform.localScale = 0.001f * Vector3.one;
            c.name = namePrefix + ":"+ boneTransform.name;
        }
    }
    private void Update()
    {

#if UNITY_EDITOR
        if(createPrimitives)
            CreatePrimitives();


        foreach(var debugBone in bonesToDebug)
        {
            if (gizmoByBoneId.ContainsKey(debugBone) == false)
            {
                Transform boneTransform = transform.FindChildRecursive(OVRSkeleton.BoneLabelFromBoneId(OVRSkeleton.SkeletonType.HandLeft, debugBone));
                var gizmo = GameObject.Instantiate(gizmoPrefab);
                gizmo.transform.parent = boneTransform;
                gizmo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                gizmo.transform.localScale = gizmoScale * Vector3.one;
                gizmoByBoneId[debugBone] = gizmo.transform;
            }
        }
        var keys = new List<OVRSkeleton.BoneId>(gizmoByBoneId.Keys);
        foreach (var bone in keys)
        {
            if (bonesToDebug.Contains(bone) == false)
            {
                var boneTransform = gizmoByBoneId[bone];
                gizmoByBoneId.Remove(bone);
                Destroy(boneTransform.gameObject);
            }
        }
#endif 
    }
}
#else
public class DebugOVRBones : MonoBehaviour {}
#endif
