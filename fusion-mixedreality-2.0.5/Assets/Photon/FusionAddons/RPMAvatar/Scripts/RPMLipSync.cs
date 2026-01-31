#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER && !UNITY_EDITOR
#define USE_VOICE_INFO
#else
#endif
#if USE_VOICE_INFO
using Photon.Voice;
using Photon.Voice.Unity;
#endif
#if READY_PLAYER_ME
using ReadyPlayerMe;
using ReadyPlayerMe.Core;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Avatar.ReadyPlayerMe
{
    /**
     * Simple blenshape animation based on voice volume
     */
    public class RPMLipSync : MonoBehaviour
    {
        public AudioSource audioSource;
        private const string MouthOpenBlendShapeName = "mouthOpen";

        public float amplituteMultiplier = 10;
        public float minVoicePercentToDisplay = 0.04f;

        private SkinnedMeshRenderer headMesh;
        private SkinnedMeshRenderer beardMesh;
        private SkinnedMeshRenderer teethMesh;

        private int mouthOpenBlendShapeIndexOnHeadMesh = -1;
        private int mouthOpenBlendShapeIndexOnBeardMesh = -1;
        private int mouthOpenBlendShapeIndexOnTeethMesh = -1;

        public float lipSyncWeightFactor = 1f;

#if USE_VOICE_INFO
        Speaker speaker;
        bool HasSubscribedToVoice = false;
        int audioWriteCalls = 0;
        float accumulatedVolume = 0;
        private void OnDestroy()
        {
            if (speaker.RemoteVoice != null)
            {
                speaker.RemoteVoice.FloatFrameDecoded -= OnFloatFrameDecoded;
            }
        }

        private void OnFloatFrameDecoded(FrameOut<float> frame)
        {
            float voiceVolume = 0f;
            foreach (var sample in frame.Buf)
            {
                voiceVolume += Mathf.Abs(sample);
            }
            voiceVolume /= frame.Buf.Length;
            accumulatedVolume += voiceVolume;
            audioWriteCalls++;
        }
#endif 

        // Start is called before the first frame update
        void Start()
        {
            // Source: VoiceHandler from RPM SDK
#if READY_PLAYER_ME
            GetMeshAndSetIndex(MeshType.HeadMesh, ref headMesh, ref mouthOpenBlendShapeIndexOnHeadMesh);
            GetMeshAndSetIndex(MeshType.BeardMesh, ref beardMesh, ref mouthOpenBlendShapeIndexOnBeardMesh);
            GetMeshAndSetIndex(MeshType.TeethMesh, ref teethMesh, ref mouthOpenBlendShapeIndexOnTeethMesh);
#endif
        }

        // Update is called once per frame
        void Update()
        {
#if USE_VOICE_INFO
            if (speaker == null) speaker = audioSource.GetComponent<Speaker>();

            if (HasSubscribedToVoice == false && speaker != null && speaker.IsLinked)
            {
                speaker.RemoteVoice.FloatFrameDecoded += OnFloatFrameDecoded;
                HasSubscribedToVoice = true;
            }

            if (audioWriteCalls != 0)
            {
                float voiceVolume = accumulatedVolume / audioWriteCalls;
                //Debug.LogError($"Volume {voiceVolume} ({voiceVolume * amplituteMultiplier}//{minVoicePercentToDisplay})");
                var level = Mathf.Clamp01(voiceVolume * amplituteMultiplier);
                if (level < minVoicePercentToDisplay) level = 0;
                SetBlendshapeWeights(level);
            }
            accumulatedVolume = 0;
            audioWriteCalls = 0;
#else
            SetBlendshapeWeights(GetAmplitude());
#endif
        }



        private float[] audioSample = new float[1024];
        public float GetAmplitude()
        {
            if (audioSource != null && audioSource.clip != null && audioSource.isPlaying)
            {
                float amplitude = 0f;
                audioSource.clip.GetData(audioSample, audioSource.timeSamples);

                foreach (var sample in audioSample)
                {
                    amplitude += Mathf.Abs(sample);
                }

                var level = Mathf.Clamp01(amplitude / audioSample.Length * amplituteMultiplier);
                if (level < minVoicePercentToDisplay) return 0;
                return level;
            }

            return 0;
        }

        #region Blend Shape Movement
// Source: VoiceHandler from RPM SDK

#if READY_PLAYER_ME
        private void GetMeshAndSetIndex(MeshType meshType, ref SkinnedMeshRenderer mesh, ref int index)
        {
            mesh = gameObject.GetMeshRenderer(meshType);

            if (mesh != null)
            {
                index = mesh.sharedMesh.GetBlendShapeIndex(MouthOpenBlendShapeName);
            }
        }
#endif 

        private void SetBlendshapeWeights(float weight)
        {
            SetBlendShapeWeight(headMesh, mouthOpenBlendShapeIndexOnHeadMesh);
            SetBlendShapeWeight(beardMesh, mouthOpenBlendShapeIndexOnBeardMesh);
            SetBlendShapeWeight(teethMesh, mouthOpenBlendShapeIndexOnTeethMesh);

            void SetBlendShapeWeight(SkinnedMeshRenderer mesh, int index)
            {
                if (index >= 0)
                {
                    mesh.SetBlendShapeWeight(index, weight * lipSyncWeightFactor);
                }
            }
        }
#endregion
    }

}
