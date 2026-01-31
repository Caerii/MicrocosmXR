using Fusion.XR.Shared;
using Fusion.XR.Shared.Rig;
using Newtonsoft.Json;
#if READY_PLAYER_ME
using ReadyPlayerMe;
using ReadyPlayerMe.Core;
#endif
using System.Collections.Generic;
using UnityEngine;
using Fusion.Addons.EyeMovementSimulation;
using Fusion.Addons.HapticAndAudioFeedback;
using System.Threading.Tasks;

/**
 * 
 * RPMAvatarLoader is in charge of ReadyPlayerMe avatar loading when an avatar URL modification has been detected in the XRNetworkedRig by AvatarRepresentation (the ChangeAvatar() function is called).,
 * First, it tries to load the avatar 
 *		1/ by instantiating a local prefab is the URL is associated to it,
 *		2/ by cloning an existing avatar if the avatar URL has already been used to download an avatar,
 *
 * If the avatarURL is not found in caches, the ReadyPlayerMe GLB file is finaly downloaded.
 * 
 * When the ReadyPlayerMe avatar is loaded then we have to :
 *		- restructure the avatar to make it a children of the networked rig
 *		- hide the ReadyPlayerMe hands and change the Oculus color hands
 *		- configure lyp sync and some features (automatic gazer, eye blink, avatar layers, meshes optimization)
 * 
 **/

namespace Fusion.Addons.Avatar.ReadyPlayerMe
{
	/**
	 * Loads Ready player me avatar (RPM) avatar
	 * 
	 * Note:
	 * Supported url:
	 * - actual RPM url for half body avatars
	 */
	public class RPMAvatarLoader : MonoBehaviour, IAvatar
	{
		// ReadyPlayMe avatars evolved over time, hence the need to detect the version of avatar loaded to adapt to it
		public enum RPMAvatarKind
		{
			None,
			V1, // avatar with hand mesh renderers
			V2, // avatar with hands included in the main face renderer
			V3  // September 2022 avatars, similar to V2, with skin tone in the metadata, and some other modifications (texture organization change, renderers path, later on compatibility with "Use hands" config, ...)
		}

		[System.Serializable]
		public struct RPMAvatarInfo
		{
			public GameObject avatarGameObject;
			public string avatarURL;
			public RPMAvatarKind kind;
#if READY_PLAYER_ME
			public AvatarMetadata metadata;
#endif
			public List<string> handsPaths;
			public List<GameObject> handsGameObjects;
			public string facePath;
			public SkinnedMeshRenderer faceMeshRenderer;
			public string glassesPath;
			public Renderer glassesRenderer;
			public string headPath;
			public GameObject headGameObject;
			public List<string> eyesPaths;
			public List<GameObject> eyeGameObjects;
			public List<Renderer> avatarRenderers;
			public AvatarDescription avatarDescription;
		}

		[System.Flags]
		public enum OptionalFeatures
		{
			None = 0,
			// If selected, RPM hands will be hidden. Relevant is another hand representation solution is provided")]
			HideRPMHands = 1,
			OptimizeAvatarRenderers = 2,
			DetectColorForAvatarDescription = 4,
			EyeMovementSimulation = 8,
			LipSynchronisation = 16,
			LipSyncWeightPonderation = 256,
			EyeBlinking = 32,
			OnLoadedSoundEffect = 64,
			DownloadThrottling = 128,
			AllOptions = ~0,
		}

		bool ShouldApplyFeature(OptionalFeatures feature) => (feature & avatarOptionalFeatures) != 0;
		public OptionalFeatures avatarOptionalFeatures = OptionalFeatures.AllOptions;

		// Path in the avatar game object of the skeleton nodes
		protected static readonly string readyPlayerMeHead = "Armature/Hips/Spine/Neck";
		protected static readonly string readyPlayerMeEyeLeft = "Head/LeftEye";
		protected static readonly string readyPlayerMeEyeRight = "Head/RightEye";
		protected static readonly string readyPlayerMeRightHand = "Armature/Hips/Spine/RightHand";
		protected static readonly string readyPlayerMeLeftHand = "Armature/Hips/Spine/LeftHand";
		// Possible renderers path (they changed along avatar and SDK versions)
		protected static readonly string[] readyPlayerMeFaceRendererPaths = new string[] { "Avatar_Renderer_Head", "Avatar_Renderer_Avatar", "Renderer_Head", "Renderer_Avatar" };
		protected static readonly string[] readyPlayerMeHandsRendererPathsV1 = new string[] { "Avatar_Renderer_Hands", "Renderer_Hands" };
		protected static readonly string[] readyPlayerMeGlassesRendererPaths = new string[] { "Avatar_Renderer_Avatar_Transparent", "Renderer_Avatar_Transparent" };

		[Tooltip("Default starting url: usefull when not used with AvatarRepresentation and UserInfo (offline avatar selection, ...)")]
		public string startingAvatarUrl = "";
		[Header("Latest avatar info")]
		public RPMAvatarInfo avatarInfo = default;
		public float downloadProgress = 0;
#if READY_PLAYER_ME
		AvatarObjectLoader avatarLoader = null;
#endif

