using System;
using System.Collections.Generic;
using System.Linq;
using CoverShooter;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage
{
    /// <summary>An enemy that isn't a template soldier (drone, turret, heavy robot, boss).</summary>
    public interface IVantageEnemy
    {
        bool IsDead { get; }
        /// <summary>Alive and has the player in sight right now (what it checks before it fires; the music's combat state).</summary>
        bool Engaging { get; }
    }

    /// <summary>
    /// The tower as three levels and the helipad finale (VANTAGE_Tower from Blender). Each level's enemies spawn when
    /// it begins, on the Blender markers whose names start with the level's prefix (MK_, L2_, L3_, FINAL_). Doors
    /// and triggers come from the Blender properties (VantageDoor, VantageTrigger, VantageInteract); this decides who
    /// is alive for their KILL lists, when a level is clear, who hunts the player, the checkpoint after a death, and
    /// the ending (boss on the helipad, then the extraction).
    ///
    /// Procedural part: which markers get a soldier is picked per play with a seed (spread over rooms first), so the
    /// fights change while counts, pacing and difficulty stay fixed. -vantageSeed N replays a layout.
    /// </summary>
    public class VantageTowerLevels : MonoBehaviour
    {
        [Serializable]
        public class Level
        {
            public string Name;
            [Tooltip("Blender marker names of this level start with this (MK_, L2_, L3_, FINAL_).")]
            public string Prefix;
            [Tooltip("World height of the level's walking floor (set by Vantage → Tower → Set Up).")]
            public float FloorY;
            public int Soldiers;
            public int Drones;
            [Tooltip("Gun the soldiers carry: \"Pistol\" or \"Rifle\" (matched against the inventory item names).")]
            public string SoldierWeapon = "Rifle";
            public float SoldierDamage = 12f;
            [Tooltip("Multiplies soldier health.")]
            public float SoldierHealth = 1f;
            [Range(0, 1)] public float DroneAccuracy = 0.4f;
            public float DroneFireInterval = 1.4f;
            [Tooltip("Shown on the HUD when this level is cleared.")]
            public string ClearedMessage;
            [Tooltip("Where the player restarts after dying on this level.")]
            public Transform Arrival;

            [Header("Second wave (2026-11-25)")]
            [Tooltip("Markers (names, or prefixes ending in *) held back for a second wave: it spawns when the first wave is dead (and SecondWaveWeapon is in hand), and SecondWaveDoor opens.")]
            public string SecondWave = "";
            [Tooltip("Door kept shut and locked until the second wave starts (e.g. L2_DOOR_Office).")]
            public string SecondWaveDoor = "";
            [Tooltip("Gun the player must have before the second wave starts (Rifle); empty: none.")]
            public string SecondWaveWeapon = "";
            public int SecondWaveSoldiers;
            public int SecondWaveDrones;
            [Tooltip("HUD message as the second wave starts.")]
            public string SecondWaveMessage = "";
            [Tooltip("Markers never used on this level (names, or prefixes ending in *).")]
            public string Unused = "";
            [Tooltip("Optional tower object: the heavy robot stands at its middle (on the NavMesh) instead of on its marker.")]
            public string HeavyAt = "";
            [Header("Grenades (2026-11-25)")]
            [Tooltip("Optional tower object: a bucket of grenades stands on the NavMesh at its position when the level begins.")]
            public string GrenadeCrateAt = "";
            [Tooltip("The bucket fills the player up to this many grenades.")]
            public int GrenadeCount = 3;
        }

        public static VantageTowerLevels Instance { get; private set; }

        /// <summary>AI side of every enemy (the player is on another side; see VantageThirdPersonPlayer.Side).</summary>
        public const int EnemySide = 0;

        private const string EnemySpawnMarker = "EnemySpawn", TurretMarker = "Turret", HeavyMarker = "HeavyEnemy";
        private const string BossMarker = "FINAL_MK_Boss_EndingB";
        private const float LostCheckInterval = 2f;
        private const float SoldierSpawnLift = 0.05f;
        private const float StairLead = 1f;

        public Transform Tower;
        public GameObject SoldierPrefab;
        public GameObject DronePrefab;
        public GameObject TurretPrefab;
        public GameObject HeavyRobotPrefab;
        public GameObject BossPrefab;
        public bool RandomSeedEachPlay = true;
        public int Seed = 1234;
        [Tooltip("Drones spawn this high above their marker.")]
        public float DroneSpawnHeight = 1.6f;
        [Tooltip("The tower's rooms (L1–L3), world bounds (set by Setup).")]
        public Bounds TowerArea;
        [Tooltip("The outside stairs, platform and helipad, world bounds (set by Setup).")]
        public Bounds FinaleArea;
        public List<Level> Levels = new List<Level>();
        [Tooltip("Spawn markers behind a door that opens only once their level is clear (set by Setup). Never used: " +
                 "a soldier there could not be reached, so the level could not be cleared.")]
        public List<string> BehindExit = new List<string>();

        [Header("Soldier fire")]
        [Tooltip("Pause between a soldier's bursts in seconds (the template's 0.25 s kept a soldier firing 80 % of the time: " +
                 "one soldier took 100 HP in about 1.3 s at close range).")]
        public float SoldierBurstWait = 0.7f;
        [Tooltip("Degrees of aim error a soldier adds to every shot (template 2).")]
        public float SoldierAimError = 3.5f;
        [Tooltip("At most this many soldiers fire at the player at once (lecturer's feedback, 2026-11-16: everyone firing at " +
                 "once was too much). The others aim, reload and wait for a turn; 0 = no limit.")]
        public int AttackSlots = 2;
        [Tooltip("Seconds a soldier keeps its attack turn while others wait for one.")]
        public float AttackTurn = 5f;

        [Header("Arrival")]
        [Tooltip("No soldier spawns closer than this to a level's arrival point with a clear line of sight to it: " +
                 "the player comes up the stairs (or restarts after a death) without being shot at once.")]
        public float SafeArrivalRadius = 14f;
        [Tooltip("Seconds without damage after restarting at a checkpoint (the weapon is already drawn).")]
        public float RespawnProtection = 3f;

        [Header("Last enemies")]
        [Tooltip("With this many enemies or fewer left, they stop guarding and come for the player.")]
        public int HuntWhenRemaining = 2;
        [Tooltip("They also come when nobody has died for this many seconds.")]
        public float HuntAfterQuietSeconds = 45f;

        /// <summary>
        /// True when the level's remaining enemies should come to the player (alarm, the last enemies, a quiet spell).
        /// Only while the player is on that level: hunters must not leave it (they would be sent back).
        /// </summary>
        public bool Hunting => PlayerOnCurrentLevel
                               && (_huntAll || Remaining <= HuntWhenRemaining || Time.time - _lastKillTime > HuntAfterQuietSeconds);

        /// <summary>Index of the level being fought (Levels.Count once the boss is dead).</summary>
        public int Current { get; private set; } = -1;
        public bool Completed => Current >= Levels.Count;
        public string CurrentName => Completed ? "Tower clear" : Current >= 0 ? Levels[Current].Name : "";
        /// <summary>The finale's soldiers are dead; the helipad waits for the player.</summary>
        public bool AwaitingHelipad { get; private set; }
        public bool BossSpawned { get; private set; }
        /// <summary>The finale's boss once spawned (the helicopter's target).</summary>
        public VantageHeavyRobot Boss { get; private set; }
        public bool BossDead { get; private set; }
        public event Action<int> LevelCleared;
        public event Action BossDefeated;

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
                        if (_level[i] == Current && isAlive(i)) _remaining++;
                }
                return _remaining;
            }
        }
        /// <summary>Every enemy spawned so far (dead ones included).</summary>
        public IReadOnlyList<GameObject> Enemies => _alive;

        // Parallel lists, one entry per spawned enemy; components cached at spawn.
        private readonly List<GameObject> _alive = new List<GameObject>();
        private readonly List<int> _level = new List<int>();
        private readonly List<IVantageEnemy> _special = new List<IVantageEnemy>();
        private readonly List<CharacterMotor> _motors = new List<CharacterMotor>();
        private readonly List<VantageEnemyAggression> _aggression = new List<VantageEnemyAggression>();
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>();

        private int _countedFrame = -1, _remaining, _lastRemaining;
        private bool _secondWavePending, _secondWaveHinted;
        private VantageArsenal _arsenal;
        private GameObject _kit, _grenadeCrate;
        private float _lastKillTime, _nextLostCheck, _nextHelipadMessage;
        private bool _huntAll;
        private int _highestCleared = -1;
        private int _playerLevel = -2;
        private VantageFloorGate[] _gates;
        private VantageTag[] _markers;
        private System.Random _random;
        private readonly Dictionary<VantageTag, Vector3> _ground = new Dictionary<VantageTag, Vector3>();
        private const float GroundSearch = 1f, GroundMaxDrop = 0.5f;

        private void Awake() => Instance = this;

        private void OnEnable()
        {
            VantageEvents.EnemyKilled += onKilled;
            VantageEvents.PlayerDied += onPlayerDied;
            VantageEvents.WeaponPickedUp += onWeapon;
        }

        private void OnDisable()
        {
            VantageEvents.EnemyKilled -= onKilled;
            VantageEvents.PlayerDied -= onPlayerDied;
            VantageEvents.WeaponPickedUp -= onWeapon;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            VantageRun.Begin();
            var args = Environment.GetCommandLineArgs();
            var seedArg = Array.IndexOf(args, "-vantageSeed");
            if (seedArg >= 0 && seedArg + 1 < args.Length && int.TryParse(args[seedArg + 1], out var forced))
                Seed = forced;
            else if (RandomSeedEachPlay)
                Seed = (Environment.TickCount & 0x7fffffff) % 100000;
            _random = new System.Random(Seed);
            VantageEvents.RaiseLayoutGenerated(Seed);

            _gates = FindObjectsByType<VantageFloorGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _markers = Tower != null ? Tower.GetComponentsInChildren<VantageTag>(true) : new VantageTag[0];
            _arsenal = FindFirstObjectByType<VantageArsenal>();
            findGround();
            var start = Mathf.Clamp(VantageRun.Checkpoint, 0, Levels.Count - 1);
            // A restart on a level whose second wave needs a gun found there (L2: the rifle) starts without it, so the
            // level's order holds after a death: rifle, first wave, Office, key (the user's request, 2026-11-25).
            var levelGun = Levels[start].SecondWaveWeapon;
            if (start > 0 && !string.IsNullOrEmpty(levelGun) && VantageRun.Weapons.Remove(levelGun))
                Debug.Log($"[Vantage] Restart on {Levels[start].Name}: the {levelGun} has to be taken again.");
            restoreWeapons();

            _highestCleared = start - 1;
            if (start > 0)
                restoreCheckpoint(start);
            begin(start);
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
                if (_secondWavePending) trySecondWave();
                else levelDone();
            }

            var p = VantageEvents.ActivePlayer();
            var lv = p != null ? LevelAt(p.transform.position) : -1;
            if (lv != _playerLevel)
            {
                // Arriving on the level being fought starts its quiet timer, so time spent elsewhere doesn't count.
                if (lv == Current)
                    _lastKillTime = Time.time;
                _playerLevel = lv;
                updateDoors();
            }
        }

        private void levelDone()
        {
            var cleared = Current;
            var last = cleared == Levels.Count - 1;
            if (last)
            {
                if (!BossSpawned) { AwaitingHelipad = true; return; }
                BossDead = true;
                Debug.Log("[Vantage] Boss defeated.");
                Current = Levels.Count;
                VantageBossReveal.Stop();   // a boss killed mid-reveal: control back before the extraction takes the camera
                BossDefeated?.Invoke();
                return;
            }

            _highestCleared = cleared;
            foreach (var gate in _gates)
                if (gate.Level == cleared)
                    gate.Open();
            if (cleared == 0)
                VantageEvents.RaiseAlarmStopped();
            VantageDoor.ApplyPending(cleared);
            VantageDoor.RecheckKills();
            LevelCleared?.Invoke(cleared);
            Debug.Log($"[Vantage] {Levels[cleared].Name} cleared.");
            begin(cleared + 1);
        }

        /// <summary>True once this level's enemies are dead (later levels: false).</summary>
        public bool IsCleared(int level) => level <= _highestCleared || (level == Current && AwaitingHelipad) || Completed;

        #region Where things are

        /// <summary>Level index of a world position (stairs lead: the top of a flight counts as the floor above), or -1 outside.</summary>
        public int LevelAt(Vector3 world) => levelAt(world, StairLead);

        private int levelAt(Vector3 world, float lead)
        {
            if (Levels.Count == 0) return -1;
            var finale = Levels.Count - 1;
            if (FinaleArea.size != Vector3.zero && FinaleArea.Contains(world))
                return finale;
            if (TowerArea.size == Vector3.zero || world.x < TowerArea.min.x || world.x > TowerArea.max.x || world.z < TowerArea.min.z || world.z > TowerArea.max.z)
                return -1;
            if (world.y < Levels[0].FloorY - 1f || world.y > TowerArea.max.y + 1f)
                return -1;
            var level = 0;
            for (int i = 1; i < finale; i++)
                if (world.y >= Levels[i].FloorY - lead) level = i;
            return level;
        }

        /// <summary>Height between the first two floors (how far "another floor" is).</summary>
        public float FloorSpacing => Levels.Count > 1 ? Mathf.Abs(Levels[1].FloorY - Levels[0].FloorY) : 4f;

        /// <summary>"Ground floor", "Level 2" … for the HUD and logger.</summary>
        public string LevelName(int level) => level >= 0 && level < Levels.Count ? Levels[level].Name : "Outside";

        private bool isMarkerOf(VantageTag m, int level) => m.name.StartsWith(Levels[level].Prefix, StringComparison.Ordinal);

        /// <summary>
        /// Enemy spawn markers need NavMesh on their own floor: otherwise the AI snaps a soldier to the nearest NavMesh,
        /// which can be a floor above or below. Such markers are skipped (and logged).
        /// </summary>
        private void findGround()
        {
            _ground.Clear();
            var skipped = new List<string>();
            foreach (var m in _markers)
            {
                if (m.MarkerType != EnemySpawnMarker && m.MarkerType != HeavyMarker) continue;
                var p = m.transform.position;
                if (NavMesh.SamplePosition(p, out var hit, GroundSearch, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) <= GroundMaxDrop)
                    _ground[m] = hit.position;
                else
                    skipped.Add(m.name);
            }
            if (skipped.Count > 0)
                Debug.Log($"[Vantage] {skipped.Count} enemy spawn markers have no NavMesh on their floor and are not used: {string.Join(", ", skipped)}.");
        }

        private Vector3 standAt(VantageTag m) => _ground.TryGetValue(m, out var p) ? p : m.transform.position;

        #endregion

        #region Spawning

        private void begin(int index)
        {
            Current = index;
            _countedFrame = -1;
            _lastRemaining = int.MaxValue;
            _lastKillTime = Time.time;
            _huntAll = false;
            AwaitingHelipad = false;
            VantageRun.Checkpoint = index;
            if (Completed) return;

            var level = Levels[index];
            _secondWavePending = !string.IsNullOrEmpty(level.SecondWave);
            _secondWaveHinted = false;
            if (_secondWavePending && !string.IsNullOrEmpty(level.SecondWaveDoor))
            {
                var door = VantageDoor.Find(level.SecondWaveDoor);
                if (door != null) door.Lock();
                else Debug.LogWarning($"[Vantage] {level.Name}: second-wave door {level.SecondWaveDoor} not found.");
            }
            spawnWave(index, false, level.Soldiers, level.Drones);
            spawnKit(index);
            spawnGrenadeCrate(index);

            updateDoors();
            VantageDoor.RecheckKills();
            Debug.Log($"[Vantage] {level.Name}: {Remaining} enemies{(_secondWavePending ? $", {level.SecondWaveSoldiers} soldiers and {level.SecondWaveDrones} drones held for the second wave" : "")} (seed {Seed}).");
        }

        /// <summary>One wave of a level: soldiers on picked markers, the turrets and heavy robot whose markers belong to it, drones over free spawns.</summary>
        private void spawnWave(int index, bool second, int soldiers, int drones)
        {
            var level = Levels[index];
            Func<VantageTag, bool> inWave = m => inWaveOf(level, m, second);
            var spawns = pick(level, EnemySpawnMarker, soldiers, null, inWave);
            foreach (var m in spawns)
                spawnSoldier(index, m);

            foreach (var m in _markers.Where(m => m.MarkerType == TurretMarker && isMarkerOf(m, index) && inWave(m)))
                spawnSpecial(index, TurretPrefab, m.transform.position + Vector3.up * 0.2f, m.transform.rotation, m.name);
            foreach (var m in _markers.Where(m => m.MarkerType == HeavyMarker && isMarkerOf(m, index) && inWave(m)))
            {
                var at = heavyPoint(level, m, out var face);
                spawnSpecial(index, HeavyRobotPrefab, at, face, m.name);
            }

            // Drones hover over free enemy spawns.
            var droneSpots = pick(level, EnemySpawnMarker, drones, spawns, inWave);
            for (int i = 0; i < droneSpots.Count; i++)
            {
                var go = spawnSpecial(index, DronePrefab, droneSpots[i].transform.position + Vector3.up * DroneSpawnHeight, Quaternion.identity, $"Drone - {level.Name} {(second ? "B" : "")}{i + 1}");
                var drone = go != null ? go.GetComponent<VantageDrone>() : null;
                if (drone != null)
                {
                    drone.Accuracy = level.DroneAccuracy;
                    drone.FireInterval = level.DroneFireInterval;
                }
            }
        }

        /// <summary>The marker belongs to this wave: never an Unused one; the second wave's markers only in the second.</summary>
        private static bool inWaveOf(Level level, VantageTag m, bool second)
        {
            if (Matches(m.name, level.Unused)) return false;
            return string.IsNullOrEmpty(level.SecondWave) ? !second : Matches(m.name, level.SecondWave) == second;
        }

        /// <summary>A comma-separated list of names or prefixes ending in * contains this name.</summary>
        public static bool Matches(string name, string list)
        {
            if (string.IsNullOrEmpty(list)) return false;
            foreach (var raw in list.Split(','))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;
                if (token.EndsWith("*") ? name.StartsWith(token.Substring(0, token.Length - 1), StringComparison.Ordinal) : name == token)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The first wave is dead: the second starts once the player has the level's gun (L2: the rifle first, then
        /// the Office and the key, the professor's suggestion). Until then a hint says what's missing.
        /// </summary>
        private void trySecondWave()
        {
            var level = Levels[Current];
            if (!string.IsNullOrEmpty(level.SecondWaveWeapon) && (_arsenal == null || !_arsenal.IsUnlocked(level.SecondWaveWeapon)))
            {
                if (!_secondWaveHinted)
                {
                    _secondWaveHinted = true;
                    VantageEvents.RaiseMessage(VantageUI.Caps($"Take the {level.SecondWaveWeapon.ToLowerInvariant()}"));
                }
                return;
            }
            _secondWavePending = false;
            _huntAll = false;
            _lastKillTime = Time.time;
            spawnWave(Current, true, level.SecondWaveSoldiers, level.SecondWaveDrones);
            var door = string.IsNullOrEmpty(level.SecondWaveDoor) ? null : VantageDoor.Find(level.SecondWaveDoor);
            if (door != null) door.OpenNow();
            if (!string.IsNullOrEmpty(level.SecondWaveMessage)) VantageEvents.RaiseMessage(level.SecondWaveMessage);
            updateDoors();
            Debug.Log($"[Vantage] {level.Name}: second wave, {Remaining} enemies; {level.SecondWaveDoor} open.");
        }

        /// <summary>
        /// Where the heavy robot stands: its marker, or (HeavyAt) the middle of a tower object on the NavMesh, facing
        /// the marker (where the player comes from). L3 since 2026-11-25: the destroyed area, not the cramped robot room.
        /// </summary>
        private Vector3 heavyPoint(Level level, VantageTag marker, out Quaternion face)
        {
            face = Quaternion.Euler(0, marker.transform.eulerAngles.y, 0);
            var home = standAt(marker);
            if (string.IsNullOrEmpty(level.HeavyAt) || Tower == null) return home;
            var area = Tower.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == level.HeavyAt);
            if (area == null) { Debug.LogWarning($"[Vantage] {level.Name}: HeavyAt {level.HeavyAt} not found; the robot stays on its marker."); return home; }
            var c = area.GetComponent<Collider>();
            var r = area.GetComponent<Renderer>();
            var bounds = c != null ? c.bounds : r != null ? r.bounds : new Bounds(area.position, Vector3.one * 4f);
            // The area's NavMesh is broken up by debris: of the floor points walkable from the marker, the one with the
            // most room around it (distance to the NavMesh edge), nearer the middle on a tie.
            if (!best(bounds, level.FloorY, home, out var spot))
            {
                Debug.LogWarning($"[Vantage] {level.Name}: no reachable NavMesh in {level.HeavyAt}; the robot stays on its marker.");
                return home;
            }
            var look = home - spot; look.y = 0;
            if (look.sqrMagnitude > 0.01f) face = Quaternion.LookRotation(look);
            Debug.Log($"[Vantage] {level.Name}: heavy robot at {spot:F1} in {level.HeavyAt}.");
            return spot;
        }

        private static bool best(Bounds bounds, float floorY, Vector3 from, out Vector3 spot)
        {
            spot = default;
            var bestScore = float.MinValue;
            var path = new NavMeshPath();
            for (float x = bounds.min.x + 0.5f; x <= bounds.max.x - 0.5f; x += 1f)
                for (float z = bounds.min.z + 0.5f; z <= bounds.max.z - 0.5f; z += 1f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, floorY, z), out var hit, 0.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floorY) > GroundMaxDrop) continue;
                    if (!NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas)) continue;
                    var centre = new Vector2(hit.position.x - bounds.center.x, hit.position.z - bounds.center.z).magnitude;
                    var score = edge.distance * 10f - centre * 0.1f;
                    if (score <= bestScore) continue;
                    if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    bestScore = score;
                    spot = hit.position;
                }
            return bestScore > float.MinValue;
        }

        /// <summary>
        /// A health kit at the level's entrance (2026-11-25, the user's request): on the first floor point up the
        /// stairs from the arrival (inside the tower entrance on level 1). Each life gets one.
        /// </summary>
        private void spawnKit(int index)
        {
            if (_kit != null) Destroy(_kit);
            _kit = null;
            if (!kitPoint(index, out var at)) return;
            _kit = new GameObject($"Health Kit - {Levels[index].Name}");
            _kit.transform.position = at;
            _kit.AddComponent<VantageHealthKit>();
            Debug.Log($"[Vantage] {Levels[index].Name}: health kit at {at:F1}.");
        }

        /// <summary>
        /// The level's bucket of grenades (L3: where the robot used to stand, in the robot room that leads through the
        /// breach to the patrols behind the rubble: pick them up, then the first use is right there).
        /// </summary>
        private void spawnGrenadeCrate(int index)
        {
            if (_grenadeCrate != null) Destroy(_grenadeCrate);
            _grenadeCrate = null;
            var level = Levels[index];
            if (string.IsNullOrEmpty(level.GrenadeCrateAt) || Tower == null) return;
            var anchor = Tower.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == level.GrenadeCrateAt);
            if (anchor == null || !NavMesh.SamplePosition(new Vector3(anchor.position.x, level.FloorY, anchor.position.z), out var hit, 2f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[Vantage] {level.Name}: no place for the grenade bucket at {level.GrenadeCrateAt}.");
                return;
            }
            _grenadeCrate = new GameObject($"Grenade Bucket - {level.Name}");
            _grenadeCrate.transform.position = hit.position;
            _grenadeCrate.AddComponent<VantageGrenadeCrate>().Count = level.GrenadeCount;
            Debug.Log($"[Vantage] {level.Name}: grenade bucket ({level.GrenadeCount}) at {hit.position:F1}.");
        }

        private bool kitPoint(int index, out Vector3 at)
        {
            at = default;
            var level = Levels[index];
            if (index == 0)
            {
                var intro = FindFirstObjectByType<VantageIntro>();
                if (intro == null) return false;
                var inward = -intro.EntranceFacing; inward.y = 0;
                var p = intro.EntrancePoint + inward.normalized * 3f;
                if (!NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) return false;
                at = hit.position;
                return true;
            }
            if (level.Arrival == null) return false;
            var forward = level.Arrival.forward; forward.y = 0; forward.Normalize();
            for (float d = 1f; d <= 16f; d += 0.5f)
            {
                var q = level.Arrival.position + forward * d; q.y = level.FloorY;
                if (!NavMesh.SamplePosition(q, out var hit, 0.4f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - level.FloorY) > 0.3f) continue;
                var further = hit.position + forward * 1.2f;
                at = NavMesh.SamplePosition(further, out var hit2, 0.5f, NavMesh.AllAreas) && Mathf.Abs(hit2.position.y - level.FloorY) <= 0.3f ? hit2.position : hit.position;
                return true;
            }
            Debug.LogWarning($"[Vantage] {level.Name}: no floor found up the stairs from the arrival; its health kit sits at the arrival.");
            at = level.Arrival.position;
            return true;
        }

        /// <summary>
        /// Seeded pick of the level's markers, spread over rooms before doubling up. Markers with a patrol route
        /// (the L3 outside patrols, needed by a KILL door) are always taken first.
        /// </summary>
        private List<VantageTag> pick(Level level, string type, int count, ICollection<VantageTag> exclude = null, Func<VantageTag, bool> allow = null)
        {
            var index = Levels.IndexOf(level);
            var pool = _markers.Where(m => m.MarkerType == type && isMarkerOf(m, index) && _ground.ContainsKey(m)
                                           && !BehindExit.Contains(m.name) && (exclude == null || !exclude.Contains(m))
                                           && (allow == null || allow(m)))
                               .OrderBy(_ => _random.Next()).ToList();
            // Markers that cover the arrival only fill up a level that would otherwise come up short.
            var covering = pool.Where(m => !m.Has("patrol") && coversArrival(level, m)).ToList();
            pool.RemoveAll(covering.Contains);
            if (covering.Count > 0 && exclude == null)
                Debug.Log($"[Vantage] {level.Name}: kept out of the arrival's view: {string.Join(", ", covering.Select(m => m.name))}" +
                          $"{(pool.Count < count ? $" (used anyway: only {pool.Count} other spawn points for {count})" : "")}.");
            var result = new List<VantageTag>();
            if (exclude == null)
                foreach (var m in pool)
                    if (result.Count < count && m.Has("patrol"))
                        result.Add(m);
            var rooms = new HashSet<string>(result.Select(m => m.Get("room", "")));
            foreach (var m in pool)
                if (result.Count < count && !result.Contains(m) && rooms.Add(m.Get("room", m.name)))
                    result.Add(m);
            foreach (var m in pool.Concat(covering))
                if (result.Count < count && !result.Contains(m))
                    result.Add(m);
            return result;
        }

        /// <summary>
        /// The marker is near the level's arrival and a soldier standing there would see it (eye to eye, over the
        /// level geometry). Railings and unlocked doors don't block the view: soldiers see and shoot between the bars,
        /// and doors are open doorways in play (their leaves may not be switched off yet when the level spawns).
        /// </summary>
        private bool coversArrival(Level level, VantageTag m)
        {
            if (level.Arrival == null || SoldierPrefab == null) return false;
            var at = standAt(m);
            var arrival = level.Arrival.position;
            if ((at - arrival).sqrMagnitude > SafeArrivalRadius * SafeArrivalRadius) return false;
            var eye = at + Vector3.up * eyeHeight();
            var view = arrival + Vector3.up * eyeHeight() - eye;
            var count = Physics.RaycastNonAlloc(eye, view.normalized, _sightHits, view.magnitude, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _sightHits[i].collider;
                if (hit.name.Contains("Rail")) continue;
                var door = hit.GetComponentInParent<VantageDoor>();
                if (door == null || door.IsLocked) return false;
            }
            return true;
        }
        private readonly RaycastHit[] _sightHits = new RaycastHit[16];

        /// <summary>Eye height above the feet of a soldier (0 without a soldier prefab).</summary>
        public float SoldierEyeHeight => SoldierPrefab != null ? eyeHeight() : 0f;

        /// <summary>Eye height of the soldier prefab, from the top of its capsule (see VantagePhysics.Eye).</summary>
        private float eyeHeight()
        {
            if (_eyeHeight > 0f) return _eyeHeight;
            var capsule = SoldierPrefab.GetComponent<CapsuleCollider>();
            _eyeHeight = capsule != null ? (capsule.center.y + capsule.height / 2f) * SoldierPrefab.transform.lossyScale.y - 0.15f : 1.6f;
            return _eyeHeight;
        }
        private float _eyeHeight;

        private void spawnSoldier(int index, VantageTag at)
        {
            if (SoldierPrefab == null) return;
            var level = Levels[index];
            var go = Instantiate(SoldierPrefab, standAt(at) + Vector3.up * SoldierSpawnLift, facing(at));
            // Named after the marker, so the Blender KILL lists (KILL:MK_Enemy_*) find it.
            go.name = at.name;
            var actor = go.GetComponent<BaseActor>();
            if (actor != null) actor.Side = EnemySide;
            foreach (var gun in go.GetComponentsInChildren<BaseGun>(true))
                gun.Damage = level.SoldierDamage;
            arm(go, level.SoldierWeapon);
            var fire = go.GetComponent<AIFire>();
            if (fire != null)
            {
                var bursts = fire.Bursts;
                bursts.Wait = SoldierBurstWait;
                fire.Bursts = bursts;
            }
            var aim = go.GetComponent<AIAim>();
            if (aim != null) aim.AccuracyError = SoldierAimError;
            var health = go.GetComponent<CharacterHealth>();
            if (health != null)
            {
                health.MaxHealth *= level.SoldierHealth;
                health.Health = health.MaxHealth;
            }
            if (go.GetComponent<VantageKillReporter>() == null)
                go.AddComponent<VantageKillReporter>().EnemyName = "Soldier";
            var aggression = go.GetComponent<VantageEnemyAggression>();
            if (aggression == null) aggression = go.AddComponent<VantageEnemyAggression>();
            var route = at.Get("patrol");
            if (!string.IsNullOrEmpty(route))
            {
                var path = Tower.GetComponentsInChildren<VantagePatrolPath>(true).FirstOrDefault(p => p.name == route);
                if (path != null) aggression.PatrolPoints = path.GetPoints();
            }
            track(go, index, aggression);
        }

        /// <summary>
        /// The marker's direction, unless a wall is close: then the soldier faces away from it, into the room
        /// (facing a wall, its hands and gun showed through it).
        /// </summary>
        private Quaternion facing(VantageTag at)
        {
            var yaw = Quaternion.Euler(0, at.transform.eulerAngles.y, 0);
            if (!NavMesh.FindClosestEdge(standAt(at), out var edge, NavMesh.AllAreas) || edge.distance > 1.5f) return yaw;
            var away = edge.normal; away.y = 0;
            return away.sqrMagnitude > 0.01f && Vector3.Dot(yaw * Vector3.forward, away) < 0.3f ? Quaternion.LookRotation(away) : yaw;
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

        private GameObject spawnSpecial(int index, GameObject prefab, Vector3 position, Quaternion rotation, string name)
        {
            if (prefab == null)
            {
                Debug.Log($"[Vantage] No prefab for {name}; skipped.");
                return null;
            }
            var go = Instantiate(prefab, position, rotation);
            go.name = name;
            track(go, index, null);
            return go;
        }

        private void track(GameObject go, int index, VantageEnemyAggression aggression)
        {
            _byName[go.name] = _alive.Count;
            _alive.Add(go);
            _level.Add(index);
            _special.Add(go.GetComponent<IVantageEnemy>());
            _motors.Add(go.GetComponent<CharacterMotor>());
            _aggression.Add(aggression);
            _countedFrame = -1;
        }

        private bool isAlive(int i)
        {
            var go = _alive[i];
            if (go == null || !go.activeInHierarchy) return false;
            var special = _special[i];
            if (special != null) return !special.IsDead;
            var motor = _motors[i];
            return motor == null || motor.IsAlive;
        }

        /// <summary>
        /// Safety net: an enemy that has left its level (fallen through a floor, pushed out of the tower) is put back
        /// on one of the level's spawn points, so a level can always be cleared.
        /// </summary>
        private void returnLostEnemies()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_level[i] != Current || !isAlive(i) || _special[i] is VantageDrone) continue;
                var enemy = _alive[i].transform;
                if (levelAt(enemy.position, 0.3f) == Current || LevelAt(enemy.position) == Current) continue;
                // Only where the current wave lives (a second-wave marker sits behind its locked door).
                var lv = Levels[Current];
                var marker = pick(lv, EnemySpawnMarker, 1, null, m => inWaveOf(lv, m, false) || (!_secondWavePending && inWaveOf(lv, m, true))).FirstOrDefault();
                if (marker == null) continue;
                var lostAt = enemy.position;
                var position = standAt(marker) + Vector3.up * SoldierSpawnLift;
                var body = enemy.GetComponent<Rigidbody>();
                if (body != null) { body.position = position; body.linearVelocity = Vector3.zero; }
                enemy.position = position;
                Debug.Log($"[Vantage] {enemy.name} had left its level (at {lostAt:F1}); returned to {marker.name}.");
            }
        }

        #endregion

        #region Kill lists, alerts, ending

        /// <summary>
        /// True when no enemy named in the list is alive: names or prefixes ending in *. Markers of levels that haven't
        /// started yet count as alive (their enemies aren't spawned yet).
        /// </summary>
        public bool KillListDone(IEnumerable<string> names)
        {
            foreach (var raw in names)
            {
                var key = raw.Trim();
                var prefix = key.EndsWith("*");
                if (prefix) key = key.TrimEnd('*');
                bool match(string n) => prefix ? n.StartsWith(key, StringComparison.Ordinal) : n == key;

                for (int i = 0; i < _alive.Count; i++)
                    if (_alive[i] != null && match(_alive[i].name) && isAlive(i))
                        return false;
                for (int level = Current + 1; level < Levels.Count; level++)
                    foreach (var m in _markers)
                        if (isMarkerOf(m, level) && match(m.name) && (m.MarkerType == EnemySpawnMarker || m.MarkerType == TurretMarker || m.MarkerType == HeavyMarker))
                            return false;
            }
            return true;
        }

        /// <summary>The named enemies come for the player (Blender "alert:" triggers).</summary>
        public void Alert(IEnumerable<string> names)
        {
            foreach (var n in names)
                if (_byName.TryGetValue(n.Trim(), out var i) && _aggression[i] != null)
                    _aggression[i].Alerted = true;
        }

        /// <summary>Alarm: every enemy of the current level comes for the player until the level is clear.</summary>
        public void StartHunt()
        {
            _huntAll = true;
            for (int i = 0; i < _alive.Count; i++)
                if (_level[i] == Current && _aggression[i] != null)
                    _aggression[i].Alerted = true;
        }

        /// <summary>
        /// The player is on the helipad. Before the finale's soldiers are dead nothing happens (a hint is shown);
        /// then the boss appears. Returns true once the helipad has done its part.
        /// </summary>
        public bool ReachHelipad()
        {
            if (BossSpawned) return true;
            if (Current != Levels.Count - 1 || !AwaitingHelipad)
            {
                if (Time.time >= _nextHelipadMessage)
                {
                    _nextHelipadMessage = Time.time + 4f;
                    VantageEvents.RaiseMessage("Clear the platform");
                }
                return false;
            }
            BossSpawned = true;
            AwaitingHelipad = false;
            var marker = _markers.FirstOrDefault(m => m.name == BossMarker);
            var at = marker != null ? marker.transform.position : transform.position;
            // The Blender marker floats beyond the platform's edge (it was for a gunship): the boss stands on the
            // platform's walking floor, as close to the marker as it gets (not on the railing tops).
            var floorY = Levels[Current].FloorY;
            var middle = new Vector3(FinaleArea.center.x, floorY, FinaleArea.center.z);
            for (float k = 1f; k >= 0f; k -= 0.05f)
            {
                var probe = Vector3.Lerp(middle, new Vector3(at.x, floorY, at.z), k);
                if (NavMesh.SamplePosition(probe, out var ground, 0.6f, NavMesh.AllAreas) && Mathf.Abs(ground.position.y - floorY) < 0.3f
                    && levelAt(ground.position, 0.3f) == Current && !Physics.CheckCapsule(ground.position + Vector3.up * 1.0f, ground.position + Vector3.up * 2.5f, 0.8f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                {
                    at = ground.position;
                    break;
                }
            }
            var pad = VantageEvents.ActivePlayer();
            var look = pad != null ? Quaternion.LookRotation(Vector3.ProjectOnPlane(pad.transform.position - at, Vector3.up)) : Quaternion.identity;
            var boss = spawnSpecial(Current, BossPrefab, at, look, "Boss");
            _countedFrame = -1;
            Boss = boss != null ? boss.GetComponent<VantageHeavyRobot>() : null;
            VantageEvents.RaiseBossSpawned();
            if (Boss != null) VantageBossReveal.Play(Boss);
            Debug.Log($"[Vantage] Boss {(boss != null ? "spawned" : "missing (no prefab): extraction starts")}.");
            return true;
        }

        private void onKilled(string enemy, Vector3 position) => VantageDoor.RecheckKills();

        private void onPlayerDied(string cause, Vector3 position) => VantageRun.Deaths++;

        private void onWeapon(string weapon) => VantageRun.Weapons.Add(weapon);

        #endregion

        #region Checkpoint and doors

        private void restoreWeapons()
        {
            if (VantageRun.Weapons.Count == 0) return;
            var arsenal = FindFirstObjectByType<VantageArsenal>();
            if (arsenal == null) return;
            foreach (var w in VantageRun.Weapons)
                arsenal.Unlock(w);
            // In hand at once, the best gun first (the equip animation took longer than the first shots).
            arsenal.EquipNow(VantageRun.Weapons.Contains("Rifle") ? "Rifle" : VantageRun.Weapons.First());
        }

        /// <summary>
        /// Restart after a death on a later level: earlier levels count as cleared (gates and their doors open, their
        /// consoles and pickups used), and the player starts at the level's arrival point.
        /// </summary>
        private void restoreCheckpoint(int level)
        {
            foreach (var gate in _gates)
                if (gate.Level < level) gate.Open(instant: true);
            foreach (var door in VantageDoor.All)
                if (levelAt(door.transform.position, 0.3f) < level || door.ClearLevel >= 0 && door.ClearLevel < level)
                    door.ForceOpen();
            foreach (var interact in Tower.GetComponentsInChildren<VantageInteract>(true))
                if (levelAt(interact.transform.position, 0.3f) < level) interact.enabled = false;
            foreach (var pickup in Tower.GetComponentsInChildren<VantagePickup>(true))
                if (levelAt(pickup.transform.position, 0.3f) < level)
                {
                    VantageInventory.Add(pickup.item);
                    pickup.gameObject.SetActive(false);
                }

            var arrival = Levels[level].Arrival;
            var player = FindFirstObjectByType<ThirdPersonInput>();
            if (arrival != null && player != null)
            {
                var body = player.GetComponent<Rigidbody>();
                if (body != null) { body.position = arrival.position; body.linearVelocity = Vector3.zero; }
                player.transform.SetPositionAndRotation(arrival.position, arrival.rotation);
            }
            var rules = player != null ? player.GetComponent<VantageThirdPersonPlayer>() : null;
            if (rules != null) rules.Protect(RespawnProtection);
            Debug.Log($"[Vantage] Restarting at the checkpoint of {Levels[level].Name}.");
        }

        /// <summary>
        /// Doors run around the level being fought (its enemies use them) and around the player (who may go back,
        /// e.g. for a missed coin); the rest sleep.
        /// </summary>
        private void updateDoors()
        {
            foreach (var door in VantageDoor.All)
            {
                var l = levelAt(door.transform.position, 0.3f);
                door.enabled = Completed || l < 0 || Mathf.Abs(l - Current) <= 1 || (_playerLevel >= 0 && Mathf.Abs(l - _playerLevel) <= 1);
            }
        }

        /// <summary>The player is on the level being fought (alerted soldiers only chase the player there).</summary>
        public bool PlayerOnCurrentLevel => Current >= 0 && !Completed && _playerLevel == Current;

        #endregion
    }
}
