using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// Gun / key pickups: walk into them.
    // VANTAGE change: only the player picks up (not soldiers walking past); raises VantageEvents.ItemPickedUp; the
    // idle spin moved to VantageKeyItem (the key's float and fly-to-player animation).
    public class VantagePickup : MonoBehaviour
    {
        public string item;
        public UnityEvent onPickedUp;

        void OnTriggerEnter(Collider other)
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null || other.GetComponentInParent<CoverShooter.BaseActor>() != player) return;
            VantageInventory.Add(item);
            VantageEvents.RaiseItemPickedUp(item);
            onPickedUp.Invoke();
            // VantageKeyItem hides it after its animation; without one it goes at once.
            if (GetComponent<VantageKeyItem>() == null) gameObject.SetActive(false);
            else enabled = false;
        }
    }
}
