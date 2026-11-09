# Final Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish VANTAGE: guide sound and menu music, the Sci-Fi Warrior as the L3 heavy robot, the aircraft warning lights removed, unused folders deleted, a cleanup pass, everything verified by the autoplay bot.

**Architecture:** Follows the existing patterns. The guide sound is one runtime component (`VantageGoalSound`) on `VANTAGE Audio` that reads existing game state and plays one `VantageAudio.PlayLoop`. Setup fills its anchors by Blender name. The robot's look comes from a new `VantageEnemyModels.HeavyRobot()` step, the twin of `Boss()`. Removals touch only the dusk tool, one script and folders.

**Tech Stack:** Unity 6000.0.58f2 URP, C#, batch mode (`-executeMethod`), offline `dotnet build` (CLAUDE.md *Workflow* step 3), autoplay bot (`VantageAutoplay.RunBatch`, `-vantageOnly`).

**Spec:** `docs/superpowers/specs/2026-11-09-guide-sound-design.md` (Tasks 2–4). The other tasks come from the user's answers on 2026-11-09: robot ~2.4 m; warning lights removed for good; delete `_Backups`, `_UnusedAssets`, `Assets/AdvancedHelicopterController` and the robot pack's demo files.

## Global Constraints

- Unity must be closed for every batch run; check `Vantage_First-Person/Temp/UnityLockfile`.
- **No git commits.** The user commits (CLAUDE.md: "Commit only when the user asks"). Each task ends with an offline compile and a log check instead.
- Scene copies before automated scene changes go to the session scratchpad (not `_Backups`, which is deleted in Task 6).
- Event name = WAV file name. New events: `GUIDE_PA`, `GUIDE_Stairs`, `GUIDE_Radio`, `GUIDE_Phone`, `KEYSLOT_Beep`, `GUIDE_Wind`, `GUIDE_HeliRadio`, `MUS_Menu` (32 → 40).
- Guide defaults: 3D loop, group `SFX` (`GUIDE_Wind`: `Ambience`), min 2 m, max 30 m, volume 0.8. `MUS_Menu`: 2D loop, `Music`.
- Guide timing: poll 0.25 s; step crossfade 1 s; off-floor fade 0.5 s. Menu music fades out over 1.5 s on Play.
- At most one guide source plays at any time.
- No per-frame `Find*`/`GetComponent`/LINQ/string building; use `VantagePhysics` for masks and heights; one component per file.
- Template and tower-package edits are marked `// VANTAGE change`.
- Bot output lines start with `[Bot]`; our log lines with `[Vantage]`.

## Review Focus

1. **Death reload mid-ladder** (die on L2 holding the key): after the checkpoint restore the guide must match the restored state (the key slot, not the phone). Test added to Task 3 (step 3f).
2. **Player on the stairs between two floors:** the guide must not flap on and off. Expected: `LevelAt` with the stair lead decides; the volume changes at most once per crossing. Test in Task 3 (step 3e).
3. **Skipping the intro, or a cinematic starting while a guide plays** (extraction): silent within 0.5 s, resumes after. Test in Task 3 (step 3e).
4. **Robot model too big for the robot room door or its NavMesh agent:** the robot must still reach the player and be killable with the rifle. Covered by `-vantageOnly robot` in Task 5.
5. **The title-screen music after "play again"** (Enter on the end screen): the menu doesn't show again in the session, so `MUS_Menu` must not play and `VantageMusic` must start at once. Test in Task 4.

---

### Task 1: Remove the aircraft warning lights and the unused packs

**Files:**
- Modify: `Assets/Vantage/Editor/VantageDusk.cs:105-199` (drop the block from `// Aircraft-warning lights` to its log part; the log line becomes `Dusk lights: n pole lights (m floodlights within PoleReach m).`; drop `glowMaterial("Warning", …)` if nothing else uses it)
- Delete: `Assets/Vantage/Scripts/VantageBlinkLights.cs` (+ `.meta`), `Assets/Vantage/World/M_Glow_Warning.mat` (+ `.meta`) if present
- Delete: `Assets/AdvancedHelicopterController/` (+ `.meta`)
- Delete: `Assets/SciFiWarriorPBRHPPolyart/Mesh/Stage.fbx`, `Materials/Stage.mat`, `Materials/Skybox_Mat.mat`, `promotion.jpg` (+ their `.meta`). Keep the HP and Polyart variants (the prefabs reference them).

