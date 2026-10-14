using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// Console / key slot / terminal / switch: press E nearby to unlock a door.
    // VANTAGE change: no OnGUI (it ran on every console every frame); the "[E]" prompt and messages go to the HUD
    // through VantageEvents; the player comes from VantageEvents.ActivePlayer(); the shutter switch stays dead
    // while the shutter is in lockdown (its kill list is still alive).
    public class VantageInteract : MonoBehaviour
    {
        public string unlocksDoor;
        public string requiresItem;
        public bool opensShutter;          // inside switch of the maintenance shutter
        public float radius = 2.0f;
        public UnityEvent onUsed;

        bool used, inRange;

        public bool Used => used;

        void OnDisable()
        {
            if (inRange) VantageEvents.ClearPrompt(this);
            inRange = false;
        }

        void Update()
        {
            var player = VantageEvents.ActivePlayer();
            var now = false;
            if (!used && player != null)
            {
                Vector3 d = player.transform.position - transform.position;
                now = new Vector2(d.x, d.z).sqrMagnitude < radius * radius && Mathf.Abs(d.y) < 2f;
            }
            if (now != inRange)
            {
                inRange = now;
                if (now) VantageEvents.SetPrompt(this, string.IsNullOrEmpty(requiresItem) ? "Use" : "Insert the " + requiresItem);
                else VantageEvents.ClearPrompt(this);
            }
            if (inRange && VantageInput.UsePressed()) TryUse();
        }

        /// <summary>The player pressed E in range (also called by the autoplay bot). True if it was used.</summary>
        public bool TryUse()
        {
            if (used) return false;
            if (!VantageInventory.Has(requiresItem))
            {
                VantageEvents.RaiseMessage("You need the " + requiresItem);
                return false;
            }
            var door = VantageDoor.Find(unlocksDoor);
            if (opensShutter && door != null && door.HasKillList && door.ShutterClosed)
            {
                VantageEvents.RaiseMessage("Lockdown: clear the floor first");
                return false;
            }
            if (opensShutter && door) door.OpenShutter();
            else if (door) door.Unlock();
            used = true;
            inRange = false;
            VantageEvents.ClearPrompt(this);
            onUsed.Invoke();
            return true;
        }
    }
}
