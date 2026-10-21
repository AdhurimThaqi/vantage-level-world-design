# Sentries, Warning Ring and Intro Cutscene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two sentries patrol the outside of the tower's top (broken slab, helipad platform) and enforce a warning ring around the tower plot; a skippable intro cutscene shows them and the helipad at the start of a run.

**Architecture:** A static `VantageCinematic` owns every camera takeover (intro and extraction). `VantageBoundary` turns the player's position into a zone (Inside/Free/Warning/Kill) that the HUD, minimap and sentries read. `VantageSentries` spawns `Soldier.prefab` copies with a `VantageSentry` brain on routes that the editor tool `VantageSentryRoutes` traces from the baked NavMesh. `VantageIntro` flies the camera through four shots built from scene objects.

**Tech Stack:** Unity 6000.0.58f2, URP, C#, the ThirdPersonCoverShooter template (`FighterBrain`, `CharacterMotor`, `Hit`), Unity AI Navigation, uGUI + TextMeshPro via `VantageUI`. Tests: the autoplay bot (`Assets/Vantage/Scripts/Testing/VantageAutoplayBot.cs`) in batch Play mode.

**Spec:** `docs/superpowers/specs/2026-10-19-sentries-ring-intro-design.md`

## Global Constraints

- Follow `CLAUDE.md` *Workflow for changes*: Unity closed (check `Vantage_First-Person/Temp/UnityLockfile`), scene backed up to `_Backups/SampleScene_before_sentries_2026-10-21.unity` before the first scene change, offline `dotnet build` of the scratch csproj before every batch run.
- Batch commands (paths absolute, logs into the session scratchpad):
  - Setup: `Unity.exe -batchmode -projectPath <proj> -executeMethod Vantage.EditorTools.VantageTower.SetupBatch -logFile <scratch>/setup.log`
  - Bot: `Unity.exe -batchmode -projectPath <proj> -executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch [-vantageOnly <group>] [-vantageSeed N] [-vantageShots <dir>] -logFile <scratch>/bot.log` (exit 0 = no FAIL). Read lines starting `[Bot]` and `[Vantage]`.
- **No git commits.** CLAUDE.md: commit only when the user asks. Each task ends with a WORKLOG note instead.
- One component per file. No `Find*`, `GetComponent`, LINQ or string building per frame. Layers and heads via `VantagePhysics.Solid/Sight/Eye()/AimPoint()`. Damage via `SendMessage("OnHit", Hit)`. Alive via `CharacterMotor.IsAlive`. Never rename Blender objects or spawned level enemies.
- Ring defaults (spec): free ≤ 15 m past the plot, warning 15–35 m, kill beyond 35 m or after 5 s continuous in the warning band; hit damage 25, one shot per ~1.2 s per sentry; warning misses land 1–3 m from the player. Plot = tower renderers' x/z bounds (helipad included) + 8 m.
- Sentry defaults (spec): names `SENTRY_Slab`, `SENTRY_Helipad`; route 3–4 points, inset 1.5 m; end pause ~3 s with a ±60° outward scan; leash 6 m; same-level engage range 32 m; think every 0.25 s; sight checks at most every 0.2 s.
- Intro timing (spec): shots at 0–3.5, 3.5–7, 7–9.5, 9.5–13 s; captions "VANTAGE", "Sentries watch the ground", "Your way out: the helipad"; skip on any key or mouse button after 0.5 s.
- Every new sound is a public `AudioClip` field; when null, use `VantageProceduralAudio.Create(...)` or a short synthesized fallback (the user will supply SoundCrea clips).
- HUD text: single spaces only (Bebas widens word spacing), see `VantageUI`.

## Review Focus

1. **The bot's own run must not be disturbed.** The intro plays on a fresh run and the bot starts 2 s after load; sentries fire on anyone in the ring and fight on L3/finale. Expected: existing checks unaffected. Test: Task 5 step "existing suite" (the bot skips the intro after its intro check; ring checks restore the player inside the tower).
2. **Re-entering the free zone resets the grace timer**, and a dead player or a cinematic stops all ring fire at once. Test: Task 2 `ringGraceResets`, Task 4 `ringQuietInCinematic`.
3. **A sentry route region that is missing or tiny** (re-export, NavMesh change): Setup must log FAIL for that route and the game must run without that sentry. Test: Task 3 step "missing region" (temporarily require 9999 m² and read the log).
4. **Skipping the intro on its very first frame or after it already ended** must leave input, damage and `ThirdPersonCamera` exactly as before (no double restore). Test: Task 5 `introSkipRestoresControl` (skip at 0.6 s, then call `End()` again and check nothing changes).
5. **Ring hits while the player stands in cover or behind a wall**: shots need line of sight from the sentry's eye. Test: Task 4 `ringNeedsSight` (player in the kill zone behind a solid object takes no hits in 4 s).

