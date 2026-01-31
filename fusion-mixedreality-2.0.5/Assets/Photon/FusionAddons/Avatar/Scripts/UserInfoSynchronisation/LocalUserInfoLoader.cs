using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Avatar
{
    public interface IAvatarURLProvider
    {
        string RandomAvatar();
    }

    /*
    * Load local user info into UserInfo
    * It should not be placed on a NetworkRig gameobject that are not associated with a player (Bots, ...)
    */
    public class LocalUserInfoLoader : NetworkBehaviour
    {
        public DefaultLocalUserInfoScriptable defaultLocalUserInfoScriptable;
        public IAvatarURLProvider avatarURLProvider = null;
        public override void Spawned()
        {
            base.Spawned(); 
            if(avatarURLProvider == null) avatarURLProvider = Runner.GetComponentInChildren<IAvatarURLProvider>();

            if (Object.HasInputAuthority)
            {
                bool overrideUserInfoPreferences = defaultLocalUserInfoScriptable != null && defaultLocalUserInfoScriptable.overrideUserInfoPreferences;

                if (TryGetComponent<UserInfo>(out UserInfo userInfo) )
                {
                    // Load saved user info
                    string avatarURL = PlayerPrefs.GetString(UserInfo.SETTINGS_AVATARURL);
                    if (avatarURL != null && avatarURL != "" && overrideUserInfoPreferences == false)
                    {
                        userInfo.AvatarURL = avatarURL;
                    }
                    else if (defaultLocalUserInfoScriptable != null)
                    {
                        if(defaultLocalUserInfoScriptable.userRandomAvatarUrlIfPossible && avatarURLProvider != null)
                        {
                            userInfo.AvatarURL = avatarURLProvider.RandomAvatar();
                        }
                        else
                        {
                            userInfo.AvatarURL = defaultLocalUserInfoScriptable.defaultLocalAvatarURL;
                        }
                    }

                    string userName = PlayerPrefs.GetString(UserInfo.SETTINGS_USERNAME);
                    if (userName != null && userName != "" && overrideUserInfoPreferences == false)
                    {
                        userInfo.UserName = userName;
                    }
                    else if (defaultLocalUserInfoScriptable != null)
                    {
                        userInfo.UserName = defaultLocalUserInfoScriptable.defaultLocalUsername;
                    }
                }
                
            }
        }
    }

}
