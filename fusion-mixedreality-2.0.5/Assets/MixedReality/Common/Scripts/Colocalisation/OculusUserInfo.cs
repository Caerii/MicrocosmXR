using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Oculus.Platform;
using System;

public class OculusUserInfo : NetworkBehaviour
{
    SSAManager ssaManager;

    #region Oculus user id lookup
    [Networked]
    public ulong OculusUserId { get; set; }

    public enum OculusIdlookupStatus
    {
        Undefined,
        WaitingForUserId,
        UserIdMissing,
        RemoteWaitingForUserId,
        Ready,
    }
    public OculusIdlookupStatus oculusIdLookupStatus = OculusIdlookupStatus.Undefined;
    #endregion

    #region Colocalization
    [Header("Automatic resync")]
    public bool automaticAnchorCreationOnConnection = true;
    public bool automaticRecenterOncolocalizationFound = true;

    public enum ColocalizationDetectionStatus
    {
        LookingForColocalization,
        WaitingForRemoteReferenceAnchorCreation,
        WaitingForRemoteReferenceAnchorStatus,
        LocalDetectionFinished,
        DetectionFinished,
        CandidateColocReferenceFoundButStillLookingToo,
    }
    [Networked]
    public ColocalizationDetectionStatus ColocStatus { get; set; } = ColocalizationDetectionStatus.LookingForColocalization;
    [Networked]
    public ulong CandidateColocUser { get; set; } = 0;
    public List<ReferenceAnchor> colocalizedRemoteReferenceAnchors = new List<ReferenceAnchor>();
    public List<OculusUserInfo> colocalizedUsers = new List<OculusUserInfo>();

    public bool IsLocalDetectionAnalysisFinished => ColocStatus == ColocalizationDetectionStatus.DetectionFinished || ColocStatus == ColocalizationDetectionStatus.LocalDetectionFinished;
    #endregion

    public bool ShouldReceiveAnchorSharing => oculusIdLookupStatus == OculusIdlookupStatus.Ready;

    private void Awake()
    {
        ssaManager = FindObjectOfType<SSAManager>();
    }

    public void OnDestroy()
    {
        if(ssaManager) ssaManager.UnregisterOculusUserInfo(this);
    }

    public override void Spawned()
    {
        base.Spawned();
        if(ssaManager) ssaManager.RegisterOculusUserInfo(this);
        InitOculusUserIdLookup();
        InitColocalizationDetection();
    }

    public override void Render()
    {
        base.Render();
        CheckOculusUserIdAvailability();
    }

    #region Oculus user id lookup
    void InitOculusUserIdLookup()
    {
        if (Object.HasStateAuthority)
        {
            WaitForUserId();
        }
        else
        {
            oculusIdLookupStatus = OculusIdlookupStatus.RemoteWaitingForUserId;
        }
    }

    void CheckOculusUserIdAvailability()
    {
        if (oculusIdLookupStatus == OculusIdlookupStatus.RemoteWaitingForUserId && OculusUserId > 0)
        {
            oculusIdLookupStatus = OculusIdlookupStatus.Ready;
            OnLocalUserIdAvailable();
        }
    }

    void OnLocalUserIdAvailable()
    {
        if(ssaManager) ssaManager.OculusUserRefreshAnchorShareNeeded();
    }
    #endregion

    #region User platform API
    void WaitForUserId()
    {
        oculusIdLookupStatus = OculusIdlookupStatus.WaitingForUserId;

        Core.AsyncInitialize().OnComplete((initMessage) => {
            if (initMessage.IsError)
            {
                Debug.LogError("[Local user detection] Cannot init Platform " + initMessage);
            }
            else
            {
                Entitlements.IsUserEntitledToApplication().OnComplete((Message msg) => {
                    if (msg.IsError)
                    {
                        Debug.LogError("[Local user detection] Error: Entitlement check failed");
                    }
                    GetUserId(retryIfFailed: true);
                });
            }
        });
    }

    void GetUserId(bool retryIfFailed)
    {
        Users.GetLoggedInUser().OnComplete((message) => {
            if (message.IsError)
            {
                Debug.LogError("[Local user detection] Cannot get logged in user " + message);
                oculusIdLookupStatus = OculusIdlookupStatus.UserIdMissing;
            }
            else
            {
                if (((ulong)message.Data.ID) > 0)
                {
                    OculusUserId = message.Data.ID;
                    oculusIdLookupStatus = OculusIdlookupStatus.Ready;
                    OnLocalUserIdAvailable();
                }
                else
                {
                    oculusIdLookupStatus = OculusIdlookupStatus.UserIdMissing;
                    Debug.LogError("[Local user detection] Error: Bad oculus id");
                    Debug.LogError("[Local user detection] Error: " + message.GetError());

                    if (retryIfFailed)
                    {
                        GetUserId(retryIfFailed: false);
                    }
                }
            }
        });
    }
    #endregion