---

### Task 1: Shared camera takeover (`VantageCinematic`) and the bot's focused mode

**Files:**
- Create: `Vantage_First-Person/Assets/Vantage/Scripts/VantageCinematic.cs`
- Modify: `Vantage_First-Person/Assets/Vantage/Scripts/VantageEvents.cs` (two events)
- Modify: `Vantage_First-Person/Assets/Vantage/Scripts/VantageExtraction.cs:89-104` (`freezePlayer` uses the helper)
- Test: `Vantage_First-Person/Assets/Vantage/Scripts/Testing/VantageAutoplayBot.cs`

**Interfaces:**
- Produces: `public static class VantageCinematic { public static bool Active { get; } public static void Begin(); public static void End(); }`; `VantageEvents.CinematicStarted`, `VantageEvents.CinematicEnded` (`event Action`) with `RaiseCinematicStarted()`, `RaiseCinematicEnded()`; bot switch `-vantageOnly <group>` that runs only the named group's checks and then `finish()`.

- [ ] **Step 1: Write the failing check** `cinematicRoundTrip()` in the bot, group `cinematic`:
  ```csharp
  // before: input on, damage on, ThirdPersonCamera on, VantageCameraFader on
  VantageCinematic.Begin();
  // during: all four off, Active == true, CinematicStarted raised once
  VantageCinematic.End(); VantageCinematic.End();   // second End changes nothing
  // after: all four back to their "before" values, Active == false, CinematicEnded raised once
  log("PASS cinematic begin/end restores control: ...") or fail(...)
  ```
  Add the `-vantageOnly` switch at the top of `Start()` after `acquire()`: `cinematic`, `ring`, `sentries`, `intro` (later tasks add theirs).
