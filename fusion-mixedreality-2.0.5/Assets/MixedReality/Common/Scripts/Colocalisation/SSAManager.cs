using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System.Threading.Tasks;
using System;
using Fusion.Sockets;
using Fusion.Addons.ExtendedRigSelectionAddon;

public class SSAManager : MonoBehaviour
{
    public ExtendedRigSelection rigSelection;
    public SSAWaitUI waitObject;

    [Header("Reference anchor prefab")]
    public GameObject referenceAnchorPrefab;
    [Header("OVRSpatialanchor prefabs")]
    public GameObject receivedAnchorPrefab;
    public GameObject localAnchorPrefab;

    [Header("Registered SSA objects")]
    public OculusUserInfo localOculusUserInfo;
    public List<OculusUserInfo> oculusUserInfos = new List<OculusUserInfo>();
    // Locla reference anchor (only filled when the local user id is known)
    public List<ReferenceAnchor> localReferenceAnchors = new List<ReferenceAnchor>();
    public List<ReferenceAnchor> remoteReferenceAnchors = new List<ReferenceAnchor>();

    [Header("Manual creation of anchor (test)")]
    public bool createTestAnchor = false;

    private void Awake()
    {
        if (rigSelection == null) rigSelection = FindObjectOfType<ExtendedRigSelection>(true);
        if (waitObject == null) waitObject = FindObjectOfType<SSAWaitUI>(true);
        if (waitObject) waitObject.ssaManager = this;
    }

    private void Update()
    {
        if (createTestAnchor)
        {
            createTestAnchor = false;
            CreateDefaultAnchor();
        }
    }

    #region Anchor spawning
    public void CreateDefaultAnchor(NetworkRunner runner = null)
    {
        if (runner == null) runner = FindObjectOfType<NetworkRunner>();
        CreateReferenceAnchor(runner, Vector3.zero, Quaternion.identity);
    }

    public void CreateReferenceAnchor(NetworkRunner runner, Vector3 postion, Quaternion rotation)
    {
        var no = runner.Spawn(referenceAnchorPrefab, postion, rotation, onBeforeSpawned: (r, prespawnedObject) => {
            if (prespawnedObject.TryGetComponent<ReferenceAnchor>(out var anchor))
            {
                anchor.ssaManager = this;
            }
        });
    }
    #endregion

    #region Oculus user Registration
    public void RegisterOculusUserInfo(OculusUserInfo oculusUserInfo)
    {
        oculusUserInfos.Add(oculusUserInfo);
        if (oculusUserInfo.Runner.LocalPlayer == oculusUserInfo.Object.StateAuthority) localOculusUserInfo = oculusUserInfo;
        if (oculusUserInfo.oculusIdLookupStatus == OculusUserInfo.OculusIdlookupStatus.Ready)
        {
            Debug.Log("[Local user detection] Sharing anchors to new Oculus user: " + oculusUserInfo.OculusUserId);
            OculusUserRefreshAnchorShareNeeded();
        }
    }

    public async void OculusUserRefreshAnchorShareNeeded()
    {
        await ShareAnchors();
    }

    public void UnregisterOculusUserInfo(OculusUserInfo oculusUserInfo)
    {
        oculusUserInfos.Remove(oculusUserInfo);
        foreach (var user in oculusUserInfos)
        {
            user.RemoteUserDestroyed(oculusUserInfo);
        }
        foreach (var referenceAnchor in localReferenceAnchors)
        {
            referenceAnchor.UserListChanged();
        }
    }
    #endregion

    #region Anchor Registration
    public void RegisterLocalReferenceAnchor(ReferenceAnchor anchor)
    {
        localReferenceAnchors.Add(anchor);
    }

    public void UnregisterLocalReferenceAnchor(ReferenceAnchor anchor)
    {
        localReferenceAnchors.Remove(anchor);
    }

    public void RegisterRemoteReferenceAnchor(ReferenceAnchor anchor)
    {
        remoteReferenceAnchors.Add(anchor);
    }