		[Header("Avatar configuration")]
		[Tooltip("Skintone data are not always of the actual skin color. If true, color will be taken from the texture color")]
		public bool ignoreSkinToneMetadata = true;
		[Tooltip("If true, and the avatar texture is not readable, a copy will be made to be able to fill AvatarDescription with the hair and clothes color")]
		public bool avatarColorRequired = true;
		[Tooltip("If true, and with a parent AvatarRepresentation, the avatar will be loaded also for the local user (required for mirrors, ...)")]
		public bool loadLocalAvatar = true;
		public int lodLevel = 0;
		public float avatarRPMScalefactor = 1.2f;
		[Tooltip("The parent under which the spawned avatar will be stored. Automatically set if under a network rig")]
		public Transform avatarParent = null;
		private Vector3 readyPlayerMeHeadOffset = new Vector3(0f, -0.28f, -0.079f);
		public string soundForAvatarLoaded = "OnAvatarLoaded";

		private NetworkRig networkRig;
		private SoundManager soundManager;
		private AvatarRepresentation avatarRepresentation;

		private PerformanceManager performanceManager;
		private PerformanceManager.TaskToken? avatarLoadingToken;

		// Viseme list: https://docs.readyplayer.me/avatar-specification/avatar-configuration
		static readonly List<string> facialAnimationBlendShapes = new List<string>() {
				"viseme_sil",
				"viseme_PP",
				"viseme_FF",
				"viseme_TH",
				"viseme_DD",
				"viseme_kk",
				"viseme_CH",
				"viseme_SS",
				"viseme_nn",
				"viseme_RR",
				"viseme_aa",
				"viseme_E",
				"viseme_I",
				"viseme_O",
				"viseme_U"
			};
		List<int> facialAnimationBlendShapesIndex = new List<int>();

		[Header("LipSync")]
		[SerializeField] float OVRLipSyncWeightPonderation = 100f;

#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER && !UNITY_EDITOR
		[SerializeField] float simpleLipSyncWeightFactor = 1f;
		[SerializeField] float lipSyncVolumeAmplification = 20f;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR || UNITY_EDITOR_OSX
		[SerializeField] float simpleLipSyncWeightFactor = 1f;
		[SerializeField] float lipSyncVolumeAmplification = 20f;
#endif

		[Header("Cache option")]
		[Tooltip("Max time waiting for another download to finish")]
		[SerializeField] float maxSameDownloadWaitTime = 10f;
		[Tooltip("If true, the avatar game object provided by the RPM avatar loader will be duplicated. This will prevent some cases were RPM might destroy an avatar reused twice (typically if a download takes more than maxSameDownloadWaitTime to finish)")]
		[SerializeField] bool copyRPMLoaderAvatar = false;

		[Header("RPM model customization")]
		[Tooltip("Inactive eye rotation for left eye in avatar V1 and V2")]
		[SerializeField]
		Vector3 baseLeftEyeRotation = new Vector3(90, 0, 0);
		[Tooltip("Inactive eye rotation for right eye in avatar V1 and V2")]
		[SerializeField]
		Vector3 baseRightEyeRotation = new Vector3(90, 0, 0);
		[Tooltip("Inactive eye rotation for left eye in avatar V3")]
		[SerializeField]
		Vector3 baseLeftEyeRotationV3 = new Vector3(-90, 180, 0);
		[Tooltip("Inactive eye rotation for right eye in avatar V3")]
		[SerializeField]
		Vector3 baseRightEyeRotationV3 = new Vector3(-90, 180, 0);
		#region Cache
		[System.Serializable]
		public struct RPMCachedAvatarInfo
		{
			public GameObject avatarGameObject;
			public string avatarURL;
#if READY_PLAYER_ME
			public AvatarMetadata metadata;
#endif
		}
		// Shared storage, by URL of already downloaded avatar, in case of reuse on another user avatar
		static Dictionary<string, RPMCachedAvatarInfo> SharedAvatarCacheByURL = new Dictionary<string, RPMCachedAvatarInfo>();
		static List<string> LoadingAvatarURLs = new List<string>();
#endregion

#region IAvatar
		[Header("IAvatar info")]
		[SerializeField]
		private AvatarStatus _avatarStatus = AvatarStatus.RepresentationLoading;
		public AvatarStatus AvatarStatus
		{
			get { return _avatarStatus; }
			set { _avatarStatus = value; }
		}

		public int TargetLODLevel => lodLevel;

		public AvatarUrlSupport SupportForURL(string url)
		{
			// There is no way to determine if an URL is a proper RPM url
			return AvatarUrlSupport.Maybe;
		}

		public AvatarDescription AvatarDescription => avatarInfo.avatarDescription;

		public string AvatarURL => avatarInfo.avatarURL;
		public GameObject AvatarGameObject => avatarInfo.avatarGameObject;

		public bool ShouldLoadLocalAvatar => loadLocalAvatar;

		RPMAvatarLibrary avatarLibrary;
		public string LoadRandomAvatar() {
			if(avatarLibrary == null) avatarLibrary = FindObjectOfType<RPMAvatarLibrary>();
			if (avatarLibrary == null) return null;
			return avatarLibrary.RandomAvatar(); 
		}

		// ChangeAvatar checks if the avatarURL parameter is valid and if avatar representation has to be updated
		public void ChangeAvatar(string avatarURL)
		{
#if READY_PLAYER_ME
			if (avatarURL == avatarInfo.avatarURL)
			{	
				// No avatar change (already loaded or currently loading)
				return;
			}

			// Remove the current avatar
			RemoveCurrentAvatarObject();

			// check if avatarURL is valid
			if (string.IsNullOrEmpty(avatarURL))
			{
				UnableToLoadAvatar();
			}
			else
			{
				// launch the avatar loading (with the last requested url, if several where requested while waiting for download authorization from the performance manager)
				LoadAvatar(avatarURL);
			}
#else
			Debug.LogError("Ready Player Me package not installed (READY_PLAYER_ME not defined)");
#endif
		}
#endregion

#region MonoBehaviour
        void OnEnable()
		{
			networkRig = GetComponentInParent<NetworkRig>();
			avatarRepresentation = GetComponentInParent<AvatarRepresentation>();
#if READY_PLAYER_ME
#else
			Debug.LogError("Ready Player Me package not installed (READY_PLAYER_ME not defined)");
#endif

		}

