using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SSAWaitUI : MonoBehaviour
{
    public TMPro.TextMeshProUGUI oculusIdStatucLabel;
    public TMPro.TextMeshProUGUI colocStatusLabel;
    public TMPro.TextMeshProUGUI usersLabel;
    public GameObject anchorsPanel;
    public GameObject anchorUIPrefab;

    [HideInInspector]
    public SSAManager ssaManager;

    [System.Serializable]
    public struct AnchorUIPanel
    {
        public GameObject panel;
        public TMPro.TextMeshProUGUI text;
        public bool isLocal;
    }
    Dictionary<ReferenceAnchor, AnchorUIPanel> anchorPanels = new Dictionary<ReferenceAnchor, AnchorUIPanel>();

    private void Update()
    {
        if (ssaManager == null)
        {
            return;
        }
        if (ssaManager.localOculusUserInfo && ssaManager.localOculusUserInfo.Object)
        {
            switch (ssaManager.localOculusUserInfo.ColocStatus)
            {
                case OculusUserInfo.ColocalizationDetectionStatus.LookingForColocalization:
                    colocStatusLabel.text = "Starting colocalization detection ...";
                    break;
                case OculusUserInfo.ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorCreation:
                    colocStatusLabel.text = "Waiting for their remote users anchor registration ...";
                    break;
                case OculusUserInfo.ColocalizationDetectionStatus.WaitingForRemoteReferenceAnchorStatus:
                    colocStatusLabel.text = "Waiting for the remote user anchors to have been analyzed ...";
                    break;
                case OculusUserInfo.ColocalizationDetectionStatus.LocalDetectionFinished:
                    colocStatusLabel.text = "Colocalization detection finished (other user detection might still be running).";
                    break;
                case OculusUserInfo.ColocalizationDetectionStatus.DetectionFinished:
                    colocStatusLabel.text = "Colocalization detection finished.";
                    break;
                case OculusUserInfo.ColocalizationDetectionStatus.CandidateColocReferenceFoundButStillLookingToo:
                    colocStatusLabel.text = "Colocalized user found, but their own colocalization is still running ...";
                    break;
            }

            int searching = 0;
            foreach (var user in ssaManager.oculusUserInfos)
            {
                if (user.Object && user.ColocStatus != OculusUserInfo.ColocalizationDetectionStatus.DetectionFinished && user != ssaManager.localOculusUserInfo)
                    searching++;
            }
            usersLabel.text = $"Remote users: {ssaManager.oculusUserInfos.Count - 1}\n" +
                $"Colocalized users: {ssaManager.localOculusUserInfo.colocalizedUsers.Count}\n" +
                $"Searching users: {searching}";
            oculusIdStatucLabel.text = $"Oculus ID Lookup: {ssaManager.localOculusUserInfo.oculusIdLookupStatus}";
        } 
        else
        {
            usersLabel.text = $"Remote users: 0\n" +
                $"Colocalized users: 0\n" +
                $"Searching users: 0";
            oculusIdStatucLabel.text = $"Oculus ID Lookup: Waiting for local user spawn ...";
        }

        foreach (var anchor in ssaManager.localReferenceAnchors) CreateAnchorPanel(anchor, isLocal: true);
        foreach (var anchor in ssaManager.remoteReferenceAnchors) CreateAnchorPanel(anchor, isLocal: false);

        List<ReferenceAnchor> panelanchorToRemove = new List<ReferenceAnchor>();       
        foreach(var anchor in anchorPanels.Keys)
        {
            if (ssaManager.localReferenceAnchors.Contains(anchor) == false && ssaManager.remoteReferenceAnchors.Contains(anchor) == false)
            {
                panelanchorToRemove.Add(anchor);
                continue;
            }
            string uuid = anchor.Uuid.ToString();
            uuid = uuid == "" ? "" : $"[{anchor.Uuid.ToString().Substring(0, Mathf.Min(4, anchor.Uuid.ToString().Length))}...] ";
            string status = anchor.status.ToString();
            if(anchor.status == ReferenceAnchor.Status.RemoteAnchorCheckingIfAnchorPresentInIRLRoom)
            {
                status = "CheckingIfAnchorPresentInIRLRoom #"+(anchor.currentAnchorDetectionAttempts+1);
            }
            anchorPanels[anchor].text.text = $"{(anchorPanels[anchor].isLocal?"[Local] ":"")} {uuid}{status}";
        }
        foreach (var anchor in panelanchorToRemove)
        {
            Destroy(anchorPanels[anchor].panel);
            anchorPanels.Remove(anchor);
        }
    }

    void CreateAnchorPanel(ReferenceAnchor anchor, bool isLocal)
    {
        if (anchorPanels.ContainsKey(anchor)) return;

        var panel = GameObject.Instantiate(anchorUIPrefab, anchorsPanel.transform);
        var anchorPanel = new AnchorUIPanel { panel = panel, text = panel.GetComponentInChildren<TMPro.TextMeshProUGUI>(), isLocal = isLocal };
        anchorPanels.Add(anchor, anchorPanel);
        // To be sure the layout constraints are updated
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(anchorsPanel.GetComponent<RectTransform>());
    }

    public void UpdateColocalationDetectionStatus(bool enabled)
    {
        //gameObject.SetActive(enabled);
    }
}