- [ ] **Step 1:** Check nothing else references what's deleted: `grep -rn "VantageBlinkLights\|M_Glow_Warning\|AHC_\|AdvancedHelicopter" Assets --include=*.cs --include=*.unity --include=*.prefab --include=*.asset`. Expected: matches only in the deleted files and `VantageDusk.cs`.
- [ ] **Step 2:** Make the edits and deletions above.
- [ ] **Step 3:** Offline compile (CLAUDE.md *Workflow* 3; drop `AdvancedHelicopterController/**` from the scratch csproj if it was globbed). Expected: 0 errors.
- [ ] **Step 4:** Copy `SampleScene.unity` to the scratchpad. Run `Vantage.EditorTools.VantageDusk.ApplyBatch`. Expected log: `[Vantage] Dusk lights: … pole lights …` with no warning-light part. `grep -c "Aircraft Warning" Assets/Scenes/SampleScene.unity` → 0.

### Task 2: Guide sound events and the synthesised fallback

**Files:**
- Modify: `Assets/Vantage/Scripts/VantageSounds.cs` (8 constants: `GuidePa`, `GuideStairs`, `GuideRadio`, `GuidePhone`, `KeySlotBeep`, `GuideWind`, `GuideHeliRadio`, `MusMenu`; add them to `All`)
- Modify: `Assets/Vantage/Editor/VantageAudioSetup.cs:26-60` (`Defaults` rows per Global Constraints)
- Modify: `Assets/Vantage/Scripts/VantageProceduralAudio.cs:12` (add `Kind.GuidePulse`: a soft two-tone pulse, ~1.2 s period, quiet)

**Interfaces:**
- Produces: `VantageSounds.GuidePa … MusMenu` (string constants); `VantageProceduralAudio.Create(Kind.GuidePulse, seed) → AudioClip`.

- [ ] **Step 1:** In the bot's `audioChecks`, add a check: `VantageSounds.All.Length == 40` and each new name has a bank entry with the default spatial/loop/group. Run `-vantageOnly audio`. Expected: FAIL on the new check.
- [ ] **Step 2:** Add the constants, defaults and `Kind.GuidePulse`.
- [ ] **Step 3:** Compile offline, run `Vantage.EditorTools.VantageAudioSetup.RefreshBatch`. Expected: `Sound bank: 0 / 40 events filled`. Run `-vantageOnly audio`. Expected: all `[Bot] PASS`.

### Task 3: `VantageGoalSound`, its anchors and the `guide` bot group

**Files:**
- Create: `Assets/Vantage/Scripts/VantageGoalSound.cs`
- Modify (read-only state, no behaviour change): `Assets/Vantage/Scripts/VantageKeySlot.cs` (`public bool Used { get; private set; }`, set when the key is turned); `Assets/VANTAGE_Tower/Scripts/VantageTrigger.cs` (`public bool Fired => fired;` with `// VANTAGE change`)
- Modify: `Assets/Vantage/Editor/VantageTower.cs` (Setup step after the audio object exists: add `VantageGoalSound` to `VANTAGE Audio` if missing, fill `Steps[i].Anchor` by name, log `Guide sound: n / 9 anchors (missing: …)`, a FAIL line per missing anchor)
- Modify: `Assets/Vantage/Scripts/Testing/VantageAutoplayBot.cs` (new `case "guide": yield return guideChecks(levels);`, and call `guideChecks` in the full run after the audio checks)

