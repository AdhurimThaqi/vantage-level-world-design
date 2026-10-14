# VANTAGE Tower 3 Levels + Finale Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the old CommandTower with the user's `VANTAGE_Tower` and build its three levels + helipad finale (alarm, camera, key, rifle-only robot, boss, helicopter extraction, coins), optimized.

**Architecture:** Hybrid (spec approach C). The user's tower package keeps importing Blender properties into doors/triggers/consoles; `VantageTowerLevels` becomes the single level brain (spawn, alive/dead, hunting, alarm alert, checkpoint, boss, ending); new small runtime scripts for coins, camera, alarm, key animation, heavy robot, extraction; the HUD shows prompts and the end screen; the editor Setup tool is retargeted to the new tower.

**Tech Stack:** Unity 6000.0.58f2, URP, C#, ThirdPersonCoverShooter template, TextMeshPro, AI Navigation (NavMeshSurface). No Unity Test Framework in the project: "tests" are (1) offline `dotnet build`, (2) `[Vantage]` log assertions from `SetupBatch`, (3) `[Bot]` checks in `VantageAutoplay.RunBatch`.

**Spec:** `docs/superpowers/specs/2026-10-14-vantage-tower-3-levels-design.md`. Also obey `CLAUDE.md` (workflow, *Rules that break silently*).

## Global Constraints

