#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER && !UNITY_EDITOR
#define USE_VOICE_INFO
#else
#endif

using Photon.Voice;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * 
 * The VoiceDetection OnAudioFilterRead compute an average voiceVolume for data received. It is used to animate avatar mouth if voice volume exceed a specific threshold.
 * 
 **/

namespace Fusion.Addons.Avatar
{
    public class VoiceDetection : MonoBehaviour
    {
        public AudioSource audioSource;
#if USE_VOICE_INFO
        Speaker speaker;
        bool HasSubscribedToVoice = false;
#endif 

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

#if USE_VOICE_INFO
        NetworkObject no;
        int audioWriteCalls = 0;
        float accumulatedVolume = 0;

        private void OnFloatFrameDecoded(FrameOut<float> frame)
        {
            float writeVoiceVolume = 0f;
            foreach (var sample in frame.Buf)
            {
                writeVoiceVolume += Mathf.Abs(sample);
            }
            writeVoiceVolume /= frame.Buf.Length;

            // audioWriteCalls and accumulatedVolume are reset during Update
            audioWriteCalls++;
            accumulatedVolume += writeVoiceVolume;
            voiceVolume = accumulatedVolume / audioWriteCalls;
        }
#endif 


        private void OnDestroy()
        {
#if USE_VOICE_INFO
            if (speaker.RemoteVoice != null)
            {
                speaker.RemoteVoice.FloatFrameDecoded -= OnFloatFrameDecoded;
            }
#endif
        }

        private void Update()
        {
            // reset the voice volume to stop lip sync when user exit a chat bubble
            if (audioSource && !audioSource.enabled)
            {
                voiceVolume = 0;
            }
#if USE_VOICE_INFO
            if (speaker == null) speaker = GetComponent<Speaker>();
            if(HasSubscribedToVoice == false && speaker != null && speaker.IsLinked)
            {
                speaker.RemoteVoice.FloatFrameDecoded += OnFloatFrameDecoded;
                HasSubscribedToVoice = true;
            }
            audioWriteCalls = 0;
            accumulatedVolume = 0;
            if (no == null) no = GetComponentInParent<NetworkObject>();
#endif
        }

        public float voiceVolume = 0;
        private void OnAudioFilterRead(float[] data, int channels)
        {
#if !USE_VOICE_INFO
            voiceVolume = 0f;
            foreach (var sample in data)
            {
                voiceVolume += Mathf.Abs(sample);
            }
            voiceVolume /= data.Length;
#endif

        }
    }
}
