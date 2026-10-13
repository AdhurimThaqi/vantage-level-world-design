using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// Trigger volumes from Blender: alarm (closes the shutter), alert patrols, helipad ending.
    public class VantageTrigger : MonoBehaviour
    {
        public string action;     // close:DOOR | alert:a,b | ending:C
        bool fired;

        void OnTriggerEnter(Collider other)
        {
            if (fired || !other.CompareTag("Player") || string.IsNullOrEmpty(action)) return;
            fired = true;
            var mgr = VantageLevelManager.Instance;
            int i = action.IndexOf(':');
            string verb = i >= 0 ? action.Substring(0, i) : action;
            string arg = i >= 0 ? action.Substring(i + 1) : "";
            switch (verb)
            {
                case "close":
                    var d = VantageDoor.Find(arg);
                    if (d) d.CloseShutter();
                    if (mgr) mgr.onAlarm.Invoke();
                    break;
                case "alert":
                    if (mgr) mgr.Alert(new List<string>(arg.Split(',')));
                    break;
                case "ending":
                    if (mgr) mgr.ReachHelipad();
                    break;
            }
        }
    }
}
