using Fusion;
using Fusion.Addons.DynamicAudioGroup;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class DisplayDynamicAudioGroupInfos : NetworkBehaviour
{

    [SerializeField] private TextMeshPro placeHolder;
    [SerializeField] private DynamicAudioGroupMember dynamicAudioGroupMember;
    [SerializeField] private Speaker speaker;
    [SerializeField] private Color proximityDistanceColor;
    [SerializeField] private Color proximityLeavingDistanceColor;

    private string infos;

    private void Awake()
    {
        if (!placeHolder)
            placeHolder = GetComponentInChildren<TextMeshPro>();

        if (!dynamicAudioGroupMember)
            dynamicAudioGroupMember = GetComponentInChildren<DynamicAudioGroupMember>();

        if (!speaker)
            speaker = GetComponentInChildren<Speaker>();
    }

    public override void FixedUpdateNetwork()
    {
        UpdateDynamicAudioGroupInfo();
    }

    private void UpdateDynamicAudioGroupInfo()
    {
        if (dynamicAudioGroupMember && placeHolder)
        {
            infos = "Player's GroupID : " + dynamicAudioGroupMember.GroupId.ToString();
            infos += "\nThis player speak to the group : " + dynamicAudioGroupMember.GroupId.ToString();
            if (Object && Object.HasStateAuthority)
            {
                infos += "\nThis Player listen to groups : ";
                foreach (var listenedMember in dynamicAudioGroupMember.listenedtoMembers)
                {
                    if(listenedMember.Object == null)
                    {
                        // The member is being destroy
                        continue;
                    }
                    infos += listenedMember.GroupId.ToString() + " ";
                }
            }
            infos += "\nVoice transmission is enabled : " + !dynamicAudioGroupMember.IsMuted;
            infos += "\nProximity Distance : " + dynamicAudioGroupMember.proximityDistance;

            placeHolder.text = infos;
        }
    }

    void OnDrawGizmos()
    {
        if (dynamicAudioGroupMember && speaker)
        {
            Gizmos.color = proximityDistanceColor;
            Gizmos.DrawSphere(speaker.transform.position, dynamicAudioGroupMember.proximityDistance);
            Gizmos.color = proximityLeavingDistanceColor;
            Gizmos.DrawSphere(speaker.transform.position, dynamicAudioGroupMember.proximityLeavingDistance);
        }
    }
}