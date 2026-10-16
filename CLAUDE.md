# VANTAGE — project notes for Claude and agents

This file describes the project **as it is now** and how to change it safely. It is the source of truth for any agent working here (e.g. `.claude/agents/vantage-watchdog.md`). If you change architecture, tools or rules, update this file in the same change.

## The project

- Solo student project by Adhurim Thaqi for the HSLU module **I.BA_LWD (Level & World Design, HS26)**. The module grades **level design** (layout, pacing, guidance, environmental storytelling, procedural methods, performance, playtesting) more than systems code. Weigh every change against that.
- The user may make it a **commercial game** later. So prefer root-cause fixes, verify gameplay changes with the autoplay test, keep the code lean, and flag licensing questions (the course template, Pistol Animset Pro and the Stylized Coin Pack must be licensed for commercial use before shipping).
- **The game:** a third-person shooter level in a military training base in a 1 km forested valley. The player starts unarmed in front of the user's three-floor tower (`Assets/VANTAGE_Tower`); a pistol lies outside the entrance. **Three levels and a finale**, each with its own gate:
  - **L1 Ground floor:** an alarm trap just inside the entrance (or the security camera in Room A seeing the player) drops the maintenance shutter, starts siren and red lights and sends every L1 enemy after the player. During the lockdown the left corridor door (`DOOR_Corridor_West_OneWay`) is locked with the shutter's condition, so Room A on the right is the only way on: A → B → C → West Wing (open) → turret hallway. All L1 enemies + the hallway turret dead → shutter up, alarm off, red stair shutter open.
  - **L2 Armory:** rifle in the Armory, key on the office desk; key slot (E) + floor clear → stairwell gate opens. The key slot is the **only** E interaction in the game (the West Wing door stands open, the L1 console, shutter switch and L3 terminal are off).
  - **L3 Destroyed floor:** entering the open area alerts two outside patrols; both dead → SE room; the robot room opens when every other L3 enemy is dead; the heavy robot takes rifle damage only. Everyone dead → exit to the helipad stairs.
  - **Finale Helipad:** 2 soldiers, then stepping on the pad spawns the boss; its death → helicopter extraction → end screen ("THE TOWER IS SILENT", time, deaths, coins, achievements).
  - Enemies: L1 5 soldiers + the hallway turret; L2 5 soldiers, 2 turrets, 2 drones; L3 patrols, soldiers and the robot; finale 2 soldiers + boss.
  - 5 coins hidden off the critical path. Dying restarts the current level (checkpoint); weapons and coins are kept.
- **Design source of truth:** `VANTAGE — Level & World Design Concept.md` (section *The current level*). Read it before changing gameplay or layout. When the design changes, add a row to its *Revisions* table (it is a graded deliverable). The approved design spec and build plan of the 3-level tower are in `docs/superpowers/`.
- Player-facing documentation is `README.md`; keep it in step.
- The robot, boss and helicopter are **placeholders**; the user will model them in Blender.

## Work log

`WORKLOG.md` (workspace root) records every request and what was done about it, newest first. It applies to every Claude session and the `vantage-watchdog` agent:
- **At the start:** read its latest entries (what changed recently, what is still open).
- **After every task**, including small ones, questions answered with a check, and reviews that found nothing: add an entry at the top with **Asked** (the user's words), **Done** (files, tools, decisions), **Verified** (what ran and the result, or "not verified") and **Open** (what's left, what to check in Play mode). The format is at the top of the file.
- The log is history; this file is the current state. Update both when a change alters tools, rules or gameplay.

## Repository

- This folder is a **git repository** (`origin` = `github.com/AdhurimThaqi/vantage-level-world-design`, branch `main`). Commit only when the user asks.
- **Git LFS** is used for models, textures, audio, `.raw` terrain data and `*_TerrainData.asset` (see `.gitattributes`). NavMesh, LightingData, OcclusionCullingData, terrain layers and TerrainData are marked `binary` so line-ending conversion can't corrupt them.
- `_Backups/` holds scene copies taken before every automated change; `_UnusedAssets/` holds asset files moved out of the project (including the old six-storey `CommandTower_Unity`), with a list in `MOVED_FILES.txt`. Both are git-ignored and local only.
- `Vantage_First-Person/` is the Unity project: **Unity 6000.0.58f2, URP** (PC_RPAsset). The only level scene is `Assets/Scenes/SampleScene.unity`.
- `Vantage_First-Person/Documentation/` has screenshots and maps for the hand-in (`tower_*`, `world_*`); the `tower_*` shots there are still of the old command tower.
- `Vantage_First-Person/Playtests/` is created on play: session reports, CSV position trails and performance captures.
- The lecturer's template original is at `C:\Users\at488\Downloads\LWD-TPCST-6000.3`. Use it as a reference only: its binary LightingData/NavMesh/terrain assets and `Main Camera.prefab` are corrupted at the source.

