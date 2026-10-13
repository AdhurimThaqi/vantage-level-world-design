using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// One per scene. Spawns your prefabs on the Blender markers, hides the marker meshes in Play mode,
    /// tracks which enemies are alive (for "kill X to open the door") and runs the chosen ending.
    public class VantageLevelManager : MonoBehaviour
    {
        public static VantageLevelManager Instance { get; private set; }

        public enum Ending { A_FloorBreaks, B_BossAppears, C_SceneCloses }

        [Header("Prefabs (leave empty to playtest the layout without enemies)")]
        public GameObject enemyPrefab;
        public GameObject turretPrefab;
        public GameObject heavyRobotPrefab;
        public GameObject bossPrefab;
        [Tooltip("Blender enemy markers face along their local -Z after import. Untick if your spawned enemies face backwards.")]
        public bool flipMarkerFacing = true;

        [Header("Markers")]
        public bool hideMarkersInPlay = true;

        [Header("Ending (pick one, triggered on the helipad)")]
        public Ending ending = Ending.C_SceneCloses;
        public float floorBreakDelay = 1.5f;
        public UnityEvent onAlarm, onEndingA, onEndingB, onEndingC;

        readonly Dictionary<string, GameObject> spawned = new Dictionary<string, GameObject>();
        bool endingFired, showWin;

        static readonly HashSet<string> markerTypes = new HashSet<string>
        { "EnemySpawn", "Turret", "HeavyEnemy", "Objective", "Exit", "PatrolPath", "Trigger", "SecurityCamera" };

        void Awake()
        {
            Instance = this;
            VantageInventory.Clear();
        }

        void Start()
        {
            foreach (var tag in FindObjectsByType<VantageTag>(FindObjectsSortMode.None))
            {
                string mt = tag.MarkerType;
                GameObject prefab = null;
                bool flip = flipMarkerFacing;
                if (mt == "EnemySpawn") prefab = enemyPrefab;
                else if (mt == "Turret") prefab = turretPrefab;
                else if (mt == "HeavyEnemy") { prefab = heavyRobotPrefab; flip = false; }

                if (prefab)
                {
                    Vector3 fwd = flip ? -tag.transform.forward : tag.transform.forward;
                    fwd.y = 0;
                    var rot = fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
                    var g = Instantiate(prefab, tag.transform.position, rot);
                    g.name = tag.name;
                    spawned[tag.name] = g;
                }

                bool isMarker = markerTypes.Contains(mt) || (mt == "EndingOption" && tag.GetComponent<VantageBreakable>() == null);
                if (hideMarkersInPlay && isMarker)
                    foreach (var r in tag.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
        }

        /// name, or prefix ending with * (e.g. "MK_Enemy_*")
        public bool IsAlive(string nameOrPrefix)
        {
            bool prefix = nameOrPrefix.EndsWith("*");
            string key = prefix ? nameOrPrefix.TrimEnd('*') : nameOrPrefix;
            foreach (var kv in spawned)
            {
                bool match = prefix ? kv.Key.StartsWith(key) : kv.Key == key;
                if (match && kv.Value != null && kv.Value.activeInHierarchy) return true;
            }
            return false;
        }

        public void Alert(IEnumerable<string> names)
        {
            foreach (var n in names)
                if (spawned.TryGetValue(n.Trim(), out var g) && g)
                    g.SendMessage("OnAlert", SendMessageOptions.DontRequireReceiver);
        }

        public void ReachHelipad()
        {
            if (endingFired) return;
            endingFired = true;
            switch (ending)
            {
                case Ending.A_FloorBreaks:
                    var floor = FindAnyObjectByType<VantageBreakable>();
                    if (floor) floor.Invoke(nameof(VantageBreakable.Break), floorBreakDelay);
                    onEndingA.Invoke();
                    break;
                case Ending.B_BossAppears:
                    var boss = GameObject.Find("FINAL_MK_Boss_EndingB");
                    if (bossPrefab && boss) Instantiate(bossPrefab, boss.transform.position, Quaternion.LookRotation(-boss.transform.forward));
                    onEndingB.Invoke();
                    break;
                default:
                    if (onEndingC.GetPersistentEventCount() == 0) showWin = true;
                    onEndingC.Invoke();
                    break;
            }
        }

        void OnGUI()
        {
            if (!showWin) return;
            var s = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "MISSION COMPLETE", s);
        }
    }

    public static class VantageInventory
    {
        static readonly HashSet<string> items = new HashSet<string>();
        public static void Add(string item) { if (!string.IsNullOrEmpty(item)) items.Add(item); }
        public static bool Has(string item) => string.IsNullOrEmpty(item) || items.Contains(item);
        public static void Clear() => items.Clear();
    }
}
