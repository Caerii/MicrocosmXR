using Fusion.XR.Shared.Grabbing;
using Fusion.XR.Shared.Rig;
using UnityEngine;

namespace Fusion.Addons.HapticAndAudioFeedback
{
    /***
     * 
     * Feedback manages the audio and haptic feedbacks for NetworkGrabbable
     * It provides methods to :
     *  - start/pause/stop playing audio feedback only
     *  - start playing audio and haptic feeback in the same time
     * If the audio source is not defined or not find on the object, Feedback uses the SoundManager audio source.
     * 
     ***/
    public class Feedback : MonoBehaviour
    {
        public bool EnableAudioFeedback = true;
        public bool EnableHapticFeedback = true;

        public AudioSource audioSource;
        private SoundManager soundManager;

        [Header("Haptic feedback")]
        public float defaultHapticAmplitude = 0.2f;
        public float defaultHapticDuration = 0.05f;

        NetworkGrabbable grabbable;
        public bool IsGrabbed => grabbable.IsGrabbed;
        public bool IsGrabbedByLocalPLayer => IsGrabbed && grabbable.CurrentGrabber.Object.StateAuthority == grabbable.CurrentGrabber.Object.Runner.LocalPlayer;

        private void Awake()
        {
            grabbable = GetComponent<NetworkGrabbable>();
        }

        void Start()
        {
            if (soundManager == null) soundManager = SoundManager.FindInstance();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null && soundManager)
                audioSource = soundManager.GetComponent<AudioSource>();
            if (audioSource == null)
                Debug.LogError("AudioSource not found");
        }

        public void PlayAudioAndHapticFeeback(string audioType, float hapticAmplitude)
        {
            PlayHapticFeedback(hapticAmplitude, GrabbingHand());
            PlayAudioFeeback(audioType);
        }

        public void PlayAudioAndHapticFeeback(string audioType)
        {
            PlayHapticFeedback();
            PlayAudioFeeback(audioType);
        }

        public void PlayAudioFeeback(string audioType)
        {
            if (EnableAudioFeedback == false) return;

            if (audioSource && audioSource.isPlaying == false && soundManager)
                soundManager.Play(audioType, audioSource);
        }

        public void StopAudioFeeback()
        {
            if (audioSource && audioSource.isPlaying)
                audioSource.Stop();
        }

        public void PauseAudioFeeback()
        {
            if (audioSource && audioSource.isPlaying)
                audioSource.Pause();
        }

        public void PlayHapticFeedback(float hapticAmplitude, HardwareHand hardwareHand)
        {
            if (EnableHapticFeedback == false || hardwareHand == null) return;

            hardwareHand.SendHapticImpulse(amplitude: hapticAmplitude, duration: defaultHapticDuration);
        }


        public void PlayHapticFeedback(HardwareHand hardwareHand = null)
        {
            if (hardwareHand == null)
            {
                hardwareHand = GrabbingHand();
            }
            PlayHapticFeedback(defaultHapticAmplitude, hardwareHand);
        }

        HardwareHand GrabbingHand()
        {
            if (grabbable != null)
            {
                if (IsGrabbedByLocalPLayer && grabbable.CurrentGrabber.hand && grabbable.CurrentGrabber.hand.LocalHardwareHand != null)
                {
                    return grabbable.CurrentGrabber.hand.LocalHardwareHand;
                }
            }
            return null;
        }
    }
}