		void Start()
		{
            if (string.IsNullOrEmpty(startingAvatarUrl) == false)
            {
				// Default hardcoded url
				ChangeAvatar(startingAvatarUrl);
            }
		}
		private void OnDestroy()
		{
			// clean caches on destroy
			RemoveCachedEntries();

#if READY_PLAYER_ME
            // Prevent locking the cache if destroyed while downloading
            if (avatarLoader != null && string.IsNullOrEmpty(avatarInfo.avatarURL) == false)
            {
				UnregisterLoadingURL(avatarInfo.avatarURL);
            }
#endif
		}

        private void LateUpdate()
        {
			if (ShouldApplyFeature(OptionalFeatures.LipSyncWeightPonderation))
				AdaptOVRLipsyncWeights();
		}
        #endregion


        #region Avatar cleanup

        // Remove the current avatar
        public void RemoveAvatar()
		{
			RemoveCurrentAvatarObject();
		}

		// RemoveCurrentAvatarObject :
		// - clean the caches
		// - disable the avatar automatic gazer
		// - remove the avatar LOD renderers
		// - destroy the gameObject
		void RemoveCurrentAvatarObject()
		{
			// Check if the avatar GameObject exist
			if (avatarInfo.avatarGameObject)
			{
				// remove the avatar from the caches
				RemoveCachedEntries();
				Debug.Log("[RPMAvatar] Remove CurrentRPMAvatar");
				// disable the avatar automatic gazer
				Gazer gazer;
				if (networkRig)
					gazer = networkRig.headset.GetComponentInChildren<Gazer>();
				else
					gazer = GetComponentInChildren<Gazer>();
                if (gazer)
                {
					gazer.gazingTransforms = new List<Transform>();
					gazer.eyeRendererVisibility = null;
				}
				// remove the avatar LOD renderers
				AvatarStatus = AvatarStatus.NotLoaded;
				if (avatarRepresentation) avatarRepresentation.RemoveRepresentation(this, avatarInfo.avatarRenderers);
				// Destroy the gameObject
				Destroy(avatarInfo.avatarGameObject);
			}

			// Reset avatar details
			avatarInfo = default;
		}
#endregion

#region Avatar loading

#if READY_PLAYER_ME
		// LoadAvatar
		// - first tries to load the avatarURL using cached avatar
		// - cancels eventual previous avatar loading request and asks for a loading request
		// - then, launch the avatar loading
		private async void LoadAvatar(string avatarURL)
		{
			// Warn the avatar representation we are loading (it will display low level avatar LOD while nothing is loaded)
			AvatarStatus = AvatarStatus.RepresentationLoading;
			if (avatarRepresentation) avatarRepresentation.LoadingRepresentation(this);

			// Tries to load the avatarURL using cached avatar
			if (await TryLoadCachedAvatar(avatarURL))
				return;

			if (avatarURL == avatarInfo.avatarURL)
			{
				// No avatar change: the same avatar has been requested twice, and the first attempt was waiting due to TryLoadCachedAvatar
				return;
			}

			// Memorize the avatar URL: this way, if another avatar is called while we are waiting, we will only download the last requested url (or block the next attempts in the ChangeAvatar call)
			avatarInfo.avatarURL = avatarURL;
			if (ShouldApplyFeature(OptionalFeatures.DownloadThrottling))
            {
				await RequestDownloadAuthorizationToken();
				// Check the last requested url: this way, if another avatar is called while we are waiting for the performance manager authorization to download, we will only download the last requested url
				avatarURL = avatarInfo.avatarURL;
			}

			// Prepare and launch the ReadyPlayerMe loader
			if (avatarLoader != null)
            {
				avatarLoader.Cancel();
			}
			avatarLoader = new AvatarObjectLoader();

			Debug.Log($"[RPMAvatar] Loading avatar url {avatarURL} ...");
			float time = Time.realtimeSinceStartup;
			avatarLoader.OnCompleted += (a, args) => {
				Debug.Log($"[RPMAvatar] Loaded avatar url ({(int)((Time.realtimeSinceStartup - time)*1000f)}ms): {avatarURL}");
				avatarLoader = null;
				AvatarLoadedCallback(args.Avatar, args.Metadata, avatarURL);
				UnregisterLoadingURL(avatarURL);
			};
			avatarLoader.OnFailed += (a, args) =>
			{
				avatarLoader = null;
				Debug.LogError($"[RPMAvatar] Unable to load RPM avatar for {avatarURL}: {args.Message}");
				UnableToLoadAvatar();
				UnregisterLoadingURL(avatarURL);
				if (ShouldApplyFeature(OptionalFeatures.DownloadThrottling))
                {
					// Inform the task manager when the avatar loading is finished (failed here)
					FreeAuthorizationToken();
				}
			};
			avatarLoader.OnProgressChanged += (a, args) => {
				downloadProgress = args.Progress;
			};

			RegisterLoadingURL(avatarURL);
			avatarLoader.LoadAvatar(avatarURL);
	}
#endif