    #region Colocalization
    async void InitColocalizationDetection()
    {
        if (ssaManager == null) return;
        // The colocalization is only done for the local Oculus user
        if (Object.HasStateAuthority == false) return;

        ssaManager.ChangeWaitObjectStatus(true);
        ColocStatus = ColocalizationDetectionStatus.LookingForColocalization;
        ssaManager.CreateDefaultAnchor(Runner);

        // 1 - We determine how many (max) users we should expect to receive. Waiting for their anchor registration in the SSAManager
        ColocStatus = ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorCreation;

        int watchDog = 10;
        while (watchDog > 0 && ColocStatus == ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorCreation)
        {
            int currentRemoteUserCount = 0;
            foreach (var activePlayer in Runner.ActivePlayers) if (activePlayer != Runner.LocalPlayer) currentRemoteUserCount++;

            if (currentRemoteUserCount == ssaManager.remoteReferenceAnchors.Count)
            {
                // All users have created their anchor
                break;
            }
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }

        if (ColocStatus != ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorCreation)
        {
            // Colocalization status changed else where: no need to check anymore anchors reception
            Debug.Log("[Local user detection] Colocalization status changed else where: no need to check anymore anchors reception");
            return;
        }

        ColocStatus = ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorStatus;
        // 2 - We wait for these anchors to have been analyzed
        watchDog = 100;
        bool alone = true;
        while (watchDog > 0 && ColocStatus == ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorStatus)
        {
            int anchorWithStatusDetermined = 0;
            foreach (var remoteAnchor in ssaManager.remoteReferenceAnchors)
            {
                if (remoteAnchor.status == ReferenceAnchor.Status.RemoteAnchorColocalized || remoteAnchor.status == ReferenceAnchor.Status.RemoteAnchorNotcolocalized)
                {
                    alone = alone && remoteAnchor.status != ReferenceAnchor.Status.RemoteAnchorColocalized;
                    anchorWithStatusDetermined++;
                }
                else
                {
                    //Debug.LogError($"[Local user detection] Waiting for info about anchor {remoteAnchor.Uuid}/{remoteAnchor.status} ...");
                }
            }
            if (anchorWithStatusDetermined == ssaManager.remoteReferenceAnchors.Count)
            {
                break;
            }
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }
        if (ColocStatus == ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorStatus)
        {
            // All anchor checked, no change to status: probably no detection
            Debug.Log($"[Local user detection] All anchor checked, no change to status: probably no detection (alone: {alone})");
            if (alone)
            {
                // We wait a bit to let the user see the display status anyway, even if they are alone
                await Fusion.XR.Shared.AsyncTask.Delay(3_000);
            }
            FinishLocalColocalizationDetection();
        }
    }

    // RPC to arn a remote user of its detetion, in case the detection fais for them
    [Rpc]
    public void RpcColocUserDetected()
    {
        //TODO
    }

