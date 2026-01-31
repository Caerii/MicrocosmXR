using UnityEngine;

namespace Fusion.Samples.IndustriesComponents
{
    /***
     * 
     * DisableSound is in charge to disable the sounds during the awake by setting the master volume to 0.
     * This can be used when the application is build for specific use case (recorder on screen sharing sample for example)
     * 
     ***/
    public class DisableSound : MonoBehaviour
    {
        [SerializeField] AudioSettingsManager audioSettingsManager;

        private void Awake()
        {
            if (audioSettingsManager == null)
            {
                audioSettingsManager = FindObjectOfType<AudioSettingsManager>(true);
            }
            audioSettingsManager.onInitialVolumeSet.AddListener(MuteMasterVolume);
        }

        void MuteMasterVolume() 
        {
            if (gameObject.activeInHierarchy == false) return;
            audioSettingsManager.SetMasterVolume(0, store: false);
        }
    }
}
