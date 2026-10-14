using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// One per scene. Spawns your prefabs on the Blender markers, hides the marker meshes in Play mode,
    /// tracks which enemies are alive (for "kill X to open the door") and runs the chosen ending.
    // VANTAGE change: reduced to a bridge. VantageTowerLevels (Assets/Vantage/Scripts) spawns the enemies, tracks
    // who is dead, alerts patrols and runs the ending (boss, then helicopter); the Vantage setup hides the markers
    // once in the editor. This keeps the package's API and events for scripts that use them.
    public class VantageLevelManager : MonoBehaviour
    {
        public static VantageLevelManager Instance { get; private set; }

        public UnityEvent onAlarm;

        void Awake()
        {
            Instance = this;
            VantageInventory.Clear();
        }

        void OnEnable() => VantageEvents.AlarmRaised += alarm;
        void OnDisable() => VantageEvents.AlarmRaised -= alarm;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void alarm(string source) => onAlarm.Invoke();

        /// name, or prefix ending with * (e.g. "MK_Enemy_*")
        public bool IsAlive(string nameOrPrefix) =>
            VantageTowerLevels.Instance != null && !VantageTowerLevels.Instance.KillListDone(new[] { nameOrPrefix });

        public void Alert(IEnumerable<string> names) { if (VantageTowerLevels.Instance) VantageTowerLevels.Instance.Alert(names); }

        public void ReachHelipad() { if (VantageTowerLevels.Instance) VantageTowerLevels.Instance.ReachHelipad(); }
    }

    public static class VantageInventory
    {
        static readonly HashSet<string> items = new HashSet<string>();
        public static void Add(string item) { if (!string.IsNullOrEmpty(item)) items.Add(item); }
        public static bool Has(string item) => string.IsNullOrEmpty(item) || items.Contains(item);
        public static void Clear() => items.Clear();
    }
}
