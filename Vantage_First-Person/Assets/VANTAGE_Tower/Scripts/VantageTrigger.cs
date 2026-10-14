using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// Trigger volumes from Blender: alarm (closes the shutter), alert patrols, helipad ending.
    // VANTAGE change: only the player sets them off; "close:" raises the alarm (VantageAlarm closes the shutter, so
    // the security camera can set off the same alarm); the level brain receives alerts and the helipad.
    public class VantageTrigger : MonoBehaviour
    {
        public string action;     // close:DOOR | alert:a,b | ending:C
        bool fired;
        float nextStay;

        void OnTriggerEnter(Collider other)
        {
            if (fired || string.IsNullOrEmpty(action)) return;
            var player = VantageEvents.ActivePlayer();
            if (player == null || other.GetComponentInParent<CoverShooter.BaseActor>() != player) return;
            int i = action.IndexOf(':');
            string verb = i >= 0 ? action.Substring(0, i) : action;
            string arg = i >= 0 ? action.Substring(i + 1) : "";
            var levels = VantageTowerLevels.Instance;
            switch (verb)
            {
                case "close":
                    fired = true;
                    VantageEvents.RaiseAlarm("trigger");
                    break;
                case "alert":
                    fired = true;
                    if (levels) levels.Alert(new List<string>(arg.Split(',')));
                    break;
                case "ending":
                    // Not one-shot: the helipad waits until the platform is clear.
                    if (levels) fired = levels.ReachHelipad();
                    break;
            }
        }

        // The helipad is re-checked while the player stands on it.
        void OnTriggerStay(Collider other)
        {
            if (fired || action == null || !action.StartsWith("ending") || Time.time < nextStay) return;
            nextStay = Time.time + 0.5f;
            OnTriggerEnter(other);
        }
    }
}
