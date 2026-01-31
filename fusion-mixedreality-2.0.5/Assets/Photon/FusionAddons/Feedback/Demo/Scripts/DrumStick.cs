using Fusion.Addons.HapticAndAudioFeedback;
using Fusion.XR.Shared.Rig;
using UnityEngine;
using Fusion;

public class DrumStick : NetworkBehaviour
{
    [SerializeField] private Feedback feedback;
    [SerializeField] private string DrumStickSound = "DrumStick";


    private void Awake()
    {
        if (!feedback)
            feedback = GetComponent<Feedback>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object && Object.HasStateAuthority == false)
            return;

        // Not feedback when the stick is grabbed by a hand
        if (other.gameObject.GetComponentInParent<HardwareHand>() || other.gameObject.GetComponentInParent<NetworkHand>())
            return;

        // Create local feedbacks when the drumstick collides with an object
        if (feedback)
        {
            // Audio & Haptic feedback
            feedback.PlayAudioAndHapticFeeback(DrumStickSound);
        }

        // Play the drum's sound if the drumstick collides with a drum pad for all players thanks to RPC
        Drum drum = other.GetComponent<Drum>();
        if (drum)
        {
            drum.PlayDrumSound();
        }
    }
}