	void UnableToLoadAvatar()
		{
			AvatarStatus = AvatarStatus.RepresentationMissing;
			if (avatarRepresentation)
			{
				// Warn the AvatarRepresentation (will replace the avatar represention by a higher avatar LOD representation)
				avatarRepresentation.RepresentationUnavailable(this);
			}
		}

#if READY_PLAYER_ME
		RPMAvatarInfo ParseAvatar(GameObject avatar, AvatarMetadata metadata, string avatarURL)
        {
			RPMAvatarInfo info = default;
			info.avatarURL = avatarURL;
			info.avatarGameObject = avatar;
			info.metadata = metadata;
			info.kind = RPMAvatarKind.None;
			info.handsPaths = new List<string>();
			info.handsGameObjects = new List<GameObject>();
			info.eyesPaths = new List<string>();
			info.eyeGameObjects = new List<GameObject>();
			info.avatarRenderers = new List<Renderer>();

			// Hands
			Transform handsRendererTransform = null;
			foreach(var handsPath in readyPlayerMeHandsRendererPathsV1)
            {
				handsRendererTransform = avatar.transform.Find(handsPath);
				if (handsRendererTransform)
				{
					info.kind = RPMAvatarKind.V1;
					info.handsPaths.Add(handsPath);
					info.handsGameObjects.Add(handsRendererTransform.gameObject);
					break;
				}
			}
			if (handsRendererTransform == null)
			{
				info.kind = RPMAvatarKind.V2;
				foreach (var handPath in new string[] { readyPlayerMeLeftHand, readyPlayerMeRightHand })
				{
					Transform hand = info.avatarGameObject.transform.Find(handPath);
					if (hand == null)
					{
						Debug.LogError("Hand not found " + handPath);
						continue;
					}
					info.handsPaths.Add(handPath);
					info.handsGameObjects.Add(hand.gameObject);
				}
			}

			if (info.kind == RPMAvatarKind.V2 && string.IsNullOrEmpty(metadata.SkinTone) == false)
			{
				info.kind = RPMAvatarKind.V3;
			}

			// Head
			Transform headTransform = avatar.transform.Find(readyPlayerMeHead);
            if (headTransform)
            {
				info.headPath = readyPlayerMeHead;
				info.headGameObject = headTransform.gameObject;
			}
			else
            {
				Debug.LogError("[RPMAvatar] ReadyPlayerMe Head has not been found !");
			}

			// Face
			foreach (var faceTransformPath in readyPlayerMeFaceRendererPaths)
			{
				Transform faceRendererTransform = avatar.transform.Find(faceTransformPath);
				if (faceRendererTransform)
				{
					info.facePath = faceTransformPath;
					info.faceMeshRenderer = faceRendererTransform.gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
					break;
				}
			}

			if (info.faceMeshRenderer == null)
			{
				Debug.LogError("Unable to find faceMeshRenderer");
			}

			// Get the glasses renderers, it will be passed in parameter to build the avatar LOD level
			foreach(var glassPath in readyPlayerMeGlassesRendererPaths)
            {
				Transform glasses = avatar.transform.Find(glassPath);
				if (glasses)
				{
					info.glassesPath = glassPath;
					info.glassesRenderer = glasses.GetComponentInChildren<SkinnedMeshRenderer>();
					break;
				}
			}

			// Eyes
			if (info.headGameObject)
			{
				foreach (var eyePath in new string[] { readyPlayerMeEyeLeft, readyPlayerMeEyeRight })
                {
					var eye = info.headGameObject.transform.Find(eyePath);
                    if (eye)
                    {
						info.eyeGameObjects.Add(eye.gameObject);
					}
					else
                    {
						Debug.LogError($"Eye {eyePath} not found !");
					}
				}
			}

			// Mesh renderers
			info.avatarRenderers = new List<Renderer>(info.avatarGameObject.GetComponentsInChildren<Renderer>());

			return info;
		}
#endif


// AvatarLoadedCallback is the call back function called when the ReadyPlayerMe avatar is loaded with components and anim controller 

#if READY_PLAYER_ME
		private void AvatarLoadedCallback(GameObject avatar, AvatarMetadata metaData, string avatarURL)
		{
            if (copyRPMLoaderAvatar)
            {
				GameObject avatarCopy = GameObject.Instantiate(avatar);
				Destroy(avatar);
				avatar = avatarCopy;
            }

			Debug.Log($"[RPMAvatar] Avatar Loaded. Metadata: {metaData.SkinTone}");

			// remove potential previous avatar 
			RemoveCurrentAvatarObject();

			// Detect avatar info/kind
			avatarInfo = ParseAvatar(avatar, metaData, avatarURL);

            if (ShouldApplyFeature(OptionalFeatures.HideRPMHands))
            {
				// Hide the ReadyPlayerMe avatar hands
				HideHands(avatarInfo);
			}

			// Restructure avatar
			if (avatarInfo.headGameObject != null)
			{
				if (avatarParent == null && networkRig) avatarParent = networkRig.headset.transform;
				if (avatarParent == null) avatarParent = transform;
				var headsetPosition = avatar.transform.InverseTransformPoint(avatarInfo.headGameObject.transform.position);
				// move the avatar under the NetworkedRig
				avatar.transform.SetParent(avatarParent, false);
				avatar.transform.position = avatarParent.position;
				avatar.transform.rotation = avatarParent.rotation;
				avatar.transform.localPosition = -headsetPosition + readyPlayerMeHeadOffset;
				avatar.transform.localScale = avatarRPMScalefactor * Vector3.one;
			} 
			else
            {
				Debug.LogError("[RPMAvatar] Missing head gameobject");
            }

			// optimize avatar skinned mesh renderers
			if (ShouldApplyFeature(OptionalFeatures.OptimizeAvatarRenderers))
            {
				OptimizeAvatarRenderers(avatarInfo);
			}

			// Find the hand color from the avatar mesh renderer and update the description. It will allow AvatarRepresentation listeners to update hands colors, ...
			if (ShouldApplyFeature(OptionalFeatures.DetectColorForAvatarDescription))
            {
				CompleteAvatarDescription(ref avatarInfo, ignoreSkinToneMetadata: ignoreSkinToneMetadata, avatarColorRequired: avatarColorRequired);
			}

			if (ShouldApplyFeature(OptionalFeatures.EyeMovementSimulation))
            {
				// Activate eyes gazer
				Gazer gazer = null;
				if (networkRig)
					gazer = networkRig.headset.GetComponentInChildren<Gazer>();
				else
					gazer = GetComponentInChildren<Gazer>();
				if (gazer)
				{
					Vector3[] eyeRotations;
					if (avatarInfo.kind == RPMAvatarKind.V1 || avatarInfo.kind == RPMAvatarKind.V2)
					{
						eyeRotations = new Vector3[] {baseLeftEyeRotation, baseRightEyeRotation};
					}
					else
                    {
						eyeRotations = new Vector3[] { baseLeftEyeRotationV3, baseRightEyeRotationV3 };
					}

					ActivateEyes(avatarInfo, gazer, eyeRotations);
				}
				else
				{
					Debug.LogWarning("[RPMAvatar] No gazer: eye movement simulation won't be activated");
				}
			}

			if (ShouldApplyFeature(OptionalFeatures.LipSynchronisation))
            {
				// Initialize the lip synchronisation
				ConfigureLipSync();
			}

			if (ShouldApplyFeature(OptionalFeatures.EyeBlinking))
            {
				// Add the eye animation handler
				ConfigureEyeBlink(avatar);
			}



			// Add the avatar to the cache
			CacheAvatar(avatarInfo);

			if (ShouldApplyFeature(OptionalFeatures.OnLoadedSoundEffect))
            {
				// Audio feedback to inform player that the avatar is loaded
				if (string.IsNullOrEmpty(soundForAvatarLoaded) == false)
				{
					if (soundManager == null) soundManager = SoundManager.FindInstance();
					if (soundManager) soundManager.PlayOneShot(soundForAvatarLoaded);
				}
			}

			if (ShouldApplyFeature(OptionalFeatures.DownloadThrottling))
            {
				// Inform the task manager when the avatar loading is completed
				FreeAuthorizationToken();
			}

			// Inform that the ReadyPlayerMe avatar had been loaded 
			// Configure the avatar LOD system properly
			AvatarStatus = AvatarStatus.RepresentationAvailable;

			if (avatarRepresentation) avatarRepresentation.RepresentationAvailable(this, avatarInfo.avatarRenderers);
		}
#endif
#endregion

#region Avatar edits
        // HideHands hides avatar hands according to ReadyPlayerMe avatar structure
        static void HideHands(RPMAvatarInfo info)
		{
			// V1 and V2 avatar ignore AvatarConfig "Use hands", so we have to hide them manually
			if (info.kind == RPMAvatarKind.V1)
			{
				// for first kind of ReadyPlayerMe avatar, the hand gameobject must be disable
				foreach (var hand in info.handsGameObjects) hand.SetActive(false);
			}
			else
			{
				// for second kind of ReadyPlayerMe avatar, the hands gameobjects localscale must be set to zero
				foreach (var hand in info.handsGameObjects)
				{
					// TODO See with RPM if the hands in V2 format can be hidden more properly (without requiring server configuration)
					hand.transform.localScale = Vector3.zero;
					if (info.headGameObject) hand.transform.position = info.headGameObject.transform.position;
				}
			}
		}