- Unity must be closed for batch runs (check `Vantage_First-Person/Temp/UnityLockfile`). Back up `Assets/Scenes/SampleScene.unity` to `_Backups/SampleScene_before_<what>_<date>.unity` before any scene change.
- Offline compile before every batch run: scratch csproj per CLAUDE.md *Workflow* step 3, plus `Assets/VANTAGE_Tower/Scripts/**`.
- No per-frame `Find*`, `GetComponent`, LINQ, string building or `OnGUI` in new or edited runtime code. Player = `VantageEvents.ActivePlayer()`.
- No layer numbers or fixed head heights: `VantagePhysics.Solid` / `Sight`, `Eye()` / `AimPoint()`.
- Soldiers only from `Assets/Vantage/Prefabs/Soldier.prefab`; enemies side 0, player side 1, layer 10.
- Every stair flight gets a smooth walkable surface (≤ 25.6°); no step-ups.
- Edits in `Assets/VANTAGE_Tower/Scripts/**` are marked `// VANTAGE change` (user's package; keep them small).
- Coins: **1–2 per level, 5 total**, off the critical path. Model `Assets/PolyKebap/Stylized Coin Pack/Prefabs/Gold Coins/Coin_Gold_*`.
- Rifle-only robot message: `You need a better gun`. Key message: `You need the Key`. End title: `THE TOWER IS SILENT`. Achievements: `All coins`, `No deaths`, `Under 15 minutes`.
- Commit only when the user asks (CLAUDE.md overrides the skill's "commit" steps: replace each commit step with a `WORKLOG.md` note).

## Review Focus

1. Player dies on L2 after taking the key but before inserting it → restart at the L2 checkpoint with the key back on the desk and the gate shut; weapons and coins kept. (Task 4 bot check.)
2. Pistol hits on the heavy robot → no damage, and the message shows once per 2 s, not per bullet. (Task 7 bot check.)
3. Camera sees the player, then the player walks into the alarm trigger → alarm fires once (one siren, one shutter close, one hunt). (Task 5 bot check.)
4. Enemies die while the player is elsewhere / dead bodies stay in the scene → KILL doors and level gates still open. (Task 4 bot check.)
5. A coin is collected, then the player dies → the coin stays collected after the reload and the counter keeps it. (Task 8 bot check.)

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Vantage/Scripts/VantageRun.cs` (new) | Static session state that survives the death reload: checkpoint level, unlocked weapons, collected coin ids, deaths, start time. |
| `Assets/Vantage/Scripts/VantageTowerLevels.cs` (rewrite) | Level brain for L1, L2, L3, Finale. |
| `Assets/Vantage/Scripts/VantageEvents.cs` (extend) | New events: alarm, message, prompt, coin, boss, game won. |
| `Assets/Vantage/Scripts/VantageAlarm.cs` (new) | Siren, beacons, red lights; start/stop. |
| `Assets/Vantage/Scripts/VantageSecurityCamera.cs` (new) | Sweep, view cone, sight check, shootable. |
| `Assets/Vantage/Scripts/VantageKeyItem.cs` (new) | Key float/spin + fly-to-player; key-slot turn + lamp. |
| `Assets/Vantage/Scripts/VantageHeavyRobot.cs` (new) | Heavy robot and boss behaviour, rifle-only damage. |
| `Assets/Vantage/Scripts/VantageCoin.cs` (new) | Coin spin/bob and collection. |
| `Assets/Vantage/Scripts/VantageExtraction.cs` (new) | Helicopter landing, camera pull-away, win. |
| `Assets/Vantage/Scripts/VantagePlayerHUD.cs` (extend) | Prompt, messages, coin counter, alarm tag, end screen. |
| `Assets/VANTAGE_Tower/Scripts/*.cs` (edit) | Perf fixes and bridge to the level brain. |
| `Assets/Vantage/Editor/VantageCommandTower.cs` (retarget) | Scene swap, stairs, gates, covers, pickups, coins, markers, NavMesh, occlusion, shots, checks for the new tower. |
| `Assets/Vantage/Editor/VantagePlaceholders.cs` (new) | Builds robot/boss/helicopter placeholder prefabs (one mesh, one material). |
| `Assets/Vantage/Editor/VantageMilitaryBase.cs` (edit) | Plot/terrain/minimap fitted to the new tower. |
| `Assets/Vantage/Scripts/Testing/VantageAutoplayBot.cs` (rewrite) | Bot checks for the new flow. |
| `Assets/Vantage/Scripts/VantagePlaytestLogger.cs` (edit) | Spaces = outside + L1/L2/L3/Finale; coins; session time reset bug. |

---

### Task 1: Tower report and scene swap

**Files:**
- Modify: `Assets/Vantage/Editor/VantageCommandTower.cs` (`InstanceName`, `FindTower()`, `Setup()`, new `TowerReportBatch()`)
- Modify: `Assets/Scenes/SampleScene.unity` (via the tool)

**Interfaces:**
- Produces: `VantageCommandTower.InstanceName = "VANTAGE_Tower"`; `FindTower()` returns the scene root `VANTAGE_Tower` (children `VANTAGE_Tower_Level`, `VANTAGE_Tower_Gameplay`); `TowerReportBatch()` (batch, `-vantageShots dir`).
- Produces (log): `[Vantage] Level heights: L1 <y>, L2 <y>, L3 <y>, Finale <y>` measured as the min world y of `L1_Structure`… floor meshes (`L2_FloorSlab`, `L3_Structure`, `FINAL_Platform`).

- [ ] **Step 1:** Backup scene. Write `TowerReportBatch()`: opens the scene, logs every object under the two FBX instances as `[Vantage] Node <path> pos <world> size <bounds> tags <k=v;…>` (VantageTag), and the level heights line above. No scene change.
- [ ] **Step 2:** Offline compile. Run `TowerReportBatch`. Expected: ~300 `Node` lines, the heights line with 4 rising values, no exceptions. Save the log to the scratchpad; Tasks 2, 5, 8 read room positions from it.
- [ ] **Step 3:** In `Setup()`: if the old `CommandTower` instance or `VANTAGE Tower Setup` exist, record the old tower's position/rotation, delete both, and place `VANTAGE_Tower` (same as `VantageTowerSetup.AddTower` minus its PlayerStart/LevelManager) at that position, yaw chosen so `DOOR_Entrance_L/R` faces the old entrance direction. Re-running finds the existing `VANTAGE_Tower` and keeps its transform.
- [ ] **Step 4:** Run `SetupBatch` with shots. Expected log: `[Vantage] Tower placed at …`; shots show the new tower on the plot (terrain may clip until Task 10). WORKLOG note.

### Task 2: Setup retargeted (stairs, gates, covers, pickups, markers, NavMesh)

**Files:**
- Modify: `Assets/Vantage/Editor/VantageCommandTower.cs`

**Interfaces:**
- Consumes: Task 1 `FindTower()`, heights.
- Produces (scene): `VANTAGE Tower Setup` with `Stair Surfaces` (one mesh per flight in `World/Tower/`), one `VantageFloorGate` (Level 0) on the L1→L2 flight, pickups `Pistol Pickup` (WeaponPickup "Pistol", 3 m inside `DOOR_Entrance_L/R` midpoint, on the floor) and `Rifle Pickup` (WeaponPickup "Rifle" at `L2_PICKUP_Gun`, which is disabled), covers on `*_PROP_*` / `L3_PROP_Rubble` / `L3_PROP_BreachRubble` / `FINAL_Props_Cover` via `VantageCoverUtil.AddForOriented`, the `Tower Levels` object (Task 4 component), player start 4 m outside the entrance facing it.
- Produces (doors): `VantageDoor.ClearLevel` set by Setup: `L2_GATE_Stairwell_LOCKED` = 1, `L3_DOOR_Exit_To_Final` = 2 with `locked = true`.

- [ ] **Step 1:** Remove old-tower-only steps from `Setup()` (`placeFurniture`, `clearEntranceRails`, `entranceRamps` if the new entrance is flush; `realtimeLights`). Keep `sunAngle`, `staticFlags` reworked: structure (`*_Structure`, `*_FloorSlab`, `*_Ceiling`, `*_Roof`, `FINAL_Platform/Supports`) occluder+occludee+batching; props occludee+batching; anything with `VantageDoor`/`VantagePickup`/`VantageInteract`/`VantageBreakable` dynamic.
- [ ] **Step 2:** `smoothStairs` over `L1_Stairs`, `L2_Stairs_To_L3`, `FINAL_Landing_Stairs` and any mesh named `*Stairs*`; disable their mesh colliders as for the old tower (`rampPieces` misfit test unchanged).
- [ ] **Step 3:** Marker stripping: for every object with `VantageTag.MarkerType` in {EnemySpawn, Turret, HeavyEnemy, Objective, Exit, PatrolPath, SecurityCamera, EndingOption without VantageBreakable} or named `MK_*`, remove its `MeshRenderer`/`MeshFilter` (instance override) but keep the transform and tag. Log `[Vantage] Markers stripped: N`.
- [ ] **Step 4:** Doors: add `NavMeshModifier` (ignore) to every `VantageDoor`; add a carving `NavMeshObstacle` to locked doors and the shutter. Bake NavMesh (`VantageSetup.BakeNavMesh`).
- [ ] **Step 5:** Gate/door check (extend `SetupBatch`): log `[Vantage] Check: <door> ok|FAIL <why>` for every door named in Blender `unlock_by`/`unlocks`/`open_by`/`close:` (must resolve to a `VantageDoor`), and that each flight has a surface. Expected: no FAIL.
- [ ] **Step 6:** Offline compile, `SetupBatch` with shots. Expected: stairs/gates/pickup lines, `Check` lines all ok, shots `tower_*` from outside and per level. WORKLOG note.

### Task 3: Tower package runtime edits (perf + bridge)

**Files:**
- Modify: `Assets/VANTAGE_Tower/Scripts/VantageDoor.cs`, `VantageInteract.cs`, `VantagePickup.cs`, `VantageTrigger.cs`, `VantageLevelManager.cs`
- Modify: `Assets/Vantage/Scripts/VantageEvents.cs`

**Interfaces:**
- Produces in `VantageEvents`:
  - `event Action<string> Message; RaiseMessage(string text)` (HUD shows 2 s)
  - `static string Prompt { get; set; }` (null = none; HUD shows `[E] <Prompt>`; whoever set it clears it)
  - `event Action<string> AlarmRaised; RaiseAlarm(string source)`; `event Action AlarmStopped; RaiseAlarmStopped()`
  - `event Action<string> ItemPickedUp; RaiseItemPickedUp(string item)`
- Produces in `VantageDoor`: `public int ClearLevel = -1;` (`Unlock()` while `VantageTowerLevels.Instance.Current <= ClearLevel` is remembered and applied when that level clears); `public static void RecheckKills()` (re-evaluates every door's KILL list; called on `VantageEvents.EnemyKilled`); `public void ForceOpen()` (checkpoint restore).
- Produces in `VantageLevelManager`: bridge only. `IsAlive(string nameOrPrefix)` → `VantageTowerLevels.Instance.IsAlive(nameOrPrefix)`; `Alert(names)` → `VantageTowerLevels.Instance.Alert(names)`; `ReachHelipad()` → `VantageTowerLevels.Instance.ReachHelipad()`; `onAlarm` invoked by the bridge when `VantageEvents.AlarmRaised` fires. Spawning, marker hiding and `OnGUI` removed.

- [ ] **Step 1:** `VantageDoor`: player from `VantageEvents.ActivePlayer()`; KILL lists checked in `RecheckKills()` (subscribed once statically) instead of `InvokeRepeating`; `enabled` toggled by `VantageTowerLevels` (doors not on the current/next level sleep); `ClearLevel` deferral. `VantageInteract`: `OnGUI` removed; on entering/leaving range set/clear `VantageEvents.Prompt` ("Use", or `Insert the Key` for the key slot); missing item → `RaiseMessage("You need the " + requiresItem)`; range checked against `ActivePlayer()`. `VantagePickup`: raise `ItemPickedUp(item)`; the spin moves to `VantageKeyItem` for the key (Task 6). `VantageTrigger`: `close:` raises `VantageEvents.RaiseAlarm("trigger")` (closing the shutter moves into `VantageAlarm`); trigger only for the player (`ActivePlayer()` collider check instead of tag).
- [ ] **Step 2:** Offline compile. Expected: 0 errors, no `OnGUI`/`FindGameObjectWithTag` left in `Assets/VANTAGE_Tower/Scripts` (grep). WORKLOG note.

### Task 4: Level brain and checkpoint

**Files:**
- Create: `Assets/Vantage/Scripts/VantageRun.cs`
- Rewrite: `Assets/Vantage/Scripts/VantageTowerLevels.cs`
- Modify: `Assets/Vantage/Scripts/VantageThirdPersonPlayer.cs` (death → `VantageRun.Deaths++`), `VantageArsenal.cs` (unlock from `VantageRun.Weapons` at start), `Assets/Vantage/Editor/VantageCommandTower.cs` (`levelManager()` fills the Levels list)

**Interfaces:**
- `VantageRun` (static): `int Checkpoint` (level index), `HashSet<string> Weapons`, `HashSet<string> Coins`, `int Deaths`, `float StartTime` (realtime), `bool Started`; `void Begin()` (first load only), `void Reset()` (bot / new game).
- `VantageTowerLevels.Level` fields: `Name`, `Prefix` (`"MK_"`, `"L2_"`, `"L3_"`, `"FINAL_"`), `Soldiers`, `Drones`, `SoldierWeapon`, `SoldierDamage`, `SoldierHealth`, `DroneAccuracy`, `DroneFireInterval`, `ClearedMessage`, `Transform Arrival` (checkpoint spawn point).
  Defaults (Setup): L1 "Ground" `MK_` 6 soldiers, 0 drones, Pistol, dmg 8; L2 "Armory" `L2_` 6 soldiers + 3 drones, Rifle, dmg 10; L3 "Destroyed floor" `L3_` 5 soldiers (patrols A/B always included), dmg 12; Finale "Helipad" `FINAL_` 2 soldiers, dmg 12. Turrets: every `Turret` marker of the level spawns `TurretPrefab`; `HeavyEnemy` marker spawns `HeavyRobotPrefab`.
- Public API (kept names where they existed): `Instance`, `EnemySide`, `Current`, `Remaining`, `Hunting`, `Completed`, `CurrentName`, `Enemies`, `LevelAt(Vector3)`, `event LevelCleared(int)`; new: `bool IsAlive(string nameOrPrefix)` (by marker name, dead = health check), `void Alert(IEnumerable<string>)`, `void StartHunt()` (alarm: all current enemies hunt regardless of count), `void ReachHelipad()`, `event Action BossSpawned`, `bool BossDead`.
- Spawned enemy names: `<marker name>` exactly (so Blender `KILL:MK_Enemy_*` lists match).
- Level of a position: by world y against the Task 1 floor heights inside the measured footprint.
- Seeded pick kept: soldiers = seeded subset of the level's `EnemySpawn` markers (with NavMesh on their floor), spread by marker-name room part (`MK_Enemy_<Room>_<n>`). `-vantageSeed` kept.
- Level clear: L1 → `VantageFloorGate` level 0 opens, `VantageEvents.RaiseAlarmStopped()`; L2 → doors with `ClearLevel` 1 apply pending unlocks; L3 → `L3_DOOR_Exit_To_Final` unlocks; Finale cleared → waits for `ReachHelipad()`.
- Checkpoint: on level start `VantageRun.Checkpoint = index`. On load with `Checkpoint > 0`: teleport player to `Levels[Checkpoint].Arrival`, open gates/doors of earlier levels (`ForceOpen`), mark the alarm as finished, start at that level. Key state is per level, so it resets naturally.
- Door sleeping: on level change, enable `VantageDoor`s whose position is on `Current` or `Current+1`, disable the rest.

- [ ] **Step 1:** Write `VantageRun`, rewrite `VantageTowerLevels` to the interface above (reuse `findGround`, `standAt`, `arm`, `spawnSoldier`, `spawnDrone`, `returnLostEnemies`, `isAlive` logic; remove storey/lights code). On `EnemyKilled` call `VantageDoor.RecheckKills()`.
- [ ] **Step 2:** Setup `levelManager()` creates the 4 levels with the defaults, `Arrival` transforms (L1 = player start; L2 = top of the L1 flight; L3 = top of `L2_Stairs_To_L3`; Finale = `L3_DOOR_Exit_To_Final` outside side), prefab slots.
- [ ] **Step 3:** Offline compile; `SetupBatch`. Expected log: `[Vantage] Ground: N enemies (seed S)`.
- [ ] **Step 4:** Bot checks (add to Task 11's bot now as functions, run with `VantageAutoplay.RunBatch`): `clearLevelOpensGates` for L1 (kill all → floor gate open, L2 spawned); `killDoorsOpen` (kill `L3_MK_Enemy_Patrol_A/B` while the player is on L1 → `L3_DOOR_SERoom_LOCKED` unlocked once L3 is live); `checkpointRestore` (set `VantageRun.Checkpoint = 1`, collect key, kill player → after reload player at L2 arrival, key pickup active again, gate shut, `VantageRun.Weapons` still contains Pistol). Expected `[Bot] PASS` for each. WORKLOG note.

### Task 5: Alarm and security camera

**Files:**
- Create: `Assets/Vantage/Scripts/VantageAlarm.cs`, `Assets/Vantage/Scripts/VantageSecurityCamera.cs`
- Modify: `Assets/Vantage/Scripts/VantageProceduralAudio.cs` (add `Kind.Siren`)
- Modify: `Assets/Vantage/Editor/VantageCommandTower.cs` (adds both to the scene)

**Interfaces:**
- `VantageAlarm` (one, on `VANTAGE Tower Setup/Alarm`): `bool Active`; listens to `AlarmRaised` → once: `VantageDoor.Find("SHUTTER_Maintenance").CloseShutter()`, siren on, beacons (`L1_AlarmBeacons`) rotate 180°/s, up to 4 red non-shadow point lights at the beacons on, `VantageTowerLevels.Instance.StartHunt()`; listens to `AlarmStopped` → siren off, beacons stop, lights off. Second `AlarmRaised` is ignored.
- `VantageSecurityCamera` (at `MK_SecurityCamera_RoomA`, head = placeholder box + lens): sweeps ±50° at 20°/s; spotlight cone 60°, range 12 m (no shadows); sees the player when inside the cone and a `VantagePhysics.Sight` ray to `AimPoint` hits the player, for 0.6 s → `RaiseAlarm("camera")`. `OnHit(Hit)` → disabled (light off, sweep stops). Checks sight every 0.1 s, not every frame.

- [ ] **Step 1:** Implement both; Setup creates them (camera at the marker, facing the marker's forward).
- [ ] **Step 2:** Offline compile; `SetupBatch`.
- [ ] **Step 3:** Bot: `alarmByTrigger` (teleport into `TRIGGER_Alarm_ShutterClose` → within 1 s shutter closed, `VantageAlarm.Active`, `Hunting` true); `alarmOnce` (raise via camera, then walk into the trigger → `AlarmRaised` counted exactly once); `alarmStopsOnClear` (kill L1 → shutter open, alarm inactive); `cameraShot` (send `OnHit` → camera disabled, standing in the cone 2 s raises nothing). Shot `hud_alarm.png`. Expected PASS. WORKLOG note.

### Task 6: Key and key slot animations

**Files:**
- Create: `Assets/Vantage/Scripts/VantageKeyItem.cs`
- Modify: `Assets/Vantage/Editor/VantageCommandTower.cs`

**Interfaces:**
- `VantageKeyItem` on `L2_PICKUP_Key`: idle float ±0.08 m at 1 Hz + spin 90°/s + small point light (no shadow); on `ItemPickedUp("Key")` (from `VantagePickup`) flies to the player's `AimPoint` in 0.35 s, shrinking, then hides; raises `RaiseMessage("Key acquired")`.
- On `L2_KEYSLOT`: `VantageKeySlot` (same file) listens to the slot's `VantageInteract.onUsed`: shows a key mesh copy in the slot, rotates it 90° over 0.4 s, lamp material red → green (emissive), then calls the door unlock (still deferred by `ClearLevel`). If L2 isn't clear yet: `RaiseMessage("Clear the floor to open the gate")`.

- [ ] **Step 1:** Implement; Setup wires it (key light, slot lamp quad on `L2_KEYSLOT` front face).
- [ ] **Step 2:** Offline compile; bot `keyOpensGate`: gate shut without key (E → message), pick key, E → slot animation, gate opens after L2 is cleared (not before). Shot `hud_key_prompt.png`. Expected PASS. WORKLOG note.

### Task 7: Heavy robot, boss and placeholders

**Files:**
- Create: `Assets/Vantage/Scripts/VantageHeavyRobot.cs`, `Assets/Vantage/Editor/VantagePlaceholders.cs`
- Create (generated): `Assets/Vantage/Prefabs/HeavyRobot.prefab`, `Boss.prefab`, `Helicopter.prefab`

**Interfaces:**
- `VantageHeavyRobot : MonoBehaviour`: `float MaxHealth = 600` (boss 1500), `bool RifleOnly = true`, `bool IsBoss`, `bool IsDead`; layer 10 + `BaseActor` side 0 (so the template targets/hit-detects it); NavMeshAgent at 1.6 m/s; fires with the `VantageDrone` telegraph pattern (eye charges 0.8 s, then a 3-shot burst, 9 dmg each); boss adds a charge: every 10 s, 1 s wind-up (eye red, stomp sound), dash 8 m towards the player, 25 dmg on contact. `OnHit(Hit hit)`: `hit.Type != HitType.Rifle` and `RifleOnly` → no damage, `RaiseMessage("You need a better gun")` at most every 2 s. Death → `VantageEvents.RaiseEnemyKilled`, collapses (tilt + sink 1 s), stays as wreck.
- `VantageTowerLevels` tracks it as an enemy (its `IsDead` in `isAlive`).
- `VantagePlaceholders.Build()` (menu **Vantage → Enemies → Rebuild Placeholders**, also called by Setup if a prefab is missing): robot = boxes (legs, torso, head, gun arm) combined into one mesh, one dark-metal material, eye child (emissive) as separate renderer; boss = same ×1.6 with red eye; helicopter = fuselage, tail, skid boxes + 2 rotor boxes (rotors separate so they can spin). Each has a `Model` child: replacing it with the user's Blender model needs no code change.

- [ ] **Step 1:** Implement; Setup assigns `HeavyRobotPrefab`, `BossPrefab`.
- [ ] **Step 2:** Offline compile; `SetupBatch` builds prefabs (log `[Vantage] Placeholders built`).
- [ ] **Step 3:** Bot `robotRifleOnly`: 10 pistol `OnHit`s → health unchanged and exactly 1 message in 2 s; rifle hits → health drops; kill → counted dead, L3 exit unlocks once L3 is clear. Expected PASS. WORKLOG note.

### Task 8: Coins, HUD and end screen

**Files:**
- Create: `Assets/Vantage/Scripts/VantageCoin.cs`
- Modify: `Assets/Vantage/Scripts/VantagePlayerHUD.cs`, `VantageEvents.cs`, `VantagePlaytestLogger.cs`, `Assets/Vantage/Editor/VantageCommandTower.cs`

**Interfaces:**
- `VantageEvents`: `event Action<int,int> CoinCollected; RaiseCoinCollected(int collected, int total)`; `event Action GameWon; RaiseGameWon()`.
- `VantageCoin`: `string Id` (stable, e.g. `coin_L1_RoomC`); spin 120°/s + bob, sphere trigger 0.8 m; on the player: add `Id` to `VantageRun.Coins`, raise event, pickup sound, hide. At `Start`, hides itself if `VantageRun.Coins` contains `Id`. Static `Total` = count of coins in the scene.
- Coin spots (Setup, using Task 1 report positions; each in an optional space, on the floor, 0.4 m up): L1 Room C dead-end corner; L2 Office behind the shelves (`L2_PROP_Shelf_Office2`); L2 Armory corner beside `L2_PROP_Lockers_Armory`; L3 broken slab edge (`L3_BrokenSlab_West`); Finale under the platform stairs landing (`FINAL_Landing_Stairs`). Exactly 5.
- HUD: `[E] <Prompt>` centre-low when `VantageEvents.Prompt != null`; message line 2 s (`Message`); `COINS x/5` under the level name, shown after the first coin; `ALARM` red tag top-centre while `VantageAlarm.Active`; end screen on `GameWon`: `THE TOWER IS SILENT`, time mm:ss, deaths, coins x/5, achievements with tick/cross. Strings rebuilt only on change.
- Logger: spaces outside/L1/L2/L3/Finale via `LevelAt`; log coins; fix "Session length 0s" after reload (use `VantageRun.StartTime`).

- [ ] **Step 1:** Implement; Setup places the 5 coins (gold prefabs from the pack, scale to 0.35 m diameter).
- [ ] **Step 2:** Offline compile; `SetupBatch` log `[Vantage] Coins: 5`.
- [ ] **Step 3:** Bot `coinKeptAfterDeath`: collect `coin_L1_RoomC`, kill player → after reload the coin is hidden, HUD counter `1/5`. Shot `hud_end_screen.png` (raise `GameWon` directly). Expected PASS. WORKLOG note.

### Task 9: Boss trigger and helicopter extraction

**Files:**
- Create: `Assets/Vantage/Scripts/VantageExtraction.cs`
- Modify: `Assets/Vantage/Scripts/VantageTowerLevels.cs` (`ReachHelipad`), `Assets/Vantage/Editor/VantageCommandTower.cs`

**Interfaces:**
- `ReachHelipad()`: ignored until the Finale's soldiers are dead (then `RaiseMessage("Clear the platform")` once); else spawns `BossPrefab` at `FINAL_MK_Boss_EndingB` facing the pad, raises `BossSpawned`. Boss death → `VantageExtraction.Begin()`.
- `VantageExtraction`: helicopter (`Helicopter.prefab`) flies in from 120 m along a curve over 6 s, rotors spin, lands on `FINAL_Helipad`; player input off; camera (Cinemachine-free: our camera rig's target moved) pulls back and up over 4 s; then `VantageEvents.RaiseGameWon()` and `RaiseLevelCompleted()`. `VantageRun.Reset()` happens when the player starts a new game from the end screen (press Enter → reload with checkpoint 0).
- `FINAL_BreakableFloor_EndingA`: kept static, `VantageBreakable` removed by Setup.

- [ ] **Step 1:** Implement; Setup places `Extraction` under `VANTAGE Tower Setup` with the helicopter inactive.
- [ ] **Step 2:** Offline compile; bot `finale`: teleport to Finale, kill its soldiers, enter pad → boss spawned; kill boss → within 12 s `GameWon` raised. Expected PASS. WORKLOG note.

### Task 10: World fit, minimap, occlusion

**Files:**
- Modify: `Assets/Vantage/Editor/VantageMilitaryBase.cs` (`plotUnder`, tower lookup via `VantageCommandTower.FindTower()`), minimap world rect/tower icon.

- [ ] **Step 1:** Plot and terrain levelling use the new tower's measured footprint; path-out check from the new entrance.
- [ ] **Step 2:** Offline compile; `VantageMilitaryBase.RunBatch` (runs Setup too) with shots; `LedgeReportBatch`. Expected: no ledge > 0.15 m on the entrance route and each level's main route (log), plot shot shows no terrain through the floors; occlusion baked (`Performance → Bake Occlusion Culling` in batch). WORKLOG note.

### Task 11: Bot rewrite and full verification

**Files:**
- Rewrite: `Assets/Vantage/Scripts/Testing/VantageAutoplayBot.cs` (keeps `soldiersFightBack`, `walkStairs`, `hudSnapshot`, `teleport`, `kill`)

- [ ] **Step 1:** Order: soldiersFightBack (L1) → alarmByTrigger → alarmOnce → cameraShot → alarmStopsOnClear/clearLevelOpensGates → walkStairs (every opened flight incl. `FINAL_Landing_Stairs`) → keyOpensGate → killDoorsOpen → robotRifleOnly → finale → coinKeptAfterDeath → checkpointRestore (the two reload checks last). `VantageRun.Reset()` at bot start.
- [ ] **Step 2:** Run `VantageAutoplay.RunBatch -vantageShots <dir>` (Start-Process, 8 min timeout). Expected: every `[Bot] PASS`, `ALL PASSED`, exit 0; shots `hud_combat`, `hud_alarm`, `hud_key_prompt`, `hud_level_cleared`, `hud_end_screen` look right.
- [ ] **Step 3:** Run it twice more with `-vantageSeed 13375` and a random seed. Expected: ALL PASSED both times.

### Task 12: Cleanup and docs

**Files:**
- Move: `Assets/Blender_Asset/CommandTower_Unity` → `_UnusedAssets/` (with `.meta`), list in `_UnusedAssets/MOVED_FILES.txt`; remove now-dead code (`VantageCommandTower` furniture/rail functions, `CommandTowerKit` references).
- Modify: `CLAUDE.md`, `README.md`, `VANTAGE — Level & World Design Concept.md` (*Revisions* row + new pacing section), `WORKLOG.md`.

- [ ] **Step 1:** Move the old tower (Unity closed), grep for `CommandTowerKit` / `CommandTower_Unity` → none left; offline compile; `SetupBatch` + bot once more → ALL PASSED.
- [ ] **Step 2:** Docs: CLAUDE.md scene/code map/rules (new scripts, `VantageRun`, `ClearLevel`, marker prefixes, tower package edits), README (levels, E key, coins, asset table: Stylized Coin Pack licence, Poly Haven CC0, placeholder note), concept doc Revisions row dated 2026-10-14. WORKLOG entry with the verified/open lists (F9 captures on L1 alarm and the boss fight, feel and balance in Play mode).
