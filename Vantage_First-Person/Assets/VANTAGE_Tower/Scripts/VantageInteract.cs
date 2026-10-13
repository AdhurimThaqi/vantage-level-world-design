using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// Console / key slot / terminal / switch: press E nearby to unlock a door.
    public class VantageInteract : MonoBehaviour
    {
        public string unlocksDoor;
        public string requiresItem;
        public bool opensShutter;          // inside switch of the maintenance shutter
        public float radius = 2.0f;
        public UnityEvent onUsed;

        Transform player;
        bool used, inRange;
        string message;
        float messageUntil;

        void Update()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (!p) return;
                player = p.transform;
            }
            inRange = !used && Vector3.Distance(player.position, transform.position) < radius;
            if (!inRange || !VantageInput.UsePressed()) return;

            if (!VantageInventory.Has(requiresItem))
            {
                message = "You need the " + requiresItem;
                messageUntil = Time.time + 2f;
                return;
            }
            var door = VantageDoor.Find(unlocksDoor);
            if (opensShutter && door) door.OpenShutter();
            else if (door) door.Unlock();
            used = true;
            onUsed.Invoke();
        }

        void OnGUI()
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            Rect r = new Rect(0, Screen.height * 0.62f, Screen.width, 40);
            if (Time.time < messageUntil) GUI.Label(r, message, s);
            else if (inRange) GUI.Label(r, "[E] Use", s);
        }
    }
}
