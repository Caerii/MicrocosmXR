using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * 
 * At start, the script search the AvatarRepresentation in parent to know which type of avatar should be loaded. It can be a simple avatar model or a Ready Player Me model.
 * 
 * Then, materials for body, hair and clothes colors are configured according to the avatar colors.
 * Also, if the avatar model is a simple avatar, the hair mesh is configured with the hair LOD mesh corresponding to the simple avatar model.
 * 
 **/

namespace Fusion.Addons.Avatar
{
    public class LowPolySimpleAvatar : MonoBehaviour, IAvatarRepresentationListener
    {
        public MeshRenderer hairRenderer;
        public MeshFilter hairMeshFilter;
        public MeshRenderer bodyRenderer;
        public MeshRenderer clothRenderer;

        public AvatarRepresentation avatarRepresentation;

        [Header("Avatar options")]
        public List<Mesh> hairMeshes = new List<Mesh>();

        private void Awake()
        {
            if (avatarRepresentation == null)
            {
                avatarRepresentation = GetComponentInParent<AvatarRepresentation>();
            }
            if (hairMeshFilter == null)
            {
                hairMeshFilter = hairRenderer.GetComponent<MeshFilter>();
            }
        }

        #region LOD Avatar
        public void LoadAvatarDescription(IAvatar avatar, AvatarDescription avatarDescription)
        {
            switch (avatarDescription.colorMode)
            {
                case AvatarDescription.ColorMode.Color:
                    LoadAvatarColors(avatar, avatarDescription.skinColor, avatarDescription.clothColor, avatarDescription.hairColor);
                    break;
                case AvatarDescription.ColorMode.Material:
                    LoadAvatarSharedMaterials(avatar, avatarDescription.skinMaterial, avatarDescription.clothMaterial, avatarDescription.hairMaterial);
                    break;
            }
            switch (avatarDescription.hairMode)
            {
                case AvatarDescription.HairMode.HairStyle:
                    LoadAvatarLODHairStyle(avatar, avatarDescription.hairStyle);
                    break;
                case AvatarDescription.HairMode.OnOff:
                    LoadAvatarBaldness(avatar, avatarDescription.isBald);
                    break;
            }
        }

        public void LoadAvatarColors(IAvatar avatar, Color skinColor, Color clothColor, Color hairColor)
        {
            bodyRenderer.material.color = skinColor;
            clothRenderer.material.color = clothColor;
            hairRenderer.material.color = hairColor;
        }

        public void LoadAvatarBaldness(IAvatar avatar, bool isBald)
        {
            // do no display the hair renderer if avatar hair is transparent (for bald-headed models)
            hairRenderer.enabled = isBald == false;
        }

        public void LoadAvatarSharedMaterials(IAvatar avatar, Material bodySharedMaterial, Material clothSharedMaterial, Material hairSharedMaterial)
        {
            bodyRenderer.sharedMaterial = bodySharedMaterial;
            clothRenderer.sharedMaterial = clothSharedMaterial;
            hairRenderer.sharedMaterial = hairSharedMaterial;
        }

        public void LoadAvatarLODHairStyle(IAvatar avatar, int hairStyle)
        {
            if (hairStyle < hairMeshes.Count)
            {
                hairMeshFilter.sharedMesh = hairMeshes[hairStyle];
            }
        }
        #endregion

        #region IAvatarRepresentationListener
        public void OnAvailableAvatarsListed(AvatarRepresentation avatarRepresentation) {}

        public void OnRepresentationAvailable(IAvatar avatar, bool isLocalUserAvatar)
        {
            LoadAvatarDescription(avatar, avatar.AvatarDescription);
        }

        public void OnRepresentationUnavailable(IAvatar avatar) {}
        #endregion
    }
}
