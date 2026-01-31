using Fusion.Addons.MetaOVRHandsSynchronization;
using Fusion.XR.Shared;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleHandDisplayMode : MonoBehaviour
{
#if OCULUS_SDK_AVAILABLE

    public UnityEngine.InputSystem.InputActionProperty toggleAction;
    public OVRHandRepresentationManager ovrHandRepresentationManager;

    private void Awake()
    {
        toggleAction.EnableWithDefaultXRBindings(
            leftBindings: new List<string> { "primaryButton"/*, "secondaryTouched"*/ },
            rightBindings: new List<string> { "primaryButton"/*, "secondaryTouched"*/ });
    }

    private void Update()
    {
        var togglePressed = toggleAction.action.WasPressedThisFrame();

        if (togglePressed)
        {
            if (ovrHandRepresentationManager.materialOverrideMode == OVRHandRepresentationManager.MaterialOverrideMode.InitialMaterial)
                ovrHandRepresentationManager.materialOverrideMode = OVRHandRepresentationManager.MaterialOverrideMode.Override;
            else
                ovrHandRepresentationManager.materialOverrideMode = OVRHandRepresentationManager.MaterialOverrideMode.InitialMaterial;
        }
    }
#endif
}
