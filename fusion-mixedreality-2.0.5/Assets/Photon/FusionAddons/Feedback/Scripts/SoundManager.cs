using System.Collections.Generic;
using UnityEngine;


/**
 * 
 * Store all sound effect, and the default audioSource to play them if no audio source is defined
 * 
 **/

namespace Fusion.Addons.HapticAndAudioFeedback
{
    public class SoundManager : MonoBehaviour
    {
        public AudioSource defaultSceneAudioSource;
        public List<Sound> sounds = new List<Sound>();

        private void Awake()
        {
            defaultSceneAudioSource = GetComponent<AudioSource>();
            if (!defaultSceneAudioSource)
                Debug.LogError("defaultSceneAudioSource NOT found !");
        }

        // Try to find and return the sound manager instance
        public static SoundManager FindInstance(NetworkRunner runner = null)
        {
            SoundManager soundManager;
            if (runner != null)
            {
                // In multipeer scenerio, we will prefer to store the SoundManager under the Runner, to find the good SoundManager
                soundManager = runner.GetComponentInChildren<SoundManager>();
                if (soundManager) return soundManager;
            }

            if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
            {
                Debug.LogError("In multipeer mode, you should manually reference SoundManager (as several may coexists)");
                return null;
            }
            soundManager = FindObjectOfType(typeof(SoundManager), true) as SoundManager;
            if (!soundManager)
            {
                Debug.LogError("Sound manager not found !");
            }
            return soundManager;
        }

        // Look for a sound in the sounds library
        public Sound SearchForSound(string soundName) {
            Sound s = null;

            for (int i = 0; i < sounds.Count; i++)
            {
                if (sounds[i].name == soundName)
                {
                    s = sounds[i];
                    break;
                }
            }

            if (s == null)
            {
                Debug.LogError("Sound: " + soundName + " not found!");
            }
            return s;
        }

        // play a sound one shot using the default scene audio source
        public void PlayOneShot(string soundName)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            defaultSceneAudioSource.volume = s.volume;
            defaultSceneAudioSource.PlayOneShot(s.clip);
        }

        // play a sound one shot using the audio source provided in parameter
        public void PlayOneShot(string soundName, AudioSource audioSource)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            if (audioSource == null)
            {
                audioSource = defaultSceneAudioSource;
            }
            audioSource.volume = s.volume;
            audioSource.PlayOneShot(s.clip);
        }

        public void Play(string soundName, AudioSource audioSource)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            if (audioSource == null)
            {
                audioSource = defaultSceneAudioSource;
            }
            if (audioSource.isPlaying)
                audioSource.Stop();
            audioSource.clip = s.clip;
            audioSource.volume = s.volume;
            audioSource.Play();
        }


        // Play a sound selected by its name with a random start position, using the audio source provided in parameter
        public void PlayRandomPosition(string soundName, AudioSource audioSource)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            int randomStartTime = UnityEngine.Random.Range(0, s.clip.samples - 1);
            if(audioSource == null)
            {
                audioSource = defaultSceneAudioSource;
            }
            audioSource.timeSamples = randomStartTime;
            audioSource.Play();
            StartCoroutine(FadeAudioSource.StartFade(audioSource, 0f, 2.5f, audioSource.volume));
        }

        // Play a sound selected by its name with a random start position, using the default scene audio source
        public void PlayRandomPosition(string soundName)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            int randomStartTime = UnityEngine.Random.Range(0, s.clip.samples - 1);

            defaultSceneAudioSource.timeSamples = randomStartTime;
            defaultSceneAudioSource.Play();
            StartCoroutine(FadeAudioSource.StartFade(defaultSceneAudioSource, 0f, 2.5f, defaultSceneAudioSource.volume));
        }
    }
}
