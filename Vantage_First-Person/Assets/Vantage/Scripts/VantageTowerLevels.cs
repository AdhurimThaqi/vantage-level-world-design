using System;
using System.Collections.Generic;
using System.Linq;
using CommandTowerKit;
using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The climb through the command tower as four levels of rising difficulty. Each level covers storeys of the
    /// tower; its enemies spawn when the level begins, and the gates (stair shutters) to the next level open only
    /// when every enemy on it is dead. Clearing the roof ends the game.
    ///
    /// Procedural part: enemy positions are picked per play from the tower's authored EnemySpawn / DroneSpawn
    /// markers with a seed (spread over different rooms first), so the fights change while the pacing, counts
    /// and difficulty stay fixed. The seed is shown by the playtest logger.
    ///
    /// Only the lights of the storeys around the player's level are switched on (the tower has 108).
    /// </summary>
    public class VantageTowerLevels : MonoBehaviour
    {
        [Serializable]
        public class Level
        {
            public string Name;
            [Tooltip("Tower storeys of this level: 0 = ground floor … 5 = top floor, 6 = roof.")]
            public int[] Storeys;
            public int Soldiers;
            public int Drones;
            public bool Turret;
            [Tooltip("Gun the soldiers carry: \"Pistol\" or \"Rifle\" (matched against the inventory item names).")]
            public string SoldierWeapon = "Rifle";
            [Tooltip("Damage per soldier bullet.")]
            public float SoldierDamage = 12f;
            [Tooltip("Multiplies soldier health.")]
            public float SoldierHealth = 1f;
            [Range(0, 1)] public float DroneAccuracy = 0.4f;
            public float DroneFireInterval = 1.4f;
            [Tooltip("Shown on the HUD when this level is cleared.")]
            public string ClearedMessage;
            [Tooltip("Activated when this level is cleared (e.g. a weapon pickup).")]
            public GameObject Reward;
        }

        public static VantageTowerLevels Instance { get; private set; }

        /// <summary>AI side of every enemy (the player is on another side; see VantageThirdPersonPlayer.Side).</summary>
        public const int EnemySide = 0;

        // Marker types authored in the tower (CommandTowerKit.GameplayMarker.markerType).
        private const string EnemySpawnMarker = "EnemySpawn", DroneSpawnMarker = "DroneSpawn", OverwatchMarker = "Overwatch";
        // A point this far below a floor (top of a flight of stairs) already counts as that storey.
        private const float StairLead = 1f;
        private const float LostCheckInterval = 2f;
        private const float SoldierSpawnLift = 0.05f;

        public Transform Tower;
        public GameObject SoldierPrefab;
        public GameObject DronePrefab;
        public GameObject TurretPrefab;
        public bool RandomSeedEachPlay = true;
        public int Seed = 1234;
        [Tooltip("Local height of the ground floor surface; each storey is StoreyHeight higher.")]
        public float GroundFloor = 0.45f;
        public float StoreyHeight = 4f;
        [Tooltip("Drones spawn this high above their marker.")]
        public float DroneSpawnHeight = 1.6f;
        [Tooltip("The tower's footprint (measured from its meshes at start) is widened by this much.")]
        public float FootprintMargin = 0.5f;
        public List<Level> Levels = new List<Level>();

        [Header("Last enemies")]
        [Tooltip("With this many enemies or fewer left, they stop guarding and come for the player.")]
        public int HuntWhenRemaining = 2;
        [Tooltip("They also come when nobody has died for this many seconds.")]
        public float HuntAfterQuietSeconds = 45f;

        /// <summary>
        /// True when the level's remaining enemies should come to the player (see VantageEnemyAggression, VantageDrone).
        /// Only while the player is on that level: hunters must not leave it (they would be sent back, see returnLostEnemies).
        /// </summary>
        public bool Hunting => Current >= 0 && !Completed && _playerLevel == Current
                               && (Remaining <= HuntWhenRemaining || Time.time - _lastKillTime > HuntAfterQuietSeconds);

        /// <summary>Index of the level being fought (Levels.Count once the tower is clear).</summary>
        public int Current { get; private set; } = -1;
        /// <summary>Living enemies of the current level (counted once per frame).</summary>
        public int Remaining
        {
            get
            {
                if (_countedFrame != Time.frameCount)
                {
                    _countedFrame = Time.frameCount;
                    _remaining = 0;
                    for (int i = 0; i < _alive.Count; i++)
                        if (isAlive(i)) _remaining++;
                }
                return _remaining;
            }
        }
        /// <summary>Enemies spawned for the current level (dead ones included until the next level starts).</summary>
        public IReadOnlyList<GameObject> Enemies => _alive;
        public bool Completed => Current >= Levels.Count;
        public string CurrentName => Completed ? "Tower clear" : Current >= 0 ? Levels[Current].Name : "";
        public event Action<int> LevelCleared;

        private readonly List<GameObject> _alive = new List<GameObject>();
        // Components cached at spawn, so counting the living needs no GetComponent calls.
        private readonly List<VantageDrone> _aliveDrones = new List<VantageDrone>();
        private readonly List<CharacterMotor> _aliveMotors = new List<CharacterMotor>();
        private int _countedFrame = -1, _remaining;
        private int _lastRemaining;
        private float _lastKillTime, _nextLostCheck;
        private VantageFloorGate[] _gates;
        private Light[] _lights;
        private GameplayMarker[] _markers;
        private System.Random _random;
        private int _playerLevel = -2;
        private Bounds _footprint;
        private int _topStorey;

        private void Awake()
        {
            Instance = this;
            // Measured before anything asks LevelAt / StoreyAt.
            _topStorey = Levels.Count > 0 ? Levels.Max(l => l.Storeys.Length > 0 ? l.Storeys.Max() : 0) : 0;
            measureFootprint();
        }

        private void Start()
        {
            if (RandomSeedEachPlay)
                Seed = (Environment.TickCount & 0x7fffffff) % 100000;
            _random = new System.Random(Seed);
            VantageEvents.RaiseLayoutGenerated(Seed);

            _gates = FindObjectsByType<VantageFloorGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _lights = Tower != null ? Tower.GetComponentsInChildren<Light>(true) : new Light[0];
            _markers = Tower != null ? Tower.GetComponentsInChildren<GameplayMarker>(true) : new GameplayMarker[0];
            var misfiled = _markers.Count(m => (m.markerType == EnemySpawnMarker || m.markerType == DroneSpawnMarker)
                                               && StoreyAt(Tower.InverseTransformPoint(m.transform.position).y) != m.floor);
            if (misfiled > 0)
                Debug.Log($"[Vantage] {misfiled} spawn markers have a floor field that doesn't match their height; spawning goes by height.");
            foreach (var l in Levels)
                if (l.Reward != null)
                    l.Reward.SetActive(false);

            begin(0);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Completed || Current < 0)
                return;

            var remaining = Remaining;
            if (remaining < _lastRemaining)
                _lastKillTime = Time.time;
            _lastRemaining = remaining;

            if (Time.time >= _nextLostCheck)
            {
                _nextLostCheck = Time.time + LostCheckInterval;
                returnLostEnemies();
            }

            if (remaining == 0)
            {
                var cleared = Current;
                var level = Levels[cleared];
                foreach (var gate in _gates)
                    if (gate.Level == cleared)
                        gate.Open();
                if (level.Reward != null)
                    level.Reward.SetActive(true);
                LevelCleared?.Invoke(cleared);
                Debug.Log($"[Vantage] {level.Name} cleared.");
                begin(cleared + 1);
            }

            var p = VantageEvents.ActivePlayer();
            var lv = p != null ? LevelAt(p.transform.position) : -1;
            if (lv != _playerLevel)
            {
                // Arriving on the level being fought starts its quiet timer, so time spent elsewhere doesn't count.
                if (lv == Current)
                    _lastKillTime = Time.time;
                _playerLevel = lv;
                updateLights();
            }
        }

        /// <summary>Level index a world position is in, or -1 outside the tower.</summary>
        public int LevelAt(Vector3 world) => levelAt(world, true);

        /// <summary>
        /// stairLead: the top of a flight already counts as the storey above (for the player arriving).
        /// Without it, a point belongs to the storey whose floor is under it (a drone under the ceiling stays on its storey).
        /// </summary>
        private int levelAt(Vector3 world, bool stairLead)
        {
            if (Tower == null) return -1;
            var local = Tower.InverseTransformPoint(world);
            if (!insideFootprint(local))
                return -1;
            var storey = stairLead ? StoreyAt(local.y) : storeyBelow(local.y);
            for (int i = 0; i < Levels.Count; i++)
                if (Array.IndexOf(Levels[i].Storeys, storey) >= 0)
                    return i;
            return -1;
        }

        public int StoreyAt(float localY) => Mathf.Clamp(Mathf.FloorToInt((localY - GroundFloor + StairLead) / StoreyHeight), 0, _topStorey);

        private bool insideFootprint(Vector3 local) =>
            local.x >= _footprint.min.x && local.x <= _footprint.max.x && local.z >= _footprint.min.z && local.z <= _footprint.max.z;

        /// <summary>Is a marker inside the tower on one of these storeys, judged by its height (not its floor field)?</summary>
        private bool isOn(GameplayMarker marker, int[] storeys)
        {
            var local = Tower.InverseTransformPoint(marker.transform.position);
            return insideFootprint(local) && Array.IndexOf(storeys, StoreyAt(local.y)) >= 0;
        }

        /// <summary>The storey whose floor is under a point (ceiling lights belong to the storey below them).</summary>
        private int storeyBelow(float localY) => Mathf.Clamp(Mathf.FloorToInt((localY - GroundFloor) / StoreyHeight), 0, _topStorey);

        /// <summary>"ground floor", "floor 3" or "roof" (the highest storey of the last level).</summary>
        public string StoreyName(int storey) => storey <= 0 ? "ground floor" : storey >= _topStorey ? "roof" : $"floor {storey}";

        /// <summary>
        /// The tower's footprint in its local x/z, from the bounds of its meshes: porch, loading bay and fire-escape
        /// platform included, so it follows the model instead of hand-measured numbers.
        /// </summary>
        private void measureFootprint()
        {
            _footprint = new Bounds();
            if (Tower == null) return;
            var first = true;
            foreach (var filter in Tower.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = Tower.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (first) { _footprint = new Bounds(local, Vector3.zero); first = false; }
                    else _footprint.Encapsulate(local);
                }
            }
            _footprint.Expand(new Vector3(FootprintMargin * 2f, 0f, FootprintMargin * 2f));
            Debug.Log($"[Vantage] Tower footprint (local): x {_footprint.min.x:F1}..{_footprint.max.x:F1}, z {_footprint.min.z:F1}..{_footprint.max.z:F1}; top storey {_topStorey}.");
        }

        #region Spawning

        private void begin(int index)
        {
            Current = index;
            _alive.Clear();
            _aliveDrones.Clear();
            _aliveMotors.Clear();
            _countedFrame = -1;
            _lastRemaining = int.MaxValue;
            _lastKillTime = Time.time;
            if (Completed)
            {
                VantageEvents.RaiseLevelCompleted();
                return;
            }

            var level = Levels[index];
            var spawns = pick(EnemySpawnMarker, level.Storeys, level.Soldiers);
            foreach (var m in spawns)
                spawnSoldier(level, m.transform);

            // Drones: drone pads/hangars on the level first, then hovering over free enemy spawns.
            var droneSpots = pick(DroneSpawnMarker, level.Storeys, level.Drones);
            if (droneSpots.Count < level.Drones)
                droneSpots.AddRange(pick(EnemySpawnMarker, level.Storeys, level.Drones - droneSpots.Count, spawns));
            foreach (var m in droneSpots)
                spawnDrone(level, m.transform.position + Vector3.up * DroneSpawnHeight, DronePrefab);

            if (level.Turret && TurretPrefab != null)
            {
                var spot = _markers.FirstOrDefault(m => m.markerType == OverwatchMarker && isOn(m, level.Storeys))
                           ?? _markers.FirstOrDefault(m => isOn(m, level.Storeys));
                if (spot != null)
                    spawnDrone(level, spot.transform.position + Vector3.up * 0.2f, TurretPrefab);
            }

            Debug.Log($"[Vantage] {level.Name}: {Remaining} enemies (seed {Seed}).");
            updateLights();
        }

        /// <summary>Seeded pick of markers, spread over different rooms before doubling up.</summary>
        private List<GameplayMarker> pick(string type, int[] storeys, int count, ICollection<GameplayMarker> exclude = null)
        {
            // Judged by where the marker is, not its floor field (some exported markers carry the wrong floor), and
            // only inside the tower: an enemy spawned outside (a drone pad on a ledge) can't be reached.
            var pool = _markers.Where(m => m.markerType == type && isOn(m, storeys) && (exclude == null || !exclude.Contains(m)))
                               .OrderBy(_ => _random.Next()).ToList();
            var result = new List<GameplayMarker>();
            var rooms = new HashSet<string>();
            foreach (var m in pool)
                if (result.Count < count && rooms.Add(m.floor + "/" + m.room))
                    result.Add(m);
            foreach (var m in pool)
                if (result.Count < count && !result.Contains(m))
                    result.Add(m);
            return result;
        }

        private void spawnSoldier(Level level, Transform at)
        {
            if (SoldierPrefab == null) return;
            var go = Instantiate(SoldierPrefab, at.position + Vector3.up * SoldierSpawnLift, Quaternion.Euler(0, at.eulerAngles.y, 0));
            go.name = $"Soldier - {level.Name}";
            var actor = go.GetComponent<BaseActor>();
            if (actor != null) actor.Side = EnemySide;
            foreach (var gun in go.GetComponentsInChildren<BaseGun>(true))
                gun.Damage = level.SoldierDamage;
            arm(go, level.SoldierWeapon);
            var health = go.GetComponent<CharacterHealth>();
            if (health != null)
            {
                health.MaxHealth *= level.SoldierHealth;
                health.Health = health.MaxHealth;
            }
            if (go.GetComponent<VantageKillReporter>() == null)
                go.AddComponent<VantageKillReporter>().EnemyName = "Soldier";
            if (go.GetComponent<VantageEnemyAggression>() == null)
                go.AddComponent<VantageEnemyAggression>();
            track(go);
        }

        /// <summary>Puts the level's gun in the soldier's hands and tells its AI to keep using that inventory slot.</summary>
        private static void arm(GameObject soldier, string weapon)
        {
            var inventory = soldier.GetComponent<CharacterInventory>();
            var motor = soldier.GetComponent<CharacterMotor>();
            if (inventory == null || motor == null || inventory.Weapons == null) return;
            var index = Array.FindIndex(inventory.Weapons, w => w.RightItem != null && w.Gun != null && w.RightItem.name.StartsWith(weapon));
            if (index < 0) index = Array.FindIndex(inventory.Weapons, w => w.Gun != null);
            if (index < 0) return;
            motor.Weapon = inventory.Weapons[index];
            motor.IsEquipped = true;
            var fire = soldier.GetComponent<AIFire>();
            if (fire != null)
            {
                fire.InventoryUsage = InventoryUsage.index;
                fire.InventoryIndex = index;
            }
        }

        private void spawnDrone(Level level, Vector3 position, GameObject prefab)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, position, Quaternion.identity);
            go.name = (prefab == TurretPrefab ? "Turret - " : "Drone - ") + level.Name;
            var drone = go.GetComponent<VantageDrone>();
            if (drone != null && prefab != TurretPrefab)
            {
                drone.Accuracy = level.DroneAccuracy;
                drone.FireInterval = level.DroneFireInterval;
            }
            track(go);
        }

        private void track(GameObject go)
        {
            _alive.Add(go);
            _aliveDrones.Add(go.GetComponent<VantageDrone>());
            _aliveMotors.Add(go.GetComponent<CharacterMotor>());
            _countedFrame = -1;
        }

        /// <summary>
        /// Safety net: an enemy that has left its level (fallen through a floor, pushed out of the tower) is put back
        /// on one of the level's spawn points, so a level can always be cleared.
        /// </summary>
        private void returnLostEnemies()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                if (!isAlive(i)) continue;
                var enemy = _alive[i].transform;
                if (levelAt(enemy.position, false) == Current || LevelAt(enemy.position) == Current) continue;
                var marker = pick(EnemySpawnMarker, Levels[Current].Storeys, 1).FirstOrDefault();
                if (marker == null) continue;
                var lostAt = Tower.InverseTransformPoint(enemy.position);
                if (_aliveDrones[i] != null)
                    _aliveDrones[i].Relocate(marker.transform.position + Vector3.up * DroneSpawnHeight);
                else
                {
                    var position = marker.transform.position + Vector3.up * SoldierSpawnLift;
                    var body = enemy.GetComponent<Rigidbody>();
                    if (body != null) { body.position = position; body.linearVelocity = Vector3.zero; }
                    enemy.position = position;
                }
                Debug.Log($"[Vantage] {enemy.name} had left its level (at local {lostAt:F1}); returned to {marker.room}.");
            }
        }

        private bool isAlive(int i)
        {
            var go = _alive[i];
            if (go == null || !go.activeInHierarchy) return false;
            var drone = _aliveDrones[i];
            if (drone != null) return !drone.IsDead;
            var motor = _aliveMotors[i];
            return motor == null || motor.IsAlive;
        }

        #endregion

        /// <summary>Lights on for the player's level and the storeys next to it; the rest of the tower stays dark.</summary>
        private void updateLights()
        {
            if (_lights == null || Tower == null) return;
            var level = _playerLevel >= 0 ? _playerLevel : Mathf.Max(0, Current);
            var storeys = new HashSet<int>();
            if (level < Levels.Count)
                foreach (var s in Levels[level].Storeys) { storeys.Add(s - 1); storeys.Add(s); storeys.Add(s + 1); }
            foreach (var l in _lights)
                if (l != null)
                    l.enabled = storeys.Contains(storeyBelow(Tower.InverseTransformPoint(l.transform.position).y));
        }
    }
}
