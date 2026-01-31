using Fusion.Addons.HapticAndAudioFeedback;
using UnityEngine;
using Fusion;

public class Drum : NetworkBehaviour
{
    [SerializeField] private Feedback feedback;
    public string DrumSound = "Drum1";

    private void Awake()
    {
        if (!feedback)
            feedback = GetComponent<Feedback>();
    }


    public void PlayDrumSound()
    {
        RpcPlayDrumSound();
    }

    private void LocalPlayDrumSound()
    {
        if (feedback)
        {
            feedback.StopAudioFeeback();
            feedback.PlayAudioFeeback(DrumSound);
        }
    }

    [Rpc(RpcSources.All,RpcTargets.All)]
    public void RpcPlayDrumSound()
    {
        LocalPlayDrumSound();
    }

}
