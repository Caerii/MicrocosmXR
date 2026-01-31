using Fusion;
using Fusion.Addons.MetaOVRHandsSynchronization;
using Fusion.XR.Shared.Rig;
using UnityEngine;

public class SpawnObject : NetworkBehaviour
{
#if OCULUS_SDK_AVAILABLE

    public GameObject networkObjectPrefab;

    HardwareOVRHandCollecter localInputCollector;
    float lastSpawntime = 0;
    public float minDelayBetweenSpawns = 0.5f;

    // Update is called once per frame
    void Update()
    {
        if (Object.HasStateAuthority && localInputCollector == null)
        {
            var networkHand = GetComponentInParent<NetworkHand>();
            var rig = GetComponentInParent<NetworkRig>();
            if (rig && rig.hardwareRig && networkHand)
            {
                foreach(var collector in rig.hardwareRig.GetComponentsInChildren<HardwareOVRHandCollecter>())
                {
                    if (collector.Side == networkHand.side)
                    {
                        localInputCollector = collector;
                    }
                }
            }
        }
        if (networkObjectPrefab && Object.HasStateAuthority && localInputCollector)
        {
            bool shouldSpawn = false;
            if(localInputCollector.IsUsingFingerTracking && localInputCollector.IsHoveringGrabbable == false && localInputCollector.IsPinchingIndex)
            {
                shouldSpawn = true;
            }
            else if (localInputCollector.IsUsingFingerTracking == false && localInputCollector.IsUsingTrigger)
            {
                shouldSpawn = true;
            } 

            if (shouldSpawn)
            {
                if ((lastSpawntime == 0 || (Time.time - lastSpawntime) > minDelayBetweenSpawns))
                {
                    Object.Runner.Spawn(networkObjectPrefab, transform.position, transform.rotation, Runner.LocalPlayer);
                }
                lastSpawntime = Time.time;
            }
        }

    }
#endif
}
