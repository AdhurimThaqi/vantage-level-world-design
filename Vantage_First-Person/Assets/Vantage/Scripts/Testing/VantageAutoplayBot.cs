#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoverShooter;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage.Testing
{
    /// <summary>
    /// Automated playtest, editor only. Started by Vantage.EditorTools.VantageAutoplay (batch mode or menu): it plays
    /// the real scene and logs measurable answers to "do soldiers shoot back", "do the gates open", "do the stairs
    /// work", then quits. Every line it writes starts with "[Bot]".
    /// </summary>
    public class VantageAutoplayBot : MonoBehaviour
    {
        // Play mode reloads the scripting domain, so the request and the result travel in SessionState.
        public const string RequestKey = "Vantage.Autoplay", ResultKey = "Vantage.Autoplay.Result";

        private CharacterMotor _motor;
        private CharacterHealth _health;
        private ThirdPersonController _controller;
        private float _damageTaken;
        private float _lastHealth;
        private int _failures;
        private bool _snapshotting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void boot()
        {
            if (UnityEditor.SessionState.GetBool(RequestKey, false))
                new GameObject("Autoplay Bot").AddComponent<VantageAutoplayBot>();
        }

        private IEnumerator Start()
        {
            // Survives the scene reload the checkpoint test triggers (a death reloads the scene).
            DontDestroyOnLoad(gameObject);
            log("started");
            yield return new WaitForSeconds(2f);
            if (!acquire()) { yield return finish(); yield break; }
            var levels = VantageTowerLevels.Instance;
            VantageRun.Reset();
            log($"level {levels.Current + 1} '{levels.CurrentName}', {levels.Remaining} enemies, seed {levels.Seed}");

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vantageDiagFall") >= 0)
            {
                foreach (var e in levels.Enemies.ToList()) kill(e);
                for (float t = 0; t < 3f && levels.Current == 0; t += Time.deltaTime) yield return null;
                var below = levels.Tower.GetComponentsInChildren<VantageTag>(true).First(m => m.name == "MK_Enemy_B_1").transform.position;
                teleport(below, below + Vector3.forward);
                var soldiers = levels.Enemies.Where(e => e != null && e.GetComponent<FighterBrain>() != null).ToList();
                var last = soldiers.ToDictionary(e => e, e => (pos: e.transform.position, state: ""));
                var floor = levels.Levels[1].FloorY;
                for (float t = 0; t < 45f; t += 0.5f)
                {
                    yield return new WaitForSeconds(0.5f);
                    foreach (var e in soldiers)
                    {
                        if (e == null) continue;
                        var p = e.transform.position;
                        var brain = e.GetComponent<FighterBrain>();
                        var motor = e.GetComponent<CharacterMotor>();
                        var state = $"{brain.State} cover {motor.IsInCover} climbing {motor.IsClimbingOrVaulting} grounded {motor.IsGrounded}";
                        if (p.y < floor - 0.4f && last[e].pos.y >= floor - 0.4f)
                            log($"DIAG FALL t={t:F1} {e.name}: {last[e].pos:F2} [{last[e].state}] -> {p:F2} [{state}]");
                        last[e] = (p, state);
                    }
                }
                log("DIAG fall watch done: " + string.Join("; ", soldiers.Where(e => e != null).Select(e => $"{e.name} {e.transform.position:F1} {last[e].state}")));
                yield return finish();
                yield break;
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vantageDiagPaths") >= 0)
            {
                yield return new WaitForSeconds(2f);   // carving obstacles settle
                var marks = levels.Tower.GetComponentsInChildren<VantageTag>(true).Where(m => m.MarkerType == "EnemySpawn").ToList();
                foreach (var a in marks.Where(m => m.name.StartsWith("L2_") || m.name.StartsWith("L3_")))
                {
                    var lowerPrefix = a.name.StartsWith("L3_") ? "L2_" : "MK_";
                    foreach (var b in marks.Where(m => m.name.StartsWith(lowerPrefix)).Take(3))
                    {
                        if (!NavMesh.SamplePosition(a.transform.position, out var ha, 1f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b.transform.position, out var hb, 1f, NavMesh.AllAreas)) continue;
                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
                        if (path.status == NavMeshPathStatus.PathComplete)
                            log($"DIAG path {a.name} -> {b.name}: COMPLETE via " + string.Join(" ", path.corners.Select(c => c.ToString("F1"))));
                    }
                }
                log("DIAG paths done");
                yield return finish();
                yield break;
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vantageDiagStairs") >= 0)
            {
                foreach (var e in levels.Enemies.ToList()) kill(e);
                for (float t = 0; t < 3f && levels.Current == 0; t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(2f);
                yield return walkUpLikeAPlayer(levels, 0, "LVL1");
                foreach (var e in levels.Enemies.ToList()) kill(e);
                VantageInventory.Add("Key");
                var slot = GameObject.Find("L2_KEYSLOT_InsertKey");
                if (slot != null) slot.GetComponent<VantageInteract>().TryUse();
                for (float t = 0; t < 3f && levels.Current == 1; t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(2f);
                yield return walkUpLikeAPlayer(levels, 1, "LVL2");
                yield return finish();
                yield break;
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vantageDiag") >= 0)
            {
                yield return diagnose(levels);
                yield return finish();
                yield break;
            }

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vantageBench") >= 0)
            {
                yield return bench(levels);
                yield return finish();
                yield break;
            }

            yield return cameraSpotsPlayer();
            yield return hudSnapshot("hud_alarm");
            teleport(_start, _start + Vector3.forward);
            yield return new WaitForSeconds(0.5f);
            yield return walkEntrance(levels);
            yield return alarmOnce(levels);
            yield return alertedStayOnLevel(levels);
            yield return westDoorLockedDuringLockdown();
            yield return doorsOpenOnRoute();
            yield return onlyLevel2UsesE();
            yield return coversHaveRoom();
            yield return soldiersFightBack(levels);
            yield return hudSnapshot("hud_combat");
            yield return killDoorWaitsForItsLevel(levels);
            yield return clearLevel(levels, 0);
            yield return alarmStopsOnClear();
            yield return westDoorOpensAfterClear();
            yield return soldiersStayOnTheirFloor(levels);
            yield return stairsFromTheFloor(levels, 0, "LVL1");
            yield return hudSnapshot("hud_level_cleared");
            yield return keyOpensGate(levels);
            yield return lastEnemiesCome(levels);
            yield return clearLevel(levels, 1);
            yield return gateOpensAfterClear();
            yield return stairsFromTheFloor(levels, 1, "LVL2");
            yield return killDoorsOpen(levels);
            yield return robotRoomOpensLast(levels);
            yield return robotModelAndFriendlyFire(levels);
            yield return robotRifleOnly(levels);
            yield return clearLevel(levels, 2);
            yield return doorsWorkOnEarlierLevels(levels);
            yield return walkStairs(levels);
            yield return finale(levels);
            yield return playAgain();
            yield return checkpointRestore();
            yield return shotCameraStaysQuiet();
            yield return finish();
        }

        /// <summary>Finds the player and makes it a test dummy: invulnerable, no auto-cover, driven by the bot.</summary>
        private bool acquire()
        {
            var player = VantageEvents.ActivePlayer();
            _motor = player != null ? player.GetComponent<CharacterMotor>() : null;
            if (_motor == null || VantageTowerLevels.Instance == null)
            {
                fail("no player or no VantageTowerLevels in the scene");
                return false;
            }
            _health = _motor.GetComponent<CharacterHealth>();
            _controller = _motor.GetComponent<ThirdPersonController>();
            // The player snaps into cover when standing still next to it (template AutoTakeCover); the bot stands
            // still a lot and is teleported, so it would end up crouched out of sight or held in place by the cover.
            if (_controller != null) _controller.AutoTakeCover = false;
            var input = _motor.GetComponent<ThirdPersonInput>();
            if (input != null) input.enabled = false; // the bot drives MovementInput itself
            _controller.WaitForUpdateCall = false;   // the input component normally ticks the controller
            // Invulnerable for the test: damage is measured, but the bot can't die and trigger a level restart.
            var rules = _motor.GetComponent<VantageThirdPersonPlayer>();
            if (rules != null) rules.enabled = false;
            _health.MaxHealth = 1e6f;
            _health.Health = 1e6f;
            _lastHealth = _health.Health;
            _start = _motor.transform.position;
            return true;
        }

        private Vector3 _start;

        // ---------------- finale ----------------

        private bool _won;
        private void onWon() => _won = true;

        /// <summary>
        /// The helipad with its soldiers dead: stepping onto the pad brings the boss (before that, nothing happens);
        /// the boss's death starts the extraction, which ends with the win.
        /// </summary>
        private IEnumerator finale(VantageTowerLevels levels)
        {
            var pad = GameObject.Find("FINAL_TRIGGER_EndScene_EndingC");
            var box = pad != null ? pad.GetComponent<Collider>() : null;
            if (box == null) { fail("no helipad trigger"); yield break; }
            var waiting = levels.AwaitingHelipad && levels.Current == levels.Levels.Count - 1;
            var onPad = box.bounds.center - Vector3.up * (box.bounds.extents.y - 0.1f);
            teleport(onPad, onPad + Vector3.forward);
            for (float t = 0; t < 2f && !levels.BossSpawned; t += Time.deltaTime) yield return null;
            var boss = levels.Enemies.Where(e => e != null).Select(e => e.GetComponent<VantageHeavyRobot>()).FirstOrDefault(r => r != null && r.IsBoss);
            yield return new WaitForSeconds(1f);
            yield return hudSnapshot("hud_boss");
            _won = false;
            VantageEvents.GameWon += onWon;
            if (boss != null) boss.OnHit(new Hit(boss.transform.position + Vector3.up, Vector3.up, 1e5f, _motor.gameObject, boss.gameObject, HitType.Rifle, 0));
            float wait = 0;
            for (; wait < 15f && !_won; wait += Time.deltaTime) yield return null;
            VantageEvents.GameWon -= onWon;
            var heli = FindFirstObjectByType<VantageExtraction>();
            var line = $"waiting for the pad {waiting}, boss spawned {levels.BossSpawned} ({(boss != null ? "found" : "missing")}), boss dead {levels.BossDead}, helicopter landed {(heli != null && heli.Landed)}, won {_won} after {wait:F1} s";
            if (waiting && boss != null && levels.BossDead && heli != null && heli.Landed && _won) log("PASS finale: " + line);
            else fail("finale: " + line);
            yield return hudSnapshot("hud_end_screen");
        }

        // ---------------- diagnosis (-vantageDiag) ----------------

        /// <summary>
        /// Evidence for playtest complaints, no pass/fail: covers inside walls, patrol points off the NavMesh, then for
        /// each level the enemies after 40 s with the player at the level's arrival (where, inside a wall?, state),
        /// and a walk along the real route to the next level (stalls and what blocks them).
        /// </summary>
        private IEnumerator diagnose(VantageTowerLevels levels)
        {
            var capsule = _motor.GetComponent<CapsuleCollider>();
            var radius = capsule != null ? capsule.radius : 0.3f;
            int covers = 0, coversBlocked = 0;
            var blockedNames = new Dictionary<string, int>();
            foreach (var cover in FindObjectsByType<Cover>(FindObjectsSortMode.None))
            {
                covers++;
                // A cover's forward points into its obstacle; the character stands in front of it.
                var stand = cover.transform.position - cover.transform.forward * 0.45f;
                var floor = Physics.Raycast(stand + Vector3.up * 0.5f, Vector3.down, out var fh, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore) ? fh.point : stand;
                var inside = Physics.CheckCapsule(floor + Vector3.up * (radius + 0.1f), floor + Vector3.up * 1.6f, radius * 0.8f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
                var offMesh = !NavMesh.SamplePosition(floor, out var nh, 0.4f, NavMesh.AllAreas);
                if (inside || offMesh)
                {
                    coversBlocked++;
                    blockedNames.TryGetValue(cover.name, out var n);
                    blockedNames[cover.name] = n + 1;
                }
            }
            log($"DIAG covers: {coversBlocked}/{covers} have no room to stand in front (inside a wall or off the NavMesh): " + string.Join(", ", blockedNames.OrderByDescending(k => k.Value).Take(15).Select(k => $"{k.Key} x{k.Value}")));

            foreach (var path in levels.Tower.GetComponentsInChildren<VantagePatrolPath>(true))
            {
                var pts = path.GetPoints();
                log($"DIAG patrol {path.name}: " + string.Join(" | ", pts.Select(p => NavMesh.SamplePosition(p, out var h, 3f, NavMesh.AllAreas) ? $"{p:F1} nav {Vector3.Distance(p, h.position):F2} m" : $"{p:F1} NO NAVMESH")));
            }

            for (int level = 0; level < 3; level++)
            {
                if (levels.Current != level) { log($"DIAG stopped: current level {levels.Current + 1}, expected {level + 1}"); yield break; }
                var arrival = levels.Levels[level].Arrival;
                teleport(arrival.position, arrival.position + arrival.forward);
                var spawned = levels.Enemies.Where(e => e != null && levels.LevelAt(e.transform.position) == level).ToDictionary(e => e, e => e.transform.position);
                yield return new WaitForSeconds(40f);
                foreach (var kv in spawned)
                {
                    var e = kv.Key;
                    if (e == null) continue;
                    var p = e.transform.position;
                    var brain = e.GetComponent<FighterBrain>();
                    var motor = e.GetComponent<CharacterMotor>();
                    var c = e.GetComponent<CapsuleCollider>();
                    var overl = Physics.OverlapCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.4f, 0.25f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)
                                       .Where(o => !o.transform.IsChildOf(e.transform)).Select(o => o.name).Distinct().ToList();
                    var nav = NavMesh.SamplePosition(p, out var nh, 1f, NavMesh.AllAreas) ? Vector3.Distance(new Vector3(p.x, nh.position.y, p.z), nh.position) : -1f;
                    log($"DIAG L{level + 1} {e.name}: moved {Vector3.Distance(kv.Value, p):F1} m, level {levels.LevelAt(p) + 1}, state {(brain != null ? brain.State.ToString() : "-")}, cover {(motor != null && motor.IsInCover)}, alive {(motor != null ? motor.IsAlive : true)}, nav off {nav:F2} m, inside [{string.Join(",", overl)}]");
                }
                log($"DIAG L{level + 1}: {levels.Remaining} enemies alive after 40 s");
                foreach (var e in levels.Enemies.ToList()) kill(e);
                if (level == 1)
                {
                    VantageInventory.Add("Key");
                    GameObject.Find("L2_KEYSLOT_InsertKey")?.GetComponent<VantageInteract>()?.TryUse();
                }
                for (float t = 0; t < 3f && levels.Current == level; t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(1.5f);
                // The real route from this level's arrival to the next one's.
                var next = levels.Levels[level + 1].Arrival;
                yield return walkRoute($"L{level + 1}->L{level + 2}", arrival.position, next.position);
            }
        }

        /// <summary>
        /// Walks from the level's arrival point to the foot of its first flight (NavMesh path on the floor), then up
        /// each flight of that staircase in order and 2 m onto the next floor, like a player: logs every stall,
        /// what blocks it and a screenshot.
        /// </summary>
        private IEnumerator walkUpLikeAPlayer(VantageTowerLevels levels, int level, string stairs)
        {
            var flights = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c => c.name.StartsWith("Stair Surface " + stairs)).OrderBy(c => c.bounds.min.y).ToList();
            var points = new List<Vector3>();
            var approaches = new List<Vector3>();
            foreach (var surface in flights)
            {
                var v = surface.sharedMesh.vertices.Select(x => surface.transform.TransformPoint(x)).ToList();
                float lo = v.Min(p => p.y), hi = v.Max(p => p.y);
                var lows = v.Where(p => p.y < lo + 0.05f).ToList();
                var highs = v.Where(p => p.y > hi - 0.05f).ToList();
                var low = lows.Aggregate(Vector3.zero, (a, p) => a + p) / lows.Count;
                var high = highs.Aggregate(Vector3.zero, (a, p) => a + p) / highs.Count;
                var dir = high - low; dir.y = 0; dir.Normalize();
                // The surface reaches 1.4 m past each end of the steps: onto the flight, up it, onto the landing or floor.
                points.Add(low + dir * 1.0f);
                points.Add(high + dir * 0.6f);
                if (approaches.Count == 0)
                {
                    // Ways onto the first flight: from the front, or from either side (a foot against a wall).
                    var side = Vector3.Cross(Vector3.up, dir);
                    var half = surface.bounds.extents.x * Mathf.Abs(side.x) + surface.bounds.extents.z * Mathf.Abs(side.z);
                    approaches.Add(low - dir * 0.2f);
                    approaches.Add(low + dir * 1.0f + side * (half + 0.9f));
                    approaches.Add(low + dir * 1.0f - side * (half + 0.9f));
                }
            }
            var lastDir = points.Count >= 2 ? (points[points.Count - 1] - points[points.Count - 2]) : Vector3.forward; lastDir.y = 0;
            points.Add(points[points.Count - 1] + lastDir.normalized * 2.0f);

            var start = levels.Levels[level].Arrival.position;
            // The reachable approach with the shortest floor path.
            NavMeshPath path = null;
            float best = float.MaxValue;
            foreach (var a in approaches)
            {
                if (!NavMesh.SamplePosition(a, out var ah, 0.6f, NavMesh.AllAreas) || Mathf.Abs(ah.position.y - levels.Levels[level].FloorY) > 0.5f) continue;
                var p = new NavMeshPath();
                if (!NavMesh.CalculatePath(start, ah.position, NavMesh.AllAreas, p) || p.status != NavMeshPathStatus.PathComplete) continue;
                float length = 0;
                for (int c = 1; c < p.corners.Length; c++) length += Vector3.Distance(p.corners[c - 1], p.corners[c]);
                if (length < best) { best = length; path = p; }
            }
            path ??= new NavMeshPath();
            var route = path.corners.Length > 1 ? path.corners.Skip(1).ToList() : new List<Vector3>();
            route.AddRange(points);
            log($"DIAG walk {stairs}: floor path {path.status} ({path.corners.Length} corners), then {flights.Count} flights; route " + string.Join(" ", route.Select(p => p.ToString("F1"))));
            teleport(start, route[0]);
            yield return new WaitForSeconds(0.5f);
            int stalls = 0;
            for (int i = 0; i < route.Count; i++)
            {
                var target = route[i];
                float t = 0, window = 0, stuckFor = 0;
                var windowStart = _motor.transform.position;
                while (t < 10f)
                {
                    var d = target - _motor.transform.position; d.y = 0;
                    if (d.magnitude < 0.4f) break;
                    _controller.MovementInput = new CharacterMovement(d.normalized, 1f);
                    yield return null;
                    t += Time.deltaTime;
                    window += Time.deltaTime;
                    if (window < 0.25f) continue;
                    var p = _motor.transform.position;
                    if (new Vector2(p.x - windowStart.x, p.z - windowStart.z).magnitude < 0.075f && t > 0.5f) stuckFor += window; else stuckFor = 0;
                    window = 0; windowStart = p;
                    if (stuckFor < 1f) continue;
                    stalls++;
                    _controller.MovementInput = new CharacterMovement();
                    log($"DIAG walk {stairs}: STUCK #{stalls} at {p:F2} (level {levels.LevelAt(p) + 1}) going to point {i} {target:F2}, grounded {_motor.IsGrounded}:{obstacles(d.normalized)}");
                    yield return hudSnapshot($"diag_stuck_{stairs}_{stalls}");
                    teleport(target, i + 1 < route.Count ? route[i + 1] : target + d.normalized);
                    yield return new WaitForSeconds(0.3f);
                    break;
                }
            }
            _controller.MovementInput = new CharacterMovement();
            var end = _motor.transform.position;
            _walkStalls = stalls;
            log($"DIAG walk {stairs}: {stalls} stalls, ended at {end:F2} on level {levels.LevelAt(end) + 1}");
        }

        /// <summary>Follows the NavMesh path between two points with the character's own movement; logs every stall and what is in front.</summary>
        private IEnumerator walkRoute(string name, Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);
            log($"DIAG route {name}: path {path.status}, {path.corners.Length} corners");
            if (path.corners.Length < 2) yield break;
            teleport(path.corners[0], path.corners[1]);
            yield return new WaitForSeconds(0.5f);
            int stalls = 0;
            for (int i = 1; i < path.corners.Length; i++)
            {
                var corner = path.corners[i];
                float t = 0, window = 0, stuckFor = 0;
                var windowStart = _motor.transform.position;
                while (t < 12f)
                {
                    var d = corner - _motor.transform.position; d.y = 0;
                    if (d.magnitude < 0.35f) break;
                    _controller.MovementInput = new CharacterMovement(d.normalized, 1f);
                    yield return null;
                    t += Time.deltaTime;
                    window += Time.deltaTime;
                    if (window >= 0.25f)
                    {
                        var p = _motor.transform.position;
                        if (new Vector2(p.x - windowStart.x, p.z - windowStart.z).magnitude < 0.075f && t > 0.5f) stuckFor += window; else stuckFor = 0;
                        window = 0; windowStart = p;
                        if (stuckFor >= 1f)
                        {
                            stalls++;
                            log($"DIAG route {name}: STUCK at {p:F2} (y above corner {p.y - corner.y:F2}) heading to corner {i} {corner:F2}:{obstacles(d.normalized)}");
                            // Hop past it so the rest of the route is checked too.
                            teleport(corner, i + 1 < path.corners.Length ? path.corners[i + 1] : corner + d.normalized);
                            yield return new WaitForSeconds(0.3f);
                            break;
                        }
                    }
                }
            }
            _controller.MovementInput = new CharacterMovement();
            log($"DIAG route {name}: {stalls} stalls, ended {Vector3.Distance(_motor.transform.position, to):F1} m from the target");
        }

        // ---------------- review fixes ----------------

        private int _leftLevel, _walkStalls;
        private void onLog(string text, string stack, LogType type) { if (text.Contains("had left its level")) _leftLevel++; }

        /// <summary>
        /// Alerted soldiers (the alarm) don't follow the player out of their level: with the player 20 m outside
        /// the entrance for 6 s, no soldier leaves level 1 (and none is teleported back by the safety net).
        /// </summary>
        private IEnumerator alertedStayOnLevel(VantageTowerLevels levels)
        {
            var l = VantageDoor.Find("DOOR_Entrance_L");
            var r = VantageDoor.Find("DOOR_Entrance_R");
            var door = (l.transform.position + r.transform.position) / 2;
            var outward = door - levels.TowerArea.center; outward.y = 0; outward.Normalize();
            var outside = door + outward * 20f;
            if (NavMesh.SamplePosition(outside, out var hit, 3f, NavMesh.AllAreas)) outside = hit.position;
            _leftLevel = 0;
            Application.logMessageReceived += onLog;
            teleport(outside, door);
            var outsideLevel = 0;
            for (float t = 0; t < 6f; t += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                outsideLevel = Mathf.Max(outsideLevel, levels.Enemies.Count(e => e != null && e.GetComponent<FighterBrain>() != null
                    && e.GetComponent<CharacterMotor>().IsAlive && levels.LevelAt(e.transform.position) != 0));
            }
            Application.logMessageReceived -= onLog;
            teleport(door - outward * 2f, door - outward * 6f);
            var line = $"most soldiers off level 1 at once {outsideLevel}, safety-net returns {_leftLevel}";
            if (outsideLevel == 0 && _leftLevel == 0) log("PASS alerted soldiers stay on their level while the player is outside: " + line);
            else fail("alerted soldiers left their level: " + line);
        }

        /// <summary>
        /// A model with its own colliders (the user's Blender robot) must still take hits: a rifle hit on a child
        /// collider hurts the robot. Rifle rounds from soldiers (enemy side) don't.
        /// </summary>
        private IEnumerator robotModelAndFriendlyFire(VantageTowerLevels levels)
        {
            var robot = levels.Enemies.Where(e => e != null).Select(e => e.GetComponent<VantageHeavyRobot>()).FirstOrDefault(x => x != null && !x.IsBoss);
            var soldier = levels.Enemies.FirstOrDefault(e => e != null && e.GetComponent<FighterBrain>() != null);
            if (robot == null || soldier == null) { fail($"robot model test: robot {robot != null}, soldier {soldier != null}"); yield break; }
            var part = new GameObject("Model Part (bot)");
            part.transform.SetParent(robot.Model != null ? robot.Model : robot.transform, false);
            part.transform.localPosition = Vector3.up * 1.5f;
            part.AddComponent<BoxCollider>();
            robot.ForwardChildHits();   // what the robot does at start for its model's colliders
            yield return null;
            var before = robot.Health;
            part.SendMessage("OnHit", new Hit(part.transform.position, Vector3.up, 40f, _motor.gameObject, part, HitType.Rifle, 0), SendMessageOptions.DontRequireReceiver);
            var afterPart = robot.Health;
            robot.OnHit(new Hit(robot.transform.position, Vector3.up, 40f, soldier, robot.gameObject, HitType.Rifle, 0));
            var afterSoldier = robot.Health;
            Destroy(part);
            var line = $"health {before:F0} -> {afterPart:F0} after a rifle hit on a model collider -> {afterSoldier:F0} after a soldier's rifle hit; eye {(robot.Eye != null)}, muzzle {(robot.Muzzle != null)} outside the model {(robot.Muzzle != null && robot.Model != null && !robot.Muzzle.IsChildOf(robot.Model))}";
            if (afterPart < before && afterSoldier == afterPart && robot.Muzzle != null && robot.Model != null && !robot.Muzzle.IsChildOf(robot.Model) && !robot.Eye.transform.IsChildOf(robot.Model))
                log("PASS robot model: hits on model colliders count, soldiers can't hurt it, eye and muzzle survive a model swap: " + line);
            else fail("robot model / friendly fire: " + line);
        }

        /// <summary>
        /// During the lockdown the door on the left of the entrance corridor (into the turret hallway) stays shut,
        /// even with the player (or the soldiers the alarm sends) right at it: Room A on the right is the only way on.
        /// </summary>
        private IEnumerator westDoorLockedDuringLockdown()
        {
            var door = VantageDoor.Find("DOOR_Corridor_West_OneWay");
            if (door == null) { fail("no DOOR_Corridor_West_OneWay"); yield break; }
            teleport(near(door.transform.position), door.transform.position);
            float most = 0;
            for (float t = 0; t < 3f; t += 0.25f) { yield return new WaitForSeconds(0.25f); most = Mathf.Max(most, door.OpenAmount); }
            var line = $"alarm {VantageAlarm.Instance.Active}, locked {door.IsLocked}, opened at most {most:P0}";
            if (VantageAlarm.Instance.Active && door.IsLocked && most < 0.05f) log("PASS the left corridor door stays shut during the lockdown: " + line);
            else fail("left corridor door during lockdown: " + line);
        }

        /// <summary>Only the level 2 key slot is used with E; every other console is switched off.</summary>
        private IEnumerator onlyLevel2UsesE()
        {
            yield return null;
            var active = FindObjectsByType<VantageInteract>(FindObjectsSortMode.None).Where(i => i.enabled).Select(i => i.name).ToList();
            var line = "consoles in use: " + string.Join(", ", active);
            if (active.Count == 1 && active[0] == "L2_KEYSLOT_InsertKey") log("PASS only the level 2 key slot uses E: " + line);
            else fail("E consoles: " + line);
        }

        /// <summary>Every cover of the tower has room for a character in front of it (none faces into a wall).</summary>
        private IEnumerator coversHaveRoom()
        {
            yield return null;
            var capsule = _motor.GetComponent<CapsuleCollider>();
            var radius = capsule != null ? capsule.radius : 0.3f;
            var setup = GameObject.Find("VANTAGE Tower Setup");
            var covers = setup != null ? setup.GetComponentsInChildren<Cover>() : new Cover[0];
            var blocked = covers.Select(c =>
            {
                var stand = c.transform.position - c.transform.forward * 0.45f;
                var floor = Physics.Raycast(stand + Vector3.up * 0.5f, Vector3.down, out var fh, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore) ? fh.point : stand;
                // Characters' hit boxes aren't on the Character layer: a soldier standing there is not a wall.
                var hits = Physics.OverlapCapsule(floor + Vector3.up * (radius + 0.15f), floor + Vector3.up * 1.6f, radius * 0.8f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)
                                  .Where(h => h.GetComponentInParent<CharacterMotor>() == null).ToArray();
                if (hits.Length > 0) return $"{c.name} (blocked by {hits[0].name}, floor hit {(fh.collider != null ? fh.collider.name : "none")})";
                if (!NavMesh.SamplePosition(floor, out var nh, 0.4f, NavMesh.AllAreas))
                    return $"{c.name} (no NavMesh at {floor}, player {Vector3.Distance(floor, _motor.transform.position):F1} m away, floor hit {(fh.collider != null ? fh.collider.name : "none")})";
                return null;
            }).Where(s => s != null).ToList();
            var line = $"{blocked.Count}/{covers.Length} tower covers without room in front" + (blocked.Count > 0 ? ": " + string.Join(", ", blocked.Take(10)) : "");
            if (covers.Length > 0 && blocked.Count == 0) log("PASS no cover faces into a wall: " + line);
            else fail("covers: " + line);
        }

        /// <summary>The robot room stays locked until every other level 3 enemy is dead.</summary>
        private IEnumerator robotRoomOpensLast(VantageTowerLevels levels)
        {
            var door = VantageDoor.Find("L3_DOOR_RobotRoom_LOCKED");
            if (door == null) { fail("no L3_DOOR_RobotRoom_LOCKED"); yield break; }
            var lockedBefore = door.IsLocked;
            foreach (var e in levels.Enemies.Where(e => e != null && e.GetComponent<FighterBrain>() != null && levels.LevelAt(e.transform.position) == 2).ToList()) kill(e);
            yield return new WaitForSeconds(0.5f);
            var line = $"locked while soldiers lived {lockedBefore}, after they died locked {door.IsLocked}, open {door.OpenAmount:P0}";
            if (lockedBefore && !door.IsLocked && door.OpenAmount == 1f) log("PASS the robot room opens when the rest of level 3 is dead: " + line);
            else fail("robot room: " + line);
        }

        /// <summary>
        /// With the player on level 1 right below level 2's soldiers for 20 s, none of them ends up on another floor
        /// (the template AI used to snap a soldier onto a search target on the floor below: a teleport through the floor).
        /// </summary>
        private IEnumerator soldiersStayOnTheirFloor(VantageTowerLevels levels)
        {
            var below = levels.Tower.GetComponentsInChildren<VantageTag>(true).First(m => m.name == "MK_Enemy_B_1").transform.position;
            if (NavMesh.SamplePosition(below, out var hit, 2f, NavMesh.AllAreas)) below = hit.position;
            teleport(below, below + Vector3.forward);
            _leftLevel = 0;
            Application.logMessageReceived += onLog;
            var soldiers = levels.Enemies.Where(e => e != null && e.GetComponent<FighterBrain>() != null && levels.LevelAt(e.transform.position) == 1).ToList();
            var dropped = 0;
            var floor = levels.Levels[1].FloorY;
            for (float t = 0; t < 20f; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                dropped = Mathf.Max(dropped, soldiers.Count(e => e != null && e.transform.position.y < floor - 0.4f));
            }
            Application.logMessageReceived -= onLog;
            var line = $"{soldiers.Count} level 2 soldiers, most below their floor at once {dropped}, safety-net returns {_leftLevel}";
            if (soldiers.Count > 0 && dropped == 0 && _leftLevel == 0) log("PASS soldiers stay on their floor with the player below: " + line);
            else fail("soldiers changed floor: " + line);
        }

        /// <summary>The real way up from the level's floor (through the shutter and the gate on level 1) has no catches.</summary>
        private IEnumerator stairsFromTheFloor(VantageTowerLevels levels, int level, string stairs)
        {
            yield return walkUpLikeAPlayer(levels, level, stairs);
            var end = _motor.transform.position;
            var line = $"{_walkStalls} stalls, ended on level {levels.LevelAt(end) + 1}";
            if (_walkStalls == 0 && levels.LevelAt(end) == level + 1) log($"PASS walking up the {stairs} stairs from the floor: " + line);
            else fail($"{stairs} stairs from the floor: " + line);
        }

        /// <summary>Once level 1 is clear the left corridor door is unlocked.</summary>
        private IEnumerator westDoorOpensAfterClear()
        {
            var door = VantageDoor.Find("DOOR_Corridor_West_OneWay");
            yield return null;
            if (door != null && !door.IsLocked && door.OpenAmount == 1f) log("PASS the left corridor door opens when level 1 is clear");
            else fail($"left corridor door after level 1: {(door == null ? "missing" : "still locked")}");
        }

        /// <summary>
        /// Doors on the route stand open from the start (Room A's door, no leaf in the way); a locked door (the West
        /// Wing, opened by the console) is shut.
        /// </summary>
        private IEnumerator doorsOpenOnRoute()
        {
            var open = VantageDoor.Find("DOOR_Corridor_East_RoomA");
            var westWing = VantageDoor.Find("DOOR_SecurityOffice_WestWing_LOCKED");
            var shut = VantageDoor.Find("L3_DOOR_SERoom_LOCKED");
            if (open == null || shut == null || westWing == null) { fail($"doors: Room A {open != null}, West Wing {westWing != null}, SE room {shut != null}"); yield break; }
            yield return null;
            var blocking = open.GetComponentsInChildren<Collider>().Any(c => c.enabled);
            var line = $"Room A door open {open.OpenAmount:P0} (collider {(blocking ? "on" : "off")}), West Wing open {westWing.OpenAmount:P0}, SE room door open {shut.OpenAmount:P0} (locked {shut.IsLocked})";
            if (open.OpenAmount == 1f && !blocking && westWing.OpenAmount == 1f && shut.OpenAmount == 0f && shut.IsLocked) log("PASS route doors are open, locked doors shut: " + line);
            else fail("doors on the route: " + line);
        }

        /// <summary>Once level 3 is reached, a ground floor door still opens for the player going back (missed coins).</summary>
        private IEnumerator doorsWorkOnEarlierLevels(VantageTowerLevels levels)
        {
            var door = VantageDoor.Find("DOOR_RoomA_RoomB");
            if (door == null) { fail("no DOOR_RoomA_RoomB"); yield break; }
            teleport(near(door.transform.position), door.transform.position);
            yield return new WaitForSeconds(1.5f);
            var line = $"level {levels.Current + 1}, door enabled {door.enabled}, open {door.OpenAmount:P0}";
            if (door.OpenAmount > 0.5f) log("PASS ground floor door opens after level 3 started: " + line);
            else fail("ground floor door stuck: " + line);
        }

        /// <summary>Enter on the end screen (the same call): a new run starts on level 1 with nothing collected.</summary>
        private IEnumerator playAgain()
        {
            var extraction = FindFirstObjectByType<VantageExtraction>();
            if (extraction == null) { fail("no extraction"); yield break; }
            extraction.PlayAgain();
            yield return new WaitForSeconds(2.5f);
            if (!acquire()) yield break;
            var levels = VantageTowerLevels.Instance;
            var line = $"level {levels.Current + 1}, weapons {VantageRun.Weapons.Count}, coins {VantageRun.Coins.Count}, deaths {VantageRun.Deaths}";
            if (levels.Current == 0 && VantageRun.Weapons.Count == 0 && VantageRun.Coins.Count == 0 && VantageRun.Deaths == 0) log("PASS play again starts a new run: " + line);
            else fail("play again: " + line);
        }

        // ---------------- robot ----------------

        /// <summary>
        /// Level 3's heavy robot: ten pistol hits leave its health alone and say "You need a better gun" once (not per
        /// bullet); rifle hits hurt it; killed, it counts as dead for the level.
        /// </summary>
        private IEnumerator robotRifleOnly(VantageTowerLevels levels)
        {
            var robot = levels.Enemies.Where(e => e != null).Select(e => e.GetComponent<VantageHeavyRobot>()).FirstOrDefault(r => r != null && !r.IsBoss);
            if (robot == null) { fail("level 3 has no heavy robot"); yield break; }
            var before = robot.Health;
            _messages.Clear();
            for (int i = 0; i < 10; i++)
            {
                robot.OnHit(new Hit(robot.transform.position + Vector3.up, Vector3.up, 30f, _motor.gameObject, robot.gameObject, HitType.Pistol, 0));
                yield return new WaitForSeconds(0.1f);
            }
            var afterPistol = robot.Health;
            var warnings = _messages.Count(m => m == "You need a better gun");
            robot.OnHit(new Hit(robot.transform.position + Vector3.up, Vector3.up, 50f, _motor.gameObject, robot.gameObject, HitType.Rifle, 0));
            var afterRifle = robot.Health;
            var remaining = levels.Remaining;
            robot.OnHit(new Hit(robot.transform.position + Vector3.up, Vector3.up, 1e5f, _motor.gameObject, robot.gameObject, HitType.Rifle, 0));
            yield return null;
            var line = $"health {before:F0} -> {afterPistol:F0} after 10 pistol hits ({warnings} warning), -> {afterRifle:F0} after a rifle hit, dead {robot.IsDead}, remaining {remaining} -> {levels.Remaining}";
            // The robot may be the floor's last enemy (the robot room opens last), then its death starts the next level.
            if (afterPistol == before && warnings == 1 && afterRifle < before && robot.IsDead && (levels.Remaining == remaining - 1 || levels.Current == 3)) log("PASS robot is rifle-only: " + line);
            else fail("robot: " + line);
        }

        // ---------------- key ----------------

        private string _lastMessage;
        private readonly List<string> _messages = new List<string>();
        private void onMessage(string text) { _lastMessage = text; _messages.Add(text); }
        private void OnEnable() => VantageEvents.Message += onMessage;
        private void OnDisable() => VantageEvents.Message -= onMessage;

        /// <summary>
        /// Level 2: the key slot refuses without the key; walking into the key picks it up (it flies to the player and
        /// is gone); with the key the slot is used, but the gate waits until level 2 is cleared.
        /// </summary>
        private IEnumerator keyOpensGate(VantageTowerLevels levels)
        {
            var slot = GameObject.Find("L2_KEYSLOT_InsertKey")?.GetComponent<VantageInteract>();
            var key = GameObject.Find("L2_PICKUP_Key");
            var gate = VantageDoor.Find("L2_GATE_Stairwell_LOCKED");
            if (slot == null || key == null || gate == null) { fail($"key test: slot {slot != null}, key {key != null}, gate {gate != null}"); yield break; }

            teleport(near(slot.transform.position), slot.transform.position);
            yield return new WaitForSeconds(0.3f);
            var prompt = VantageEvents.Prompt;
            yield return hudSnapshot("hud_key_prompt");
            _lastMessage = null;
            var refused = !slot.TryUse() && _lastMessage == "You need the Key" && gate.IsLocked;

            teleport(near(key.transform.position), key.transform.position);
            for (float t = 0; t < 2f && key.activeSelf; t += Time.deltaTime) yield return null;
            var taken = !key.activeSelf && VantageInventory.Has("Key");

            teleport(near(slot.transform.position), slot.transform.position);
            yield return new WaitForSeconds(0.3f);
            var used = slot.TryUse();
            yield return new WaitForSeconds(0.6f);
            var waits = gate.IsLocked;
            var line = $"prompt '{prompt}', refused without key {refused}, key taken {taken}, slot used {used}, gate waits for the level {waits}";
            if (prompt == "Insert the Key" && refused && taken && used && waits) log("PASS key: " + line);
            else fail("key: " + line);
        }

        /// <summary>After level 2 is cleared the stairwell gate (key already in) unlocks.</summary>
        private IEnumerator gateOpensAfterClear()
        {
            yield return new WaitForSeconds(0.3f);
            var gate = VantageDoor.Find("L2_GATE_Stairwell_LOCKED");
            if (gate != null && !gate.IsLocked) log("PASS stairwell gate opens once level 2 is clear (key in)");
            else fail($"stairwell gate still locked after level 2: {(gate != null ? "locked" : "missing")}");
        }

        /// <summary>A walkable spot within 1.2 m of a point, on its floor.</summary>
        private static Vector3 near(Vector3 point)
        {
            for (float r = 0.6f; r <= 1.6f; r += 0.2f)
                for (int a = 0; a < 8; a++)
                {
                    var p = point + Quaternion.Euler(0, a * 45f, 0) * Vector3.forward * r;
                    if (NavMesh.SamplePosition(p, out var hit, 0.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - point.y) < 1.8f)
                        return hit.position;
                }
            return point;
        }

        // ---------------- alarm and camera ----------------

        /// <summary>Stands the player in Room A in front of the security camera: within a sweep it raises the alarm.</summary>
        private IEnumerator cameraSpotsPlayer()
        {
            var cam = FindFirstObjectByType<VantageSecurityCamera>();
            var alarm = VantageAlarm.Instance;
            if (cam == null || alarm == null) { fail($"camera {cam != null}, alarm {alarm != null}"); yield break; }
            var spot = inViewOf(cam);
            if (spot == null) { fail("no walkable spot in front of the camera"); yield break; }
            teleport(spot.Value, cam.transform.position);
            float t = 0;
            while (t < 12f && alarm.Raised == 0) { yield return null; t += Time.deltaTime; }
            var shutter = VantageDoor.Find(alarm.Shutter);
            yield return new WaitForSeconds(1f);
            var line = $"alarm raised {alarm.Raised}x after {t:F1} s, active {alarm.Active}, shutter closed {(shutter != null && shutter.ShutterClosed)}";
            if (alarm.Raised == 1 && alarm.Active && shutter != null && shutter.ShutterClosed) log("PASS camera spots the player: " + line);
            else fail("camera did not raise the alarm: " + line);
        }

        /// <summary>A walkable spot 5 m in front of the camera, on its floor.</summary>
        private static Vector3? inViewOf(VantageSecurityCamera cam)
        {
            var forward = cam.transform.forward; forward.y = 0; forward.Normalize();
            for (float d = 5f; d >= 2.5f; d -= 0.5f)
                if (NavMesh.SamplePosition(cam.transform.position + forward * d - Vector3.up * 2.5f, out var hit, 1.5f, NavMesh.AllAreas))
                    return hit.position;
            return null;
        }

        /// <summary>Walking through the alarm trigger after the camera already raised it: still one alarm, hunting on.</summary>
        private IEnumerator alarmOnce(VantageTowerLevels levels)
        {
            yield return new WaitForSeconds(0.5f);
            var alarm = VantageAlarm.Instance;
            var line = $"raised {alarm.Raised}x, active {alarm.Active}, hunting {levels.Hunting}";
            if (alarm.Raised == 1 && alarm.Active && levels.Hunting) log("PASS alarm fires once (camera, then trigger): " + line);
            else fail("alarm once: " + line);
        }

        /// <summary>Clearing level 1 stops the alarm and the shutter rolls up again.</summary>
        private IEnumerator alarmStopsOnClear()
        {
            yield return new WaitForSeconds(2f);
            var alarm = VantageAlarm.Instance;
            var shutter = VantageDoor.Find(alarm.Shutter);
            var line = $"alarm active {alarm.Active}, shutter closed {(shutter != null && shutter.ShutterClosed)}";
            if (!alarm.Active && shutter != null && !shutter.ShutterClosed) log("PASS level 1 clear stops the alarm and reopens the shutter: " + line);
            else fail("after level 1: " + line);
        }

        /// <summary>
        /// New run (reload at level 1): the camera is shot first, so standing in its view raises nothing; walking into
        /// the alarm trigger still sets off the trap (shutter closed).
        /// </summary>
        private IEnumerator shotCameraStaysQuiet()
        {
            VantageRun.Reset();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return new WaitForSeconds(2f);
            if (!acquire()) yield break;
            var cam = FindFirstObjectByType<VantageSecurityCamera>();
            var alarm = VantageAlarm.Instance;
            cam.SendMessage("OnHit", new Hit(cam.transform.position, Vector3.up, 30f, _motor.gameObject, cam.gameObject, HitType.Pistol, 0));
            var spot = inViewOf(cam);
            if (spot != null) teleport(spot.Value, cam.transform.position);
            yield return new WaitForSeconds(3f);
            var quiet = alarm.Raised == 0 && cam.Disabled;
            var trigger = GameObject.Find("TRIGGER_Alarm_ShutterClose");
            var box = trigger != null ? trigger.GetComponent<Collider>() : null;
            if (box != null) teleport(box.bounds.center - Vector3.up * (box.bounds.extents.y - 0.05f), box.bounds.center + Vector3.forward);
            yield return new WaitForSeconds(1.5f);
            var shutter = VantageDoor.Find(alarm.Shutter);
            var line = $"camera disabled {cam.Disabled}, quiet in view {quiet}, trigger raised {alarm.Raised}x, shutter closed {(shutter != null && shutter.ShutterClosed)}";
            if (quiet && alarm.Raised == 1 && shutter != null && shutter.ShutterClosed) log("PASS shot camera stays quiet, the trigger still springs the trap: " + line);
            else fail("shot camera / trigger: " + line);
        }

        private void Update()
        {
            if (_health == null || _snapshotting) return;
            if (_health.Health < _lastHealth) _damageTaken += _lastHealth - _health.Health;
            if (_health.Health < 5e5f) _health.Health = 1e6f;
            _lastHealth = _health.Health;
        }

        // ---------------- 1. soldiers ----------------

        private IEnumerator soldiersFightBack(VantageTowerLevels levels)
        {
            var soldiers = levels.Enemies.Select(e => e != null ? e.GetComponent<FighterBrain>() : null).Where(b => b != null).ToList();
            if (soldiers.Count == 0) { fail("level 1 has no soldiers"); yield break; }

            // Stand up to 12 m from a soldier with a clear line of sight, armed with the pistol: in front of it if
            // possible, else anywhere around it (a soldier can spawn facing a wall); the next soldier if neither works.
            FighterBrain soldier = null;
            Vector3? spot = null;
            foreach (var candidate in soldiers)
            {
                var shooter = candidate.GetComponent<BaseActor>();
                spot = visibleSpotNear(candidate.transform.position, 2.5f, 12f, candidate.transform.forward, 60f, shooter)
                       ?? visibleSpotNear(candidate.transform.position, 2.5f, 12f, candidate.transform.forward, 180f, shooter);
                if (spot != null) { soldier = candidate; break; }
            }
            if (spot == null) { fail($"no visible standing spot near any of {soldiers.Count} soldiers"); yield break; }
            teleport(spot.Value, soldier.transform.position);
            if (_motor.GetComponent<VantageArsenal>() is VantageArsenal arsenal && arsenal.Unlock("Pistol"))
                VantageEvents.RaiseWeaponPickedUp("Pistol");
            log($"player placed {Vector3.Distance(spot.Value, soldier.transform.position):F1} m from {soldier.name} (room floor y {soldier.transform.position.y:F1})");

            var gun = soldier.GetComponentInChildren<BaseGun>(true);
            var firstBullets = gun != null ? gun.LoadedBulletsLeft : -1;
            int fired = 0, lastBullets = firstBullets;
            var damageBefore = _damageTaken;
            for (int s = 1; s <= 12; s++)
            {
                for (float t = 0; t < 1f; t += Time.deltaTime)
                {
                    if (gun != null && gun.LoadedBulletsLeft < lastBullets) fired += lastBullets - gun.LoadedBulletsLeft;
                    if (gun != null) lastBullets = gun.LoadedBulletsLeft;
                    yield return null;
                }
                var motor = soldier.GetComponent<CharacterMotor>();
                var eye = VantagePhysics.Eye(soldier.GetComponent<BaseActor>());
                var view = Physics.Linecast(eye, VantagePhysics.AimPoint(VantageEvents.ActivePlayer()), out var block, VantagePhysics.Sight, QueryTriggerInteraction.Ignore) ? block.collider.name : "clear";
                log($"  t={s,2}s sight={view} soldier state={soldier.State} threat={(soldier.Threat != null ? soldier.Threat.name : "none")} sees={soldier.CanSeeTheThreat} " +
                    $"equipped={motor.IsEquipped} weapon={(motor.ActiveWeapon.Gun != null ? motor.ActiveWeapon.Gun.name : "none")} bullets={(gun != null ? gun.LoadedBulletsLeft : -1)} " +
                    $"dist={Vector3.Distance(soldier.transform.position, _motor.transform.position):F1} damageToPlayer={_damageTaken - damageBefore:F0}");
            }
            var damage = _damageTaken - damageBefore;
            if (fired == 0 && damage <= 0) fail($"soldier did not shoot in 12 s (state {soldier.State})");
            else log($"PASS soldier fired {fired} rounds, player took {damage:F0} damage");
        }

        /// <summary>A walkable spot on the target's floor, within 'spread' degrees of 'facing', with line of sight.</summary>
        private Vector3? visibleSpotNear(Vector3 target, float min, float max, Vector3 facing, float spread, BaseActor shooter)
        {
            facing.y = 0; facing.Normalize();
            int noMesh = 0, otherFloor = 0, blocked = 0;
            string blocker = "";
            for (int i = 0; i < 300; i++)
            {
                var d = Random.Range(min, max);
                var p = target + Quaternion.Euler(0, Random.Range(-spread, spread), 0) * facing * d;
                if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) { noMesh++; continue; }
                if (Mathf.Abs(hit.position.y - target.y) > 0.8f) { otherFloor++; continue; }
                // The soldier's own sight line: its eye to where the player's aim point would be (what
                // VantageEnemyAggression checks); a hit on the soldier itself doesn't count.
                var from = VantagePhysics.Eye(shooter);
                var to = hit.position + (VantagePhysics.AimPoint(_motor.GetComponent<BaseActor>()) - _motor.transform.position);
                if (Physics.Linecast(from, to, out var h, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)
                    && !h.collider.transform.IsChildOf(shooter.transform))
                { blocked++; blocker = h.collider.name; continue; }
                return hit.position;
            }
            log($"  no spot: {noMesh} off NavMesh, {otherFloor} other floor, {blocked} blocked (last by {blocker}); soldier at {target:F1}, NavMesh under soldier: {NavMesh.SamplePosition(target, out var s, 1f, NavMesh.AllAreas)}");
            return null;
        }

        // ---------------- levels and doors ----------------

        /// <summary>Kills the level's enemies: its gates open (L1), the next level spawns, the alarm stops.</summary>
        private IEnumerator clearLevel(VantageTowerLevels levels, int level)
        {
            if (levels.Current == level + 1) { log($"PASS level {level + 1} already cleared by the checks before (level {levels.Current + 1} '{levels.CurrentName}' running)"); yield break; }
            if (levels.Current != level) { fail($"clear level {level + 1}: the current level is {levels.Current + 1}"); yield break; }
            foreach (var e in levels.Enemies.ToList()) kill(e);
            for (float t = 0; t < 3f && levels.Current == level; t += Time.deltaTime)
                yield return null;
            var gates = FindObjectsByType<VantageFloorGate>(FindObjectsSortMode.None).Where(g => g.Level == level).ToList();
            var open = gates.Count(g => g.IsOpen);
            if (levels.Current != level + 1 || open != gates.Count)
                fail($"after clearing level {level + 1}: current level {levels.Current + 1}, gates open {open}/{gates.Count}");
            else
                log($"PASS level {level + 1} cleared -> gates open {open}/{gates.Count}, level {levels.Current + 1} '{levels.CurrentName}' spawned {levels.Remaining} enemies");
        }

        /// <summary>
        /// A KILL door of a later level (L3 SE room: both outside patrols) must stay locked while its enemies have not
        /// even spawned, however many enemies die on the levels before.
        /// </summary>
        private IEnumerator killDoorWaitsForItsLevel(VantageTowerLevels levels)
        {
            var door = VantageDoor.Find("L3_DOOR_SERoom_LOCKED");
            if (door == null) { fail("no L3_DOOR_SERoom_LOCKED"); yield break; }
            var victim = levels.Enemies.FirstOrDefault(e => e != null && e.GetComponent<FighterBrain>() != null);
            kill(victim);
            yield return new WaitForSeconds(0.5f);
            if (door.IsLocked) log("PASS L3 SE door stays locked while its patrols are not spawned (after an L1 kill)");
            else fail("L3 SE door unlocked by a kill on L1");
        }

        /// <summary>On L3: killing both outside patrols unlocks the SE room door.</summary>
        private IEnumerator killDoorsOpen(VantageTowerLevels levels)
        {
            var door = VantageDoor.Find("L3_DOOR_SERoom_LOCKED");
            var patrols = levels.Enemies.Where(e => e != null && e.name.StartsWith("L3_MK_Enemy_Patrol")).ToList();
            if (door == null || patrols.Count != 2) { fail($"L3 patrols: door found {door != null}, {patrols.Count}/2 patrols spawned"); yield break; }
            var lockedBefore = door.IsLocked;
            kill(patrols[0]);
            yield return new WaitForSeconds(0.3f);
            var lockedAfterOne = door.IsLocked;
            kill(patrols[1]);
            yield return new WaitForSeconds(0.3f);
            if (lockedBefore && lockedAfterOne && !door.IsLocked) log("PASS L3 SE door: locked, still locked after one patrol, open after both");
            else fail($"L3 SE door: locked before {lockedBefore}, after one {lockedAfterOne}, after both {door.IsLocked}");
        }

        private static void kill(GameObject enemy)
        {
            if (enemy == null) return;
            var hit = new Hit(enemy.transform.position + Vector3.up, Vector3.up, 10000f, null, enemy, HitType.Rifle, 0);
            foreach (var c in enemy.GetComponentsInChildren<Collider>())
                c.SendMessage("OnHit", hit, SendMessageOptions.DontRequireReceiver);
            enemy.SendMessage("OnHit", hit, SendMessageOptions.DontRequireReceiver);
            var health = enemy.GetComponent<CharacterHealth>();
            if (health != null) health.Health = 0;
        }

        // ---------------- stairs and entrance ----------------

        /// <summary>
        /// Walks up every stair surface (all gates and doors are open by now): from its low end to its high end,
        /// measuring the climb and where the character stalls.
        /// </summary>
        private IEnumerator walkStairs(VantageTowerLevels levels)
        {
            foreach (var e in levels.Enemies.ToList()) kill(e);
            yield return new WaitForSeconds(0.5f);
            foreach (var surface in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c => c.name.StartsWith("Stair Surface")).OrderBy(c => c.bounds.min.y))
            {
                var v = surface.sharedMesh.vertices.Select(x => surface.transform.TransformPoint(x)).ToList();
                float lo = v.Min(p => p.y), hi = v.Max(p => p.y);
                var lows = v.Where(p => p.y < lo + 0.05f).ToList();
                var highs = v.Where(p => p.y > hi - 0.05f).ToList();
                var low = lows.Aggregate(Vector3.zero, (a, p) => a + p) / lows.Count;
                var high = highs.Aggregate(Vector3.zero, (a, p) => a + p) / highs.Count;
                var dir = high - low; dir.y = 0; dir.Normalize();
                // Start on the surface's flat foot (it reaches 1.4 m past the first step), at the first spot along the
                // flight where the character fits: some flights are entered from the side, with a wall at their foot.
                var capsule = _motor.GetComponent<CapsuleCollider>();
                var radius = capsule != null ? capsule.radius : 0.3f;
                var start = low + dir * 0.3f;
                for (float k = 0.3f; k < 3f; k += 0.1f)
                {
                    var c = low + dir * k;
                    if (!Physics.Raycast(c + Vector3.up * 1.5f, Vector3.down, out var surfaceHit, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)) continue;
                    c = surfaceHit.point;
                    if (Physics.CheckCapsule(c + Vector3.up * (radius + 0.08f), c + Vector3.up * 1.7f, radius, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)) continue;
                    start = c;
                    break;
                }
                teleport(start, start + dir * 5f);
                yield return new WaitForSeconds(0.6f);
                var rise = hi - lo;
                var startY = lo;
                float stalled = 0, t = 0, window = 0;
                var windowStart = _motor.transform.position;
                var path = new StringBuilder();
                var reported = false;
                while (t < 14f && _motor.transform.position.y < startY + rise - 0.25f)
                {
                    _controller.MovementInput = new CharacterMovement(dir, 1f);
                    yield return null;
                    t += Time.deltaTime;
                    var p = _motor.transform.position;
                    // Stall = less than 7.5 cm of horizontal progress over a 0.25 s window (single frames are
                    // unreliable: in batch mode some frames have no physics step).
                    window += Time.deltaTime;
                    if (window >= 0.25f)
                    {
                        if (new Vector2(p.x - windowStart.x, p.z - windowStart.z).magnitude < 0.075f && t > 0.6f) stalled += window;
                        window = 0; windowStart = p;
                    }
                    if (stalled > 0.5f && !reported)
                    {
                        reported = true;
                        log($"  {surface.name}: stalled at {p:F2}, grounded={_motor.IsGrounded}, in front:{obstacles(dir)}");
                    }
                    if (Mathf.Repeat(t, 1f) < Time.deltaTime) path.Append($" {p.y - startY:F2}");
                }
                _controller.MovementInput = new CharacterMovement();
                var climbed = _motor.transform.position.y - startY;
                var line = $"{surface.name}: climbed {climbed:F2}/{rise:F2} m in {t:F1} s, stalled {stalled:F1} s, height per second:{path}";
                if (climbed >= rise - 0.35f && stalled < 1f) log("PASS stairs " + line);
                else fail("stairs " + line);
            }
        }

        /// <summary>Walks from the player start through the main entrance to 4 m inside the doors.</summary>
        private IEnumerator walkEntrance(VantageTowerLevels levels)
        {
            var l = VantageDoor.Find("DOOR_Entrance_L");
            var r = VantageDoor.Find("DOOR_Entrance_R");
            if (l == null || r == null) { fail("no entrance doors"); yield break; }
            var door = (l.transform.position + r.transform.position) / 2;
            var start = _motor.transform.position;
            var inward = door - start; inward.y = 0; inward.Normalize();
            var distance = Vector3.Dot(door - start, inward) + 4f;
            float t = 0, stalled = 0, window = 0;
            var windowStart = start;
            while (t < 12f && Vector3.Dot(_motor.transform.position - start, inward) < distance)
            {
                _controller.MovementInput = new CharacterMovement(inward, 1f);
                yield return null;
                t += Time.deltaTime;
                window += Time.deltaTime;
                if (window >= 0.25f)
                {
                    var p = _motor.transform.position;
                    if (new Vector2(p.x - windowStart.x, p.z - windowStart.z).magnitude < 0.075f && t > 0.6f) stalled += window;
                    window = 0;
                    windowStart = p;
                }
            }
            _controller.MovementInput = new CharacterMovement();
            var progress = Vector3.Dot(_motor.transform.position - start, inward);
            var line = $"main entrance: walked {progress:F1}/{distance:F1} m in {t:F1} s, stalled {stalled:F1} s, now on level {levels.LevelAt(_motor.transform.position) + 1}";
            if (progress >= distance - 0.5f && stalled < 1f) log("PASS " + line);
            else fail($"{line}, in front:{obstacles(inward)}");
        }

        // ---------------- last enemies ----------------

        /// <summary>
        /// Leaves two enemies alive on the level and stands the player far from them, out of sight: within 30 s they
        /// must have come to the player (hunt rule) instead of waiting somewhere to be found.
        /// </summary>
        private IEnumerator lastEnemiesCome(VantageTowerLevels levels)
        {
            var enemies = levels.Enemies.Where(e => e != null && e.activeInHierarchy && levels.LevelAt(e.transform.position) == levels.Current).ToList();
            if (enemies.Count < 3) { log("  last enemies: skipped (too few enemies)"); yield break; }
            // The soldier that has the longest walk to a spawn point it can reach (not one shut in behind a door
            // that is still locked, such as the stairwell gate before the key), and that spot for the player.
            var prefix = levels.Levels[levels.Current].Prefix;
            var markers = levels.Tower.GetComponentsInChildren<VantageTag>(true)
                                .Where(m => m.MarkerType == "EnemySpawn" && m.name.StartsWith(prefix))
                                .Select(m => NavMesh.SamplePosition(m.transform.position, out var mh, 2f, NavMesh.AllAreas) ? (Vector3?)mh.position : null)
                                .Where(p => p != null).Select(p => p.Value).ToList();
            bool reachable(Vector3 from, Vector3 to)
            {
                if (!NavMesh.SamplePosition(from, out var b, 1f, NavMesh.AllAreas)) return false;
                var path = new NavMeshPath();
                return NavMesh.CalculatePath(b.position, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            }
            GameObject soldier = null;
            var spot = Vector3.zero;
            float best = -1f;
            foreach (var s in enemies.Where(e => e.GetComponent<FighterBrain>() != null))
                foreach (var m in markers)
                {
                    var d = Vector3.Distance(s.transform.position, m);
                    if (d > best && reachable(s.transform.position, m)) { best = d; soldier = s; spot = m; }
                }
            var drone = enemies.FirstOrDefault(e => e.GetComponent<VantageDrone>() != null && e.GetComponent<VantageDrone>().Mobile);
            var keep = new List<GameObject>();
            if (soldier != null) keep.Add(soldier);
            if (drone != null) keep.Add(drone);
            if (keep.Count == 0) { log("  last enemies: skipped (no soldier or drone)"); yield break; }
            if (soldier == null) spot = markers.OrderByDescending(m => Vector3.Distance(m, drone.transform.position)).First();
            foreach (var e in enemies.Where(e => !keep.Contains(e))) kill(e);
            teleport(spot, spot + Vector3.forward);
            var damageBefore = _damageTaken;
            var startDistance = keep.Min(e => Vector3.Distance(e.transform.position, spot));
            log($"  last enemies: {keep.Count} left ({string.Join(", ", keep.Select(e => e.name))}), nearest {startDistance:F1} m, hunting={levels.Hunting}");
            // The soldier must walk up itself (a drone's shot from afar doesn't count): it once stood circling where it
            // last saw the player, because the hunt order never reached its AIMovement.
            var judged = soldier != null ? new List<GameObject> { soldier } : keep;
            float judgedDistance() => judged.Where(e => e != null).Select(e => Vector3.Distance(e.transform.position, _motor.transform.position)).DefaultIfEmpty(0f).Min();
            var brain = soldier != null ? soldier.GetComponent<FighterBrain>() : null;
            bool soldierSees() => brain != null && brain.Threat != null && brain.Threat.transform == _motor.transform && brain.CanSeeTheThreat;
            float t = 0, nearest = judgedDistance(), first = nearest;
            bool sees = false;
            while (t < 30f && nearest > 8f && !sees)
            {
                yield return new WaitForSeconds(0.5f);
                t += 0.5f;
                nearest = judgedDistance();
                sees = soldierSees();
                if (Mathf.Approximately(t % 10f, 0f) && brain != null)
                    log($"  last enemies t={t:F0}: {soldier.name} {nearest:F1} m away, state {brain.State}, hunting={levels.Hunting}");
            }
            var line = $"{(soldier != null ? soldier.name : "nearest enemy")} {first:F1} m -> {nearest:F1} m in {t:F1} s{(sees ? ", has the player in sight" : "")}, damage taken {_damageTaken - damageBefore:F0}";
            if (nearest <= 8f || sees) log("PASS last enemies come to the player: " + line);
            else fail("last enemies did not come: " + line);
        }

        // ---------------- checkpoint ----------------

        /// <summary>
        /// Dying on level 2 restarts there: after the reload the player is at the level 2 arrival point, level 1's gate
        /// and shutter are open, level 2's enemies are back, and the pistol is still in the inventory.
        /// </summary>
        private IEnumerator checkpointRestore()
        {
            // A coin collected before dying stays collected after the reload.
            var coin = FindObjectsByType<VantageCoin>(FindObjectsSortMode.None).FirstOrDefault(c => c.Id == "coin_L1_NorthWest");
            if (coin != null) coin.Collect();
            else fail("no coin_L1_NorthWest in the scene");
            if (_motor.GetComponent<VantageArsenal>() is VantageArsenal owned && owned.Unlock("Pistol"))
                VantageEvents.RaiseWeaponPickedUp("Pistol");
            VantageRun.Checkpoint = 1;
            VantageEvents.RaisePlayerDied("bot", _motor.transform.position);
            var deaths = VantageRun.Deaths;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return new WaitForSeconds(2f);
            if (!acquire()) yield break;
            var levels = VantageTowerLevels.Instance;
            var arrival = levels.Levels[1].Arrival;
            var near = arrival != null && Vector3.Distance(_motor.transform.position, arrival.position) < 2f;
            var gateOpen = FindObjectsByType<VantageFloorGate>(FindObjectsSortMode.None).Where(g => g.Level == 0).All(g => g.IsOpen);
            var shutter = VantageDoor.Find("SHUTTER_Maintenance");
            var pistol = VantageRun.Weapons.Contains("Pistol");
            var line = $"current level {levels.Current + 1}, player at arrival {near}, L1 gate open {gateOpen}, shutter closed {(shutter != null && shutter.ShutterClosed)}, " +
                       $"{levels.Remaining} enemies, pistol kept {pistol}, deaths {deaths}";
            if (levels.Current == 1 && near && gateOpen && shutter != null && !shutter.ShutterClosed && levels.Remaining > 0 && pistol && deaths >= 1)
                log("PASS checkpoint restore: " + line);
            else
                fail("checkpoint restore: " + line);

            var coins = FindObjectsByType<VantageCoin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var again = coins.FirstOrDefault(c => c.Id == "coin_L1_NorthWest");
            var coinLine = $"{coins.Length} coins in the level, coin_L1_NorthWest {(again == null ? "missing" : again.gameObject.activeSelf ? "back" : "gone")}, run coins {VantageRun.Coins.Count}";
            if (coins.Length == 5 && again != null && !again.gameObject.activeSelf && VantageRun.Coins.Contains("coin_L1_NorthWest"))
                log("PASS coin kept after a death: " + coinLine);
            else
                fail("coin after death: " + coinLine);
        }

        // ---------------- performance ----------------

        /// <summary>
        /// -vantageBench: renders the game camera at 1920x1080 from five typical views at each PC quality level and
        /// times it (a 1-pixel read-back waits for the GPU, so this is CPU + GPU per frame on this machine).
        /// </summary>
        private IEnumerator bench(VantageTowerLevels levels)
        {
            var main = Camera.main;
            var t = levels.Tower;
            foreach (var b in main.GetComponents<MonoBehaviour>()) b.enabled = false; // camera scripts would move it
            var startPos = main.transform.position;
            var startRot = main.transform.rotation;
            var views = new (string name, Vector3 pos, Quaternion rot)[]
            {
                ("start", startPos, startRot),
                ("forest", startPos, Quaternion.LookRotation(-(startRot * Vector3.forward))),
                ("lobby", t.TransformPoint(new Vector3(0, 2.2f, 9f)), Quaternion.LookRotation(t.TransformDirection(Vector3.back))),
                ("floor3", t.TransformPoint(new Vector3(-6f, 14.6f, 6f)), Quaternion.LookRotation(t.TransformDirection(new Vector3(1, -0.1f, -1)))),
                ("roof", t.TransformPoint(new Vector3(0, 27f, 9f)), Quaternion.LookRotation(t.TransformDirection(new Vector3(0, -0.3f, -1)))),
            };
            var rt = new RenderTexture(1920, 1080, 24);
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            main.targetTexture = rt;
            var timer = new System.Diagnostics.Stopwatch();
            var original = QualitySettings.GetQualityLevel();
            for (int q = 2; q < QualitySettings.names.Length; q++)
            {
                QualitySettings.SetQualityLevel(q, true);
                yield return null;
                var line = new StringBuilder($"BENCH {QualitySettings.names[q],-9}");
                foreach (var v in views)
                {
                    main.transform.SetPositionAndRotation(v.pos, v.rot);
                    var times = new List<double>();
                    for (int i = 0; i < 35; i++)
                    {
                        timer.Restart();
                        main.Render();
                        RenderTexture.active = rt;
                        pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                        RenderTexture.active = null;
                        timer.Stop();
                        if (i >= 5) times.Add(timer.Elapsed.TotalMilliseconds);
                    }
                    times.Sort();
                    line.Append($" | {v.name} {times[times.Count / 2]:F1} ms");
                    yield return null;
                }
                log(line.ToString());
            }
            QualitySettings.SetQualityLevel(original, true);
            main.targetTexture = null;
            rt.Release();
        }

        // ---------------- helpers ----------------

        /// <summary>What the character's capsule would run into in 'direction': colliders at four heights.</summary>
        private string obstacles(Vector3 direction)
        {
            var what = new StringBuilder();
            var p = _motor.transform.position;
            foreach (var h in new[] { 0.1f, 0.35f, 1.0f, 1.7f })
                if (Physics.Raycast(p + Vector3.up * h, direction, out var hit, 1.2f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    what.Append($" @{h:F2}:{hit.collider.name}({hit.distance:F2}m, n.y={hit.normal.y:F2})");
            var capsule = _motor.GetComponent<CapsuleCollider>();
            var radius = capsule != null ? capsule.radius : 0.3f;
            foreach (var hit in Physics.CapsuleCastAll(p + Vector3.up * (radius + 0.05f), p + Vector3.up * 1.7f, radius, direction, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(_motor.transform))
                    what.Append($" capsule:{hit.collider.name}[layer {hit.collider.gameObject.layer}]({hit.distance:F2}m at {hit.point:F2})");
            return what.Length > 0 ? what.ToString() : " nothing";
        }

        private void teleport(Vector3 position, Vector3 lookAt)
        {
            if (_motor.IsInCover) _motor.InputLeaveCover();
            var body = _motor.GetComponent<Rigidbody>();
            if (body != null) { body.linearVelocity = Vector3.zero; body.position = position + Vector3.up * 0.05f; }
            var look = lookAt - position; look.y = 0;
            _motor.transform.SetPositionAndRotation(position + Vector3.up * 0.05f, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : _motor.transform.rotation);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// With -vantageShots folder: renders the game view including the screen-space UI into folder/name.png
        /// (batch mode has no game view, so the overlay canvases are drawn through a temporary camera).
        /// </summary>
        private IEnumerator hudSnapshot(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(args, "-vantageShots");
            var main = Camera.main;
            if (i < 0 || i + 1 >= args.Length || main == null) yield break;
            // Show a normal health value (the bot is invulnerable at 1e6) for a frame so the HUD updates.
            _snapshotting = true;
            _health.MaxHealth = 100f;
            _health.Health = 72f;
            yield return null;
            yield return null;
            const int width = 1920, height = 1080;
            var rt = new RenderTexture(width, height, 24);
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(cv => cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var previous = main.targetTexture;
            try
            {
                main.targetTexture = rt;
                foreach (var cv in canvases) { cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = main; cv.planeDistance = main.nearClipPlane + 0.05f; }
                Canvas.ForceUpdateCanvases();
                main.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                System.IO.Directory.CreateDirectory(args[i + 1]);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(args[i + 1], name + ".png"), texture.EncodeToPNG());
                Destroy(texture);
                log("HUD snapshot " + name);
            }
            finally
            {
                RenderTexture.active = null;
                main.targetTexture = previous;
                foreach (var cv in canvases) cv.renderMode = RenderMode.ScreenSpaceOverlay;
                rt.Release();
                _health.MaxHealth = 1e6f;
                _health.Health = 1e6f;
                _lastHealth = _health.Health;
                _snapshotting = false;
            }
        }

        private IEnumerator finish()
        {
            log(_failures == 0 ? "ALL PASSED" : $"{_failures} FAILURE(S)");
            yield return null;
            UnityEditor.SessionState.SetInt(ResultKey, _failures == 0 ? 0 : 2);
            UnityEditor.EditorApplication.ExitPlaymode();
        }

        private void fail(string message) { _failures++; Debug.Log("[Bot] FAIL " + message); }
        private static void log(string message) => Debug.Log("[Bot] " + message);
    }
}
#endif
