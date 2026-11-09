#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CoverShooter;
using UnityEngine;

namespace Vantage.Testing
{
    /// <summary>The sound system's checks (group -vantageOnly audio). Batch mode has no audio output: these check
    /// that the right events fire and that nothing breaks, not how it sounds.</summary>
    public partial class VantageAutoplayBot
    {
        private IEnumerator audioChecks(VantageTowerLevels levels)
        {
            // The title screen ran first (moves between pages, chooses Play): its clicks are counted before any reload.
            _menuClicks = (VantageAudio.Plays(VantageSounds.UiMove), VantageAudio.Plays(VantageSounds.UiSelect));
            yield return audioBankPlaceholders();
            yield return audioZonesFollowFloors(levels);
            yield return audioMusic();
            yield return audioFootsteps(VantageTowerLevels.Instance);
            yield return audioEvents();
        }

        private (int move, int select) _menuClicks;

        /// <summary>
        /// Every gameplay moment fires its event (fake clips in every event, a fresh scene): the alarm (siren, shutter
        /// slam), the key, the key slot, the floor gate rolling up, a sentry's glint and shot, a pistol round bouncing
        /// off the robot, a soldier waking, a coin; the title screen clicked; two soldiers waking within a second bark once.
        /// </summary>
        private IEnumerator audioEvents()
        {
            var bank = VantageAudio.Instance.Bank;
            foreach (var e in bank.Events) { e.Clips.Clear(); e.Clips.Add(fakeClip("fake_" + e.Name, 0.3f)); }
            VantageRun.Reset();   // a new run: the alarm only springs on level 1
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            yield return null;
            if (!acquire()) { foreach (var e in bank.Events) e.Clips.Clear(); yield break; }
            yield return skipIntro();
            var levels = VantageTowerLevels.Instance;
            // Two quiet soldiers wake within a second (before the alarm, which wakes them all): one bark.
            var soldiers = levels.Enemies.Where(e => e != null && e.GetComponent<FighterBrain>() != null).Take(2).ToList();
            var barksBefore = VantageAudio.Plays(VantageSounds.VoContact);
            foreach (var s in soldiers) s.GetComponent<FighterBrain>().ToAttack(_motor.GetComponent<Actor>());
            for (float t = 0; t < 1.2f; t += Time.deltaTime) yield return null;
            var barks = VantageAudio.Plays(VantageSounds.VoContact) - barksBefore;
            teleport(_start, levels.TowerArea.center);

            VantageEvents.RaiseAlarm("bot");
            yield return null;
            var key = FindAnyObjectByType<VantageKeyItem>();
            var pickup = key != null ? key.GetComponent<VantagePickup>() : null;
            if (pickup != null) VantageEvents.RaiseItemPickedUp(pickup.item);
            var slot = FindAnyObjectByType<VantageKeySlot>();
            if (slot != null) slot.GetComponent<VantageInteract>().onUsed.Invoke();
            var gate = FindAnyObjectByType<VantageFloorGate>();
            if (gate != null) gate.Open();
            var sentry = VantageSentries.All.FirstOrDefault(x => x != null);
            if (sentry != null) { sentry.Glint(1f); sentry.ScriptedShot(_start + Vector3.forward * 30f, true); }
            var robotObject = levels.HeavyRobotPrefab != null ? Instantiate(levels.HeavyRobotPrefab, _start + Vector3.right * 6f, Quaternion.identity) : null;
            yield return null;
            if (robotObject != null)
            {
                var arsenal = _motor.GetComponent<VantageArsenal>();
                arsenal.Unlock("Pistol");
                arsenal.EquipNow("Pistol");
                var robot = robotObject.GetComponent<VantageHeavyRobot>();
                robot.OnHit(new Hit(robot.transform.position + Vector3.up, Vector3.up, 30f, _motor.gameObject, robot.gameObject, HitType.Pistol, 0));
            }
            var coin = FindObjectsByType<VantageCoin>(FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.activeInHierarchy);
            if (coin != null) coin.Collect();
            if (robotObject != null) Destroy(robotObject);
            foreach (var e in bank.Events) e.Clips.Clear();

            var expected = new[] { VantageSounds.AlarmSiren, VantageSounds.ShutterSlam, VantageSounds.ShutterRoll, VantageSounds.KeyPickup, VantageSounds.KeySlotTurn,
                VantageSounds.SentryGlint, VantageSounds.SentryCrack, VantageSounds.RobotDeflect, VantageSounds.CoinCollect };
            var silent = expected.Where(n => VantageAudio.Plays(n) == 0).ToList();
            var line = $"silent moments {silent.Count} ({string.Join(", ", silent)}), soldiers waking {soldiers.Count} → barks {barks}, title screen moves {_menuClicks.move} selects {_menuClicks.select}" +
                $" [diag: barks before {barksBefore}, threats {string.Join("/", soldiers.Select(x => x.GetComponent<FighterBrain>().Threat != null ? x.GetComponent<FighterBrain>().Threat.name : "none"))}]";
            if (silent.Count == 0 && soldiers.Count == 2 && barks == 1 && _menuClicks.move > 0 && _menuClicks.select > 0) log("PASS audio events: " + line);
            else fail("audio events: " + line);
        }

