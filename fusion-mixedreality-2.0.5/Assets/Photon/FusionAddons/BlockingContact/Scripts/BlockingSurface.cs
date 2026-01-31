using UnityEngine;

namespace Fusion.Addons.BlockingContact
{
    /***
     * 
     * BlockingSurface is used to define a surface that will block a BlockableTip
     * 
     ***/
    public class BlockingSurface : MonoBehaviour
    {
        public Transform referential;
        public Vector3 positiveProximityThresholds = new Vector3(0.5f, 0.5f, 0.2f);
        public Vector3 negativeProximityThresholds = new Vector3(-0.5f, -0.5f, -0.001f);

        public float maxDepth = 0.005f;

        private void Awake()
        {
            if (referential == null) referential = transform;
        }
    }
}
