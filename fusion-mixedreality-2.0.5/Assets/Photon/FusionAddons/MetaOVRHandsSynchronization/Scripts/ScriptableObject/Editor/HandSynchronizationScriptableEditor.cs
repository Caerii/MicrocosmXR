using UnityEditor;
using UnityEngine;
#if OCULUS_SDK_AVAILABLE
using Fusion.Addons.MetaOVRHandsSynchronization;


[CustomEditor(typeof(HandSynchronizationScriptable))]
public class HandSynchronizationScriptableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        HandSynchronizationScriptable scriptable = (HandSynchronizationScriptable)target;
        if (GUILayout.Button($"Storage byte size required: {scriptable.BoneInfoByteSize}\n(click to refresh total byte size)", GUILayout.Height(40f)))
        {
            scriptable.ResetBoneInfo();
            scriptable.UpdateBonesInfo();
        }
        base.OnInspectorGUI();
    }
}
#else

#endif