    public void UnregisterRemoteReferenceAnchor(ReferenceAnchor anchor)
    {
        foreach(var user in oculusUserInfos)
        {
            user.RemoteReferenceAnchorDestroyed(anchor);
        }
        remoteReferenceAnchors.Remove(anchor);
    }
    #endregion

    #region SSA interface
    public async Task WaitForLocalOculusUserIdAvailability()
    {
        int watchDog = 100;
        while (watchDog > 0)
        {
            if (localOculusUserInfo)
            {
                if (localOculusUserInfo.oculusIdLookupStatus == OculusUserInfo.OculusIdlookupStatus.Ready)
                {
                    // Oculus user id found allowing anchor API calls start
                    break;
                }
            }
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }
        if (localOculusUserInfo && localOculusUserInfo.oculusIdLookupStatus != OculusUserInfo.OculusIdlookupStatus.Ready)
        {
            Debug.LogError("[Local user detection] Error: waiting for Oculus user id failed !");
        }
    }
    public async Task<OVRSpatialAnchor.OperationResult> SaveAnchorsToCloud(List<OVRSpatialAnchor> anchors)
    {
        await WaitForLocalOculusUserIdAvailability();
        OVRSpatialAnchor.OperationResult savingResult = OVRSpatialAnchor.OperationResult.Failure;
        bool waitingForRequestEnd = true;
        OVRSpatialAnchor.Save(
          anchors,
          new OVRSpatialAnchor.SaveOptions { Storage = OVRSpace.StorageLocation.Cloud },
          (_, result) => {
              waitingForRequestEnd = false;
              savingResult = result;
          });
        int watchDog = 80;
        while (waitingForRequestEnd && watchDog > 0)
        {
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }
        if (savingResult != OVRSpatialAnchor.OperationResult.Success) Debug.LogError("[Local user detection] Non-success result:" + savingResult);
        return savingResult;
    }

    public async Task<OVRSpatialAnchor.OperationResult> ShareAnchors()
    {
        await WaitForLocalOculusUserIdAvailability();
        var ovrSpatialAnchors = AllToBeSharedOVRSpatialAnchors();
        var authorizedReferenceAnchors = AllToBeSharedReferenceAnchors();
        var users = AllOculusUsersWithIdReady();
        if (ovrSpatialAnchors.Count == 0) return OVRSpatialAnchor.OperationResult.Success;
        if (users.Count == 0) return OVRSpatialAnchor.OperationResult.Success;

        OVRSpatialAnchor.OperationResult shareResult = default;
        bool waitingForRequestEnd = true;


        string sharedAnchorsUUIDs = "";
        foreach (var anchor in ovrSpatialAnchors)
        {
            sharedAnchorsUUIDs += "  " + anchor.Uuid;
        }
        foreach (var sharedWithuser in users)
        {
            Debug.Log($"[Local user detection] Sharing anchors ({sharedAnchorsUUIDs}) with user: {sharedWithuser.Id}");
        }

        OVRSpatialAnchor.Share(
            ovrSpatialAnchors,
            users,
            (_, result) => {
                waitingForRequestEnd = false;
                shareResult = result;
            }
        );
        int watchDog = 80;
        while (waitingForRequestEnd && watchDog > 0)
        {
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }

        if (shareResult == OVRSpatialAnchor.OperationResult.Success)
        {
            foreach (var authorizedReferenceAnchor in authorizedReferenceAnchors)
            {
                authorizedReferenceAnchor.SharingAuthorizedFor(AllOculusUsersWithIdReady());
            }
        }
        if(shareResult != OVRSpatialAnchor.OperationResult.Success)  Debug.LogError("[Local user detection] Non-success result:" + shareResult);

        return shareResult;
    }