**Interfaces:**
- Consumes: Task 2's constants and `Kind.GuidePulse`; `VantageAudio.PlayLoop(string, Transform, AudioClip) → AudioSource`, `VantageAudio.Stop(AudioSource, float fade)`; `VantageTowerLevels.Instance.Current/LevelAt(Vector3)/BossSpawned`; `VantageAlarm.Instance.Active`; `VantageInventory.Has("Key")`; `VantageDoor.Find(name).IsLocked`; `VantageFloorGate.IsOpen`; `VantageCinematic.Active`; `VantageMainMenu.Instance.Showing`.
- Produces:
  - `[Serializable] public class Step { public string Name; public string Event; public Transform Anchor; }`, `public List<Step> Steps` (9 rows in the spec's order and names)
  - `public int Current` (−1 = none), `public string CurrentName`, `public bool Audible`, `public Transform Anchor`, `public int Changes`, `public static VantageGoalSound Instance`, `public AudioSource Source` (for the bot)
  - `public static int StepFor(…)` is **not** needed. The ladder is a private `int evaluate()` with one `switch`/`if` chain mirroring the spec's table.

- [ ] **Step 1 (3a): write `guideChecks`** with these assertions (exact step names from the spec table: `L1 lockdown`, `L1 cleared`, `Find the rifle`, `Find the key`, `Use the key`, `L2 gate open`, `Robot room open`, `L3 cleared`, `Reach the pad`):
  - Before the alarm: `Current == -1`.
  - The alarm raised (existing helper) → `CurrentName == "L1 lockdown"`, `Source` within 1 m of `MK_Turret_Hallway`, `Audible`.
  - L1 cleared (existing `clearLevel(levels, 0)`) → within 0.5 s `"L1 cleared"`; the player teleported onto L2 → `"Find the rifle"`.
  - Rifle unlocked (existing pickup helper) → `"Find the key"`; key taken → `"Use the key"`; slot used → `"L2 gate open"`.
  - Count of `AudioSource`s playing a `GUIDE_*`/`KEYSLOT_Beep` clip (or the pulse) is ≤ 1 at every step.
  - `Changes` increases by exactly 1 per step change.
- [ ] **Step 2 (3b):** L3 and the finale: on L3 with the robot-room door open → `"Robot room open"`, near `LVL3_BreachEdges`; after `TRIGGER_L3_OpenArea_Enter` fired → `Current == -1`; after L3 clear, on L3 → `"L3 cleared"`; on the finale before the boss → `"Reach the pad"`; after `BossSpawned` → −1.
- [ ] **Step 3 (3e):** Floor gating and silence (Review Focus 2, 3): during `"Find the rifle"`, put the player on L1 → `Audible == false` within 0.6 s; back on L2 → `true`. Walk the player up the L1→L2 flight in 10 steps and count `Audible` flips (≤ 1). Start a cinematic (`VantageCinematic.Begin`, as the existing cinematic checks do) → `Audible == false` within 0.5 s; `End` → `true`.
- [ ] **Step 4 (3f): death mid-ladder** (Review Focus 1): holding the key on L2, kill the player (existing `respawnIsSafe`-style helper), wait for the reload → `CurrentName == "Use the key"`.
- [ ] **Step 5:** Run `-vantageOnly guide`. Expected: FAIL (`VantageGoalSound` missing).
- [ ] **Step 6:** Implement `VantageGoalSound`. A step change stops the old loop (`Stop(source, 1f)`) and starts the new one with `PlayLoop(step.Event, step.Anchor, pulse)`. The pulse clip is made once in `Awake` from `Kind.GuidePulse`. Floor gating uses `LevelAt(player) == LevelAt(anchor)`; for a stair anchor (step names ending in "cleared" or "gate open") use the floor below. The audible volume eases to `bank volume` or 0. Silent when `VantageCinematic.Active`, the menu is showing or the player is dead. Add the two read-only properties and the Setup step.
- [ ] **Step 7:** Compile offline. Copy the scene to the scratchpad, run `SetupBatch`. Expected: `[Vantage] Guide sound: 9 / 9 anchors`, `Door check: n ok, 0 FAIL`, `Coins: 5`.
- [ ] **Step 8:** Run `-vantageOnly guide`. Expected: all `[Bot] PASS`.

### Task 4: Menu music

**Files:**
- Modify: `Assets/Vantage/Scripts/VantageMainMenu.cs` (start `PlayLoop(MusMenu, null)` when it shows; `Play()` → `Stop(source, 1.5f)`)
- Modify: `Assets/Vantage/Scripts/VantageMusic.cs:51-65` (move the `PlayLoop`/`PlayScheduled` start into a coroutine that waits while `VantageMainMenu.Instance != null && Instance.Showing`)
- Modify: `VantageAutoplayBot.cs` (title-screen checks in the existing menu block)

- [ ] **Step 1:** Bot assertions in the title-screen block:
  - While showing: `VantageAudio.Plays("MUS_Menu") == 1`, `Plays("MUS_Explore") == 0`.
  - 2 s after `Play()`: the menu source is stopped (null or `!isPlaying`), and `Plays("MUS_Explore") == 1`.
  - After "play again" (existing end-screen reload; Review Focus 5): `Plays("MUS_Menu") == 0` and `Plays("MUS_Explore") == 1` within 1 s of the load.
- [ ] **Step 2:** Run the full bot once without `-vantageOnly`, or the menu path. Expected: FAIL on the new assertions.
- [ ] **Step 3:** Implement. Compile offline.
- [ ] **Step 4:** Run `-vantageOnly audio` and `-vantageOnly intro` (the menu runs in both). Expected: all PASS, including the existing "music layers in step" check.

### Task 5: The Sci-Fi Warrior as the L3 heavy robot

**Files:**
- Modify: `Assets/Vantage/Editor/VantageEnemyModels.cs`:
  - New constants: `RobotModel = "Assets/SciFiWarriorPBRHPPolyart/Prefabs/PBRCharacter.prefab"`, `RobotPack = "Assets/SciFiWarriorPBRHPPolyart"`, `RobotHeight = 2.4f`
  - New `public static void HeavyRobot()` plus a menu item next to the boss's
  - New private `robotMaterial()` → `Assets/Vantage/Characters/SciFi_Warrior/M_SciFi_Warrior_URP.mat`
  - New private `robotController()` → `…/SciFi_Warrior/HeavyRobot.controller`
  - `ShotsBatch` adds the robot beside the boss
- Modify: `Assets/Vantage/Editor/VantageTower.cs` (call `VantageEnemyModels.HeavyRobot()` right after `Boss()`)
- Modify: `Assets/Vantage/Scripts/Testing/VantageAutoplayBot.cs` (`robotRifleOnly`: assert `robot.Animator != null` and the model's renderer height is 2.4 ± 0.1 m)

**Interfaces:**
- Consumes: the existing private helpers `heightOf`, `boundsIn`, `find`, `ensureFolder` in the same file.
- Produces: `HeavyRobot.prefab` with `Model` = the Sci-Fi Warrior, scaled to 2.4 m, and `VantageHeavyRobot.Animator` set. `Eye` and `EyeLight` go on the `Head` bone (humanoid `Animator.GetBoneTransform(HumanBodyBones.Head)`). `Muzzle` goes at the front of the gun: the renderer bounds' front-most point along the model's forward, at right-hand height; if the pack's gun isn't a separate renderer, use the right hand plus 0.5 m forward. The `CapsuleCollider` and `NavMeshAgent` height become 2.4 m, and the radius is kept.
- Material: URP Lit copied from `Materials/PBR.mat`: albedo `_MainTex`, normal `PBR_Free_NM`, metallic/smoothness `PBR_Free_MS`, occlusion `PBR_Free_AO`, emission `PBR_Free_EM` (emission on, white × 1).
- Controller: parameters `Speed` (float) and `Die` (trigger), the same contract as `Boss.controller`. Idle = `Idle_Shoot_ar` (gun raised), walk = `WalkFront_Shoot_ar`, death = `Die`, from any state on `Die`. Idle ↔ walk at `Speed` > 0.3 / < 0.2. Idle and walk loop.
- Idempotent like `Boss()`: the old `Model` child is replaced; the gameplay values on `VantageHeavyRobot` are kept. Log line: `[Vantage] Heavy robot model: Sci-Fi Warrior, 2.40 m tall (scale s), n animations.`

- [ ] **Step 1:** Add the bot assertions. Run `-vantageOnly robot`. Expected: FAIL (placeholder: no Animator).
- [ ] **Step 2:** Implement `HeavyRobot()` and call it in Setup.
- [ ] **Step 3:** Compile offline. Copy the scene to the scratchpad. Run `SetupBatch`, then `VantageEnemyModels.ShotsBatch -vantageShots <scratch>/shots`. Expected: the log line above. In the screenshot the robot is not pink, faces forward and holds its gun (inspect the image).
- [ ] **Step 4:** Run `-vantageOnly robot`. Expected: all PASS, including the robot-room path/softlock check and pistol-bounces / rifle-hurts (Review Focus 4).

### Task 6: Cleanup pass, full verification, docs, deletions

**Files:**
- Modify: any `Assets/Vantage/**/*.cs` the review touches (behaviour unchanged)
- Delete: `Assets/Vantage/World/Sky_GoldenHour.mat` (unused per CLAUDE.md), and any other `Assets/Vantage/**` asset with no dependants (check with `AssetDatabase.GetDependencies` from a small batch report, not by guess)
- Delete (last, after the full run passes): `_Backups/`, `_UnusedAssets/` (workspace root)
- Modify: `CLAUDE.md`, `README.md`, `VANTAGE — Level & World Design Concept.md` (*Revisions*), `Audio_Production/README.md`, `WORKLOG.md`, `.gitignore` (drop the `_Backups`/`_UnusedAssets` lines only if nothing else needs them)

- [ ] **Step 1: Code review for cleanup.** Run the `simplify` skill on `Assets/Vantage/Scripts` and `Assets/Vantage/Editor`. Apply only: dead code, unused fields/usings, duplicate helpers that already exist in `VantagePhysics`/`VantageUI`/`VantageEditorUtil`, and per-frame allocations or lookups. No gameplay changes, no template edits. List every change in the worklog.
- [ ] **Step 2:** Unused-asset report: a batch method in `VantageEditorUtil` (`UnusedAssetsBatch`) that logs every asset under `Assets/Vantage` not in `GetDependencies(SampleScene.unity, true)`, excluding `Editor/`, `Scripts/` and `Testing/`. Delete only `.mat`/`.asset`/`.prefab`/textures on that list that no tool loads by path (grep the path string in `Assets/Vantage/Editor` first). Remove the batch method afterwards if it isn't worth keeping. `Assets/Textures` is the user's: report it, don't delete it.
- [ ] **Step 3:** Compile offline. Copy the scene to the scratchpad. Run `SetupBatch`. Expected: `Door check: n ok, 0 FAIL`, `Coins: 5`, 4 levels, `Guide sound: 9 / 9`, the robot model line.
- [ ] **Step 4:** Full `VantageAutoplay.RunBatch -vantageShots <scratch>/shots` (8-minute timeout). Expected: `ALL PASSED`. On a known flaky FAIL ("alerted soldiers stay on their level", see Known gaps), rerun once with another seed and report both runs.
- [ ] **Step 5: Docs.**
  - CLAUDE.md: code map (`VantageGoalSound`, robot model step, no `VantageBlinkLights`), Scene table (Dusk Lights without warning lights), Assets table (Sci-Fi Warrior row; AdvancedHelicopterController removed; robot no longer a placeholder), Workflow step 2 (scratch copy or git instead of `_Backups`), Repository (no `_Backups`/`_UnusedAssets`), event count 40, Known gaps, History.
  - README: sound section, robot, asset table.
  - Concept doc Revisions row: "2026-11-09: sound as a wayfinding layer (teacher feedback): one diegetic goal sound at a time; Sci-Fi Warrior heavy robot; warning lights removed".
  - `Audio_Production/README.md`: the 8 WAVs with a one-line brief each, plus the listening checklist items (guide loudness next to combat music; heard through floors at the stairs).
- [ ] **Step 6:** Delete `_Backups/` and `_UnusedAssets/` (`rm -rf`, workspace root; confirm both paths first). Add the WORKLOG entry (Asked / Done / Verified / Open).
