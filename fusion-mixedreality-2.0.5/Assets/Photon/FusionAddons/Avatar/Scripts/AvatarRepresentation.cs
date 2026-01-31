using Fusion;
using Fusion.XR;
using Fusion.XR.Shared;
using Fusion.XR.Shared.Rig;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/**
 * 
 *  AvatarRepresentation : 
 *  - listens the UserInfo onUserAvatarChange event and replace the current avatar by the new one. 
 *	- changes the avatar hands' material & color (for "simple avatar" or ReadyPlayerMe avatar)
 *	- manage the avatar LOD
 * 
 **/

namespace Fusion.Addons.Avatar
{
	// States of an IAvatar status
	public enum AvatarStatus
	{
		NotLoaded,
		RepresentationAvailable,
		RepresentationLoading,
		RepresentationMissing
	}

	// Compatibility results of a URL compatibility test with a IAvatar class
	public enum AvatarUrlSupport
	{
		Compatible,
		Incompatible,
		Maybe
	}

	[System.Serializable]
	public struct AvatarDescription
	{
		public enum ColorMode
		{
			NoColorInfo = 0,
			Material = 1,
			Color = 2,
		}
		public enum HairMode {
			Undefined = 0,
			// Defined by baldness
			OnOff = 1,
			// Defined by har style int
			HairStyle = 2
	    }

		// If not true, use color info
		public ColorMode colorMode;
		public Material skinMaterial;
		public Material clothMaterial;
		public Material hairMaterial;
		public Color skinColor;
		public Color clothColor;
		public Color hairColor;
		public HairMode hairMode;
		public bool isBald;
		public int hairStyle;
		public bool includeHandRepresentation;
	}

	// Interface for Avatar representations
	public interface IAvatar
    {
		public AvatarStatus AvatarStatus { get; }
		public string AvatarURL { get; }
		public AvatarDescription AvatarDescription { get; }
		public int TargetLODLevel { get; }
		// Should Changeavatar be called for the local user
		public bool ShouldLoadLocalAvatar { get; }
		// Tells if a provided url could be loaded by this avatar
		public AvatarUrlSupport SupportForURL(string url);
		// Launch the avatar loading. When loading, the IAvatar has to call back RepresentationAvailable
		public void ChangeAvatar(string avatarURL);
		// Called when another avatar is loaded
		public void RemoveAvatar();
		public GameObject AvatarGameObject { get; }
		// If the avatar system does not support random avatar, should return null
		public string LoadRandomAvatar();
	}

	///  AvatarRepresentation callbacks for listeners
	public interface IAvatarRepresentationListener
    {
		public void OnAvailableAvatarsListed(AvatarRepresentation avatarRepresentation);
		public void OnRepresentationAvailable(IAvatar avatar, bool isLocalUserAvatar);
		public void OnRepresentationUnavailable(IAvatar avatar);
	}

	public class AvatarRepresentation : MonoBehaviour
	{
		[System.Flags]
		public enum AvatarLoadingMode
		{
			DisplayNextLODForLocalUser = 1,
			DisplayNextLODForRemoteUser = 2
		}

		public IAvatar currentAvatar;
		public List<IAvatar> availableAvatars = new List<IAvatar>();

		LODGroup lod;
		public NetworkRig networkRig;

		[Header("LOD")]

		[Tooltip("If true, the LOD won't be used (unless the LOD level 0 avatar is loading)")]
		bool ignoreDistance = false;

		[Tooltip("How the avatar system behave when an avatar is loading (remote avatar download, ...)")]
		public AvatarLoadingMode loadingMode = AvatarLoadingMode.DisplayNextLODForRemoteUser;

		LocalAvatarCulling localAvatarCulling;

		#region Hardware rig listeners detection
		bool hardwareRigListenersSearched = false;
		List<IAvatarRepresentationListener> _hardwareRigListeners = new List<IAvatarRepresentationListener>();
		List<IAvatarRepresentationListener> HardwareRigListeners
		{
			get
			{
				if (hardwareRigListenersSearched == false)
				{
					if (RigInfo && RigInfo.localHardwareRig)
					{
						hardwareRigListenersSearched = true;
						_hardwareRigListeners = new List<IAvatarRepresentationListener>(RigInfo.localHardwareRig.GetComponentsInChildren<IAvatarRepresentationListener>());
					}
				}
				return _hardwareRigListeners;
			}
		}
		#endregion

		RigInfo _rigInfo;
		RigInfo RigInfo
        {
			get
            {
				if(_rigInfo == null)
                {
					if (!networkRig || networkRig.Object == null) return null;
					_rigInfo = RigInfo.FindRigInfo(networkRig.Object.Runner);
				}
				return _rigInfo;
            }
        }

		UserInfo userInfo;
		IAvatarRepresentationListener[] listeners;

		public GameObject avatarNameGO;
		private TextMeshPro avatarNameTMP;