		// OptimizeAvatarMeshes optimize avatar skinned mesh renderer for each avatar part
		private static void OptimizeAvatarRenderers(RPMAvatarInfo info)
		{
			foreach (Renderer avatarRenderer in info.avatarRenderers)
			{
				avatarRenderer.receiveShadows = false;
				avatarRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				avatarRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                if (avatarRenderer.sharedMaterial == null)
                {
					Debug.LogError("[RPMAvatar] Missing avatar material (corrupted cache/library data, ...)");
					return;
                }
				avatarRenderer.sharedMaterial.SetFloat("_Roughness", 1);				
				avatarRenderer.sharedMaterial.SetFloat("roughnessFactor", 1); // For Shader: glTF-pbrMetallicRoughness.shadergraph
				avatarRenderer.sharedMaterial.SetTexture("_BumpMap", null);
			}
		}
				
		// ConfigureEyeBlink adds the ReadyPlayerMe eye animation Handler if it is missing
		private void ConfigureEyeBlink(GameObject avatar)
		{
#if READY_PLAYER_ME
			if (avatar.GetComponent<EyeAnimationHandler>() == null)
			{
				avatar.AddComponent<EyeAnimationHandler>();
			}
#endif
		}

		public static Texture2D FindFaceRendererMaterialTexture(SkinnedMeshRenderer faceMeshRenderer)
        {
			Texture2D skinTexture = null;
			Material skinMaterial = faceMeshRenderer.sharedMaterials[0];
			if (skinMaterial)
            {
				if (skinMaterial.HasTexture("_MainTex"))
				{
					skinTexture = (Texture2D)skinMaterial.GetTexture("_MainTex");
				}
				else if (skinMaterial.HasTexture("baseColorTexture"))
				{
					// For Shader: glTF-pbrMetallicRoughness.shadergraph
					skinTexture = (Texture2D)skinMaterial.GetTexture("baseColorTexture");
				}
				if (skinTexture == null) skinTexture = (Texture2D)skinMaterial.mainTexture;
			} 
			else
            {
				Debug.LogError("Missing face material");
            }
			return skinTexture;
		}

