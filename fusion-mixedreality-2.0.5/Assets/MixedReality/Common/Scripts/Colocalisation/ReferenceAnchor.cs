using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System.Threading.Tasks;
using Oculus.Platform;
using System;


// Based on doc https://developer.oculus.com/documentation/unity/unity-shared-spatial-anchors/ and sample https://github.com/oculus-samples/Unity-Discover
public class ReferenceAnchor : NetworkBehaviour
{
    [Networked]
    public NetworkString<_128> Uuid { get; set; }

    public const int MAX_SHARED_USERS = 255;
    [Networked, Capacity(MAX_SHARED_USERS)]
    public NetworkArray<ulong> SharedWithOculusUserIds { get; }

    public SSAManager ssaManager;

    public OVRSpatialAnchor spatialAnchor;

    [Tooltip("Number of time we'll try to check if an anchor is presednt in the real life room")]
    public int anchorDetectionAttempts = 4;
    [HideInInspector]
    public int currentAnchorDetectionAttempts = 0;


    public bool synchronizePositionWithLocalSpatialAnchor = true;

    bool remoteReferenceAnchorUUIDSharedWithUs = false;

    public enum Status
    {
        // For the local anchor creator
        Undefined,
        WaitingForUUID,
        UIIDMissing,
        SavingToCloud,
        UnableToSaveToCloud,
        SavedToCloud,
        SharingFailed,
        Ready,
        // For remote users
        RemoteAnchorWithoutUUIDYetOrSharingNotYetAllowed,
        RemoteAnchorCheckingIfAnchorPresentInIRLRoom,
        RemoteAnchorColocalized,
        RemoteAnchorNotcolocalized
    }

    public Status status = Status.Undefined;

    bool IsPendingCreation => spatialAnchor && spatialAnchor.PendingCreation;
    bool IsSpatialAnchorUUIDValid => spatialAnchor && spatialAnchor.PendingCreation == false && spatialAnchor.Uuid != null && string.IsNullOrEmpty(spatialAnchor.Uuid.ToString().Replace("0", "").Replace("-", "")) == false;
    public bool IsCreated => spatialAnchor && spatialAnchor.Created;
    bool savedToCloud = false;

    // We only share the anchor we created localy 
    public bool ShouldBeShared => IsCreated && Object && Object.HasStateAuthority && savedToCloud;

    public override void Spawned()
    {
        base.Spawned();
        if (ssaManager == null)
        {
            ssaManager = FindObjectOfType<SSAManager>();
        }
        if (Object.HasStateAuthority)
        {
            status = Status.WaitingForUUID;
            StartAnchorSharing();
        }
        else
        {
            status = Status.RemoteAnchorWithoutUUIDYetOrSharingNotYetAllowed;
            ssaManager.RegisterRemoteReferenceAnchor(this);
        }
    }

    public async void StartAnchorSharing()
    {
        // To avoid issue with Oculus backend, we make sure to know our Oculus id before starting manipulating anchors
        await ssaManager.WaitForLocalOculusUserIdAvailability();

        spatialAnchor = GetComponent<OVRSpatialAnchor>();
        if (spatialAnchor == null)
        {
            GameObject spatialAnchorGO = null;
            if (ssaManager && ssaManager.localAnchorPrefab)
            {
                spatialAnchorGO = GameObject.Instantiate(ssaManager.localAnchorPrefab);
                spatialAnchor = spatialAnchorGO.GetComponent<OVRSpatialAnchor>();
            }
            else
            {
                spatialAnchorGO = new GameObject();
                spatialAnchor = spatialAnchorGO.AddComponent<OVRSpatialAnchor>();
            }
            spatialAnchorGO.name = "LocalSpatialAnchor";
            spatialAnchorGO.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }
        ssaManager.RegisterLocalReferenceAnchor(this);
        ShareAnchor();
    }

