#if OCULUS_SDK_AVAILABLE
using Fusion.Addons.Avatar;
using Fusion.XR.Shared.Rig;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
    /**
    * 
    * OVRHandRepresentationManager and its subclasses
    * - changes the hand's color or material based on the selected avatar skin
    * - handles which mesh is displayed based on hand mode
    * 
    * Requirements:
    * - OVRMeshRenderer confidence behaviour has to be set to None
    * - OVRMeshRenderer system gesture behaviour has to be set to None
    **/
    public abstract class OVRHandRepresentationManager : MonoBehaviour, IAvatarRepresentationListener
    {
        public SkinnedMeshRenderer fingerTrackingHandMeshRenderer;
        public SkinnedMeshRenderer secondaryFingerTrackingHandMeshRenderer;
        public SkinnedMeshRenderer controllerTrackingHandMeshRenderer;

        [Header("Override material")]
        [SerializeField]
        Material overrideMaterialForRenderers;
        public enum MaterialOverrideMode
        {
            Override,
            InitialMaterial
        }
        public MaterialOverrideMode materialOverrideMode = MaterialOverrideMode.InitialMaterial;
        Material initialMaterial;

        Dictionary<Renderer, MaterialOverrideMode> currentMaterialStateByRenderer = new Dictionary<Renderer, MaterialOverrideMode>();

        public abstract bool IsFingerDataValid { get; }

        public abstract RigPart Side { get; }
        public abstract GameObject FingerTrackingHandSkeletonParentGameObject { get; }
        public abstract GameObject ControllerTrackingHandSkeletonParentGameObject { get; }

        public void UpdateHandMeshesEnabled()
        {
            if (fingerTrackingHandMeshRenderer && controllerTrackingHandMeshRenderer)
            {
                fingerTrackingHandMeshRenderer.enabled = IsFingerDataValid;
                controllerTrackingHandMeshRenderer.enabled = IsFingerDataValid == false;
            }
            else
            {
                Debug.LogError("Missing mesh renderer");
            }
            if (secondaryFingerTrackingHandMeshRenderer) secondaryFingerTrackingHandMeshRenderer.enabled = IsFingerDataValid;

        }

        void FindRenderers()
        {
            if (fingerTrackingHandMeshRenderer == null)
            {
                fingerTrackingHandMeshRenderer = FingerTrackingHandSkeletonParentGameObject.GetComponent<SkinnedMeshRenderer>();
            }

            if (controllerTrackingHandMeshRenderer == null)
            {
                foreach (var renderer in ControllerTrackingHandSkeletonParentGameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (renderer == fingerTrackingHandMeshRenderer) continue;
                    if (renderer == secondaryFingerTrackingHandMeshRenderer) continue;
                    if (controllerTrackingHandMeshRenderer) Debug.LogError("Ambiguity in controllerTrackingHandMeshRenderer detection. Specify it manually " + name);
                    controllerTrackingHandMeshRenderer = renderer;
                }
            }
        }

        void UpdateRenderersMaterial()
        {

            ApplyOverrideMaterial(controllerTrackingHandMeshRenderer);
            ApplyOverrideMaterial(fingerTrackingHandMeshRenderer);
        }


        MaterialOverrideMode MaterialModeForRenderer(Renderer renderer)
        {
            if (currentMaterialStateByRenderer.ContainsKey(renderer)) return currentMaterialStateByRenderer[renderer];
            return MaterialOverrideMode.InitialMaterial;

        }

        public void ApplyOverrideMaterial(Renderer renderer)
        {
            if (renderer == null) return;

            if (MaterialModeForRenderer(renderer) == materialOverrideMode) return;

            currentMaterialStateByRenderer[renderer] = materialOverrideMode;

            bool initialMaterialStillUsed = false;
            if (initialMaterial == null)
            {
                initialMaterialStillUsed = true;
                initialMaterial = renderer.material;
            }

            if (materialOverrideMode == MaterialOverrideMode.Override)
            {
                if (overrideMaterialForRenderers == null) Debug.LogError("Missing overrideMaterialForRenderers");
                renderer.material = overrideMaterialForRenderers;
            }
            else if (renderer.material == null || initialMaterialStillUsed == false)
            {
                renderer.material = initialMaterial;
            }
        }

        protected virtual void Awake()
        {
            // Set directly override material if renderers are set in the inspector
            ApplyOverrideMaterial(controllerTrackingHandMeshRenderer);
            ApplyOverrideMaterial(fingerTrackingHandMeshRenderer);
        }

        protected virtual void Update()
        {
            FindRenderers();
            UpdateRenderersMaterial();
            UpdateHandMeshesEnabled();
        }

#region IAvatarRepresentationListener
        public void OnRepresentationAvailable(IAvatar avatar, bool isLocalUserAvatar)
        {
            if (avatar.AvatarDescription.colorMode == AvatarDescription.ColorMode.Color)
            {
                ChangeHandColor(avatar.AvatarDescription.skinColor);
            }
            else if (avatar.AvatarDescription.colorMode == AvatarDescription.ColorMode.Material)
            {
                ChangeHandMaterial(avatar.AvatarDescription.skinMaterial);
            }
        }

        public void OnAvailableAvatarsListed(AvatarRepresentation avatarRepresentation) { }
        public void OnRepresentationUnavailable(IAvatar avatar) { }
#endregion

        public virtual void ChangeHandColor(Color color)
        {
            FindRenderers();
            if (fingerTrackingHandMeshRenderer && MaterialModeForRenderer(fingerTrackingHandMeshRenderer) == MaterialOverrideMode.InitialMaterial) ChangeMaterialColor(fingerTrackingHandMeshRenderer.material, color);
            if (controllerTrackingHandMeshRenderer && MaterialModeForRenderer(controllerTrackingHandMeshRenderer) == MaterialOverrideMode.InitialMaterial) ChangeMaterialColor(controllerTrackingHandMeshRenderer.material, color);
            ChangeMaterialColor(initialMaterial, color);
        }

        public virtual void ChangeHandMaterial(Material material)
        {
            FindRenderers();
            if (MaterialModeForRenderer(fingerTrackingHandMeshRenderer) == MaterialOverrideMode.InitialMaterial) fingerTrackingHandMeshRenderer.sharedMaterial = material;
            if (MaterialModeForRenderer(controllerTrackingHandMeshRenderer) == MaterialOverrideMode.InitialMaterial) controllerTrackingHandMeshRenderer.sharedMaterial = material;
            initialMaterial = material;
        }

        void ChangeMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            material.color = color;
            material.SetTexture("_BaseMap", null);
        }
    }
}
#endif