		// CompleteAvatarDescription extract the skin, hair & cloth colors from the skinned mesh renderer and record them in the AvatarDescription
		static void CompleteAvatarDescription(ref RPMAvatarInfo info, bool ignoreSkinToneMetadata, bool avatarColorRequired)
		{
#if READY_PLAYER_ME
			if (info.faceMeshRenderer == null)
				Debug.LogError("Skinned Mesh Renderer not found on avatar");
			else
			{
				if (info.faceMeshRenderer.sharedMaterials.Length == 0)
					Debug.LogError("No material found on Skinned Mesh Renderer");
				else
				{
					Texture2D skinTexture = FindFaceRendererMaterialTexture(info.faceMeshRenderer);

					Color skinColor = Color.clear;
					bool isSkinToneValid = string.IsNullOrEmpty(info.metadata.SkinTone) == false && ColorUtility.TryParseHtmlString(info.metadata.SkinTone, out skinColor);
					bool shouldDestroySkinTexture = false;
					if(skinTexture.isReadable == false)
                    {
						// Unreadable texture. Makes a readable copy, if needed
                        if (string.IsNullOrEmpty(info.metadata.SkinTone) || avatarColorRequired || ignoreSkinToneMetadata)
                        {
							// Copy the texture to find color
							RenderTexture copyRT = RenderTexture.GetTemporary(
								skinTexture.width,
								skinTexture.height);
							Graphics.Blit(skinTexture, copyRT);
							RenderTexture currentRT = RenderTexture.active;
							RenderTexture.active = copyRT;
							skinTexture = new Texture2D(skinTexture.width, skinTexture.height, skinTexture.format, false);
							shouldDestroySkinTexture = true;
							skinTexture.ReadPixels(new Rect(0, 0, copyRT.width, copyRT.height), 0, 0);
							skinTexture.Apply();
							RenderTexture.active = currentRT;
						}
					}

					// Skin color
					if (skinTexture.isReadable)
					{
						if (info.kind == RPMAvatarKind.V1)
						{
							skinColor = skinTexture.GetPixel(0, (int)(skinTexture.height * 0.75f));
						}
						else if (info.kind == RPMAvatarKind.V2)
						{
							skinColor = skinTexture.GetPixel(skinTexture.width - 1, (int)(skinTexture.height * 0.75f));
						}
					}

					if (info.kind == RPMAvatarKind.V3)
					{
						if(isSkinToneValid == false) Debug.LogError("Unable to parse skintone in RPM V3");
						if (ignoreSkinToneMetadata || isSkinToneValid == false)
                        {
                            if (skinTexture.isReadable)
                            {
								skinColor = skinTexture.GetPixel(0, (int)(skinTexture.height * 0.75f));
							}
						}
					}

					// Hair & cloth color
					if (skinTexture.isReadable)
                    {
						if (info.kind == RPMAvatarKind.V3)

						{
							info.avatarDescription.hairColor = skinTexture.GetPixel((int)(skinTexture.width * 0.75f + 1), (int)(skinTexture.height * 0.75f - 1));
							info.avatarDescription.clothColor = skinTexture.GetPixel((int)(skinTexture.width - 1), (int)(skinTexture.height - 1));
						}
						else
						{
							info.avatarDescription.hairColor = skinTexture.GetPixel(0, skinTexture.height - 1);
							info.avatarDescription.clothColor = skinTexture.GetPixel((int)(skinTexture.width * 0.75f), (int)(skinTexture.height * 0.1f));
						}
					}						

					info.avatarDescription.colorMode = AvatarDescription.ColorMode.Color;
					info.avatarDescription.skinColor = skinColor;

					info.avatarDescription.isBald = info.avatarDescription.hairColor.a == 0;
					info.avatarDescription.hairMode = AvatarDescription.HairMode.OnOff;

                    if (shouldDestroySkinTexture)
                    {
						Destroy(skinTexture);
                    }
				}
			}
#endif
		}

		//  ActivateEyes configure the gazer to activate the eyes movements based on gaze target detection
		static void ActivateEyes(RPMAvatarInfo info, Gazer gazer, Vector3[] eyeRotations)
		{
			// Add the RendererVisible component if not found on the mesh renderer, to optimize gaze system
			RendererVisible rendererVisible = null;
			if (info.faceMeshRenderer)
			{
				rendererVisible = info.faceMeshRenderer.gameObject.GetComponent<RendererVisible>();
				if (rendererVisible == null) rendererVisible = info.faceMeshRenderer.gameObject.AddComponent<RendererVisible>();
			}
			else
			{
				Debug.LogError("faceMeshRenderer not found: unable to add RendererVisible to optimize gazer");
			}

			gazer.eyeRendererVisibility = rendererVisible;
			gazer.gazingTransformOffsets = new List<Vector3>(eyeRotations);
			
			gazer.gazingTransforms = new List<Transform>();
			foreach (var eye in info.eyeGameObjects)
            {
				gazer.gazingTransforms.Add(eye.transform);
			}
		}