		private void Awake()
		{
			availableAvatars = new List<IAvatar>(GetComponentsInChildren<IAvatar>());

			lod = GetComponentInChildren<LODGroup>();
			networkRig = GetComponentInParent<NetworkRig>();
			listeners = GetComponentsInChildren<IAvatarRepresentationListener>();
			foreach (var listener in listeners) listener.OnAvailableAvatarsListed(this);

			if (avatarNameGO)
				avatarNameTMP = avatarNameGO.GetComponentInChildren<TextMeshPro>();
		}

        void OnEnable()
		{
			if (userInfo == null && networkRig) userInfo = networkRig.GetComponentInChildren<UserInfo>();
			else if (userInfo == null) userInfo = GetComponentInParent<UserInfo>();

			if (userInfo)
			{
				userInfo.onUserAvatarChange.AddListener(OnUserAvatarChange);
				userInfo.onUserNameChange.AddListener(OnUserNameChange);
			}
		}

        #region UserInfo callbacks
        // OnUserAvatarChange replaces the current avatar by the new avatar specified in the XRNetworkedRig
        private void OnUserAvatarChange()
		{
			string avatarURL = userInfo.AvatarURL.ToString();
			ChangeAvatar(avatarURL);
		}

		// OnUserAvatarChange replaces the current username by the new name specified in the XRNetworkedRig/UserInfo 
		void OnUserNameChange()
        {
			ChangeAvatarName(userInfo.UserName.ToString());
		}
        #endregion

        #region API
        public void ChangeAvatarName(string username)
		{
			if (avatarNameGO == null) return;

			// Display the username
			if (username != "")
			{
				avatarNameGO.SetActive(true);
				if (!avatarNameTMP)
						avatarNameTMP = avatarNameGO.GetComponentInChildren<TextMeshPro>();
				if (avatarNameTMP)
					avatarNameTMP.text = username;
				else
					Debug.LogError("TextMeshPro to display username not found");
			}
			else
            {
				avatarNameGO.SetActive(false);
			}
		}

		public void ChangeAvatar(string avatarURL)
		{
			Debug.Log("ChangeAvatar: " + avatarURL);
			var avatar = FindSuitableAvatar(avatarURL);

			// If loadLocalAvatar is false, do not spawn our local avatar (to be review if a mirror is needed)
			if (avatar != null && !avatar.ShouldLoadLocalAvatar && networkRig.IsLocalNetworkRig)
			{
				return;
			}
			// remove the previous loaded avatar if the new one is a different type of avatar
			WillLoadAvatar(avatar);
			// load the new avatar. RepresentationAvailable has to be then called (and/or RepresentationUnavailable if needed)
			if (avatar != null)
			{
				avatar.ChangeAvatar(avatarURL);
			}
			// memorize the new type of avatar
			currentAvatar = avatar;
            if (avatar == null)
            {
				Debug.Log("No compatible avatar for url " + avatarURL);
            }
		}

		// if the avatar type is a simple avatar, RandomAvatar return a random config URL (and set the avatar representation to this one)
		public string RandomAvatar()
		{
			string url = "";
			List<string> validUrls = new List<string>();
			foreach (var avatar in availableAvatars)
			{
				var avatarUrl = avatar.LoadRandomAvatar();
				if (avatarUrl != "") validUrls.Add(avatarUrl);
			}
			if (validUrls.Count > 0) url = validUrls[Random.Range(0, validUrls.Count)];
			return url;
		}

		public string RandomAvatar<T>() where T:IAvatar
		{
			string url = "";
			foreach (var avatar in availableAvatars)
			{
                if ((avatar is T) == false)
                {
					continue;
                }
				var avatarUrl = avatar.LoadRandomAvatar();
				if (avatarUrl != "") url = avatarUrl;
			}
			return url;
		}

		// Disable the LOD (to see the best LOD available all the time)
		public void IgnoreDistance(bool ignore)
		{
			ignoreDistance = ignore;
			if (ignore)
			{
				ForceLOD(0);
			}
			else
			{
				if (currentAvatar == null || currentAvatar.AvatarStatus == AvatarStatus.RepresentationAvailable)
				{
					ForceLOD(-1);
				}
			}
		}
		#endregion

		#region Avatar loading logic steps
		IAvatar FindSuitableAvatar(string avatarURL, int lodLevel = 0)
        {
			IAvatar compatibleAvatar = null;
			foreach(var avatar in availableAvatars)
            {
				if(avatar.TargetLODLevel != lodLevel) continue;
				var compatibility = avatar.SupportForURL(avatarURL);
				if (compatibility == AvatarUrlSupport.Compatible)
				{
					compatibleAvatar = avatar;
					break;
				}
				if (compatibility == AvatarUrlSupport.Maybe && compatibleAvatar == null)
				{
					compatibleAvatar = avatar;
				}
			}
			return compatibleAvatar;
        }

		// WillLoadAvatar removes the previous loaded avatar if the new one is a different type of avatar
		void WillLoadAvatar(IAvatar newAvatar)
		{
			foreach (var avatar in availableAvatars)
			{
				if (newAvatar != avatar) avatar.RemoveAvatar();
			}
		}
		#endregion

		#region IAvatar callbacks