    public void OnDestroy()
    {
        if (ssaManager != null)
        {
            // If local user
            ssaManager.UnregisterLocalReferenceAnchor(this);
            // If remove anchor
            ssaManager.UnregisterRemoteReferenceAnchor(this);
        }
        if (spatialAnchor)
        {
            Destroy(spatialAnchor.gameObject);
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        SyncWithLocalanchor();
    }

    void SyncWithLocalanchor()
    {
        if (Object.HasStateAuthority && spatialAnchor && synchronizePositionWithLocalSpatialAnchor)
        {
            transform.position = spatialAnchor.transform.position;
            transform.rotation = spatialAnchor.transform.rotation;
        }
    }

    public override void Render()
    {
        base.Render();
        SyncWithLocalanchor();

        // Checking if remote anchor user has authorized us to access their anchor on Oculus backend (we only check when out oculus id is ready)
        if (Object.HasStateAuthority == false && remoteReferenceAnchorUUIDSharedWithUs == false && Uuid != "")
        {
            if (ssaManager.TryGetOculusUserInfoForPlayer(Runner.LocalPlayer, out var localOculusUser) && localOculusUser.oculusIdLookupStatus == OculusUserInfo.OculusIdlookupStatus.Ready)
            {
                //Debug.Log($"[Local user detection] Checking if anchor ({Uuid}) shared with us ({localOculusUser.OculusUserId})... ");
                foreach (var userId in SharedWithOculusUserIds)
                {
                    if (userId == localOculusUser.OculusUserId)
                    {
                        remoteReferenceAnchorUUIDSharedWithUs = true;
                        Debug.Log($"[Local user detection] ReferenceAnchor: anchor ({Uuid}) shared with us ({localOculusUser.OculusUserId})");

                        break;
                    }
                }
            }
        }

        // Checking colocalization of remote anchor (sharing authorized, UUID known)
        if (status == Status.RemoteAnchorWithoutUUIDYetOrSharingNotYetAllowed && Uuid != "" && remoteReferenceAnchorUUIDSharedWithUs)
        {
            // uuid received
            CheckIfAnchorIsColocalized();
        }
    }

    async void CheckIfAnchorIsColocalized()
    {
        status = Status.RemoteAnchorCheckingIfAnchorPresentInIRLRoom;
        Debug.Log($"[Local user detection] CheckIfAnchorIsColocalized: LookForAnchor for UUID: " + Uuid.ToString());
        for(currentAnchorDetectionAttempts = 0; currentAnchorDetectionAttempts < anchorDetectionAttempts; currentAnchorDetectionAttempts++)
        {
            foreach (var colocalizedAnchor in await ssaManager.DetectColocalizedAnchor(this))
            {
                // DetectColocalizedAnchor should return up to 1 value in normal cases
                spatialAnchor = colocalizedAnchor;
                status = Status.RemoteAnchorColocalized;
                OculusUserInfo user = null;
                if (ssaManager.TryGetOculusUserInfoForPlayer(Object.StateAuthority, out var foundUser))
                {
                    user = foundUser;
                }
                Debug.Log($"[Local user detection] Remote spatial anchor found: {Uuid}. Position found:{ colocalizedAnchor.transform.position } / network position: { transform.position } / user: {(user == null ? "Not found" : $"{user.Object.StateAuthority}-{user.Object.Id}")}");
                ssaManager.RemoteReferenceAnchorColocalized(this);
            }
            if (status == Status.RemoteAnchorColocalized)
            {
                break;
            } 
            else
            {
                Debug.Log($"[Local user detection] No IRL anchor found for UUID (attempt #{currentAnchorDetectionAttempts}): {Uuid.ToString()}");
            }
        }

        if (status == Status.RemoteAnchorCheckingIfAnchorPresentInIRLRoom)
        {
            Debug.Log($"[Local user detection] No IRL anchor found for UUID (after all attempts): " + Uuid.ToString());
            status = Status.RemoteAnchorNotcolocalized;
        }
    }

    #region SSA handling
    async void ShareAnchor()
    {
        await WaitForUUID();
        if (IsCreated == false) return;

        if (await SaveToCloud() != OVRSpatialAnchor.OperationResult.Success) 
        {
            return;
        }

        if (ssaManager.TryGetOculusUserInfoForPlayer(Runner.LocalPlayer, out var oculusUser))
        {
            Debug.Log("[Local user detection] Sharing anchor as Oculus user: " + oculusUser.OculusUserId);
        } 
        else
        {
            Debug.Log("[Local user detection] Sharing anchor as unknown (yet) Oculus user ");
        }

        if (await ssaManager.ShareAnchors() != OVRSpatialAnchor.OperationResult.Success)
        {
            status = Status.SharingFailed;
            Debug.LogError("[Local user detection] Error sharing failed");
            return;
        }
        // The anchor is fully shared: we saved in in the network var
        // Might be stored as a byte array too
        Uuid = spatialAnchor.Uuid.ToString();

        status = Status.Ready;
    }

    public void SharingAuthorizedFor(List<OVRSpaceUser> ovrSpaceUsers)
    {
        int i = 0;
        SharedWithOculusUserIds.Clear();
        foreach(var user in ovrSpaceUsers)
        {
            SharedWithOculusUserIds.Set(i, user.Id);
            i++;
        }
    }

    public void UserListChanged()
    {
        if (Object == null) return;
        for(int i = 0; i < MAX_SHARED_USERS; i++)
        {
            if (SharedWithOculusUserIds[i] != 0)
            {
                var sharedUserId = SharedWithOculusUserIds[i];
                var stillconnected = false;
                // We check if this user is still connected
                foreach (var user in ssaManager.oculusUserInfos)
                {
                    if (user.OculusUserId == sharedUserId)
                    {
                        stillconnected = true;
                        break;
                    }
                }
                if(stillconnected == false)
                {
                    Debug.Log("[Local user detection] Removing disconnected user from the share list");
                    SharedWithOculusUserIds.Set(i, 0);
                }
            }
        }
    }

    async Task WaitForUUID()
    {
        status = Status.WaitingForUUID;
        // The request of the OVRSpacialAnchor might not yet be started (it is launched in the Start() of OVRSpatialAnchor)
        int watchDog = 10;
        while (IsPendingCreation == false && IsSpatialAnchorUUIDValid == false && watchDog > 0)
        {
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }
        // waoiting for the request to answer
        watchDog = 40;
        while (IsPendingCreation && IsSpatialAnchorUUIDValid == false && watchDog > 0)
        {
            watchDog--;
            await Fusion.XR.Shared.AsyncTask.Delay(100);
        }
        if (IsCreated)
        {
            Debug.Log("[Local user detection] Anchor created: " + spatialAnchor.Uuid);
        }
        else
        {
            Debug.LogError("[Local user detection] Error: anchor creation failed: " + spatialAnchor.Uuid);
            status = Status.UIIDMissing;
        }
    }

    public async Task<OVRSpatialAnchor.OperationResult> SaveToCloud()
    {
        status = Status.SavingToCloud;
        OVRSpatialAnchor.OperationResult savingResult = await ssaManager.SaveAnchorsToCloud(new List<OVRSpatialAnchor> { spatialAnchor });
        if (savingResult != OVRSpatialAnchor.OperationResult.Success)
        {
            status = Status.UnableToSaveToCloud;
        }
        else
        {
            status = Status.SavedToCloud;
            savedToCloud = true;
        }
        return savingResult;
    }
    #endregion
}
