using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Avatar
{
    /***
     * 
     * HardwareHandRepresentationManager manages the hand representation according to the connection status : 
     * When the player is offline, the local hand is displayed
     * When the player is online, the local hand is displayed according to the displayLocalReprWhenOnline bool.
     * 
     * HardwareHandRepresentationManager is also in charge to forward hardware hand commands to children IHandRepresentation.
     * 
     ***/
    [RequireComponent(typeof(HardwareHand))]
    public class HardwareHandRepresentationManager : MonoBehaviour, IAvatarRepresentationListener
    {
        public bool displayLocalReprWhenOnline = false;

        HardwareHand hand;
        RigInfo rigInfo;
        public IHandRepresentation localRepresentation;
        public bool hideLocalHandWhenAvatarWithHandRepresentationIsLoaded = false;
        public bool adaptHardwareHandColor = false;

        // Transform that should be moved with the network representation if the local representation of the hand is not displayed
        public Transform handDecoration;


        private void Awake()
        {
            hand = GetComponent<HardwareHand>();
            var rig = GetComponentInParent<HardwareRig>();
            rigInfo = RigInfo.FindRigInfo(allowSceneSearch: true);
            localRepresentation = GetComponentInChildren<IHandRepresentation>();
        }


        private void Update()
        {
            ManageLocalHandRepresentationDisplay();
        }

        void ManageLocalHandRepresentationDisplay()
        {
            if (rigInfo != null && rigInfo.localNetworkedRig != null)
            {
                // Online: we hide the local representation if displayLocalReprWhenOnline is false
                if (displayLocalReprWhenOnline != true && localRepresentation != null && localRepresentation.IsMeshDisplayed)
                {
                    localRepresentation.DisplayMesh(false);
                } else if (displayLocalReprWhenOnline == true && localRepresentation != null && localRepresentation.IsMeshDisplayed == false)
                {
                    localRepresentation.DisplayMesh(true);
                }
            }
            else if (localRepresentation != null && !localRepresentation.IsMeshDisplayed)
            {
                // Offline, but local hand is currently not displayed: we display it
                localRepresentation.DisplayMesh(true);
            }
        }
        
        public void ChangeHandColor(Color color)
        {
            if (localRepresentation != null)
            {
                localRepresentation.SetHandColor(color);
            }
        }

        public void ChangeHandMaterial(Material material)
        {
            localRepresentation.SetHandMaterial(material);
        }

        #region IAvatarRepresentationListener
        public void OnAvailableAvatarsListed(AvatarRepresentation avatarRepresentation) {}

        public void OnRepresentationAvailable(IAvatar avatar, bool isLocalUserAvatar)
        {
            if (adaptHardwareHandColor)
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

            if (isLocalUserAvatar && avatar.AvatarDescription.includeHandRepresentation && hideLocalHandWhenAvatarWithHandRepresentationIsLoaded)
            {
                displayLocalReprWhenOnline = false;
            }
        }

        public void OnRepresentationUnavailable(IAvatar avatar) {}
        #endregion 
    }
}
