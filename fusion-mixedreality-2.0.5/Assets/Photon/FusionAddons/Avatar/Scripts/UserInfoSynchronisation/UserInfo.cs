using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.Avatar
{
    /*
     * Synchornize user metadata
     */
    public class UserInfo : NetworkBehaviour
    {
        public const string SETTINGS_AVATARURL = "avatarURL";
        public const string SETTINGS_USERNAME = "userName";

        [Networked]
        public NetworkString<_32> UserName { get; set; }

        [Networked]
        public NetworkString<_128> AvatarURL { get; set; }


        [Header("Events")]
        public UnityEvent onUserNameChange;
        public UnityEvent onUserAvatarChange;

        ChangeDetector renderChangeDetector;

        public override void Spawned()
        {
            base.Spawned();
            OnUserNameChange();
            OnAvatarURLChange();
            renderChangeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);
        }

        void OnUserNameChange()
        {
            Debug.Log("[UserInfo] Username changed: " + UserName);
            if (onUserNameChange != null) onUserNameChange.Invoke();
        }
        void OnAvatarURLChange()
        {
            Debug.Log("[UserInfo] Avatar changed: " + AvatarURL);
            if (onUserAvatarChange != null) onUserAvatarChange.Invoke();
        }

        void DetectChanges()
        {
            foreach (var changedNetworkedVarName in renderChangeDetector.DetectChanges(this))
            {
                switch (changedNetworkedVarName)
                {
                    case nameof(UserName):
                        OnUserNameChange();
                        break;

                    case nameof(AvatarURL):
                        OnAvatarURLChange();
                        break;
                }
            }
        }

        public override void Render()
        {
            DetectChanges();
        }
    }
}

