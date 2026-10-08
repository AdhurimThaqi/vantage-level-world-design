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
            log("started");
            yield return new WaitForSeconds(2f);

            var player = VantageEvents.ActivePlayer();
            _motor = player != null ? player.GetComponent<CharacterMotor>() : null;
            var levels = VantageTowerLevels.Instance;
            if (_motor == null || levels == null)
            {
                fail("no player or no VantageTowerLevels in the scene");
                yield return finish();
                yield break;
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
            var player2 = _motor.GetComponent<VantageThirdPersonPlayer>();
            if (player2 != null) player2.enabled = false;
            _health.MaxHealth = 1e6f;
            _health.Health = 1e6f;
            _lastHealth = _health.Health;
            log($"level {levels.Current + 1} '{levels.CurrentName}', {levels.Remaining} enemies, seed {levels.Seed}");

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vantageBench") >= 0)
            {
                yield return bench(levels);
                yield return finish();
                yield break;
            }

            yield return soldiersFightBack(levels);
            yield return hudSnapshot("hud_combat");
            yield return clearLevelOpensGates(levels);
            yield return hudSnapshot("hud_level_cleared");
            yield return lastEnemiesCome(levels);
            yield return walkStairs(levels);
            yield return walkEntrances(levels);
            yield return finish();
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
                spot = visibleSpotNear(candidate.transform.position, 2.5f, 12f, candidate.transform.forward, 60f)
                       ?? visibleSpotNear(candidate.transform.position, 2.5f, 12f, candidate.transform.forward, 180f);
                if (spot != null) { soldier = candidate; break; }
            }
            if (spot == null) { fail($"no visible standing spot near any of {soldiers.Count} soldiers"); yield break; }
            teleport(spot.Value, soldier.transform.position);
            _motor.GetComponent<VantageArsenal>()?.Unlock("Pistol");
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
                log($"  t={s,2}s soldier state={soldier.State} threat={(soldier.Threat != null ? soldier.Threat.name : "none")} sees={soldier.CanSeeTheThreat} " +
                    $"equipped={motor.IsEquipped} weapon={(motor.ActiveWeapon.Gun != null ? motor.ActiveWeapon.Gun.name : "none")} bullets={(gun != null ? gun.LoadedBulletsLeft : -1)} " +
                    $"dist={Vector3.Distance(soldier.transform.position, _motor.transform.position):F1} damageToPlayer={_damageTaken - damageBefore:F0}");
            }
            var damage = _damageTaken - damageBefore;
            if (fired == 0 && damage <= 0) fail($"soldier did not shoot in 12 s (state {soldier.State})");
            else log($"PASS soldier fired {fired} rounds, player took {damage:F0} damage");
        }

        /// <summary>A walkable spot on the target's floor, within 'spread' degrees of 'facing', with line of sight.</summary>
        private Vector3? visibleSpotNear(Vector3 target, float min, float max, Vector3 facing, float spread)
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
                // Line of sight chest to chest; a hit right at the soldier (it stands against a wall) doesn't count.
                var from = hit.position + Vector3.up * 1.4f;
                var to = target + Vector3.up * 1.2f;
                if (Physics.Linecast(from, to, out var h, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)
                    && Vector3.Distance(h.point, to) > 0.6f)
                { blocked++; blocker = h.collider.name; continue; }
                return hit.position;
            }
            log($"  no spot: {noMesh} off NavMesh, {otherFloor} other floor, {blocked} blocked (last by {blocker}); soldier at {target:F1}, NavMesh under soldier: {NavMesh.SamplePosition(target, out var s, 1f, NavMesh.AllAreas)}");
            return null;
        }

        // ---------------- 2. gates ----------------

        private IEnumerator clearLevelOpensGates(VantageTowerLevels levels)
        {
            var level = levels.Current;
            foreach (var e in levels.Enemies.ToList())
                kill(e);
            for (float t = 0; t < 3f && levels.Current == level; t += Time.deltaTime)
                yield return null;
            var gates = FindObjectsByType<VantageFloorGate>(FindObjectsSortMode.None).Where(g => g.Level == level).ToList();
            var open = gates.Count(g => g.IsOpen);
            if (levels.Current != level + 1 || open != gates.Count)
                fail($"after clearing level {level + 1}: current level {levels.Current + 1}, gates open {open}/{gates.Count}");
            else
                log($"PASS level {level + 1} cleared -> gates open {open}/{gates.Count}, level {levels.Current + 1} spawned {levels.Remaining} enemies");
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

        // ---------------- 3. stairs ----------------

        /// <summary>Walks every opened flight from its foot, measuring climb progress and where the character stalls.</summary>
        private IEnumerator walkStairs(VantageTowerLevels levels)
        {
            // Kill whatever spawned on the next level so the walk isn't interrupted.
            foreach (var e in levels.Enemies.ToList()) kill(e);
            yield return new WaitForSeconds(0.5f);

            foreach (var gate in FindObjectsByType<VantageFloorGate>(FindObjectsSortMode.None).Where(g => g.IsOpen).OrderBy(g => g.name))
            {
                // Geometry from the blocker box (the shutter panel has rolled up once the gate is open).
                var box = gate.Blocker.transform;
                var dir = box.forward; dir.y = 0; dir.Normalize();
                var rise = gate.Blocker.size.y - 3f;
                var foot = box.position - dir * (gate.Blocker.size.z / 2f - 0.5f) - Vector3.up * (gate.Blocker.size.y / 2f - 0.15f);
                // Start just onto the flight (its foot end can be a narrow landing), on the walkable surface.
                var start = foot + dir * 0.6f;
                if (Physics.Raycast(start + Vector3.up * 1.5f, Vector3.down, out var surfaceHit, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    start = surfaceHit.point;
                else
                    log($"  {gate.name}: NO SURFACE under the flight start {start:F2}");
                teleport(start, start + dir * 5f);
                yield return new WaitForSeconds(0.6f);
                log($"  {gate.name}: start {start:F2} (foot {foot:F2}), grounded={_motor.IsGrounded} alive={_motor.IsAlive} cover={_motor.IsInCover} controller={_controller.enabled} y after settle {_motor.transform.position.y:F2}");

                var startY = foot.y; // measure from the foot of the flight, not from where the bot was put down
                float stalled = 0, t = 0;
                var reported = false;
                float window = 0; var windowStart = _motor.transform.position;
                var path = new StringBuilder();
                while (t < 14f && _motor.transform.position.y < startY + rise - 0.2f)
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
                        var moved = new Vector2(p.x - windowStart.x, p.z - windowStart.z).magnitude;
                        if (moved < 0.075f && t > 0.6f) stalled += window;
                        window = 0; windowStart = p;
                    }
                    if (stalled > 0.5f && !reported)
                    {
                        reported = true;
                        var tower = levels.Tower;
                        var what = new StringBuilder();
                        foreach (var h in new[] { 0.1f, 0.35f, 1.0f, 1.7f })
                            if (Physics.Raycast(p + Vector3.up * h, dir, out var fh, 1.2f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                                what.Append($" front@{h:F2}:{fh.collider.name}({fh.distance:F2}m,n.y={fh.normal.y:F2})");
                        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, out var up, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                            what.Append($" ceiling:{up.collider.name}({up.distance + 0.5f:F2}m)");
                        var side = Vector3.Cross(Vector3.up, dir);
                        foreach (var s2 in new[] { -1f, 1f })
                            if (Physics.Raycast(p + Vector3.up * 1f, side * s2, out var sh, 0.6f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                                what.Append($" side{(s2 < 0 ? "L" : "R")}:{sh.collider.name}({sh.distance:F2}m)");
                        log($"  stalled at local {tower.InverseTransformPoint(p):F2}, grounded={_motor.IsGrounded}:{what}");
                    }
                    if (Mathf.Repeat(t, 1f) < Time.deltaTime) path.Append($" {p.y - startY:F2}");
                }
                _controller.MovementInput = new CharacterMovement();
                var climbed = _motor.transform.position.y - startY;
                var ok = climbed >= rise - 0.3f;
                var line = $"{gate.name}: climbed {climbed:F2}/{rise:F2} m in {t:F1} s, stalled {stalled:F1} s, height per second:{path}";
                if (ok && stalled < 1f) log("PASS stairs " + line);
                else fail("stairs " + line);
            }
        }

        // ---------------- 3b. last enemies ----------------

        /// <summary>
        /// Leaves two enemies alive on the level and stands the player far from them, out of sight: within 30 s they
        /// must have come to the player (hunt rule) instead of waiting somewhere to be found.
        /// </summary>
        private IEnumerator lastEnemiesCome(VantageTowerLevels levels)
        {
            var enemies = levels.Enemies.Where(e => e != null).ToList();
            if (enemies.Count < 3) { log("  last enemies: skipped (too few enemies)"); yield break; }
            // Keep one soldier and one drone if there are both.
            var keep = new List<GameObject>();
            var soldier = enemies.FirstOrDefault(e => e.GetComponent<VantageDrone>() == null);
            var drone = enemies.FirstOrDefault(e => e.GetComponent<VantageDrone>() != null);
            if (soldier != null) keep.Add(soldier);
            if (drone != null) keep.Add(drone);
            foreach (var e in enemies.Where(e => !keep.Contains(e)).Take(enemies.Count - 2)) kill(e);
            keep = enemies.Where(e => e != null && e.activeInHierarchy && (e.GetComponent<CharacterHealth>()?.Health ?? 0) > 0).ToList();

            // Stand on the level, at the spawn point furthest from the survivors.
            var storeys = levels.Levels[levels.Current].Storeys;
            var spot = levels.Tower.GetComponentsInChildren<CommandTowerKit.GameplayMarker>(true)
                             .Where(m => m.markerType == "EnemySpawn" && storeys.Contains(m.floor))
                             .OrderByDescending(m => keep.Min(e => Vector3.Distance(e.transform.position, m.transform.position)))
                             .First().transform.position;
            if (NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas)) spot = hit.position;
            teleport(spot, spot + Vector3.forward);
            var damageBefore = _damageTaken;
            var startDistance = keep.Min(e => Vector3.Distance(e.transform.position, spot));
            log($"  last enemies: {keep.Count} left ({string.Join(", ", keep.Select(e => e.name))}), nearest {startDistance:F1} m, hunting={levels.Hunting}");

            float t = 0, nearest = startDistance;
            while (t < 30f && nearest > 8f && _damageTaken <= damageBefore)
            {
                yield return new WaitForSeconds(0.5f);
                t += 0.5f;
                nearest = keep.Where(e => e != null).Select(e => Vector3.Distance(e.transform.position, _motor.transform.position)).DefaultIfEmpty(0f).Min();
                if (Mathf.Repeat(t, 5f) < 0.25f)
                    log($"    t={t:F0}s hunting={levels.Hunting} remaining={levels.Remaining}: " + string.Join("; ", keep.Where(e => e != null).Select(e =>
                        $"{e.name} {Vector3.Distance(e.transform.position, _motor.transform.position):F1} m at {levels.Tower.InverseTransformPoint(e.transform.position):F1}" +
                        (e.GetComponent<FighterBrain>() is FighterBrain b ? $" state={b.State}" : ""))));
            }
            var line = $"nearest enemy {startDistance:F1} m -> {nearest:F1} m in {t:F1} s, damage taken {_damageTaken - damageBefore:F0}";
            if (nearest <= 8f || _damageTaken > damageBefore) log("PASS last enemies come to the player: " + line);
            else fail("last enemies did not come: " + line);
        }

        // ---------------- 4. entrances ----------------

        /// <summary>Walks into the tower through every ground-floor entrance, from 4 m outside to 4 m inside.</summary>
        private IEnumerator walkEntrances(VantageTowerLevels levels)
        {
            var tower = levels.Tower;
            foreach (var entry in tower.GetComponentsInChildren<CommandTowerKit.GameplayMarker>(true).Where(m => m.markerType == "PlayerEntry" && m.floor < 0))
            {
                var local = tower.InverseTransformPoint(entry.transform.position);
                var inward = tower.TransformDirection(Mathf.Abs(local.x) > Mathf.Abs(local.z) ? new Vector3(-Mathf.Sign(local.x), 0, 0) : new Vector3(0, 0, -Mathf.Sign(local.z)));
                var start = entry.transform.position - inward * 4f;
                if (Physics.Raycast(start + Vector3.up * 3f, Vector3.down, out var ground, 6f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    start = ground.point;
                teleport(start, start + inward * 5f);
                yield return new WaitForSeconds(0.6f);

                float t = 0, stalled = 0, window = 0;
                var windowStart = _motor.transform.position;
                while (t < 8f && Vector3.Dot(_motor.transform.position - start, inward) < 8f)
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
                var line = $"{entry.name}: walked {progress:F1}/8.0 m in {t:F1} s, stalled {stalled:F1} s";
                if (progress >= 7.5f && stalled < 1f) log("PASS entrance " + line);
                else fail($"entrance {line}, stopped at local {tower.InverseTransformPoint(_motor.transform.position):F2}, in front:{obstacles(inward)}");
            }
        }

        /// <summary>What the character's capsule would run into in 'direction': colliders at four heights.</summary>
        private string obstacles(Vector3 direction)
        {
            var what = new StringBuilder();
            var p = _motor.transform.position;
            foreach (var h in new[] { 0.1f, 0.35f, 1.0f, 1.7f })
                if (Physics.Raycast(p + Vector3.up * h, direction, out var hit, 1.2f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    what.Append($" @{h:F2}:{hit.collider.name}({hit.distance:F2}m, n.y={hit.normal.y:F2})");
            // The character's capsule (rays can pass beside thin posts the capsule still catches on).
            var capsule = _motor.GetComponent<CapsuleCollider>();
            var radius = capsule != null ? capsule.radius : 0.3f;
            foreach (var hit in Physics.CapsuleCastAll(p + Vector3.up * (radius + 0.05f), p + Vector3.up * 1.7f, radius, direction, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(_motor.transform))
                    what.Append($" capsule:{hit.collider.name}[layer {hit.collider.gameObject.layer}]({hit.distance:F2}m at {hit.point:F2})");
            // Already overlapping: which way and how far the geometry pushes the capsule out.
            if (capsule != null)
                foreach (var other in Physics.OverlapCapsule(p + Vector3.up * (radius + 0.05f), p + Vector3.up * 1.7f, radius, ~0, QueryTriggerInteraction.Ignore))
                    if (!other.transform.IsChildOf(_motor.transform)
                        && Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation, other, other.transform.position, other.transform.rotation, out var push, out var depth))
                        what.Append($" overlap:{other.name} push {push:F2} by {depth:F2}m");
            return what.Length > 0 ? what.ToString() : " nothing";
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