## Assets

| Folder | What | Rules |
|---|---|---|
| `Assets/VANTAGE_Tower` | **User's own** tower from Blender: `Models/VANTAGE_Tower_Level.fbx` (structure, props, doors, signs, stairs) and `VANTAGE_Tower_Gameplay.fbx` (markers, triggers, pickups, patrol paths), Poly Haven CC0 textures in the `.fbm` folders, and its own scripts: `Editor/VantageTowerPostprocessor.cs` turns Blender custom properties (`VantageTag`: `marker_type`, `door_type`, `unlock_by`, `unlocks`, `requires`, `action`, …) into `VantageDoor`, `VantageTrigger`, `VantageInteract`, `VantagePickup`, `VantagePatrolPath`, `VantageBreakable`, colliders and static flags on import. | Source art; never edit inside the model instances (overrides only, made by Setup). The package's **runtime scripts are edited** (each marked `// VANTAGE change`, see Rules); re-apply if the package is re-imported. Its menu *VANTAGE ▸ Add Tower To Open Scene* is not used (it would add a second tower); use Vantage → Tower → Set Up Tower. |
| `Assets/Blender_Asset/AdhurimCharacter` | **User's own** player character (`Adhurim_Avatar.fbx`, an Avaturn avatar exported from Blender: Mixamo-style skeleton, 4 meshes, packed textures). | Source art. Its import settings (Humanoid, no animation/cameras/lights) are set by `VantagePlayerCharacter`; its textures and materials are extracted to `Assets/Vantage/Characters/Adhurim_Avatar/` (tune those, not the FBX). |
| `Assets/Blender_Asset/MilitaryEnvUnity` | **User's own** 1 km world: terrain data, forest, military base, props, tiling textures. Its `MilitaryEnvBuilder` (Tools → Military Env → Build Everything) builds a *separate* scene; harmless, but not our level. | Read-only. Our world is built by `VantageMilitaryBase`, which only calls the pack's material step. |
| `Assets/PolyKebap/Stylized Coin Pack` | Bought coin models (URP materials). Setup uses `Coin_Gold_Crown`. | Third-party; check its licence before a commercial release. |
| `Assets/Textures` | Two 4K forest-floor textures, an 8K skybox, a macro-variation map (added by the user, from another project). | Not referenced yet; record their source and licence before using them. |
| `Assets/TextMesh Pro` | TMP Essential Resources (TMP Settings, LiberationSans fallback font, shaders). | Package resources; the HUD needs them. Reimport via Window → TextMeshPro if deleted. |
| `Assets/ThirdPersonCoverShooter`, `Assets/PistolAnimsetPro` | Course template: character, soldier AI, cover, weapons, hit effects, animations. | Third-party. **Two deliberate edits** (marked "VANTAGE change"): `CharacterMotor.AlwaysUpdateIK` and `AIMovement` (no snap to a target on another floor; see Rules). Re-apply them if the package is ever reimported. |
| `Assets/Vantage` | All our code, prefabs, materials and generated data. | See below. |
| `Assets/Vantage/World` | Generated by tools: terrain data and layers, reshaped road meshes, tree prefabs with colliders, `Sky_GoldenHour.mat`, `Minimap.png`, `Tower/` stair surface meshes, `Placeholders/` robot/boss/helicopter meshes and materials. | Regenerated by the tools; don't hand-edit. |

Removed, don't reintroduce:
- the industrial pack (`RPG_FPS_game_assets_industrial`)
- `Starter Assets` (first-person)
- `TutorialInfo` and `Readme.asset`
- the six-storey command tower (`Blender_Asset/CommandTower_Unity`, `CommandTowerKit`) and its setup tool `VantageCommandTower`

All of these are in `_UnusedAssets/` (the tool is in git history).

## Scene (`SampleScene`) root objects

