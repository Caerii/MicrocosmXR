using Fusion.Addons.HapticAndAudioFeedback;
using Photon.Voice.Fusion;
using UnityEngine;

/**
 * 
 * Reference all the application managers, to easily find them
 * Should be stored aside or under the NetworkRunner
 * 
 **/

namespace Fusion.Samples.IndustriesComponents
{
    public class Managers : MonoBehaviour
    {
        //RGU public ZoneManager zoneManager;
        //RGU public DynamicZonesPool dynamicZonespool;
        public AudioSettingsManager audioSettingsManager;
        public SoundManager soundManager;
        public FusionVoiceClient fusionVoiceClient;
        public NetworkRunner runner;
        public ApplicationManager applicationManager;

        private void Awake()
        {
            if (runner == null) runner = GetComponentInParent<NetworkRunner>();
            if (fusionVoiceClient == null && runner != null) fusionVoiceClient = runner.GetComponentInChildren<FusionVoiceClient>();
        }

        public static Managers FindInstance(NetworkRunner runner = null)
        {
            Managers managers = null;
            if (runner == null)
            {
                if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
                {
                    Debug.LogWarning("Should not be used in a multipeer context, where we could have several peer, and several Managers");
                }
                if (NetworkRunner.Instances.Count > 0)
                {
                    runner = NetworkRunner.Instances[0];
                }
            }
            if (runner != null) managers = runner.GetComponentInChildren<Managers>();
            if (managers == null) managers = FindObjectOfType<Managers>(true);// Should not be used in a multipeer context
            if (managers == null)
            {
                Debug.LogError("Unable to find Managers");
            }
            return managers;
        }
    }
}
