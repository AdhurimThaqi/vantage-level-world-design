using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// Gun / key pickups: walk into them.
    public class VantagePickup : MonoBehaviour
    {
        public string item;
        public UnityEvent onPickedUp;

        void Update() { transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World); }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            VantageInventory.Add(item);
            onPickedUp.Invoke();
            gameObject.SetActive(false);
        }
    }
}
