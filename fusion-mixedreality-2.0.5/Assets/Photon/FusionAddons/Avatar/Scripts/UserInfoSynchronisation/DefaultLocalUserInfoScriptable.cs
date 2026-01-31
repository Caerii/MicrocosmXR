using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Avatar
{
    [CreateAssetMenu(fileName = "DefaultLocalUserInfo", menuName = "Fusion Addons/DefaultLocalUserInfoScriptable", order = 1)]
    public class DefaultLocalUserInfoScriptable : ScriptableObject
    {
        public string defaultLocalUsername;
        public string defaultLocalAvatarURL;
        public bool userRandomAvatarUrlIfPossible = false;
        public bool overrideUserInfoPreferences = false;
    }
}
