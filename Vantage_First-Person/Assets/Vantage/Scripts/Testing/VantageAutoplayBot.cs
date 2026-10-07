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

            yield return soldiersFightBack(levels);
            yield return clearLevelOpensGates(levels);
            yield return walkStairs(levels);
            yield return finish();
        }

        private void Update()
        {
            if (_health == null) return;
            if (_health.Health < _lastHealth) _damageTaken += _lastHealth - _health.Health;
            if (_health.Health < 5e5f) _health.Health = 1e6f;
            _lastHealth = _health.Health;
        }

        // ---------------- 1. soldiers ----------------

        private IEnumerator soldiersFightBack(VantageTowerLevels levels)
        {
            var soldier = levels.Enemies.Select(e => e != null ? e.GetComponent<FighterBrain>() : null).FirstOrDefault(b => b != null);
            if (soldier == null) { fail("level 1 has no soldiers"); yield break; }

            // Stand 9 m away from the soldier with a clear line of sight, armed with the pistol.
            var spot = visibleSpotNear(soldier.transform.position, 2.5f, 12f, soldier.transform.forward);
            if (spot == null) { fail("no visible standing spot near " + soldier.name); yield break; }
            teleport(spot.Value, soldier.transform.position);
            _motor.GetComponent<VantageArsenal>()?.Unlock("Pistol");
            log($"player placed {Vector3.Distance(spot.Value, soldier.transform.position):F1} m from {soldier.name} (room floor y {soldier.transform.position.y:F1})");

            var inventory = soldier.GetComponent<CharacterInventory>();
            if (inventory == null) log("  soldier has NO CharacterInventory");
            else
                for (int i = 0; i < inventory.Weapons.Length; i++)
                {
                    var w = inventory.Weapons[i];
                    log($"  inventory[{i}] right={(w.RightItem != null ? w.RightItem.name + (w.RightItem.activeInHierarchy ? "" : " (inactive)") : "null")} gun={(w.Gun != null ? w.Gun.GetType().Name : "null")}");
                }
            var fire = soldier.GetComponent<AIFire>();
            log($"  AIFire {(fire != null ? $"enabled={fire.enabled} usage={fire.InventoryUsage} index={fire.InventoryIndex}" : "MISSING")}; guns in children: {string.Join(", ", soldier.GetComponentsInChildren<BaseGun>(true).Select(g => g.name))}");
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
                if (fire != null)
                {
                    object field(object o, string n) => o.GetType().GetField(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o);
                    var aim = field(fire, "_aim") is Vector3 a ? a : Vector3.zero;
                    var start = soldier.transform.position + Vector3.up * 2;
                    log($"      fire: firing={field(fire, "_isFiring")} aiming={field(fire, "_isAiming")} atPos={field(fire, "_isAimingAtAPosition")} reloading={field(fire, "_isReloading")} " +
                        $"obstructed={AIUtil.IsObstructed(start, aim)} aim={aim:F1} gunReady={motor.IsGunReady} weaponReady={motor.IsWeaponReady} hasCond={field(motor, "_hasFireCondition")} wants={field(motor, "_wantsToFire")}");
                    var equipped = motor.EquippedWeapon.Gun;
                    if (equipped != null)
                    {
                        object baseField(object o, string n) => typeof(BaseGun).GetField(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o);
                        log($"      gun {equipped.name}: active={equipped.isActiveAndEnabled} allowed={baseField(equipped, "_isAllowed")} goingToFire={baseField(equipped, "_isGoingToFire")} fireWait={baseField(equipped, "_fireWait")} " +
                            $"bullets={equipped.LoadedBulletsLeft} blocked={field(motor, "_isWeaponBlocked")} pumping={field(motor, "_isPumping")} sameAsLogged={equipped == gun}");
                        var ik = field(motor, "_ik");
                        var aimingArms = ik?.GetType().GetProperty("IsAimingArms")?.GetValue(ik);
                        var toTarget = (aim - soldier.transform.position); toTarget.y = 0;
                        object prop(object o, string n) => o.GetType().GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o);
                        log($"      arm aim: aimingGun={prop(motor, "IsAimingGun")} wasAiming={prop(motor, "WasAimingGun")} changing={prop(motor, "IsChangingWeaponOrHasJustChanged")} " +
                            $"coverOffsetCantAim={prop(motor, "IsMovingToCoverOffsetAndCantAim")} sprinting={motor.IsSprinting} hit={prop(motor, "IsGettingHit")} dontChange={prop(motor, "dontChangeArmAimingJustYet") ?? field(motor, "dontChangeArmAimingJustYet")} " +
                            $"loadBullet={prop(motor, "IsLoadingBullet")} loadMag={prop(motor, "IsLoadingMagazine")} reloading={motor.IsReloading}");
                        log($"      allow parts: falling={field(motor, "_isFalling")} aimingArms={aimingArms} facing={(toTarget.sqrMagnitude > 0 ? Vector3.Dot(toTarget.normalized, soldier.transform.forward) : -9):F2} inCover={motor.IsInCover} ik={(ik != null ? ik.GetType().Name : "null")}");
                    }
                }
                log($"  t={s,2}s soldier state={soldier.State} threat={(soldier.Threat != null ? soldier.Threat.name : "none")} sees={soldier.CanSeeTheThreat} " +
                    $"equipped={motor.IsEquipped} weapon={(motor.ActiveWeapon.Gun != null ? motor.ActiveWeapon.Gun.name : "none")} bullets={(gun != null ? gun.LoadedBulletsLeft : -1)} " +
                    $"dist={Vector3.Distance(soldier.transform.position, _motor.transform.position):F1} damageToPlayer={_damageTaken - damageBefore:F0}");
            }
            var damage = _damageTaken - damageBefore;
            if (fired == 0 && damage <= 0) fail($"soldier did not shoot in 12 s (state {soldier.State})");
            else log($"PASS soldier fired {fired} rounds, player took {damage:F0} damage");
        }

        /// <summary>A walkable spot on the target's floor, in front of it (within 60° of 'facing'), with line of sight.</summary>
        private Vector3? visibleSpotNear(Vector3 target, float min, float max, Vector3 facing)
        {
            facing.y = 0; facing.Normalize();
            int noMesh = 0, otherFloor = 0, blocked = 0;
            string blocker = "";
            for (int i = 0; i < 300; i++)
            {
                var d = Random.Range(min, max);
                var p = target + Quaternion.Euler(0, Random.Range(-60f, 60f), 0) * facing * d;
                if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) { noMesh++; continue; }
                if (Mathf.Abs(hit.position.y - target.y) > 0.8f) { otherFloor++; continue; }
                // Line of sight chest to chest; a hit right at the soldier (it stands against a wall) doesn't count.
                var from = hit.position + Vector3.up * 1.4f;
                var to = target + Vector3.up * 1.2f;
                if (Physics.Linecast(from, to, out var h, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore)
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
                if (Physics.Raycast(start + Vector3.up * 1.5f, Vector3.down, out var surfaceHit, 3f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
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
                            if (Physics.Raycast(p + Vector3.up * h, dir, out var fh, 1.2f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
                                what.Append($" front@{h:F2}:{fh.collider.name}({fh.distance:F2}m,n.y={fh.normal.y:F2})");
                        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, out var up, 3f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
                            what.Append($" ceiling:{up.collider.name}({up.distance + 0.5f:F2}m)");
                        var side = Vector3.Cross(Vector3.up, dir);
                        foreach (var s2 in new[] { -1f, 1f })
                            if (Physics.Raycast(p + Vector3.up * 1f, side * s2, out var sh, 0.6f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
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

        // ---------------- helpers ----------------

        private void teleport(Vector3 position, Vector3 lookAt)
        {
            var body = _motor.GetComponent<Rigidbody>();
            if (body != null) { body.linearVelocity = Vector3.zero; body.position = position + Vector3.up * 0.05f; }
            var look = lookAt - position; look.y = 0;
            _motor.transform.SetPositionAndRotation(position + Vector3.up * 0.05f, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : _motor.transform.rotation);
            Physics.SyncTransforms();
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
