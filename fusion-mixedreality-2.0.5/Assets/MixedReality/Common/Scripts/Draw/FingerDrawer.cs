using Fusion.Addons.Drawing;
using Fusion.Addons.HapticAndAudioFeedback;
using Fusion.Addons.MetaOVRHandsSynchronization;
using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(FingerDrawer.EXECUTION_ORDER)]
public class FingerDrawer : Drawer
{
    const int EXECUTION_ORDER = NetworkHand.EXECUTION_ORDER + 10;
    NetworkHand networkHand;
    public HardwareOVRHandCollecter hardwareOVRHandCollecter;
    public HandIndexTipDetector handIndexTipDetector;

    BoxCollider FingerTip
    {
        get
        {
#if OCULUS_SDK_AVAILABLE

            if (handIndexTipDetector == null || hardwareOVRHandCollecter == null)
            {
                return null;
            }
            if (hardwareOVRHandCollecter.IsUsingFingerTracking)
            {
                return handIndexTipDetector.indexFingerTrackingModeCollider;
            }
            else if (hardwareOVRHandCollecter.IsControllerInHand)
            {
                return handIndexTipDetector.indexControllerModeCollider;
            }
#endif
            return null;
        }
    }

    float lastPinch = 0;
    public float delayBetweenDraw = 4f;
    public bool DrawingSessionInProgress => fingerDrawingStatus == FingerDrawingStatus.Drawing || fingerDrawingStatus == FingerDrawingStatus.DrawingPause;
    public override bool IsGrabbed => DrawingSessionInProgress;

    public override bool IsUsed => base.IsUsed;

    public override bool IsGrabbedByLocalPLayer => DrawingSessionInProgress && Object.StateAuthority == Runner.LocalPlayer;

    public enum FingerDrawingStatus
    {
        NotDrawing,
        Drawing,
        DrawingPause,
        HoveringGrabbable,
    }

    public FingerDrawingStatus fingerDrawingStatus = FingerDrawingStatus.NotDrawing;

    [SerializeField]
    GameObject DrawingParticleSystemGO;

    [SerializeField]
    GameObject DrawingPauseParticleSystemGO;

    [SerializeField]
    GameObject pulsarParticleSystemGO;

    public bool useFingerDrawerAsTip = true;

