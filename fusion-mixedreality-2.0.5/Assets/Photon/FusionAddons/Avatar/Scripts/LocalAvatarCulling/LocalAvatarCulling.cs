using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Avatar
{
    /**
     * Ensure that the user camera cannot see its own avatar, by making its layer "invisible"
     */
    [RequireComponent(typeof(HardwareRig))]
    public class LocalAvatarCulling : MonoBehaviour, IAvatarRepresentationListener
    {
        public string localAvatarLayer = "InvisibleForLocalPlayer";
        public string remoteAvatarLayer = "Default";
        public bool hideLocalAvatar = true;
        HardwareRig rig;

        private void Awake()
        {
            rig = GetComponent<HardwareRig>();
        }

        void ConfigureCamera()
        {
            int layer = LayerMask.NameToLayer(localAvatarLayer);
            if (hideLocalAvatar && layer != -1)
            {
                var camera = rig.headset.GetComponentInChildren<Camera>();
                camera.cullingMask &= ~(1 << layer);
            }
        }

        private void Start()
        {
            // Change camera culling mask to hide local user, if required by hideLocalAvatar
            ConfigureCamera();
        }

        public void ConfigureLocalRenderers(GameObject avatar)
        {
            if (localAvatarLayer != "")
            {
                int layer = LayerMask.NameToLayer(localAvatarLayer);
                if (layer == -1)
                {
                    Debug.LogError($"Local will be visible and may obstruct you vision. Please add a {localAvatarLayer} layer (it will be automatically removed on the camera culling mask)");
                }
                else
                {
                    foreach (var renderer in avatar.GetComponentsInChildren<Renderer>())
                    {
                        renderer.gameObject.layer = layer;
                    }
                    avatar.layer = layer;
                }
            }
        }

        public void ConfigureRemoteRenderers(GameObject avatar)
        {
            int layer = LayerMask.NameToLayer("Default");
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>())
            {
                renderer.gameObject.layer = layer;
            }
            avatar.layer = layer;
        }

        #region IAvatarRepresentationListener
        public void OnAvailableAvatarsListed(AvatarRepresentation avatarRepresentation) {}

        public void OnRepresentationAvailable(IAvatar avatar, bool isLocalUserAvatar)
        {
            // ConfigureAvatarLayer check if the avatar is a local player. If true, avatar renderers are move to a specific layer to be hidden by the local player camera.
            // Else, avatar renderers are set to the Default layer
            if (isLocalUserAvatar)
            {
                ConfigureLocalRenderers(avatar.AvatarGameObject);
            }
            else
            {
                ConfigureRemoteRenderers(avatar.AvatarGameObject);
            }
        }

        public void OnRepresentationUnavailable(IAvatar avatar) {}
        #endregion 
    }
}