- [ ] **Step 2: Offline build fails** (`VantageCinematic` undefined). Run the scratch `dotnet build`; expected: error CS0103.
- [ ] **Step 3: Implement `VantageCinematic`**: `Begin()` records which of `ThirdPersonInput`, `CharacterHealth.IsTakingDamage`, `ThirdPersonCamera`, `VantageCameraFader` it changed (only those that were on/true) and turns them off; `End()` restores only what it recorded, clears the record; both no-ops when already in that state. Find the player via `VantageEvents.ActivePlayer()` and the camera via `Camera.main` (called once per Begin, not per frame). Reset the static state in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` like `VantageRun.clearOnPlay`.
- [ ] **Step 4: Replace `VantageExtraction.freezePlayer`'s input/health/camera code with `VantageCinematic.Begin()`**; keep its `_camFrom/_camTo` setup.
- [ ] **Step 5: Run** offline build (0 errors), then the bot with `-vantageOnly cinematic`. Expected: `[Bot] PASS cinematic begin/end restores control`, exit 0.
- [ ] **Step 6: Run the full bot once** (no `-vantageOnly`). Expected: the finale/extraction checks still PASS, ALL PASSED.
- [ ] **Step 7: WORKLOG note** (Task 1 done, what ran). No commit.

### Task 2: The warning ring (`VantageBoundary`) with HUD and minimap feedback

**Files:**
- Create: `Vantage_First-Person/Assets/Vantage/Scripts/VantageBoundary.cs`
- Modify: `Vantage_First-Person/Assets/Vantage/Scripts/VantagePlayerHUD.cs` (warning line; hide everything while `VantageCinematic.Active`)
- Modify: `Vantage_First-Person/Assets/Vantage/Scripts/VantageMinimap.cs` (free-zone edge line, warning band tint; hidden during cinematics)
- Modify: `Vantage_First-Person/Assets/Vantage/Editor/VantageTower.cs` (new step `boundary(tower, setup)` after `extraction(...)` in `Setup()`)
- Test: `VantageAutoplayBot.cs` group `ring`

**Interfaces:**
- Consumes: `VantageCinematic.Active`, `VantageEvents.CinematicStarted/Ended` (Task 1); `VantageTowerLevels.Instance.LevelAt(Vector3)`.
- Produces:
  ```csharp
  public class VantageBoundary : MonoBehaviour {
      public enum Zone { Inside, Free, Warning, Kill }
      public static VantageBoundary Instance { get; }
      public Rect Plot;                       // x/z, set by Setup
      public float FreeDistance = 15f, WarningDistance = 35f, GraceSeconds = 5f;
      public float OutsidePlot(Vector3 world);        // 0 inside the rect
      public Zone ZoneAt(Vector3 world);              // geometry only; Inside when LevelAt(world) >= 0
      public Zone PlayerZone { get; }                 // with the grace rule; Free while not Enforcing
      public bool Enforcing { get; }                  // false during a cinematic or while the player is dead
      public event System.Action<Zone> PlayerZoneChanged;
  }
  ```
  `PlayerZone` updates every 0.2 s (not per frame).

- [ ] **Step 1: Write the failing checks** (group `ring`), each teleporting the player with the bot's `teleport()` to points computed from `VantageBoundary.Instance.Plot` (centre of the south edge pushed out by 5, 25 and 45 m, snapped to the NavMesh/terrain), then back to the entrance:
  - `ringZones`: zones are Free, Warning, Kill at those points and Inside at the L1 arrival.
  - `ringGraceResets`: in the warning band `PlayerZone` is Warning at 4 s and Kill at 5.5 s; step back to Free for 1 s, return: Warning again at 4 s.
  - `ringHudWarning`: in the warning band the HUD's warning text is visible (expose `VantagePlayerHUD.WarningVisible` as a read-only bool); in the free zone it is not. Save `hud_warning` with `hudSnapshot`.
- [ ] **Step 2: Offline build fails** (`VantageBoundary` undefined).
- [ ] **Step 3: Implement `VantageBoundary`** to the interface above. Grace: track continuous time in Warning; reaching `GraceSeconds` reports Kill until the player reaches Free (Inside also resets).
- [ ] **Step 4: Setup step `boundary(tower, setup)`**: object `Boundary` under `VANTAGE Tower Setup` with `VantageBoundary`; `Plot` = x/z of all renderers under the level model (as `levelManager` gathers `LVL*` and `FINAL_*`) expanded by 8 m. Log `[Vantage] Boundary: plot {Plot}, free {FreeDistance} m, warning {WarningDistance} m`.
- [ ] **Step 5: HUD**: subscribe to `PlayerZoneChanged`; Warning/Kill show `SENTRIES ABOVE: GET BACK TO THE TOWER` in the existing notice style (red), flashing for 1.5 s the first time per run; hide the whole HUD canvas while `VantageCinematic.Active`. Minimap: draw the free-zone rectangle edge (Plot expanded by `FreeDistance`) as a thin line and tint the band out to `WarningDistance` red at ~15 % alpha, using the minimap's existing world-to-map mapping; build once, not per frame.
- [ ] **Step 6: Run** backup scene → `SetupBatch` (log shows the Boundary line, door check 0 FAIL, 5 coins) → bot `-vantageOnly ring -vantageShots <dir>`. Expected: three PASS lines; look at `hud_warning.png`.
- [ ] **Step 7: WORKLOG note.** No commit.

### Task 3: Sentry routes from the NavMesh and the spawner

**Files:**
- Create: `Vantage_First-Person/Assets/Vantage/Editor/VantageSentryRoutes.cs`
- Create: `Vantage_First-Person/Assets/Vantage/Scripts/VantageSentries.cs`
- Modify: `Vantage_First-Person/Assets/Vantage/Editor/VantageTower.cs` (`Setup()`: after `VantageSetup.BakeNavMesh()`, call `sentries(tower, group(setup, "Sentries"), levels)`)
- Modify: `Vantage_First-Person/Assets/Vantage/Scripts/VantageRun.cs` (`DeadSentries`)
- Test: `VantageAutoplayBot.cs` group `sentries`

**Interfaces:**
- Produces:
  ```csharp
  // Editor
  public static class VantageSentryRoutes {
      // Largest connected piece of the triangles that pass `include`, its outer boundary loop resampled to `points`
      // points, each moved `inset` m toward the piece's centroid and snapped to the NavMesh (2 m). Empty list if none.
      public static List<Vector3> Trace(NavMeshTriangulation tri, System.Func<Vector3, bool> include, int points, float inset, float minArea);
  }
  // Runtime
  public class VantageSentries : MonoBehaviour {
      [System.Serializable] public class Route { public string Name; public int Level; public List<Vector3> Points = new List<Vector3>(); }
      public GameObject SoldierPrefab;
      public List<Route> Routes = new List<Route>();
      public static IReadOnlyList<VantageSentry> All { get; }   // live and dead, this scene
  }
  // VantageRun
  public static readonly HashSet<string> DeadSentries;      // cleared in Begin()
  ```
  `Route.Level`: index into `VantageTowerLevels.Levels` (Slab = the Destroyed floor, 2; Helipad = the finale, `Levels.Count - 1`).

- [ ] **Step 1: Write the failing check** `sentriesSpawnAndWalk()` (group `sentries`): both `SENTRY_Slab` and `SENTRY_Helipad` exist (`VantageSentries.All`), each within 1 m of the NavMesh, each has moved ≥ 5 m along its route within 15 s. Log route lengths.
- [ ] **Step 2: Offline build fails** (`VantageSentries` undefined).
- [ ] **Step 3: Implement `VantageSentryRoutes.Trace`**. Algorithm (not determined by the signature):
  1. Keep triangles whose three vertices pass `include`; weld vertices within 0.05 m.
  2. Connected components by shared vertices; keep the largest by area; empty if its area < `minArea` (default 12 m²).
  3. Boundary edges = edges used by one kept triangle; chain them into loops; take the longest loop.
  4. Keep the loop's vertices whose distance from the tower's x/z centre is at least the loop's median (the outer rim, facing away from the tower), in loop order.
  5. Resample that polyline to `points` evenly spaced points, move each `inset` m toward the component centroid, `NavMesh.SamplePosition` within 2 m.
- [ ] **Step 4: Setup step `sentries(...)`**: `include` for **Slab** = vertex within 1 m of `LevelHeights(tower)[2]` and outside the x/z bounds of `LVL2_FloorSlab` (the building's footprint) and outside `levels.FinaleArea`; for **Helipad** = vertex inside `levels.FinaleArea` and within 1 m of the top of `FINAL_Platform`'s renderer bounds. 4 points, inset 1.5 m. Store both on a new `VantageSentries` (with `SoldierPrefab = levels.SoldierPrefab`). Log `[Vantage] Sentry route Slab: 4 points, 31.2 m` or `[Vantage] Sentry route Helipad: FAIL no NavMesh`.
- [ ] **Step 5: Implement `VantageSentries.Start()`**: for each route with ≥ 2 points and a name not in `VantageRun.DeadSentries`, instantiate `SoldierPrefab` at `Points[0]` named `SENTRY_<Name>`, `Destroy` its `VantageEnemyAggression`, add `VantageSentry` (Task 4; until then a stub with `public void Init(VantageSentries.Route route)` that only stores the route) and call `Init`. Add the instance to `All`. Draw routes with `OnDrawGizmos`.
- [ ] **Step 6: Missing-region check** (Review Focus 3): temporarily pass `minArea: 9999` for Helipad, run `SetupBatch`, expect the FAIL log line and no exception; revert.
- [ ] **Step 7: Run** `SetupBatch` (both routes logged with lengths) → bot `-vantageOnly sentries`. Expected at this task: spawn PASS; "walk 5 m" may FAIL until Task 4 (record it).
- [ ] **Step 8: WORKLOG note.** No commit.

### Task 4: The sentry brain (`VantageSentry`): patrol, scan, leash, ring shots, death

**Files:**
- Create (replace the stub): `Vantage_First-Person/Assets/Vantage/Scripts/VantageSentry.cs`
- Test: `VantageAutoplayBot.cs` groups `sentries`, `ring`

**Interfaces:**
- Consumes: `VantageSentries.Route` (Task 3), `VantageBoundary` (Task 2), `VantageCinematic.Active` (Task 1), `VantageRun.DeadSentries`.
- Produces:
  ```csharp
  [RequireComponent(typeof(FighterBrain))]
  public class VantageSentry : MonoBehaviour {
      public void Init(VantageSentries.Route route);
      public float ScanAngle = 60f, EndPause = 3f, Leash = 6f, EngageRange = 32f, ThinkInterval = 0.25f;
      public float ShotInterval = 1.2f, ShotDamage = 25f, GlintTime = 0.35f;
      public Vector2 MissRadius = new Vector2(1f, 3f);
      public AudioClip WarningCall, RadioLoop, ShotCrack;   // null = synthesized fallback
      public bool IsDead { get; }
      public float DistanceFromRoute { get; }               // x/z distance to the route polyline
      public int WarningShots { get; }                      // counters for the bot
      public int Hits { get; }
  }
  ```

- [ ] **Step 1: Write the failing checks**:
  - `sentriesStayOnRoute` (group `sentries`): sample both every 0.5 s for 30 s; max `DistanceFromRoute` ≤ 6 m and `LevelAt` of each sentry never changes.
  - `ringFreeIsQuiet` (group `ring`): player 8 s in the free zone; `WarningShots + Hits` unchanged for both sentries, no damage taken.
  - `ringWarningThenHits` (group `ring`): player in the warning band facing a sentry with sight (pick the point on the band with a clear `VantagePhysics.Sight` line to `SENTRY_Slab`'s eye); within 0–5 s `WarningShots ≥ 1` and damage taken = 0; by 7 s damage > 0.
  - `ringKillZone` (group `ring`): beyond 35 m with sight, damage > 0 within 3 s; teleport to the free zone, no further damage over 4 s.
  - `ringNeedsSight` (Review Focus 5): kill zone behind a solid collider between the player and both sentries' eyes; no damage in 4 s.
  - `ringQuietInCinematic` (Review Focus 2): kill zone, `VantageCinematic.Begin()`; no new shots in 3 s; `End()`.
  - `sentriesNotInLocks` (group `sentries`): `VantageTowerLevels.Remaining` and the L3 exit / finale conditions never count `SENTRY_*` (kill L3 and finale enemies with `kill()`, sentries alive: `L3_DOOR_Exit_To_Final` unlocks and the boss can spawn).
  - `sentryStaysDead` (group `sentries`): `kill()` `SENTRY_Slab`, trigger a death reload as `checkpointRestore` does; after reload no `SENTRY_Slab`, `SENTRY_Helipad` present.
- [ ] **Step 2: Run the bot** `-vantageOnly sentries` then `-vantageOnly ring`; expected: these new checks FAIL (stub does nothing).
- [ ] **Step 3: Implement `VantageSentry`** (decision order every `ThinkInterval`):
  1. Dead (`CharacterMotor.IsAlive` false): once, add its name to `VantageRun.DeadSentries`, stop.
  2. Boundary `Enforcing` and `PlayerZone` is Warning or Kill and sight (≤ every 0.2 s, `Physics.Linecast` from `VantagePhysics.Eye` to `VantagePhysics.AimPoint`, mask `VantagePhysics.Sight`): stop moving (`_brain.Message("ToStopMoving")` as the template brains do), face the player, and on the shot timer fire a ring shot: glint (a small emissive quad or light at the eye for `GlintTime`), then a tracer (`LineRenderer`, 0.08 s) to the impact point, crack sound. Warning (not Kill): impact = a ground point `MissRadius` from the player on the side facing the sentry, dust puff; `WarningShots++`. Kill: `new Hit(point, -dir, ShotDamage, gameObject, playerCollider, HitType.Rifle, 0)` sent with `SendMessage("OnHit", hit)` to the collider the ray hit, as `VantageDrone` does at `VantageDrone.cs:379`; `Hits++`. First warning per run: play `WarningCall`.
  3. Player on `Route.Level` (`LevelAt`) and in sight within `EngageRange`: `_brain.ToAttack(player); _brain.ToOpenFire();` re-issued at most every 3 s.
  4. `DistanceFromRoute > Leash`: `_brain.Message("ToWalkTo", nearest route point)`.
  5. Patrol: back and forth along the route with `_brain.Message("ToWalkTo", p)` + `ToFaceWalkDirection`; at either end pause `EndPause` s and turn the body ±`ScanAngle`° away from the tower centre and back.
  Brain setup in `Init`: as `VantageEnemyAggression.Awake` (idle start, no retreat, cover distance 0). `RadioLoop` on a 3D `AudioSource` (fallback `VantageProceduralAudio.Kind.RadioStatic`).
- [ ] **Step 4: Run** `-vantageOnly sentries`, `-vantageOnly ring`. Expected: all new checks PASS.
- [ ] **Step 5: WORKLOG note.** No commit.

### Task 5: The intro cutscene (`VantageIntro`) and the full verification

**Files:**
- Create: `Vantage_First-Person/Assets/Vantage/Scripts/VantageIntro.cs`
- Modify: `VantageRun.cs` (`IntroPlayed`, cleared in `Begin()`)
- Modify: `VantagePlaytestLogger.cs` (intro watched / skipped at n s)
- Modify: `Editor/VantageTower.cs` (step `intro(tower, setup)`: object `Intro` with `VantageIntro`, references to `FINAL_Helipad` and the tower root)
- Test: `VantageAutoplayBot.cs` group `intro`; full suite

**Interfaces:**
- Consumes: `VantageCinematic` (Task 1), `VantageSentries.All` (Task 3), `VantageTower.EntrancePoint/EntranceFacing` values stored by Setup.
- Produces:
  ```csharp
  public class VantageIntro : MonoBehaviour {
      public Transform Tower, Helipad;
      public Vector3 EntrancePoint, EntranceFacing;   // set by Setup
      public AudioClip Music, Whoosh;                 // null = synthesized swell
      public static VantageIntro Instance { get; }
      public bool Playing { get; }
      public float Elapsed { get; }
      public void Skip();                             // jumps to the end pose and ends; no-op when not playing
  }
  // VantageRun: public static bool IntroPlayed;
  ```

- [ ] **Step 1: Write the failing checks** (group `intro`, and run **first** in the full suite, before the bot's other checks):
  - `introPlaysOnNewRun`: 0.3 s after `acquire()`, `VantageIntro.Instance.Playing` and `VantageCinematic.Active`; HUD hidden.
  - `introSkipRestoresControl` (Review Focus 4): at `Elapsed ≥ 0.6`, `Skip()`; next frame `Playing` false, `VantageCinematic.Active` false, input/`ThirdPersonCamera` on, camera within 4 m of the player; a second `Skip()` and `VantageCinematic.End()` change nothing.
  - `introShot2Frames`: with `-vantageShots`, a new run plays to 5 s and saves `intro_shot2` (sentry in frame: `SENTRY_Slab` inside the camera's viewport), then skip.
  - `introNotAfterDeath`: inside `checkpointRestore` after the reload: `Playing` is false.
- [ ] **Step 2: Offline build fails** (`VantageIntro` undefined).
- [ ] **Step 3: Implement `VantageIntro`**. `Start()`: play when `VantageRun.Checkpoint == 0 && !VantageRun.IntroPlayed`; set `IntroPlayed`; `VantageCinematic.Begin()`. Shots as the spec table: shot 1 from `EntrancePoint + EntranceFacing * 110 m + up 40 m` easing toward 50 m out; shot 2 rises along the facade to frame `SENTRY_Slab` (fallback: Slab route midpoint); shot 3 swings to `Helipad` (helipad sentry in frame when alive); shot 4 blends into the pose `ThirdPersonCamera` would have (enable it for one frame hidden, read the pose, disable) and calls `VantageCinematic.End()`. Letterbox bars and captions built once with `VantageUI` on their own canvas, destroyed at the end. Skip: any key or mouse button after 0.5 s (`Input.anyKeyDown`). Audio: `Music` fading into the ambience, `Whoosh` at hand-back; fallbacks synthesized.
- [ ] **Step 4: Bot run order**: call the intro checks first; afterwards every other check runs with the intro skipped. Keep `VantageRun.Reset()` after the intro checks so `IntroPlayed` is set for the rest of the run (no replay on the bot's reloads).
- [ ] **Step 5: Run** `SetupBatch` → bot `-vantageOnly intro -vantageShots <dir>`. Expected: four PASS lines; look at `intro_shot2.png`.
- [ ] **Step 6: Existing suite (Review Focus 1)**: full bot with a random seed and with `-vantageSeed 52703`, `-vantageShots <dir>`. Expected: ALL PASSED both times (old and new checks); look at `hud_warning`, `intro_shot2` and the existing HUD shots.
- [ ] **Step 7: Docs**: CLAUDE.md (code map rows for the five new scripts and `VantageSentryRoutes`; rules: "Sentries are not level enemies (no KILL lists, routes from the NavMesh after the bake)", "Every camera takeover goes through `VantageCinematic`"; the bot's new checks and `-vantageOnly`; the scene's `VANTAGE Tower Setup` contents; history line), README (ring, sentries, intro, skip key), concept doc *Revisions* row (Oct 21: sentries and ring as a boundary with a reason; intro after the professor's cutscene feedback), WORKLOG entry with Asked/Done/Verified/Open (Open: ring distances, intro pacing, shot readability in Play mode by a human).
