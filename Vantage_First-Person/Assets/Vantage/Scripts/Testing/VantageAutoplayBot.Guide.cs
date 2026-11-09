#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vantage.Testing
{
    /// <summary>The guide sound's checks (group -vantageOnly guide): the goal ladder from the alarm to the helipad, one
    /// source at a time at its anchor, floor gating, silence in a cutscene, no flapping on the stairs, a death reload.</summary>
    public partial class VantageAutoplayBot
    {
        private IEnumerator guideChecks(VantageTowerLevels levels)
        {
            var guide = VantageGoalSound.Instance;
            if (guide == null) { fail("guide: no VantageGoalSound in the scene"); yield break; }
            var seen = new List<string>();
            var ok = true;

            // One step: the expected name, the source at its anchor, at most one guide loop, audible on its floor.
            IEnumerator expect(string name, int floor, bool audible = true)
            {
                // L3: the floor in front of its arrival (the middle of L3 is the open area, whose trigger ends a step).
                if (floor >= 0) { var p = floor == 2 ? level3Floor(levels) : levelPoint(levels, floor); teleport(p, p + Vector3.forward); }
                for (float t = 0; t < 2f; t += Time.deltaTime) yield return null;   // poll + step delay + crossfade
                var src = guide.Source;
                var near = name == "none" || (src != null && guide.Anchor != null && Vector3.Distance(src.transform.position, guide.Anchor.position) <= 1f);
                var loops = guideLoops();
                var good = guide.CurrentName == name && near && loops <= (name == "none" ? 0 : 1) && guide.Audible == (audible && name != "none");
                seen.Add($"{name}: got {guide.CurrentName} at anchor {near} loops {loops} audible {guide.Audible}");
                ok &= good;
            }

            yield return expect("none", -1);
            VantageEvents.RaiseAlarm("bot");
            yield return expect("L1 lockdown", 0);
            yield return clearLevel(levels, 0);
            yield return expect("L1 cleared", 0);

            // Up the L1 flight like a player: one change (stairs → rifle), no flapping at the floor boundary.
            var before = guide.Changes;
            var flips = 0; var wasAudible = guide.Audible;
            var walk = walkUpLikeAPlayer(levels, 0, "LVL1");
            var trail = new List<string>();
            var lastName = guide.CurrentName;
            while (walk.MoveNext())
            {
                if (guide.Audible != wasAudible) { flips++; wasAudible = guide.Audible; }
                if (guide.CurrentName != lastName) { lastName = guide.CurrentName; var pp = _motor.transform.position; trail.Add($"{lastName} at y {pp.y:F2} floor {levels.LevelAt(pp)}"); }
                yield return walk.Current;
            }
            var stairChanges = guide.Changes - before;
            seen.Add($"walking up the L1 stairs: {stairChanges} changes, {flips} audible flips [{string.Join(" > ", trail)}]");
            ok &= stairChanges == 1 && flips <= 2;   // the stairs' cue off, the radio's on

            yield return expect("Find the rifle", 1);
            // Hesitating at the floor boundary (L1 and L2 points every 0.3 s for 3 s): no step churn, never two loops.
            var churnBefore = guide.Changes;
            var maxLoops = 0;
            var l1Point = levelPoint(levels, 0);
            var l2Point = levelPoint(levels, 1);
            for (int i = 0; i < 10; i++)
            {
                var at = i % 2 == 0 ? l1Point : l2Point;
                teleport(at, at + Vector3.forward);
                for (float t = 0; t < 0.3f; t += Time.deltaTime) { maxLoops = Mathf.Max(maxLoops, guideLoops()); yield return null; }
            }
            teleport(l2Point, l2Point + Vector3.forward);
            for (float t = 0; t < 2f; t += Time.deltaTime) { maxLoops = Mathf.Max(maxLoops, guideLoops()); yield return null; }
            var churn = guide.Changes - churnBefore;
            seen.Add($"back and forth across the L1/L2 boundary: {churn} step changes, at most {maxLoops} loops, ends on {guide.CurrentName}");
            ok &= churn <= 2 && maxLoops <= 1 && guide.CurrentName == "Find the rifle";
            // Off its floor (outside the tower): silent; back on L2: heard.
            teleport(_start, levels.TowerArea.center);
            for (float t = 0; t < 1.3f; t += Time.deltaTime) yield return null;   // the floor counts after 0.75 s
            var offFloor = guide.Audible;
            yield return expect("Find the rifle", 1);
            // A cutscene silences it.
            VantageCinematic.Begin();
            for (float t = 0; t < 0.5f; t += Time.deltaTime) yield return null;
            var inCinematic = guide.Audible;
            VantageCinematic.End();
            for (float t = 0; t < 0.5f; t += Time.deltaTime) yield return null;
            var afterCinematic = guide.Audible;
            seen.Add($"outside audible {offFloor}, in a cutscene {inCinematic}, after it {afterCinematic}");
            ok &= !offFloor && !inCinematic && afterCinematic;

            _motor.GetComponent<VantageArsenal>().Unlock("Rifle");
            yield return expect("Find the key", 1);
            VantageInventory.Add("Key");
            yield return expect("Use the key", 1);
            var slot = GameObject.Find("L2_KEYSLOT_InsertKey");
            if (slot != null) slot.GetComponent<VantageInteract>().TryUse();
            yield return expect("none", 1);
            yield return clearLevel(levels, 1);
            yield return expect("L2 gate open", 1);
            yield return expect("none", 2);   // the robot room still locked: the soldiers lead
            var indoor = new[] { "L3_MK_Enemy_Barracks", "L3_MK_Enemy_Comms", "L3_MK_Enemy_Hall" };
            foreach (var e in levels.Enemies.Where(e => e != null && indoor.Any(p => e.name.StartsWith(p))).ToList()) kill(e);
            yield return new WaitForSeconds(1f);
            var robotDoor = VantageDoor.Find("L3_DOOR_RobotRoom_LOCKED");
            var openArea = FindObjectsByType<VantageTrigger>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "TRIGGER_L3_OpenArea_Enter");
            seen.Add($"[robot door locked {(robotDoor != null ? robotDoor.IsLocked.ToString() : "missing")}, open-area trigger {(openArea != null ? openArea.Fired.ToString() : "missing")}, player at {_motor.transform.position}]");
            yield return expect("Robot room open", 2);
            yield return clearLevel(levels, 2);
            yield return expect("L3 cleared", 2);
            yield return expect("Reach the pad", levels.Levels.Count - 1);
            foreach (var e in levels.Enemies.ToList()) kill(e);
            yield return new WaitForSeconds(0.5f);
            levels.ReachHelipad();
            for (float t = 0; t < 3f && !levels.BossSpawned; t += Time.deltaTime) yield return null;
            yield return expect("none", -1);

            var line = string.Join("; ", seen);
            if (ok) log("PASS guide sound ladder: " + line);
            else fail("guide sound ladder: " + line);

            // A death on L2 (the checkpoint reloads the scene): the key goes back on the desk, the guide follows the state.
            VantageRun.Weapons.Add("Pistol");
            VantageRun.Weapons.Add("Rifle");
            VantageRun.IntroPlayed = true;
            VantageRun.Checkpoint = 1;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            yield return null;
            if (!acquire()) yield break;
            levels = VantageTowerLevels.Instance;
            guide = VantageGoalSound.Instance;
            var l2 = levelPoint(levels, 1);
            teleport(l2, l2 + Vector3.forward);
            for (float t = 0; t < 4f; t += Time.deltaTime) yield return null;   // protection, the checkpoint's own setup
            var expected = VantageInventory.Has("Key") ? "Use the key" : "Find the key";
            var deathLine = $"after a death on L2: key held {VantageInventory.Has("Key")}, guide {(guide != null ? guide.CurrentName : "missing")} (expected {expected}), loops {guideLoops()}";
            if (guide != null && guide.CurrentName == expected && guideLoops() <= 1) log("PASS guide sound after a death: " + deathLine);
            else fail("guide sound after a death: " + deathLine);
        }

        /// <summary>A point on the L3 floor ahead of its arrival (on the stairs below it), outside the open-area trigger.</summary>
        private static Vector3 level3Floor(VantageTowerLevels levels)
        {
            var arrival = levels.Levels[2].Arrival;
            var y = levels.Levels[2].FloorY;
            var openArea = FindObjectsByType<VantageTrigger>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "TRIGGER_L3_OpenArea_Enter");
            var area = openArea != null && openArea.TryGetComponent<Collider>(out var c) ? c.bounds : new Bounds();
            var ahead = arrival.forward; ahead.y = 0; ahead.Normalize();
            for (float r = 1f; r < 15f; r += 0.5f)
            {
                var at = arrival.position + ahead * r; at.y = y;
                if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas) && Mathf.Abs(hit.position.y - y) < 0.4f
                    && levels.LevelAt(hit.position) == 2 && (area.size == Vector3.zero || !area.Contains(hit.position)))
                    return hit.position;
            }
            return floorAhead(levels, 2);
        }

        /// <summary>The guide checks from a new run (the full run reaches them after the finale and the death checks).</summary>
        private IEnumerator guideChecksFresh()
        {
            VantageRun.Reset();
            VantageRun.IntroPlayed = true;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            yield return null;
            if (!acquire()) yield break;
            yield return guideChecks(VantageTowerLevels.Instance);
        }

        /// <summary>Guide loops playing now (VantageAudio names its loops "Loop EVENT").</summary>
        private static int guideLoops() =>
            FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.isPlaying && a.volume > 0.001f &&
                (a.name.StartsWith("Loop GUIDE_") || a.name == "Loop " + VantageSounds.KeySlotBeep));
    }
}
#endif
