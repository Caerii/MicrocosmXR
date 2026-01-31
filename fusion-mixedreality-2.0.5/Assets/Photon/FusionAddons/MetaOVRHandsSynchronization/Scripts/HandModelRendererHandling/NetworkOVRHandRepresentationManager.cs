using UnityEngine;
using Fusion.Addons.Avatar;
using Fusion.XR.Shared.Rig;

namespace Fusion.Addons.MetaOVRHandsSynchronization
{
#if OCULUS_SDK_AVAILABLE
    /***
     * 
     * NetworkOVRHandRepresentationManager component is located on players network rig.
     * It manages the local hand representation according to the displayForLocalPlayer bool,
     * (most of the time, hardware hands and network hands should not be displayed in the same time).
     * 
     ***/
    public class NetworkOVRHandRepresentationManager : OVRHandRepresentationManager, IAvatarRepresentationListener
    {
        [SerializeField]
        RemoteOVRHand remoteOVRHand;
        [SerializeField]
        NetworkHand networkHand;

        public IHandRepresentation handRepresentation;

#region OVRHandRepresentationManager implementations
        public override bool IsFingerDataValid => remoteOVRHand && remoteOVRHand.handState.isDataValid;
        public override RigPart Side => networkHand.side;
        public override GameObject FingerTrackingHandSkeletonParentGameObject => remoteOVRHand.gameObject;
        public override GameObject ControllerTrackingHandSkeletonParentGameObject => gameObject;
#endregion

        protected override void Awake()
        {
            base.Awake();
            if (handRepresentation == null) handRepresentation = GetComponentInChildren<IHandRepresentation>();
            if (remoteOVRHand == null) remoteOVRHand = GetComponentInChildren<RemoteOVRHand>();
            if (networkHand == null) networkHand = GetComponentInParent<NetworkHand>();
        }

        protected override void Update()
        {
            base.Update();
        }
    }
#else
    public class NetworkOVRHandRepresentationManager : MonoBehaviour {}
#endif
}