    public async void OnColocalizedDetection(ReferenceAnchor anchor, OculusUserInfo colocalizedOculusUser)
    {
        if(Object.HasStateAuthority == false)
        {
            Debug.LogError("[Local user detection] Unexpected error");
            return;
        }
        Debug.Log($"[Local user detection] OnColocalizedDetection anchor: {anchor.Uuid} colocalizedOculusUser: {colocalizedOculusUser.OculusUserId} ({colocalizedOculusUser.ColocStatus})");
        colocalizedRemoteReferenceAnchors.Add(anchor);
        colocalizedUsers.Add(colocalizedOculusUser);
        if (IsLocalDetectionAnalysisFinished)
        {
            // Already synched: no need to resync
            Debug.Log("[Local user detection] Already synched: not need to resync");
            return;
        }

        Debug.Log($"[Local user detection] Changing CandidateColocUser: {CandidateColocUser} -> {colocalizedOculusUser.OculusUserId}");
        CandidateColocUser = colocalizedOculusUser.OculusUserId;
        if (colocalizedOculusUser.ColocStatus == ColocalizationDetectionStatus.DetectionFinished)
        {
            // Reliable stable reference: resync on it
            ReliableReferenceAnchorDetected(anchor);
            return;
        }

        Debug.Log("[Local user detection] ColocalizationDetectionStatus CandidateColocReferenceFoundButStillLookingToo");
        ColocStatus = ColocalizationDetectionStatus.CandidateColocReferenceFoundButStillLookingToo;

        // Waiting for candidate to have either finished its detection or determined its candidate resync coloc (or that we have finished due to another anchor received which is already finished)
        int watchDog = 50;
        while (watchDog > 0 && colocalizedOculusUser.CandidateColocUser == 0 && colocalizedOculusUser.ColocStatus != ColocalizationDetectionStatus.DetectionFinished && ColocStatus != ColocalizationDetectionStatus.DetectionFinished)
        {
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }

        if (ColocStatus == ColocalizationDetectionStatus.DetectionFinished)
        {
            // Synched while waiting (with another anchor): no need to resync
            Debug.Log("[Local user detection] Synched while waiting: not need to resync");
            return;
        }

        if (colocalizedOculusUser.ColocStatus == ColocalizationDetectionStatus.DetectionFinished)
        {
            // Reliable stable reference: resync on it
            ReliableReferenceAnchorDetected(anchor);
            return;
        }

        if (colocalizedOculusUser.ColocStatus == ColocalizationDetectionStatus.CandidateColocReferenceFoundButStillLookingToo)
        {
            // Both waiting for other player to finish their detection
            Debug.Log("[Local user detection] Both waiting for other player to finish their detection");
            if (colocalizedOculusUser.CandidateColocUser == OculusUserId)
            {
                // Locked: we are waiting for each other. We consider the lower id to be the reference, and do not change, the other one recenter
                if (Runner.LocalPlayer.RawEncoded > colocalizedOculusUser.Object.StateAuthority.RawEncoded)
                {
                    Debug.Log("[Local user detection] 2 users waiting for each other. We have the greater id, we recenter");
                    // Reliable stable reference: resync on it
                    ReliableReferenceAnchorDetected(anchor);
                    return;
                } else
                {
                    Debug.Log("[Local user detection] 2 users waiting for each other. We have the lower id, we stop searching and don't change our centering");
                    FinishAllColocalizationDetection();
                }
            }
            else
            {
                // Waiting for someone else, we will wait a bit for the candidate to solve this issue on its side (or for the other candidate anchor to be found)
                watchDog = 50;
                while (watchDog > 0 && colocalizedOculusUser.ColocStatus != ColocalizationDetectionStatus.DetectionFinished && ColocStatus != ColocalizationDetectionStatus.DetectionFinished)
                {
                    watchDog--;
                    await Fusion.XR.Shared.AsyncTask.Delay(100);
                }
                if (ColocStatus == ColocalizationDetectionStatus.DetectionFinished)
                {
                    // Synched while waiting (with another anchor): no need to resync
                    Debug.Log("[Local user detection] Synched while waiting: not need to resync");
                    return;
                }

                if (colocalizedOculusUser.ColocStatus == ColocalizationDetectionStatus.DetectionFinished)
                {
                    // Reliable stable reference: resync on it
                    Debug.Log("[Local user detection] Referenced user has finished their resync to someone else. We wait a bit to be sure the OVR has updated to this new position");
                    await Fusion.XR.Shared.AsyncTask.Delay(2000);
                    if (ColocStatus == ColocalizationDetectionStatus.DetectionFinished)
                    {
                        // Synched while waiting (with another anchor): no need to resync
                        Debug.Log("[Local user detection] Synched while waiting: not need to resync");
                        return;
                    }
                    ReliableReferenceAnchorDetected(anchor);
                    return;
                }
            }
        }
    }

    void ReliableReferenceAnchorDetected(ReferenceAnchor anchor)
    {
        Debug.Log("[Local user detection] Reliable stable reference: resync on it");
        FinishAllColocalizationDetection();
        if (automaticRecenterOncolocalizationFound)
        {
            ssaManager.RecenterWithAnchor(anchor);
        }
    }

    void FinishAllColocalizationDetection() {
        Debug.Log($"[Local user detection] FinishAllColocalizationDetection ({colocalizedUsers.Count} coloc, leader: {CandidateColocUser})");
        ColocStatus = ColocalizationDetectionStatus.DetectionFinished;
        ssaManager.ChangeWaitObjectStatus(false);

    }

    async void FinishLocalColocalizationDetection()
    {
        Debug.Log($"[Local user detection] FinishColocalizationDetection ({colocalizedUsers.Count} coloc, leader: {CandidateColocUser})");
        ColocStatus = ColocalizationDetectionStatus.LocalDetectionFinished;
        // We wait a bit, in case we miss a coloc detection localy, but the coloc user detect us and warn us 
        await Fusion.XR.Shared.AsyncTask.Delay(2_000);
        FinishAllColocalizationDetection();
    }

    public void RemoteReferenceAnchorDestroyed(ReferenceAnchor anchor)
    {
        colocalizedRemoteReferenceAnchors.Remove(anchor);
    }

    public void RemoteUserDestroyed(OculusUserInfo user)
    {
        colocalizedUsers.Remove(user);
    }
    #endregion
}