        /// <summary>
        /// Footsteps follow the surface: dirt in front of the entrance, concrete on the ground floor, metal halfway up
        /// the outside helipad stairs (under their invisible walking surface); walking outside plays dirt steps and
        /// the template's own footstep clips are gone.
        /// </summary>
        private IEnumerator audioFootsteps(VantageTowerLevels levels)
        {
            var facing = (_start - levels.TowerArea.center); facing.y = 0; facing.Normalize();
            var outside = _start + facing * 12f;
            var floor = levelPoint(levels, 0);
            var stairs = GameObject.Find("FINAL_Landing_Stairs");
            var mid = Vector3.zero;
            if (stairs != null)
            {
                var b = stairs.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, x) => { a.Encapsulate(x); return a; });
                mid = b.center + Vector3.up * (b.extents.y + 0.5f);   // above the flight's middle; the ray finds the steps
            }
            var atOutside = VantageFootsteps.SurfaceAt(outside + Vector3.up * 0.3f);
            var atFloor = VantageFootsteps.SurfaceAt(floor + Vector3.up * 0.3f);
            var atStairs = stairs != null ? VantageFootsteps.SurfaceAt(mid) : "no FINAL_Landing_Stairs";
            // The stairs' own material decides (they may be concrete): the check is that the invisible walking surface
            // over them is looked through, not which material the model uses.
            var stairsRenderer = stairs != null ? stairs.GetComponentInChildren<Renderer>() : null;
            var stairsMaterial = stairsRenderer != null && stairsRenderer.sharedMaterial != null ? stairsRenderer.sharedMaterial.name : "?";
            var stairsExpected = VantageFootsteps.IsMetal(stairsMaterial) ? VantageSounds.FsMetal : VantageSounds.FsConcrete;
            var sounds = _motor.GetComponent<CharacterSounds>();
            var templateSteps = sounds != null && sounds.Footstep != null ? sounds.Footstep.Length : 0;
            teleport(outside, outside + facing * 10f);
            // Batch mode renders nothing, so a culled animator never moves the feet and the template never steps.
            var animator = _motor.GetComponent<Animator>();
            var culling = animator != null ? animator.cullingMode : AnimatorCullingMode.AlwaysAnimate;
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var before = VantageAudio.Plays(VantageSounds.FsDirt);
            for (float t = 0; t < 3f; t += Time.deltaTime) { _controller.MovementInput = new CharacterMovement(facing, 1f); yield return null; }
            _controller.MovementInput = new CharacterMovement();
            var dirtSteps = VantageAudio.Plays(VantageSounds.FsDirt) - before;
            if (animator != null) animator.cullingMode = culling;
            var anim = _motor.GetComponent<Animator>();
            var diag = $" [diag: footsteps component {(_motor.GetComponent<VantageFootsteps>() != null)}, moved {Vector3.Distance(_motor.transform.position, outside):F1} m, animator culling {(anim != null ? anim.cullingMode.ToString() : "none")}]";
            var line = $"outside {atOutside}, ground floor {atFloor}, helipad stairs {atStairs} (material {stairsMaterial}, expected {stairsExpected}), dirt steps walking 3 s {dirtSteps}, template footstep clips {templateSteps}{diag}";
            if (atOutside == VantageSounds.FsDirt && atFloor == VantageSounds.FsConcrete && atStairs == stairsExpected && dirtSteps > 0 && templateSteps == 0)
                log("PASS audio footsteps: " + line);
            else fail("audio footsteps: " + line);
        }

        /// <summary>
        /// Music: with clips in both layers (a scene reload starts them), exactly two music sources in step with the
        /// combat layer silent; a soldier attacking turns combat on, which holds 3 s after it dies and ends after the
        /// hold and fade; the alarm fires the lockdown stinger once and a level clear the cleared stinger.
        /// </summary>
        private IEnumerator audioMusic()
        {
            var bank = VantageAudio.Instance.Bank;
            var explore = bank.Find(VantageSounds.MusExplore);
            var combatLayer = bank.Find(VantageSounds.MusCombat);
            explore.Clips.Add(fakeClip("fake_explore", 4f));
            combatLayer.Clips.Add(fakeClip("fake_combat", 4f));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            yield return null;
            if (!acquire()) yield break;
            yield return skipIntro();   // the reload starts a run whose intro is on (combat music is off in cutscenes)
            var levels = VantageTowerLevels.Instance;
            for (float t = 0; t < 1f; t += Time.deltaTime) yield return null;
            var music = FindAnyObjectByType<VantageMusic>();
            if (music == null) { fail("audio music: no VantageMusic in the scene"); explore.Clips.Clear(); combatLayer.Clips.Clear(); yield break; }
            var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(a => a.isPlaying && a.clip != null && a.clip.name.StartsWith("fake_") && (a.clip.name == "fake_explore" || a.clip.name == "fake_combat")).ToList();
            var inStep = sources.Count == 2 && Mathf.Abs(sources[0].timeSamples - sources[1].timeSamples) <= sources[0].clip.samples / 100;
            var combatSource = sources.FirstOrDefault(a => a.clip.name == "fake_combat");
            var startSilent = combatSource != null && combatSource.volume < 0.01f;

            teleport(_start, levels.TowerArea.center);
            var soldier = levels.Enemies.FirstOrDefault(e => e != null && e.GetComponent<FighterBrain>() != null);
            var on = false; var held = false; var off = false; var diag = "";
            if (soldier != null)
            {
                var brain = soldier.GetComponent<FighterBrain>();
                brain.ToAttack(_motor.GetComponent<Actor>());
                var right = brain.Threat;
                yield return null;
                var afterFrame = brain.Threat;
                for (float t = 0; t < 1f && !music.Combat; t += Time.deltaTime) yield return null;
                on = music.Combat;
                diag = $" [diag: {soldier.name} alive {soldier.GetComponent<CharacterMotor>().IsAlive}, threat set {(right != null ? right.name : "null")}, after a frame {(afterFrame != null ? afterFrame.name : "null")}, after 1 s {(brain.Threat != null ? brain.Threat.name : "null")}, player {VantageEvents.ActivePlayer()?.name}, cinematic {VantageCinematic.Active}]";
                kill(soldier);
                for (float t = 0; t < 3f; t += Time.deltaTime) yield return null;
                held = music.Combat;
                for (float t = 0; t < music.CombatHold + music.CombatFade; t += Time.deltaTime) yield return null;
                off = !music.Combat;
            }
            var lockdown = VantageAudio.Plays(VantageSounds.StgLockdown);
            VantageEvents.RaiseAlarm("bot");
            yield return null;
            lockdown = VantageAudio.Plays(VantageSounds.StgLockdown) - lockdown;
            var cleared = VantageAudio.Plays(VantageSounds.StgCleared);
            yield return clearLevel(levels, 0);
            cleared = VantageAudio.Plays(VantageSounds.StgCleared) - cleared;
            explore.Clips.Clear();
            combatLayer.Clips.Clear();
            var line = $"music sources {sources.Count} in step {inStep}, combat layer silent at start {startSilent}; combat on {on}, held 3 s after {held}, off after the hold {off}; lockdown stingers {lockdown}, cleared stingers {cleared}{diag}";
            if (inStep && startSilent && on && held && off && lockdown == 1 && cleared >= 1) log("PASS audio music: " + line);
            else fail("audio music: " + line);
        }

        /// <summary>A walkable point on a level's floor near the tower's middle (the helipad: its top).</summary>
        private static Vector3 levelPoint(VantageTowerLevels levels, int level)
        {
            if (level == levels.Levels.Count - 1)
            {
                var pad = GameObject.Find("FINAL_Helipad");
                if (pad != null)
                {
                    var b = pad.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, x) => { a.Encapsulate(x); return a; });
                    return new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
                }
            }
            var c = levels.TowerArea.center;
            for (float r = 0; r < 12f; r += 1f)
                foreach (var dir in new[] { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(c.x, levels.Levels[level].FloorY, c.z) + dir * r, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas)
                        && Mathf.Abs(hit.position.y - levels.Levels[level].FloorY) < 0.4f)
                        return hit.position;
            return floorAhead(levels, level);
        }

        /// <summary>
        /// The ambience bed follows the floor: outside, L1, L2, L3, helipad; standing on the stairs below a floor
        /// (a boundary) changes the zone at most once; on L3 the debris one-shots play.
        /// </summary>
        private IEnumerator audioZonesFollowFloors(VantageTowerLevels levels)
        {
            var zones = FindAnyObjectByType<VantageAudioZones>();
            if (zones == null) { fail("audio zones: no VantageAudioZones in the scene"); yield break; }
            var facing = (_start - levels.TowerArea.center); facing.y = 0; facing.Normalize();
            var stops = new (string name, Vector3 at, string bed)[]
            {
                ("outside", _start + facing * 12f, VantageSounds.AmbExterior),
                ("L1", levelPoint(levels, 0), VantageSounds.AmbL1),
                ("L2", levelPoint(levels, 1), VantageSounds.AmbL2),
                ("L3", levelPoint(levels, 2), VantageSounds.AmbL3),
                ("helipad", levelPoint(levels, levels.Levels.Count - 1), VantageSounds.AmbHelipad),
            };
            var heard = new System.Collections.Generic.List<string>();
            var ok = true;
            foreach (var (name, at, bed) in stops)
            {
                teleport(at, at + facing);
                for (float t = 0; t < 3f; t += Time.deltaTime) yield return null;
                heard.Add($"{name} {zones.ActiveBed}");
                ok &= zones.ActiveBed == bed;
            }
            var arrival = levels.Levels[1].Arrival;
            teleport(arrival.position, arrival.position + arrival.forward);
            for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
            var changes = zones.Changes;
            for (float t = 0; t < 3f; t += Time.deltaTime) yield return null;
            var flaps = zones.Changes - changes;

            var debris = VantageAudio.Instance.Bank.Find(VantageSounds.AmbDebris);
            debris.Clips.Add(fakeClip("fake_debris"));
            var l3 = levelPoint(levels, 2);
            teleport(l3, l3 + facing);
            var before = VantageAudio.Plays(VantageSounds.AmbDebris);
            for (float t = 0; t < 20f; t += Time.deltaTime) yield return null;
            var crumbles = VantageAudio.Plays(VantageSounds.AmbDebris) - before;
            debris.Clips.Clear();
            var line = $"{string.Join(", ", heard)}; zone changes standing on the L2 stairs {flaps}; debris on L3 in 20 s {crumbles}";
            if (ok && flaps <= 1 && crumbles >= 1) log("PASS audio zones: " + line);
            else fail("audio zones: " + line);
        }

        private static AudioClip fakeClip(string name, float seconds = 0.2f) =>
            AudioClip.Create(name, Mathf.Max(1, (int)(44100 * seconds)), 1, 44100, false);

        /// <summary>
        /// The bank has every event; a filled event plays (counted), an empty one returns no source, a loop with a
        /// fallback plays, and none of it logs an error.
        /// </summary>
        private IEnumerator audioBankPlaceholders()
        {
            var audio = VantageAudio.Instance;
            if (audio == null || audio.Bank == null) { fail($"audio bank: VantageAudio {(audio != null)}, bank {(audio != null && audio.Bank != null)}"); yield break; }
            var missing = VantageSounds.All.Where(n => audio.Bank.Find(n) == null).ToList();
            var errors = 0;
            void count(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) errors++; }
            Application.logMessageReceived += count;
            var steps = audio.Bank.Find(VantageSounds.FsConcrete);
            var kept = steps != null ? steps.Clips.ToList() : null;
            if (steps != null) { steps.Clips.Clear(); steps.Clips.Add(fakeClip("fake_a")); steps.Clips.Add(fakeClip("fake_b")); }
            var before = VantageAudio.Plays(VantageSounds.FsConcrete);
            AudioSource filled = null;
            for (int i = 0; i < 5; i++) filled = VantageAudio.Play(VantageSounds.FsConcrete, _motor.transform.position);
            var plays = VantageAudio.Plays(VantageSounds.FsConcrete) - before;   // same frame: no footsteps of others in between
            var ui = audio.Bank.Find(VantageSounds.UiSelect);
            var empty = ui != null && !ui.Filled ? VantageAudio.Play2D(VantageSounds.UiSelect) : null;
            var loop = VantageAudio.PlayLoop(VantageSounds.AmbExterior, null, VantageProceduralAudio.Create(VantageProceduralAudio.Kind.Wind, 1));
            var stinger = audio.Bank.Find(VantageSounds.StgLockdown);
            var keptStinger = stinger.Clips.ToList();
            stinger.Clips.Clear();
            stinger.Clips.Add(fakeClip("fake_stinger", 3f));
            var stingerSource = VantageAudio.Play2D(VantageSounds.StgLockdown);
            var gunshot = fakeClip("fake_gunshot", 1f);
            for (int i = 0; i < VantageAudio.PoolSize * 2; i++) VantageAudio.PlayClip(gunshot, _motor.transform.position, "SFX");
            var stingerKept = stingerSource != null && stingerSource.isPlaying && stingerSource.clip != null && stingerSource.clip.name == "fake_stinger";
            stinger.Clips.Clear();
            stinger.Clips.AddRange(keptStinger);
            yield return null;
            var looping = loop != null && loop.isPlaying && loop.loop;
            VantageAudio.Stop(loop);
            if (steps != null) { steps.Clips.Clear(); steps.Clips.AddRange(kept); }
            yield return null;
            Application.logMessageReceived -= count;
            var line = $"events missing from the bank {missing.Count} ({string.Join(", ", missing)}), filled plays {plays}/5 source {(filled != null)}, empty event source {(empty != null)}, fallback loop playing {looping}, stinger kept under {VantageAudio.PoolSize * 2} gunshots {stingerKept}, errors {errors}";
            if (missing.Count == 0 && plays == 5 && filled != null && empty == null && looping && stingerKept && errors == 0) log("PASS audio bank: " + line);
            else fail("audio bank: " + line);

            // The guide sounds and the menu music (2026-11-09): 3D loops (the wind in Ambience) and a 2D music loop.
            var guides = new[] { "GUIDE_PA", "GUIDE_Stairs", "GUIDE_Radio", "GUIDE_Phone", "KEYSLOT_Beep", "GUIDE_Wind", "GUIDE_HeliRadio" };
            var wrong = guides.Where(n =>
            {
                var e = audio.Bank.Find(n);
                return e == null || !e.Loop || e.Spatial < 0.99f || e.MinDistance != 2f || e.MaxDistance != 30f || e.Group != (n == "GUIDE_Wind" ? "Ambience" : "SFX");
            }).ToList();
            var menu = audio.Bank.Find("MUS_Menu");
            var menuOk = menu != null && menu.Loop && menu.Spatial < 0.01f && menu.Group == "Music";
            var guideLine = $"events {VantageSounds.All.Length}/40, guide events with the wrong defaults {wrong.Count} ({string.Join(", ", wrong)}), menu music 2D loop in Music {menuOk}";
            if (VantageSounds.All.Length == 40 && wrong.Count == 0 && menuOk) log("PASS audio guide events: " + guideLine);
            else fail("audio guide events: " + guideLine);
        }
    }
}
#endif
