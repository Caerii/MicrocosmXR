using System.Collections.Generic;
using UnityEngine;
#if READY_PLAYER_ME
using ReadyPlayerMe.Core;
#endif 

namespace Fusion.Addons.Avatar.ReadyPlayerMe
{
    public class RPMAvatarLibrary : MonoBehaviour, IAvatarURLProvider
    {
        public List<RPMAvatarLoader.RPMCachedAvatarInfo> cachedAvatars;

        private void OnEnable()
        {
#if READY_PLAYER_ME
            foreach(var cacheInfo in cachedAvatars)
            {
                var metadata = cacheInfo.metadata;
                if (cacheInfo.avatarGameObject == null) continue;
                if(string.IsNullOrEmpty(cacheInfo.metadata.SkinTone))
                {
                    // No skintone metadata provided: check if it is not an error (as metadata presence allows to determine the avatar kind)");
                    AvatarData avatarData = cacheInfo.avatarGameObject.GetComponent<AvatarData>();
                    if (avatarData != null)
                    {
                        if (string.IsNullOrEmpty(avatarData.AvatarMetadata.SkinTone) == false)
                        {
                            metadata = avatarData.AvatarMetadata;
                        }
                    }
                }
                RPMAvatarLoader.CacheAvatar(cacheInfo.avatarURL, cacheInfo.avatarGameObject, metadata);
            }
#endif 
        }

        private void OnDisable()
        {
            foreach (var cacheInfo in cachedAvatars)
            {
                RPMAvatarLoader.UncacheAvatar(cacheInfo.avatarURL, cacheInfo.avatarGameObject);
            }
        }

        #region IAvatarURLProvider
        public string RandomAvatar()
        {
            if (cachedAvatars.Count == 0) return "";

            return cachedAvatars[Random.Range(0, cachedAvatars.Count - 1)].avatarURL;
        }
        #endregion
    }

}
