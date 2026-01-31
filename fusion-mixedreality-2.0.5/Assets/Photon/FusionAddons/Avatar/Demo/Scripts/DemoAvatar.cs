using Fusion.Addons.Avatar;
using Fusion.XR.Shared;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoAvatar : MonoBehaviour, IAvatar
{
    public GameObject avatarRoot;
    public GameObject head;
    public GameObject clothes;
    public string urlProtocol = "myprotocol";
    public float loadingTime = 0;
    public bool selected = false;
    AvatarRepresentation avatarRepresentation;

    private void Awake()
    {
        avatarRepresentation = GetComponentInParent<AvatarRepresentation>();
    }

    #region IAvatar
    public AvatarStatus AvatarStatus { get; set; } = AvatarStatus.NotLoaded;

    public AvatarDescription AvatarDescription { get; set; } = new AvatarDescription()
    {
        colorMode = AvatarDescription.ColorMode.NoColorInfo
    };

    public string AvatarURL { get; set; }
    public int TargetLODLevel => 0;

    public bool ShouldLoadLocalAvatar => true;

    public GameObject AvatarGameObject => avatarRoot;

    public async void ChangeAvatar(string avatarURL)
    {
        AvatarURL = avatarURL;
        Debug.Log("[DemoAvatar] Loading avatar: " + avatarURL);
        selected = true;
        ChangeAvatarVisibility(false);
        if(loadingTime > 0)
        {
            if (avatarRepresentation) avatarRepresentation.LoadingRepresentation(this);
            AvatarStatus = AvatarStatus.RepresentationLoading;
            await AsyncTask.Delay((int)(loadingTime * 1000f));
        }
        ChangeAvatarVisibility(true);
        AvatarStatus = AvatarStatus.RepresentationAvailable;
        if(loadingTime > 0) Debug.Log("[DemoAvatar] Avatar loaded: " + avatarURL);
        if(avatarRepresentation) avatarRepresentation.RepresentationAvailable(this);
    }

    public string LoadRandomAvatar()
    {
        return $"{urlProtocol}://avatar";
    }

    public void RemoveAvatar()
    {
        if(selected) Debug.Log($"[DemoAvatar] Unloading avatar for protocol {urlProtocol}");
        selected = false;
        ChangeAvatarVisibility(false);
        if (avatarRepresentation) avatarRepresentation.RemoveRepresentation(this);
    }

    public AvatarUrlSupport SupportForURL(string url)
    {
        if(url.Contains($"{urlProtocol}", System.StringComparison.CurrentCultureIgnoreCase))
        {
            return AvatarUrlSupport.Compatible;
        }
        return AvatarUrlSupport.Incompatible;
    }
    #endregion

    void ChangeAvatarVisibility(bool visible)
    {
        avatarRoot.SetActive(visible);
    }
}
