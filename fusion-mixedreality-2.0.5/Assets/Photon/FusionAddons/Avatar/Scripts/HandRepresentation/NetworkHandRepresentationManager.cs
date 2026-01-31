using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Avatar
{
    /***
     * 
     * NetworkHandRepresentationManager component is located on players network rig.
     * It manages the local hand representation according to the displayForLocalPlayer bool,
     * (most of the time, hardware hands and network hands should not be displayed in the same time).
     * If the hardware hand has a decoration (watch for example), NetworkHandRepresentationManager ensures that the decoration follows the network hand interpolation target (usefull when the network hand interpolation target is manually changed - hand blocked, ...)
     * 
     ***/
    public class NetworkHandRepresentationManager : MonoBehaviour, IAvatarRepresentationListener
    {
        public bool displayForLocalPlayer = false;
        NetworkRig networkRig;
        NetworkHand networkHand;
        public IHandRepresentation handRepresentation;

        bool hasSearchedHardwareHandRepresentationManager = false;
        public HardwareHandRepresentationManager hardwareHandRepresentationManager;
        public Vector3 hardwareHandDecorationOffset;
        public Quaternion hardwareHandDecorationRotationOffset;

        private void Awake()
        {
            networkRig = GetComponentInParent<NetworkRig>();
            networkHand = GetComponent<NetworkHand>();
            if (handRepresentation == null) handRepresentation = GetComponentInChildren<IHandRepresentation>();
        }

        private void Update()
        {
            ManageLocalHandRepresentationDisplay();
        }

        private void LateUpdate()
        {
            // Ensure that the hardware hand decoration, if any follows the network hand interpolation target (usefull when the network hand interpolation target is manually changed - hand blocked, ...)
            if(networkRig && networkRig.IsLocalNetworkRig)
            {
                if (!hasSearchedHardwareHandRepresentationManager)
                {
                    hasSearchedHardwareHandRepresentationManager = true;
                    if(networkHand && networkHand.LocalHardwareHand)
                    {
                        hardwareHandRepresentationManager = networkHand.LocalHardwareHand.GetComponentInChildren<HardwareHandRepresentationManager>();
                        if (hardwareHandRepresentationManager && hardwareHandRepresentationManager.handDecoration)
                        {
                            hardwareHandDecorationOffset = networkHand.LocalHardwareHand.transform.InverseTransformPoint(hardwareHandRepresentationManager.handDecoration.transform.position);
                            hardwareHandDecorationRotationOffset = Quaternion.Inverse(networkHand.LocalHardwareHand.transform.rotation) * hardwareHandRepresentationManager.handDecoration.transform.rotation;
                        }
                    }
                }
                if (hardwareHandRepresentationManager && hardwareHandRepresentationManager.handDecoration)
                {
                    hardwareHandRepresentationManager.handDecoration.transform.position = networkHand.transform.TransformPoint(hardwareHandDecorationOffset);
                    hardwareHandRepresentationManager.handDecoration.transform.rotation = networkHand.transform.rotation * hardwareHandDecorationRotationOffset;
                }
            }
        }

        void ManageLocalHandRepresentationDisplay()
        {
            if (networkRig && networkRig.IsLocalNetworkRig)
            {
                // This hand is associated to the local user. We manage its display accordingly to displayForLocalPlayer
                if (displayForLocalPlayer != true && handRepresentation != null && handRepresentation.IsMeshDisplayed)
                {
                    handRepresentation.DisplayMesh(false);
                }
                else if (displayForLocalPlayer == true && handRepresentation != null && !handRepresentation.IsMeshDisplayed)
                {
                    handRepresentation.DisplayMesh(true);
                }
            }
        }

        public void ChangeHandColor(Color color)
        {
            if (handRepresentation != null)
            {
                handRepresentation.SetHandColor(color);
            }
        }
        public void ChangeHandMaterial(Material material)
        {
            handRepresentation.SetHandMaterial(material);
        }

        #region IAvatarRepresentationListener
        public void OnAvailableAvatarsListed(AvatarRepresentation avatarRepresentation) { }

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

        public void OnRepresentationUnavailable(IAvatar avatar) { }
        #endregion 
    }
}