    private void Awake()
    {
        feedback = GetComponent<Feedback>();
        networkHand = GetComponentInParent<NetworkHand>();
        if(useFingerDrawerAsTip == false)
        {
            // Creates a tip object
            var tipGO = new GameObject(networkHand.side + "FingerDrawingTip");
            tip = tipGO.transform;
        } 
        else
        {
            // The default execution order makes sure we move the hand first (to avoid duplicated movements)
            tip = transform;
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        if (Object.HasStateAuthority == false)
        {
            DrawingParticleSystemGO.SetActive(false);
            DrawingPauseParticleSystemGO.SetActive(false);
            pulsarParticleSystemGO.SetActive(false);
        }
    }

    Vector3 lastDrawingPos = Vector3.zero;
    Vector3 erroneousPosition = Vector3.zero;
    float lastErrorPositiontime = 0;

    List<Vector3> tipPositions = new List<Vector3>();

    // Use 1 to skip smoothing)
    public int numberOfTipPositionsToSmoothOn = 5;

    #if OCULUS_SDK_AVAILABLE

    public override void Render()
    {
        base.Render();
        if (Object && Object.HasStateAuthority == false)
            return;

        if (FingerTip)
        {
            var fingerPosition = FingerTip.transform.position;
            if (numberOfTipPositionsToSmoothOn > 1 )
            {
                tipPositions.Add(fingerPosition);
                if (tipPositions.Count > 5) tipPositions.RemoveAt(0);
                var tipAverage = Vector3.zero;
                foreach (var tipPos in tipPositions) tipAverage += tipPos;
                tipAverage = tipAverage / tipPositions.Count;
                tip.transform.position = tipAverage;
            }
            else
            {
                tip.transform.position = fingerPosition;
            }

            if (DrawingParticleSystemGO)
                DrawingParticleSystemGO.transform.position = FingerTip.transform.position;
            if (pulsarParticleSystemGO)
                pulsarParticleSystemGO.transform.position = FingerTip.transform.position;
            if (DrawingPauseParticleSystemGO)
                DrawingPauseParticleSystemGO.transform.position = FingerTip.transform.position;
        }
        if (hardwareOVRHandCollecter == null && networkHand.LocalHardwareHand)
        {
            hardwareOVRHandCollecter = networkHand.LocalHardwareHand.GetComponentInParent<HardwareOVRHandCollecter>();
            if(hardwareOVRHandCollecter == null) hardwareOVRHandCollecter = networkHand.LocalHardwareHand.GetComponentInChildren<HardwareOVRHandCollecter>();
        }
        if (handIndexTipDetector == null && networkHand.LocalHardwareHand)
        {
            handIndexTipDetector = networkHand.LocalHardwareHand.GetComponentInChildren<HandIndexTipDetector>();
            if(handIndexTipDetector == null) handIndexTipDetector = networkHand.LocalHardwareHand.GetComponentInParent<HandIndexTipDetector>();
        }

        if (hardwareOVRHandCollecter)
        {

            bool isUsing = hardwareOVRHandCollecter.IsUsingFingerTracking && hardwareOVRHandCollecter.IsPinchingIndex;
            isUsing = isUsing || (hardwareOVRHandCollecter.IsUsingFingerTracking == false && hardwareOVRHandCollecter.IsUsingTrigger);

            if (FingerTip)
            {
                // Checking any jump in the finger tracking position, as if the finger tracking is lost while drawing, it might create a point at a bad position
                if (isUsing && lastDrawingPos != Vector3.zero && Vector3.Distance(lastDrawingPos, FingerTip.transform.position) > 0.5f)
                {
                    isUsing = false;
                    lastErrorPositiontime = Time.time;
                    erroneousPosition = FingerTip.transform.position;
                }
                if ((Time.time - lastErrorPositiontime) < 1f && Vector3.Distance(erroneousPosition, FingerTip.transform.position) < 0.1f)
                {
                    // Still at a default position
                    isUsing = false;
                }
                lastDrawingPos = FingerTip.transform.position;
            }

            if (fingerDrawingStatus != FingerDrawingStatus.Drawing && hardwareOVRHandCollecter.IsHoveringGrabbable)
            {
                fingerDrawingStatus = FingerDrawingStatus.HoveringGrabbable;
            }

            if (fingerDrawingStatus == FingerDrawingStatus.HoveringGrabbable && hardwareOVRHandCollecter.IsHoveringGrabbable == false)
            {
                fingerDrawingStatus = FingerDrawingStatus.NotDrawing;
            }

            if (fingerDrawingStatus == FingerDrawingStatus.NotDrawing && isUsing)
            {
                fingerDrawingStatus = FingerDrawingStatus.Drawing;
            }

            if (fingerDrawingStatus == FingerDrawingStatus.DrawingPause && isUsing)
            {
                fingerDrawingStatus = FingerDrawingStatus.Drawing;
            }

            if (fingerDrawingStatus == FingerDrawingStatus.Drawing && isUsing == false)
            {
                fingerDrawingStatus = FingerDrawingStatus.DrawingPause;
                lastPinch = Time.time;
            }

            if (fingerDrawingStatus == FingerDrawingStatus.DrawingPause && ((lastPinch + delayBetweenDraw) < Time.time))
            {
                fingerDrawingStatus = FingerDrawingStatus.NotDrawing;
            }

            forceUse = FingerTip && fingerDrawingStatus == FingerDrawingStatus.Drawing;

            if (fingerDrawingStatus == FingerDrawingStatus.Drawing || fingerDrawingStatus == FingerDrawingStatus.DrawingPause)
                hardwareOVRHandCollecter.FingerPinchUsedThisFrame();


            if (fingerDrawingStatus == FingerDrawingStatus.Drawing)
            {
                DrawingParticleSystemGO.SetActive(true);
                DrawingPauseParticleSystemGO.SetActive(false);
            }

            if (fingerDrawingStatus == FingerDrawingStatus.DrawingPause)
            {
                DrawingParticleSystemGO.SetActive(false);
                DrawingPauseParticleSystemGO.SetActive(true);
            }

            if (fingerDrawingStatus == FingerDrawingStatus.NotDrawing || fingerDrawingStatus == FingerDrawingStatus.HoveringGrabbable)
            {
                DrawingParticleSystemGO.SetActive(false);
                DrawingPauseParticleSystemGO.SetActive(false);
            }
        }
    }
#endif
}