		void ConfigureLipSync()
        {
			var audioRootGameObject = ((networkRig != null) ? networkRig.gameObject : avatarParent.gameObject);
#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER && !UNITY_EDITOR
			// Oculus lipsync needs the OnAudioFilterRead callback, triggered by audio sources, that does not work on webGL (and the binary lib it needs is not available on webGL either): we use another lipsync system
			ConfigureSimpleLipsync(avatarInfo, audioGameObject: audioRootGameObject, simpleLipSyncWeightFactor, lipSyncVolumeAmplification);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR || UNITY_EDITOR_OSX
			// Oculus lipsync has some incompatibilities with Photon voice on MacOS and WebGL: we use another lipsync system
			ConfigureSimpleLipsync(avatarInfo, audioGameObject: audioRootGameObject, simpleLipSyncWeightFactor,lipSyncVolumeAmplification);
#else
			ConfigureOculusLipsync(avatarInfo, audioGameObject: audioRootGameObject, ref facialAnimationBlendShapesIndex);
#endif
		}

		// Basic lipsync based on moving the mouth open blendshape based on volume level (required for MacOS, where )
		static void ConfigureSimpleLipsync(RPMAvatarInfo info, GameObject audioGameObject, float simpleLipSyncWeightFactor, float lipSyncVolumeAmplification = 1)
        {
			AudioSource audioSource = audioGameObject.GetComponentInChildren<AudioSource>();
			if (audioSource == null)
            {
				Debug.LogWarning("[RPMAvatar] No audio source: unable to configure lipsync");
			}
			RPMLipSync lipsync = info.avatarGameObject.GetComponent<RPMLipSync>();
			if (lipsync == null)
            {
				lipsync = info.avatarGameObject.AddComponent<RPMLipSync>();
			}
			lipsync.audioSource = audioSource;
			lipsync.lipSyncWeightFactor = simpleLipSyncWeightFactor;
			lipsync.amplituteMultiplier = lipSyncVolumeAmplification;
		}

		// ConfigureOculusLipsync intialized the LipSync context morph target with the facial animation blend shapes
		static void ConfigureOculusLipsync(RPMAvatarInfo info, GameObject audioGameObject, ref List<int> facialAnimationBlendShapesIndex)
		{
			if (info.faceMeshRenderer == null || info.headGameObject == null || audioGameObject == null)
			{
				Debug.LogError("[RPMAvatar] faceMeshRenderer, audioGameObject or head not found: unable to configure lipsync");
				return;
			}
			OVRLipSyncContext lipSyncContext = audioGameObject.GetComponentInChildren<OVRLipSyncContext>(true);
			OVRLipSyncContextMorphTarget lipSync = audioGameObject.GetComponentInChildren<OVRLipSyncContextMorphTarget>(true);
			AudioSource audioSource = audioGameObject.GetComponentInChildren<AudioSource>();
            if (audioSource == null) {
				Debug.LogWarning("[RPMAvatar] No audio source: unable to configure lipsync");
				return;
			}
            if (lipSyncContext == null)
            {
				lipSyncContext = audioSource.gameObject.AddComponent<OVRLipSyncContext>();
				lipSyncContext.audioSource = audioSource;
				// We change the algorithm, as the original is less resource consuming
				lipSyncContext.provider = OVRLipSync.ContextProviders.Original;
				// We ensure to still hear the avatar voice instead of consuming it
				lipSyncContext.audioLoopback = true;
            }
			if (lipSync == null)
			{
				lipSync = audioSource.gameObject.AddComponent<OVRLipSyncContextMorphTarget>();
			}

			lipSync.enabled = true;
			lipSync.enableVisemeTestKeys = false; //Incompatible with new input system


			int visemeCount = 0;
			lipSync.skinnedMeshRenderer = info.faceMeshRenderer;

			facialAnimationBlendShapesIndex = new List<int>();

			foreach (var facialAnimationBlendShape in facialAnimationBlendShapes)
			{
				int blendShapeIndex = info.faceMeshRenderer.sharedMesh.GetBlendShapeIndex(facialAnimationBlendShape);
				facialAnimationBlendShapesIndex.Add(blendShapeIndex);
				if (visemeCount < OVRLipSync.VisemeCount)
				{
					lipSync.visemeToBlendTargets[visemeCount] = blendShapeIndex;
				}
				visemeCount++;
			}
		}

		// AdaptOVRLipsyncWeights divides the Oculus blend shape value set by the OVRLipSyncContextMorphTarget
		// Alternatively, you can edit the OVRLipSyncContextMorphTarget code as explained here : https://youtu.be/Q4sPGTVylnY?si=26npBwb9OTtkUG_b&t=560
		void AdaptOVRLipsyncWeights()
        {
			if (avatarInfo.faceMeshRenderer == null)
				return;

			foreach (var facialAnimationBlendShapeIndex in facialAnimationBlendShapesIndex)
			{
				avatarInfo.faceMeshRenderer.SetBlendShapeWeight(facialAnimationBlendShapeIndex, avatarInfo.faceMeshRenderer.GetBlendShapeWeight(facialAnimationBlendShapeIndex)/OVRLipSyncWeightPonderation);
			}
		}

#endregion

#region Performance manager (to throttle simultaneous downloads)
		async Task RequestDownloadAuthorizationToken()
		{
			// Set the performance manager
			if (!performanceManager)
			{
				if (networkRig) performanceManager = networkRig.Runner.GetComponentInChildren<PerformanceManager>();
				else performanceManager = FindObjectOfType<PerformanceManager>(true);
			}

			if (performanceManager)
			{
				// check if a previous avatar load was requested
				if (avatarLoadingToken != null)
				{
					Debug.LogError("Cancelling previous loading request");
					// Cancel previous avatar loading request
					performanceManager.TaskCompleted(avatarLoadingToken);
				}
				// request a new avatar load
				avatarLoadingToken = await performanceManager.RequestToStartTask(PerformanceManager.TaskKind.NetworkRequest);

				// check if avatar load request has been accepted
				if (avatarLoadingToken == null)
				{
					Debug.LogError("Unable to load avatar: no time slot available");
				}
			}
			else
			{
				Debug.LogError("No PerformanceManager found !");
			}
		}

