using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// Ending A: the helipad section that falls.
    public class VantageBreakable : MonoBehaviour
    {
        public void Break()
        {
            var mc = GetComponent<MeshCollider>();
            if (mc) mc.convex = true;
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = 2000f;
            rb.AddTorque(new Vector3(1f, 0f, 0.5f) * 4000f, ForceMode.Impulse);
        }
    }
}