		// Called by an avatar system while it is loading an avatar. During this, the avatar is unavailable, so for remote user we want at least the next LOD level to be visible
		public void LoadingRepresentation(IAvatar avatar)
		{
			bool isLocalUser = (networkRig && networkRig.Object)? networkRig.Object.HasInputAuthority : false;
			bool useNextLOD = (isLocalUser && ((loadingMode & AvatarLoadingMode.DisplayNextLODForLocalUser) != 0));
			useNextLOD = useNextLOD || (isLocalUser == false && ((loadingMode & AvatarLoadingMode.DisplayNextLODForRemoteUser) != 0));
			if (lod && useNextLOD)
			{
				// Active low level LOD while nothing is loaded
				RepresentationUnavailable(avatar);
			}
		}

		// If an avatar representation for a LOD level is unavailable (loading, bad url, ...) we ensure that we see the next LOD level instead, by forcing its activation
		public void RepresentationUnavailable(IAvatar avatar)
		{
			foreach (var listener in listeners) listener.OnRepresentationUnavailable(avatar);
			int level = avatar.TargetLODLevel;
			if (lod)
			{
				Debug.Log($"Using LOD level {level + 1}, as RepresentationUnavailable received");
				ForceLOD(level + 1);
			}
		}

		// When an avatar has been loaded, we reenable the LOD system properly: we stop forcing it to the next level if we did during the loading, and we add the new avatar renderers the the LODGroup at the desired LOD level
		//  We also hide the offline hand, if needed, if this representation is capable of displaying the avatar hands
		public void RepresentationAvailable(IAvatar avatar, List<Renderer> newRenderers = null)
		{
			foreach (var listener in listeners) listener.OnRepresentationAvailable(avatar, networkRig && networkRig.IsLocalNetworkRig);
			foreach (var listener in HardwareRigListeners) listener.OnRepresentationAvailable(avatar, networkRig && networkRig.IsLocalNetworkRig);

			AddNewRenderersToLOD(avatar, newRenderers);
		}

		// When an avatar is unloaded, we remove it from the LOD level where it was
		public void RemoveRepresentation(IAvatar avatar, List<Renderer> renderers = null)
		{
			if (renderers == null) return;

			int level = avatar.TargetLODLevel;
			RemoveRenderersFromLOD(level, renderers);
		}
        #endregion

		#region LOD
		// Activate a given LOD level. A -1 value reactive the LOD normal behaviour
		void ForceLOD(int level)
		{
			if (lod) lod.ForceLOD(level);
		}

		void RemoveRenderersFromLOD(int level, List<Renderer> renderersToRemove)
		{
			if (lod)
			{
                if (renderersToRemove != null && renderersToRemove.Count > 0)
                {
					var currentLods = lod.GetLODs();
					LOD[] lods = new LOD[currentLods.Length];
					List<Renderer> newRenderers = new List<Renderer>(currentLods[level].renderers);
					foreach (var currentRenderer in currentLods[level].renderers)
					{
						if (currentRenderer == null)
						{
							Debug.LogError("Destroyed renderer");
							continue;
						}
						if (renderersToRemove != null && renderersToRemove.Contains(currentRenderer))
						{
							newRenderers.Remove(currentRenderer);
						}
					}
					ChangeLODRenderers(level, newRenderers);
				}
				RestoreDistanceHandling();
			}

		}

		void AddNewRenderersToLOD(IAvatar avatar, List<Renderer> newRenderers = null)
		{
			int lodLevel = avatar.TargetLODLevel;
			if (lod)
			{
				if (newRenderers != null)
				{
					var renderers = new List<Renderer>(newRenderers);
					var currentLods = lod.GetLODs();
					foreach (var currentRenderer in currentLods[lodLevel].renderers)
					{
						if (currentRenderer == null)
						{
							Debug.Log("Destroyed renderer");
							continue;
						}
						renderers.Add(currentRenderer);
					}
					ChangeLODRenderers(lodLevel, renderers);
				}
				RestoreDistanceHandling();
			}
		}

		void RestoreDistanceHandling()
		{
			if (ignoreDistance)
			{
				// We want to always see the best LOD level (0)
				ForceLOD(0);
			}
			else
			{
				// Re-enable normal LODGroup handling
				ForceLOD(-1);
			}
		}

		void ChangeLODRenderers(int changedLevel, List<Renderer> newRenderers)
		{
			if (!lod) return;
			var currentLods = lod.GetLODs();
			float currentLodObjectSize = lod.size;
			LOD[] lods = new LOD[currentLods.Length];
			for (int i = 0; i < currentLods.Length; i++)
			{
				if (i == changedLevel)
				{
					lods[i] = new LOD(currentLods[i].screenRelativeTransitionHeight, newRenderers.ToArray());
				}
				else
				{
					lods[i] = new LOD(currentLods[i].screenRelativeTransitionHeight, currentLods[i].renderers);
				}
			}
			lod.SetLODs(lods);          
			
			// Re-enable LODGroup normal LOD handling (no specific level)
			ForceLOD(-1);
			lod.size = currentLodObjectSize;
		}
		#endregion
	}
}