		void FreeAuthorizationToken()
		{
			if (performanceManager)
			{
				performanceManager.TaskCompleted(avatarLoadingToken);
				avatarLoadingToken = null;
			}
		}
#endregion

#region Cache
		// TryLoadCachedAvatar checks if the avatarURL was already used to load avatar. If the avatarURL is found in caches, then the avatar is cloned 
		// Return true if the avatar has been found in the cache
		private async Task<bool> TryLoadCachedAvatar(string avatarURL)
		{
			var cachedInfo = await TryFindCachedAvatar(avatarURL);
			if (cachedInfo.avatarURL == avatarURL)
			{
				if (avatarURL == avatarInfo.avatarURL)
				{
					// No avatar change: the same avatar has been requested twice, and was loaded while we were waiting with TryFindCachedAvatar
					return false;
				}

				// clone the avatar found in the cache 
				GameObject avatar = GameObject.Instantiate(cachedInfo.avatarGameObject);

#if READY_PLAYER_ME
				AvatarLoadedCallback(avatar, cachedInfo.metadata, avatarURL);
#endif
				downloadProgress = 1;
				Debug.Log($"[RPMAvatar] Reusing avatar {avatarURL}");
				return true;
			}
			return false;
		}

		// Remove the URL from caches
		void RemoveCachedEntries()
		{
			UncacheAvatar(avatarInfo);
		}

		public static void CacheAvatar(RPMAvatarInfo info)
		{
#if READY_PLAYER_ME
			CacheAvatar(info.avatarURL, info.avatarGameObject, info.metadata);
#endif
		}
#if READY_PLAYER_ME
		public static void CacheAvatar(string avatarURL, GameObject avatarGameObject, AvatarMetadata metadata)
		{
			if (avatarGameObject == null) return;
			if (SharedAvatarCacheByURL.ContainsKey(avatarURL)) return;

			SharedAvatarCacheByURL[avatarURL] = new RPMCachedAvatarInfo
			{
				avatarGameObject = avatarGameObject,
				avatarURL = avatarURL,
				metadata = metadata
			};
		}
#endif

		public static void UncacheAvatar(RPMAvatarInfo info)
        {
			UncacheAvatar(info.avatarURL, info.avatarGameObject);
		}

		public static void UncacheAvatar(string avatarURL, GameObject avatarGameObject)
		{
			if (avatarURL == null) return;
			if (SharedAvatarCacheByURL.ContainsKey(avatarURL) && SharedAvatarCacheByURL[avatarURL].avatarGameObject == avatarGameObject)
			{
				SharedAvatarCacheByURL.Remove(avatarURL);
			}
		}

		public async Task<RPMCachedAvatarInfo> TryFindCachedAvatar(string avatarURL)
		{
			RPMCachedAvatarInfo info = default;
			await WaitForCurrentDownload(avatarURL);
			if (SharedAvatarCacheByURL.ContainsKey(avatarURL))
			{
				if (SharedAvatarCacheByURL[avatarURL].avatarGameObject == null)
				{
					Debug.LogError("Cached avatar has been destroyed: uncaching it");
					SharedAvatarCacheByURL.Remove(avatarURL);
					return info;
				}
				info = SharedAvatarCacheByURL[avatarURL];
				return info;
			}
			return info;
		}

		void RegisterLoadingURL(string avatarURL)
		{
            if (LoadingAvatarURLs.Contains(avatarURL))
            {
				return;
            }
			LoadingAvatarURLs.Add(avatarURL);
		}

		void UnregisterLoadingURL(string avatarURL)
		{
			if (LoadingAvatarURLs.Contains(avatarURL) == false)
			{
				return;
			}
			LoadingAvatarURLs.Remove(avatarURL);
		}

		async Task WaitForCurrentDownload(string avatarURL)
        {
            if (LoadingAvatarURLs.Contains(avatarURL))
            {
				Debug.Log($"[RPMAvatar] Waiting for the current download to be finished... (avatarUrl: {avatarURL})");
				const int waitStep = 100;
				int watchdog = (int)(1000f * maxSameDownloadWaitTime / (float)waitStep);
                while (watchdog != 0 && LoadingAvatarURLs.Contains(avatarURL))
                {
					await AsyncTask.Delay(waitStep);
					watchdog--;
                }
				if (LoadingAvatarURLs.Contains(avatarURL))
                {
					Debug.LogError($"[RPMAvatar] Download did not end properly. Resume download for the same url. Note that if the first download ends up finishing, RPM might reuse the avatar for the second download, making the first avatar disappear. Url: {avatarURL}");
                }
			}
		}

#endregion
	}
}