| Object | Made by | Contents |
|---|---|---|
| `VANTAGE_Tower` (yaw 180°, at the old tower's plot) | `VantageTower.PlaceTower` | Instances of `VANTAGE_Tower_Level` and `VANTAGE_Tower_Gameplay` (markers' renderers off). Moved so the entrance floor is flush with the terrain. |
| `VANTAGE Tower Setup` | `VantageTower.Setup` | `Stair Surfaces`, `Level Gates` (L1→L2 red shutter), `Covers`, pickups (pistol outside the entrance, rifle at `L2_PICKUP_Gun`), `Alarm` (beacons, 4 red lights, siren), `Security Camera`, `Tower Levels` (`VantageTowerLevels` + `VantageLevelManager` bridge), `Arrivals` (checkpoint spawns), `Extraction` (landing point), `Coins` (5). |
| `VANTAGE World - Military Base` | `VantageMilitaryBase.Build` | Terrain, buildings, props, roads, distant forest, ~600 covers, world-edge walls. |
| `Third Person Rig` (prefab) | template rig, body by `VantagePlayerCharacter` | The player `Adhurim_Avatar` (the template "Cowboy" with the user's body: `VantageArsenal`, `VantageThirdPersonPlayer`), and the camera with `VantageCameraFader`. |
| `VANTAGE Minimap` | `bakeMinimap` | `VantageMinimap` settings (baked map, world rect, tower). |
| `VANTAGE Playtest Tools` | tools | `VantagePlaytestLogger`, `VantagePerfCapture`. |
| `NavMesh` | `VantageSetup.BakeNavMesh` | `NavMeshSurface` (saved as an asset next to the scene). |
| `Global Volume`, `Directional Light` | — | Post-processing; golden-hour sun at 24°. |

## Code map

### Tower package runtime (`Assets/VANTAGE_Tower/Scripts`, edited, marked `// VANTAGE change`)

| Script | Role |
|---|---|
| `VantageDoor` | Door (`Auto`, `Locked`, `OneWay`, `Shutter`). Ordinary doors are **open doorways** (`Doorway`, set by Setup): open from the start, or once unlocked, with the leaf and its colliders switched off; no motion and no sensing. The shutter and the gates roll up (only they and locked doors still matter at runtime). Opens for **any living actor** near it (`CoverShooter.Actors`, sensed every 0.15 s), one-way only for the player. KILL lists (`unlock_by=KILL:a,b,prefix*`) are re-checked on kills (`RecheckKills`, called by the level brain). `ClearLevel`: `Unlock()` waits until that level is cleared (`ApplyPending`); locked doors with no `unlockBy` open when their `ClearLevel` is cleared. `ForceOpen()` for checkpoints. Toggles the carving `NavMeshObstacle` Setup adds. `All`, `Find(name)`, `IsLocked`. |
| `VantageInteract` | Console / key slot / terminal: sets the HUD prompt (`VantageEvents.SetPrompt`) in range, E → `TryUse()` (also called by the bot). `requires` an item from `VantageInventory`; the shutter switch refuses during lockdown. |
| `VantagePickup` | Key (and the disabled Armory gun): player only, raises `ItemPickedUp`. |
| `VantageTrigger` | `close:` raises the alarm, `alert:` alerts named enemies, `ending:` → `ReachHelipad()` (re-checked every 0.5 s while on the pad). Player only. |
| `VantageLevelManager` | Bridge only (`IsAlive`, `Alert`, `ReachHelipad`, `onAlarm`) + `VantageInventory`. |
| `VantageTag`, `VantagePatrolPath`, `VantageBreakable`, `VantageInput` | Unchanged (Blender properties, patrol points, unused ending A, E key). |

### Runtime (`Assets/Vantage/Scripts/`)

| Script | Role |
|---|---|
| `VantageTowerLevels` | The level brain: `Levels` = Ground floor (`MK_`), Armory (`L2_`), Destroyed floor (`L3_`), Helipad (`FINAL_`), each with `Prefix`, `FloorY`, counts, difficulty, `Arrival`. Spawns soldiers on a **seeded pick** of the level's `EnemySpawn` markers (spread over rooms; markers with a `patrol` route always) only where NavMesh lies on the marker's floor, turrets on `Turret` markers, the heavy robot on `HeavyEnemy`, drones over free markers. **Enemies are named after their marker** (Blender KILL lists match them). `KillListDone()` (unstarted levels' markers count as alive), `Alert()`, `StartHunt()` (alarm), `ReachHelipad()` (boss on the NavMesh nearest `FINAL_MK_Boss_EndingB`), `BossDefeated`. `LevelAt()` uses `TowerArea`/`FinaleArea` bounds and floor heights (no storeys). Level clear: gates, alarm stop, `VantageDoor.ApplyPending`, next level. Checkpoint: `VantageRun.Checkpoint`; on load earlier levels are opened, the player goes to `Arrival`. Doors more than one level away from both the level being fought and the player's level sleep. Hunting, safety net and `-vantageSeed N` as before. |
| `VantageRun` | Static run state that survives the reload after a death: checkpoint, weapons, coin ids, deaths, start time. Reset at each Play session, by the end screen (Enter) and by the bot. `ReloadLevel()` is the one scene reload (death and play again; `EditorSceneManager` in the editor). |
| `VantageAlarm` | One alarm per run (L1 only): closes `SHUTTER_Maintenance`, siren, beacons (property block on `Alarm Beacon.mat`), red lights flash, `StartHunt()`. Stops on `AlarmStopped` (L1 clear). `Active`, `Raised`. |
| `VantageSecurityCamera` | Sweeps ±50° with a spot-light cone; sees the player (Sight ray, checked every 0.1 s) for 0.6 s → alarm. `OnHit` disables it. |
| `VantageKeyItem`, `VantageKeySlot` | Key floats/spins/glows and flies to the player on pickup; slot shows the key turning and its lamp red → green. |
| `VantageHeavyRobot` | Robot and boss (`IsBoss`): `CharacterHealth`, NavMeshAgent, telegraphed bursts, boss charge; **only `HitType.Rifle` from the player's side damages it** (message at most every 2 s; soldiers' rounds ignored). `ForwardChildHits()` puts a `VantageHitForwarder` on every child collider, so a replacement model's colliders take hits. `IVantageEnemy`. |
| `VantageCoin` | Coin: spin/bob, collected by walking in (`Collect()`), kept in `VantageRun.Coins`, synthesised chime. `Total`. |
| `VantageExtraction` | On `BossDefeated`: helicopter flies in along a curve to the landing point, controls off, camera pulls back, then `GameWon`; Enter → new run. |
| `VantageFloorGate` | Red stair shutter (L1→L2): BoxCollider over the flight plus a carving `NavMeshObstacle`. `Open()` rolls it up and turns it green. |
| `VantageEnemyAggression` | On every soldier: starts idle, avoid 1.2 m, never retreats, **no cover** (`CoverRange` 0: soldiers stood in "take cover" pressed into walls); attacks when it sees the player; hunts when `Alerted` or the level is hunting, **only while the player is on its level** (`PlayerOnCurrentLevel`): sets the brain to idle-alerted and broadcasts `ToRunTo` (see Rules); walks `PatrolPoints` otherwise (`ToWalkTo` broadcast). |
| `VantageDrone` | Drones and turrets (`Mobile = false`). Telegraphs shots with its eye; `All` lists live drones. `IVantageEnemy`. |
| `VantageArsenal`, `WeaponPickup` | Player starts unarmed; pickups unlock Pistol/Rifle in the template inventory (and `VantageRun.Weapons`). |
| `VantageThirdPersonPlayer` | Player health (100 HP, delayed regen to 60 %), death, scene reload after 3 s (the checkpoint takes it from there). Adds `VantagePlayerHUD`. |
| `VantagePlayerHUD` | uGUI + TextMeshPro built in code: health, weapon, level ("Level n / 3", "Finale"), name, enemy pips, goal hint (find a weapon / lockdown / destroy the machine / reach the helipad / last hostiles), coins, aimed-at health, kill marker, messages, "[E]" prompt, banner, damage flash, vignette, death screen, end screen. Strings only rebuilt on change. |
| `VantageUI` | Shared UI building blocks (palette, rects, TMP texts with the Bebas dynamic font, sprites). Write single spaces (Bebas word spacing is widened). |
| `VantagePhysics` | Shared ray masks (`Solid`, `Sight`) and body points (`Eye`, `AimPoint`). Use these instead of layer numbers or fixed heights. |
| `VantageMinimap` | Top-left minimap: baked image, north up, player arrow, tower pinned to the edge, nearby enemies, distance, current level. |
| `VantageCameraFader` | Execution order 1000. Keeps the camera 0.3 m clear of walls and fades only objects ≤ 5 m. |
| `VantageCoverUtil` | Adds template cover markers (layer 8 triggers): `AddFor`, `AddForOriented`, `AddForBounds`. |
| `VantagePlaytestLogger`, `VantagePerfCapture` | Session reports (spaces = outside + L1/L2/L3/Finale; times per session plus run time and deaths; alarm and coins logged), F1 overlay, F8 mark, F9 perf capture, F10 occlusion toggle. |
| `VantageEvents`, `VantageKillReporter`, `VantageProceduralAudio` | Shared events (`ActivePlayer()` once per frame; kills, pickups, seed, completion, message, alarm raised/stopped, item, coin, boss spawned, game won; `Prompt`); soldier kill reports; synthesised ambience, drone hum and the alarm siren. |
| `Testing/VantageAutoplayBot` | Editor-only automated playtest; see *Workflow*. |

### Editor (`Assets/Vantage/Editor/`, the **Vantage** menu)

| Script | Menu / batch | Role |
|---|---|---|
| `VantageTower` | **Tower → Set Up Tower** / `SetupBatch`; `ReportBatch` | Places the tower (replacing an old `CommandTower`), grounds it at the entrance, hides markers, static flags, door rules (`ClearLevel`; the left corridor door gets the shutter's lockdown; `Doorway` on every door but the shutter and gates), doors out of the NavMesh, **smooth stair surfaces** (flights found from a height map of the step meshes), the L1→L2 gate, covers (per mesh island of `PROP_*`/`*_PROP_*`), player start, pickups, key + slot, alarm, camera, placeholders, soldier prefab, level manager (levels, areas, arrivals), extraction, door check, NavMesh bake, then coins. `ReportBatch` logs every tower object with its Blender properties and the floor heights (read-only). `FindTower`, `LevelHeights`, `EntrancePoint`, `EntranceFacing`. |
| `VantagePlaceholders` | **Enemies → Rebuild Placeholder Models** | Robot, boss, helicopter prefabs (boxes merged into one mesh, one material, a `Model` child to replace). Setup only builds missing ones. |
| `VantageMilitaryBase` | **World → Build Military World** / `RunBatch` | Builds the world at Blender coordinates, fits the plot to `VANTAGE_Tower` (footprint + 8 m, levelled at its ground floor), relaxes the terrain into slopes, places the base and covers, runs the tower setup, checks paths out, bakes the minimap. |
| | **World → Rebake Minimap** / `MinimapBatch`; `LedgeReportBatch` | Minimap only; ledge/slope and NavMesh connectivity maps around the tower. |
| `VantagePlayerCharacter` | **Player → Use Character Model** / `ApplyBatch [-vantageModel path] [-vantageShots dir]` | Puts a humanoid model on the player rig (see the rule on the player body). |
| `VantageAutoplay` | **Test → Run Autoplay Test** / `RunBatch` | Runs the bot in Play mode; batch exit code 0 = passed. |
| `VantageSetup` | **Bake NavMesh For Enemies**, Add Pistol/Rifle Pickup | Also `VantageLayers` (ensures layers 8–11 exist). |
| `VantageEditorUtil` | **Performance → Bake Occlusion Culling** | `ScenePath`, prefab paths, `Arg()`, `Shot()` (batch screenshots). |

### Prefabs (`Prefabs/`)

- `Soldier.prefab`: **generated by Setup**, a variant of the template soldier with its guns in a real `CharacterInventory`, `AlwaysUpdateIK` on, Animator set to always animate, `VantageEnemyAggression` and `VantageKillReporter`.
- `HeavyRobot.prefab`, `Boss.prefab`, `Helicopter.prefab`: placeholders by `VantagePlaceholders`; replace their `Model` child with the user's models (keep `Eye`/`Muzzle` on the robots, `Main Rotor`/`Tail Rotor` on the helicopter).
- `Drone.prefab`, `Turret.prefab`: tuned by hand; nothing regenerates them.
- `Third Person Rig.prefab`: the player rig. Its body is set by `VantagePlayerCharacter` (don't hand-edit the bones; re-run the tool). Its template canvas keeps only `Scope`.

## Workflow for changes

1. **Unity must be closed** for batch mode (it fails on the project lock). Check `Vantage_First-Person/Temp/UnityLockfile`; a lockfile left by a crashed batch run (no `Unity.exe` running) can be deleted. If Unity is open with unsaved changes, ask the user to save and close it.
2. **Back up** `Assets/Scenes/SampleScene.unity` to `_Backups/SampleScene_before_<what>_<date>.unity` before any scene change.
3. **Compile offline first.** Build a scratch csproj from `Assembly-CSharp-Editor.csproj`:
   1. Drop its `<Compile>`, `<ProjectReference>` and `<Analyzer>` items.
   2. Make the `Library\...` HintPaths absolute.
   3. Add `Assets/ThirdPersonCoverShooter/Scripts/**`, `Assets/Vantage/**`, `Assets/VANTAGE_Tower/Scripts/**` and `Assets/Blender_Asset/*/Scripts/**`.
   4. Reference `Library/ScriptAssemblies/Unity.AI.Navigation.dll` and `Unity.InputSystem.dll`.
   5. Run `dotnet build`.
4. **Run the tool in batch mode:**
   ```
   "C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe" -batchmode -projectPath "<...>\Vantage_First-Person" -executeMethod Vantage.EditorTools.VantageTower.SetupBatch -vantageShots "<scratch>\shots" -logFile "<scratch>\unity.log"
   ```
   Our log lines start with `[Vantage]`; check `Door check: n ok, 0 FAIL`, the flights, `Coins: 5` and the levels line. Look at the screenshots. Batch screenshot rendering crashed URP once (segfault after the scene was saved); a rerun was clean.
5. **Verify gameplay** with `-executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch` (about 5 minutes; 8-minute timeout). Read the lines starting with `[Bot]`. It checks: camera spots the player and springs the trap once; the entrance walk; soldiers fire and hit; a later level's KILL door stays locked; L1 clear stops the alarm and opens shutter and stairs; the key sequence and the stairwell gate; the last enemies come (the soldier must walk up itself); E only on L2; covers have room in front; soldiers stay on their floor; stairs climbed from the floor; the robot room opens last; the L3 patrol door; the rifle-only robot; every stair flight; the finale (boss, helicopter, win); checkpoint restore with a kept coin; a shot camera stays quiet while the trigger still springs the trap. It reloads the scene twice (the bot is `DontDestroyOnLoad`). With `-vantageShots <dir>` it saves `hud_alarm`, `hud_combat`, `hud_level_cleared`, `hud_key_prompt`, `hud_boss`, `hud_end_screen` (game view **with the HUD**). Diagnostic modes: `-vantageDiag`, `-vantageDiagStairs`, `-vantageDiagPaths`, `-vantageDiagFall`. Extend the bot when you add gameplay.
6. **Re-run what the change requires:**

   | Changed | Re-run |
   |---|---|
   | Tower, doors, stairs, enemies, levels, pickups, coins, alarm, camera, ending | Tower → Set Up Tower |
   | A new tower export from Blender | Tower → Set Up Tower, then World → Build Military World (plot), then the autoplay test |
   | World, terrain, plot, base props | World → Build Military World (includes the tower setup) |
   | Navigable geometry | Bake NavMesh For Enemies (coins are placed after it by Setup) |
   | Layout visible from above | World → Rebake Minimap |
   | Occluders, static flags | Performance → Bake Occlusion Culling |
   | Any gameplay | Test → Run Autoplay Test |
7. **Update docs** in the same change: this file, `README.md`, and the concept doc's *Revisions* table if the design changed. Report what's verified and what isn't.
8. **Log it in `WORKLOG.md`** (see *Work log* above), every task, before reporting back.

## Rules that break silently

- **Targeting:** the template AI only targets objects on **layer 10 "Character"** with a `BaseActor` whose `Side` differs from theirs. Enemies are side 0, the player side 1. Layers 8–11 (Cover, Scope, Character, Zones) are hard-coded in `CoverShooter.Layers`.
- **Layers and heights:** never write layer numbers or "+ 1.6 m" for a head in new code; use `VantagePhysics.Solid` / `Sight` and `Eye()` / `AimPoint()`.
- **Blender names are the API.** Setup and the level brain find things by the names in the Blender file: `DOOR_Entrance_L/R`, `LVL1_Structure`, `LVL2_FloorSlab`, `LVL2_Ceiling`, `FINAL_Platform`, `FINAL_Helipad`, `FINAL_Landing_Stairs`, `*Stairs*`, `LVL1_AlarmBeacons`, `SHUTTER_Maintenance`, `L2_GATE_Stairwell_LOCKED`, `L3_DOOR_Exit_To_Final`, `L3_DOOR_SERoom_LOCKED`, `L2_PICKUP_Key/Gun`, `L2_KEYSLOT_InsertKey`, `MK_SecurityCamera_RoomA`, `FINAL_MK_Boss_EndingB`, `TRIGGER_*`, the coin anchors (`PROP_Crate_Top1`, `L2_PROP_Shelf_Office`, `L2_PROP_Crate_H1`, `LVL3_BrokenSlab_Joint`), and the **marker prefixes** `MK_` (L1), `L2_`, `L3_`, `FINAL_`. A re-export that renames them breaks things quietly: run `ReportBatch`, then Setup, and read the door check and the bot.
- **Enemy names = marker names.** Blender KILL lists (`KILL:MK_Enemy_*,MK_Turret_Hallway`) match spawned enemies by name. Don't rename spawned enemies.
- **Dead ≠ inactive.** Dead template soldiers stay active (bodies). Alive is decided by `CharacterMotor.IsAlive` / `IVantageEnemy.IsDead`, never by `activeInHierarchy` alone.
- **Doors and the NavMesh:** doors are left out of the bake; agents path through door openings and the door opens for them. Locked doors and the closed shutter carve the NavMesh with obstacles (`VantageDoor` switches them). A door that doesn't open for enemies traps the hunt.
- **Damage** arrives as `SendMessage("OnHit", CoverShooter.Hit)`. `CharacterHealth.Hurt` never fires (package bug); use `Changed`. The robot checks `Hit.Type` (rifle only).
- **Soldiers must use `Assets/Vantage/Prefabs/Soldier.prefab`.** The template `Soldier.prefab` keeps its guns in the deprecated `CharacterMotor.Weapons`; template soldiers spawn unarmed and run away.
- **`CharacterMotor.AlwaysUpdateIK`** (template edit, marked "VANTAGE change"). Without it, off-screen soldiers can never fire.
- **`AIMovement`** (template edit, marked "VANTAGE change"): a move target more than 1 m above or below is given up instead of snapped to. Without it soldiers teleported through floors toward a player below.
- **Ordering soldiers to move:** `FighterBrain.ToRunTo/ToWalkTo` only *record* a target. Movement needs the broadcast `brain.Message("ToRunTo", pos)` (AIMovement acts on it), and the brain's own fight states (circle, cover) override it unless the brain is set to idle-alerted first. Calling the brain method directly left hunting soldiers circling where they last saw the player.
- **Covers** are placed after the NavMesh bake and only where there is room in front; soldiers don't use them (`CoverRange` 0), the player does.
- **No step-ups:** the template character cannot step up ledges. Every stair needs a smooth walkable surface (Setup builds them over the real width, up to ~31°, flush with the floor at the foot; NavMesh blockers over the flights keep enemies on their level), and the entrance must be flush with the terrain (Setup moves the tower; the world tool levels the plot at the ground floor). Some flights are entered from the side (the L2→L3 flight has a wall at its foot; Setup adds a side entry ramp).
- **Static batching and runtime changes:** anything that moves or changes material at runtime must not be batching static (doors, consoles, `LVL1_AlarmBeacons`). Setup sets this; the tower's import marks everything static.
- **Checkpoint state:** `VantageRun` is static and survives the scene reload; it is cleared at each Play session start (`SubsystemRegistration`). New per-run state goes there, per-level state must reset with the scene.
- **One component per file** (Unity serialization): e.g. `VantageKeyItem.cs` and `VantageKeySlot.cs`.
- **Placeholders are built only when missing.** Once the user's model is in a placeholder prefab, Setup won't overwrite it; the menu item does. Put the model under `Model`; keep `Eye`, `Eye Light` (robots) and `Muzzle` beside it, and `Main Rotor`/`Tail Rotor` (helicopter) named as they are. The model may have colliders (hits are forwarded).
- **Build Settings** must list `Assets/Scenes/SampleScene.unity` (index 0); scene reloads go through `VantageRun.ReloadLevel()`.
- **Player body:** the guns, holsters, `HitBox`es (`BodyPartHealth`), `Sight` and face helpers hang on the skeleton. Use **Vantage → Player → Use Character Model**, never a hand swap. It matches the humanoid muscle pose (not the T-poses), logs the height change (Cowboy 1.69 m → Adhurim 1.64 m), and switches `CharacterFace` off when the body lacks its blend shapes.
- **Camera:** never fade by size above ~5 m; keep the clearance cast instead. The extraction disables `ThirdPersonCamera` and `VantageCameraFader` to fly the camera.
- **NavMesh:** a NavMesh baked from script must be saved as an asset (`VantageSetup.saveNavMeshAsset`), otherwise the scene silently saves as binary. The gameplay model is excluded from the bake (`NavMeshModifier`).
- **Input:** the player uses the **old Input Manager**; active input handling is "Both". TakeCover is **Space**, Use is **E**, play again is **Enter**.
- **Render pipeline mapping:** `GraphicsSettings` default must be **`PC_RPAsset`**. Don't edit `ProjectSettings/*` while Unity is open.
- **Lighting:** shadow bias in PC_RPAsset must stay around 1/1 (shadow acne at 0.1/0.5). None of our added lights casts shadows.
- **URP materials:** pack vegetation and terrain layers need low/constant smoothness (handled by the world tool).
- **Batch screenshots** need `ShaderUtil.allowAsyncCompilation = false` (`VantageEditorUtil.Shot` does it); the first render after loading can come out garbled, so `Shots` renders the first view twice.
- **Bot and cover:** the bot turns `AutoTakeCover` off and leaves cover before teleporting.
- **Batch Play mode** (the bot): the scripting domain reloads on entering Play mode, so request and result travel via `SessionState`. Batch mode never renders a game view, so anything gated on visibility behaves as off-screen there.

## Performance notes

- **Rendering:** Forward+ (PC_Renderer), soft shadow quality medium, additional-light shadows off, shadow distance 50 m with 4 cascades, SSAO on.
- **Tower:** walls, slabs, stairs occluder + occludee + batching static; glass, props, signs, lamps occludee + batching; doors, consoles, beacons dynamic. Occlusion baked. Marker renderers off (set in the editor, not at runtime).
- **AI:** only the current level's enemies exist. Doors more than one level away from the current level and the player's level sleep; doors sense actors every 0.15 s; kill doors react to kill events.
- **World:** trees are meshes without LODs (~26k) at a 450 m tree distance and 80 m grass distance.
- **Per-frame code:** no `Find*`, `GetComponent`, LINQ, string building or `OnGUI` per frame (the tower package's `OnGUI` and per-frame `FindGameObjectWithTag` were removed). HUD strings rebuild on change; `Remaining` counts once per frame with cached components; the camera checks sight every 0.1 s; robots think every 0.2 s.
- **Measure, don't guess:** F9 in Play mode (once with F10 occlusion on, once off). Batch-mode frame times mean nothing because nothing renders.

## Known gaps and open work

- **Not yet checked by a human in Play mode (new tower):**
  - feel and balance of the alarm hunt on L1 with only a pistol, and of the robot and boss fights
  - the camera inside the new rooms and on the outside stairs
  - the helicopter fly-in and camera pull-back, the end screen, Enter to restart
  - the security camera's sweep and cone readability; the key slot's key copy orientation
  - the player character's animation and guns in hand (since 2026-10-08)
  - frame time with the alarm on L1 and during the boss fight (F9, F10 on/off)
- The professor's feedback (commit 2026-10-09) is not written down anywhere yet; the concept doc's 2026-10-14 revision row says so.
- **Environmental storytelling** (story props) and **per-space mood volumes** are not built in the new tower. The profiles still exist in `Assets/Vantage/Volumes/`; the scene only uses `Global Volume`.
- **Procedural methods** are only the seeded soldier placement.
- Robot, boss and helicopter are placeholders (user to model in Blender).
- `MK_Enemy_A_1` and `MK_Enemy_A_2` have no NavMesh on their floor (next to props) and are skipped.
- Licensing: Avaturn terms, Stylized Coin Pack licence, `Assets/Textures` source.
- `README.md` asset table still needs the Asset Store links and publisher check before hand-in.
- `Documentation/tower_*` screenshots show the old tower; render new ones (Setup's `-vantageShots`).
- Ideas: ammo refills per level, story props, per-level post-processing, LODs for trees.

## History (short)

- **2026-10-05:** first-person dropped; third person only (the template "Cowboy").
- **2026-10-06:** generated ProBuilder blockout tower with a procedural level 2, roof waves, compound, HUD and playtest logging.
- **2026-10-07:** the user's Blender military world replaced the bought industrial yard; minimap; the user's six-storey command tower replaced the blockout (four kill-to-unlock levels); playtest fixes found with the new autoplay bot (armed soldiers firing off-screen, smooth stairs, camera clearance, shadow acne, static flags and occlusion); cleanup; PC render pipeline actually used; furniture and UI pass (TMP-style HUD, minimap).
- **2026-10-08:** the player's body is the user's own avatar (`VantagePlayerCharacter`); HUD and minimap on TextMeshPro; hardcoded values removed (`VantagePhysics`, measured footprints); per-frame work cut; hunters stay on their level; spawns need NavMesh on their floor; `-vantageSeed`.
- **2026-10-14:** the user's new three-floor tower with a helipad replaced the command tower (now in `_UnusedAssets`). Design: alarm trap + security camera (L1), key and key slot (L2), patrol kill-door, terminal and rifle-only heavy robot (L3), boss then helicopter extraction (finale), 5 coins with an end screen and achievements, per-level checkpoint. Tower package scripts edited for performance (no `OnGUI`, no per-frame lookups, event-driven kill doors) and bridged to our level brain. New `VantageTower` setup (replaces `VantageCommandTower`), placeholders, bot rewritten (ALL PASSED, random seed and seed 13375). World rebuilt around the new plot (0 ledges).
- **2026-10-15/16:** playtest fixes: stair surfaces flush with the floor (~31°) plus a side entry, NavMesh blockers over flights, `AIMovement` floor-snap edit, soldiers without cover, covers only with room in front, 3 fewer enemies, E only at the L2 key slot, boss on the platform floor; hunt and patrol orders now actually move soldiers (broadcast messages).