    public async Task<List<OVRSpatialAnchor>> DetectColocalizedAnchor(ReferenceAnchor referenceAnchor)
    {
        Debug.Log("[Local user detection] DetectColocalizedAnchor anchors for uuid: " + referenceAnchor.Uuid.ToString());
        var guids = new Guid[] { Guid.Parse(referenceAnchor.Uuid.ToString()) };
        var colocalizedUnboundAnchors = await DetectColocalizedUnboundAnchors(guids);
        List<OVRSpatialAnchor> foundAnchors = new List<OVRSpatialAnchor>();
        if (colocalizedUnboundAnchors != null)
        {
            foreach (var unboundAnchor in colocalizedUnboundAnchors)
            {
                try
                {
                    OVRSpatialAnchor receivedSpatialAnchor = null;
                    if (receivedAnchorPrefab)
                    {
                        var receivedSpatialAnchorGO = GameObject.Instantiate(receivedAnchorPrefab);
                        receivedSpatialAnchor = receivedSpatialAnchorGO.GetComponent<OVRSpatialAnchor>();
                    }
                    else
                    {
                        var receivedSpatialAnchorGO = new GameObject("RemoteReferenceAnchor - " + referenceAnchor.Object.StateAuthority + " - " + referenceAnchor.Uuid);
                        receivedSpatialAnchor = receivedSpatialAnchorGO.AddComponent<OVRSpatialAnchor>();
                    }
                    var initialPosition = Vector3.zero;
                    receivedSpatialAnchor.transform.position = initialPosition;
                    unboundAnchor.BindTo(receivedSpatialAnchor);
                    int watchDog = 50; // 5 seconds
                    // We wait for the anchor to be actualy localised. We also wait for pending creation, even though it is probably not relevant: seem to be used only when creating local anchors
                    while ((receivedSpatialAnchor.PendingCreation || receivedSpatialAnchor.Localized == false) && watchDog > 0)
                    {
                        Debug.Log($"[Local user detection] Waiting for remote anchor to have moved to its actual position in the room: Localized:{receivedSpatialAnchor.Localized} / Position: {receivedSpatialAnchor.transform.position} / Movement: {Vector3.Distance(initialPosition, receivedSpatialAnchor.transform.position)}");
                        watchDog--;
                        await Fusion.XR.Shared.AsyncTask.Delay(100);
                    }
                    foundAnchors.Add(receivedSpatialAnchor);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Local user detection] Error while binding anchor: " + e.Message);
                    throw e;
                }
            }
        }
        return foundAnchors;
    }

    public async Task<OVRSpatialAnchor.UnboundAnchor[]> DetectColocalizedUnboundAnchors(Guid[] anchorIds)
    {
        await WaitForLocalOculusUserIdAvailability();
        bool waitingForRequestEnd = true;
        OVRSpatialAnchor.UnboundAnchor[] foundAnchors = null;
        foreach (var anchor in anchorIds)
        {
            Debug.Log($"[Local user detection] DetectColocalizedUnboundAnchors: {anchor.ToString()}");
        }
        OVRSpatialAnchor.LoadUnboundAnchors(
            new OVRSpatialAnchor.LoadOptions
            {
                StorageLocation = OVRSpace.StorageLocation.Cloud,
                Timeout = 0,
                Uuids = anchorIds
            },
            (anchors) => {
                Debug.Log($"[Local user detection] DetectColocalizedUnboundAnchors: foundAnchors {anchors} {(anchors != null ? anchors.Length : 0)}");
                waitingForRequestEnd = false;
                foundAnchors = anchors;
            }
        );
        int watchDog = 100;
        while (waitingForRequestEnd && watchDog > 0)
        {
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }
        return foundAnchors;
    }
    #endregion

    #region Helpers
    public List<OVRSpatialAnchor> AllToBeSharedOVRSpatialAnchors()
    {
        List<OVRSpatialAnchor> anchors = new List<OVRSpatialAnchor>();
        foreach (var referenceAnchor in localReferenceAnchors)
        {
            if (referenceAnchor.ShouldBeShared) anchors.Add(referenceAnchor.spatialAnchor);
        }
        return anchors;
    }
    public List<ReferenceAnchor> AllToBeSharedReferenceAnchors()
    {
        List<ReferenceAnchor> anchors = new List<ReferenceAnchor>();
        foreach (var referenceAnchor in localReferenceAnchors)
        {
            if (referenceAnchor.ShouldBeShared) anchors.Add(referenceAnchor);
        }
        return anchors;
    }

    public List<OVRSpaceUser> AllOculusUsersWithIdReady()
    {
        List<OVRSpaceUser> users = new List<OVRSpaceUser>();
        foreach (var userInfo in oculusUserInfos)
        {
            if (userInfo.ShouldReceiveAnchorSharing) users.Add(new OVRSpaceUser(userInfo.OculusUserId));
        }
        return users;
    }

    public bool TryGetOculusUserInfoForPlayer(PlayerRef player, out OculusUserInfo user)
    {
        user = null;
        foreach (var userInfo in oculusUserInfos)
        {
            if (userInfo.Object.StateAuthority == player)
            {
                user = userInfo;
                return true;
            }
        }
        return false;
    }
    #endregion

    #region Synchronization
    public void RemoteReferenceAnchorColocalized(ReferenceAnchor anchor)
    {
        if (TryGetOculusUserInfoForPlayer(anchor.Runner.LocalPlayer, out var localOculusUser) && TryGetOculusUserInfoForPlayer(anchor.Object.StateAuthority, out var colocalizedOculusUser))
        {
            // Warn the local user that a ReferenceAnchor has been detected IRL (it has a valid OVRSpatialAnchor at a real life position)
            localOculusUser.OnColocalizedDetection(anchor, colocalizedOculusUser);
        }
        else
        {
            Debug.LogError("[Local user detection] Unexpected error");
        }
    }

    // Make sure the hardware rig teleports so that the received OVR anchor position matches the network received position (relevant for remote ReferenceAnchor only)
    public void RecenterWithAnchor(ReferenceAnchor anchor)
    {
        Debug.Log($"[Local user detection] RecenterWithAnchor");
        if(anchor.spatialAnchor == null || anchor.status != ReferenceAnchor.Status.RemoteAnchorColocalized)
        {
            Debug.LogError("[Local user detection] Unable to sync with this anchor: not found");
        }
        if (rigSelection.selectedRig.HardwareRig == null)
        {
            Debug.LogError("[Local user detection] Unable to sync with this anchor: no rig selected");
        }

        var rig = rigSelection.selectedRig.HardwareRig;
        var headsetTransform = rig.headset.transform;

        // networkTransform : position in the network room of the anchor, as created by the author (also the real life position of the anchor from their point of view)
        var networkTransform = anchor.transform;
        // colocalizedIrlTransform : Unity world position of the actual real life position of the anchor
        var colocalizedIrlTransform = anchor.spatialAnchor.transform;

        // In passthrough, we are currently seing the colocalizedIrlTransform as we should see the networkTransform
        // So, our current relative position to the colocalizedIrlTransform should be our post-resync relative position to the networkTransform
 
        // 1- capture the current relative position to the colocalizedIrlTransform
        var realHeadsetPositionInAnchorReferential = colocalizedIrlTransform.InverseTransformPoint(headsetTransform.position);
        var realHeadsetRotationInAnchorReferential = Quaternion.Inverse(colocalizedIrlTransform.rotation) * headsetTransform.rotation;

        // 2- Apply this relative position in the networkTransform referential to have our world position
        var realHeadsetPosition = networkTransform.TransformPoint(realHeadsetPositionInAnchorReferential);
        var realHeadsetRotation = networkTransform.rotation * realHeadsetRotationInAnchorReferential;

        var newPosition = realHeadsetPosition;
        newPosition.y = rig.transform.position.y;
        var changeAngle = realHeadsetRotation.eulerAngles.y - headsetTransform.rotation.eulerAngles.y;
        rig.Rotate(changeAngle);
        rig.Teleport(newPosition);
    }
    #endregion
     
    #region Wait object
    public void ChangeWaitObjectStatus(bool enabled)
    {
        if (waitObject)
            waitObject.UpdateColocalationDetectionStatus(enabled);
    }
    #endregion
}