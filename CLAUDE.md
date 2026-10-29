# VANTAGE — project notes for Claude and agents

This file describes the project **as it is now** and how to change it safely. It is the source of truth for any agent working here (e.g. `.claude/agents/vantage-watchdog.md`). If you change architecture, tools or rules, update this file in the same change.

## The project

- Solo student project by Adhurim Thaqi for the HSLU module **I.BA_LWD (Level & World Design, HS26)**. The module grades **level design** (layout, pacing, guidance, environmental storytelling, procedural methods, performance, playtesting) more than systems code. Weigh every change against that.
- The user may make it a **commercial game** later. So prefer root-cause fixes, verify gameplay changes with the autoplay test, keep the code lean, and flag licensing questions (the course template, Pistol Animset Pro, the Stylized Coin Pack, Low Poly Soldiers (demo) and the ISO Mech pack must be licensed for commercial use before shipping).
- **The game:** a third-person shooter level in a military training base in a 1 km forested valley. The player starts unarmed in front of the user's three-floor tower (`Assets/VANTAGE_Tower`); a pistol lies outside the entrance. **Three levels and a finale**, each with its own gate:
  - **L1 Ground floor:** an alarm trap just inside the entrance (or the security camera in Room A seeing the player) drops the maintenance shutter, starts siren and red lights and sends every L1 enemy after the player. During the lockdown the left corridor door (`DOOR_Corridor_West_OneWay`) is locked with the shutter's condition, so Room A on the right is the only way on: A → B → C → West Wing (open) → turret hallway. All L1 enemies + the hallway turret dead → shutter up, alarm off, red stair shutter open.
  - **L2 Armory:** rifle in the Armory, key on the office desk; key slot (E) + floor clear → stairwell gate opens. The key slot is the **only** E interaction in the game (the West Wing door stands open, the L1 console, shutter switch and L3 terminal are off).
  - **L3 Destroyed floor:** the robot room opens when the indoor soldiers (barracks, comms, hall) are dead and is the only way to the open area (breach in its south wall); entering the open area alerts two outside patrols; both dead → SE room; the heavy robot takes rifle damage only. Everyone dead → exit to the helipad stairs.
  - **Finale Helipad:** 2 soldiers, then stepping on the pad spawns the boss; its death → helicopter extraction → end screen ("THE TOWER IS SILENT", time, deaths, coins, achievements).
  - Enemies: L1 5 soldiers + the hallway turret; L2 5 soldiers, 2 turrets, 2 drones; L3 patrols, soldiers and the robot; finale 2 soldiers + boss.
  - 5 coins hidden off the critical path. Dying restarts the current level (checkpoint) at its arrival point, half way down the last flight below it, at most 3.3 m (hidden by the floor above and out of view through the stairwell railing; the finale's is outside the L3 exit), with the best gun drawn and 3 s of protection; no soldier spawns within 14 m of an arrival with a view of it; weapons and coins are kept.
  - **Enemy fire** (halved on 2026-10-26 after 15 deaths in one playtest): damage per hit L1 6, L2 7, L3 and finale 8; soldiers pause 0.7 s between bursts and add 3.5° aim error; L2 drones 30 % accuracy every 1.8 s; turrets every 0.45 s at 30 %.
  - **Sentries and the warning ring:** two sentries patrol the outside of the top (rim of the broken slab, rim of the helipad platform) from the start. Free up to 15 m past the tower plot; warning band to 35 m (HUD warning, warning shots); beyond, or after 5 s in the band, they shoot to hit. They hold no lock and stay dead once killed (per run).
  - **Intro (and briefing):** a 19 s skippable flyover at the start of a run: tower → a deserter walks out past the plot, gets a warning shot, runs and is shot by the slab sentry (the ring rule shown) → the slab sentry → the helipad → the player's camera, with objective captions ("Rule", "Objective" ×2, "First: grab the pistol"); the HUD repeats the pistol objective as a banner. Not after a death.
- **Design source of truth:** `VANTAGE — Level & World Design Concept.md` (section *The current level*). Read it before changing gameplay or layout. When the design changes, add a row to its *Revisions* table (it is a graded deliverable). The approved design spec and build plan of the 3-level tower are in `docs/superpowers/`.
- Player-facing documentation is `README.md`; keep it in step.
- **Enemy looks:** soldiers wear the Asset Store *Low Poly Soldiers* body, the boss is the Asset Store *ISO Mech* (both applied by Setup through `VantageEnemyModels`). The L3 heavy robot and the helicopter are still **placeholders** (the *Advanced Helicopter Controller* pack the user added has no helicopter model, only flight scripts and a cube test helicopter).

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
| `Assets/LowPolySoldiers_demo` | Asset Store soldier (3ds Max Biped, one textured mesh with its own rifle as a submesh, 3 generic clips). | Third-party. Setup imports `Soldier_demo.FBX` as **Humanoid** (import scale fitted to the template soldier, the stale avatar in its meta cleared): the pack's own sample scene no longer animates. The cut mesh is in `Assets/Vantage/Characters/Soldier_demo/`. |
| `Assets/RSG_UnderWater_Pack` | Asset Store *ISO Mech* (HDRP materials, generic rig, idle/walk/death clips). | Third-party. Its HDRP material is pink in URP: `VantageEnemyModels` builds `Assets/Vantage/Characters/ISO_Mech/M_ISO_Mech_URP.mat` and `Boss.controller` (idle/walk clips set to loop in the pack's import settings). |
| `Assets/AdvancedHelicopterController` | Asset Store flight controller (scripts, Input System actions, a cube test helicopter, a second `SampleScene.unity`). | **Not used** (no model; our extraction is scripted). Candidate for removal; ask the user. |
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
| `VANTAGE Tower Setup` | `VantageTower.Setup` | `Stair Surfaces`, `Level Gates` (L1→L2 red shutter), `Covers`, pickups (pistol outside the entrance, rifle at `L2_PICKUP_Gun`), `Alarm` (beacons, 4 red lights, siren), `Security Camera`, `Tower Levels` (`VantageTowerLevels` + `VantageLevelManager` bridge), `Arrivals` (checkpoint spawns), `Extraction` (landing point), `Boundary` (`VantageBoundary`, the plot), `Intro` (`VantageIntro`), `Coins` (5), `Sentries` (`VantageSentries` with the two traced routes). |
| `VANTAGE World - Military Base` | `VantageMilitaryBase.Build` | Terrain, buildings, props, roads, distant forest, ~360 covers, world-edge walls. Trees, props and buildings only within 220 m of the plot (`WorldReach`); beyond, every 3rd big tree is a low-poly far conifer and the rest is left out; the horizon forest keeps every 4th tree. |
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
| `VantageTowerLevels` | The level brain: `Levels` = Ground floor (`MK_`), Armory (`L2_`), Destroyed floor (`L3_`), Helipad (`FINAL_`), each with `Prefix`, `FloorY`, counts, difficulty, `Arrival`. Spawns soldiers on a **seeded pick** of the level's `EnemySpawn` markers (spread over rooms; markers with a `patrol` route always) only where NavMesh lies on the marker's floor and never on `BehindExit` markers (set by Setup: reached only through the level's exit, e.g. `L2_MK_Enemy_Stairwell_9` behind the stairwell gate; a soldier there made L2 impossible to clear), turrets on `Turret` markers, the heavy robot on `HeavyEnemy`, drones over free markers. **Enemies are named after their marker** (Blender KILL lists match them). `KillListDone()` (unstarted levels' markers count as alive), `Alert()`, `StartHunt()` (alarm), `ReachHelipad()` (boss on the NavMesh nearest `FINAL_MK_Boss_EndingB`), `BossDefeated`. `LevelAt()` uses `TowerArea`/`FinaleArea` bounds and floor heights (no storeys). Level clear: gates, alarm stop, `VantageDoor.ApplyPending`, next level. Checkpoint: `VantageRun.Checkpoint`; on load earlier levels are opened, the player goes to `Arrival` with the best gun drawn (`VantageArsenal.EquipNow`) and `RespawnProtection` (3 s, `VantageThirdPersonPlayer.Protect`). **Safe arrivals:** `pick()` leaves out (logs "kept out of the arrival's view") non-patrol markers within `SafeArrivalRadius` (14 m) that see the `Arrival` eye to eye (railings and unlocked doors don't block that view: soldiers shoot between the bars); they only fill a level that would otherwise come up short. Soldier fire: `SoldierBurstWait` (0.7 s) and `SoldierAimError` (3.5°) applied at spawn (`AIFire.Bursts`, `AIAim.AccuracyError`); damage per level. `SoldierEyeHeight`. Doors more than one level away from both the level being fought and the player's level sleep. Hunting, safety net and `-vantageSeed N` as before. |
| `VantageRun` | Static run state that survives the reload after a death: checkpoint, weapons, coin ids, dead sentries, whether the intro played, deaths, start time. Reset at each Play session, by the end screen (Enter) and by the bot. `ReloadLevel()` is the one scene reload (death and play again; `EditorSceneManager` in the editor). |
| `VantageAlarm` | One alarm per run (L1 only): closes `SHUTTER_Maintenance`, siren, beacons (property block on `Alarm Beacon.mat`), red lights flash, `StartHunt()`. Stops on `AlarmStopped` (L1 clear). `Active`, `Raised`. |
| `VantageSecurityCamera` | Sweeps ±50° with a spot-light cone; sees the player (Sight ray, checked every 0.1 s) for 0.6 s → alarm. `OnHit` disables it. |
| `VantageKeyItem`, `VantageKeySlot` | Key floats/spins/glows and flies to the player on pickup; slot shows the key turning and its lamp red → green. |
| `VantageHeavyRobot` | Robot and boss (`IsBoss`): `CharacterHealth`, NavMeshAgent, telegraphed bursts, boss charge; **only the pistol doesn't damage it**: a character's round is judged by the gun in its hand (`RightItem` name `Pistol*` bounces off), other hits by `Hit.Type` (rifle only); the template rig's drum rifle is a pistol-*type* gun (its hold animation/IK), so its hits arrive typed `Pistol` (message at most every 2 s; soldiers' rounds ignored). `ForwardChildHits()` puts a `VantageHitForwarder` on every child collider, so a replacement model's colliders take hits. `IVantageEnemy`. A rigged model's `Animator` (optional field) gets `Speed` and the `Die` trigger; with one, death plays the animation instead of the tip-over. |
| `VantageCoin` | Coin: spin/bob, collected by walking in (`Collect()`), kept in `VantageRun.Coins`, synthesised chime. `Total`. |
| `VantageExtraction` | On `BossDefeated`: helicopter flies in along a curve to the landing point, controls off (`VantageCinematic.Begin`, never ended), camera pulls back, then `GameWon`; Enter → new run. |
| `VantageCinematic` | Static. Every camera takeover: `Begin()` turns off player input, damage, `ThirdPersonCamera`, `VantageCameraFader` and remembers what it changed; `End()` restores exactly that; `Active`; `CinematicStarted/Ended` events. Reset on every scene load. HUD and minimap hide while `Active`. |
| `VantageIntro` | The intro flyover and briefing (5 shots: approach, the deserter, `SENTRY_Slab`, `FINAL_Helipad`, the player camera's own first pose; constants `Shot1`…`Length` 19 s, `WalkAt`/`AimAt`/`WarnAt`/`KillAt`), letterbox, kicker + caption per shot, skip on any key after 0.5 s. The deserter's path is found at runtime: straight out from the plot's centre past the slab sentry (±60° tried), from `FreeDistance`+4 to `WarningDistance`−2, on open ground in the sentry's view; none found → the intro runs without him (logged). A skip kills him at once (the body stays). `Deserter`, `WarningShotFired`. Plays when `VantageRun.Checkpoint == 0 && !IntroPlayed`. `Skip()`, `Playing`, `Elapsed`, static `Finished(skipped, at)` (logger). Slots `Music`, `Whoosh`. |
| `VantageBoundary` | The warning ring: `Plot` (set by Setup), `FreeDistance` 15, `WarningDistance` 35, `GraceSeconds` 5; `ZoneAt(pos)` (Inside/Free/Warning/Kill), `PlayerZone` with the grace rule (polled every 0.2 s), `Enforcing` (off in cinematics and when dead). |
| `VantageDeserter` | The intro's deserter: the level soldier with its AI, `NavMeshAgent` and kill report off, unarmed; `Move(0 / 0.5 walk / 1 run)`, `Kill(shooter)` (a rifle `OnHit`), `AimPoint`. Not a level enemy. |
| `VantageSentries`, `VantageSentry` | Spawner (scene start, any level; routes and the drones' tracer material from Setup; skips `VantageRun.DeadSentries`) and brain: patrol back and forth with a ±60° outward scan at the ends, leash 6 m, ring shots scripted (glint, tracer, dust, crack; warning misses, kill-zone hits via `OnHit`), template combat only while the player is on its level (template `AISight`/`AIListener` off otherwise). Counters `WarningShots`, `Hits` for the bot. Scripted for the intro: `HoldAim(at, s)`, `Glint(0..1)`, `ScriptedShot(to, miss)` (effects only, no damage, not counted). Slots `WarningCall`, `RadioLoop`, `ShotCrack`. |
| `VantageFloorGate` | Red stair shutter (L1→L2): BoxCollider over the flight plus a carving `NavMeshObstacle`. `Open()` rolls it up and turns it green. |
| `VantageEnemyAggression` | On every soldier: starts idle, avoid 1.2 m, never retreats, **no cover** (`CoverRange` 0: soldiers stood in "take cover" pressed into walls); attacks when it sees the player; hunts when `Alerted` or the level is hunting, **only while the player is on its level** (`PlayerOnCurrentLevel`): sets the brain to idle-alerted and broadcasts `ToRunTo` (see Rules); walks `PatrolPoints` otherwise (`ToWalkTo` broadcast). |
| `VantageDrone` | Drones and turrets (`Mobile = false`). Telegraphs shots with its eye; `All` lists live drones. `IVantageEnemy`. |
| `VantageArsenal`, `WeaponPickup` | Player starts unarmed; pickups unlock Pistol/Rifle in the template inventory (and `VantageRun.Weapons`). `EquipNow(name)` puts an unlocked gun in hand without the equip animation (checkpoint). |
| `VantageThirdPersonPlayer` | Player health (100 HP, delayed regen to 60 %), death, scene reload after 3 s (the checkpoint takes it from there), `Protect(s)` / `Protected` / `EndProtection()` (no damage after a checkpoint restart; a cinematic's own damage switch wins). Adds `VantagePlayerHUD`. |
| `VantagePlayerHUD` | uGUI + TextMeshPro built in code: health, weapon, level ("Level n / 3", "Finale"), name, enemy pips, goal hint (find a weapon / lockdown / destroy the machine / reach the helipad / last hostiles), coins, aimed-at health, kill marker, messages, "[E]" prompt, banner (level cleared; after the intro "Grab the pistol" while unarmed), damage flash, vignette, death screen, end screen. Strings only rebuilt on change. |
| `VantageUI` | Shared UI building blocks (palette, rects, TMP texts with the Bebas dynamic font, sprites). Write single spaces (Bebas word spacing is widened). |
| `VantagePhysics` | Shared ray masks (`Solid`, `Sight`) and body points (`Eye`, `AimPoint`). Use these instead of layer numbers or fixed heights. |
| `VantageMinimap` | Top-left minimap: baked image, north up, player arrow, tower pinned to the edge, nearby enemies, distance, current level. |
| `VantageCameraFader` | Execution order 1000. Keeps the camera 0.3 m clear of walls and fades only objects ≤ 5 m. |
| `VantageCoverUtil` | Adds template cover markers (layer 8 triggers): `AddFor`, `AddForOriented`, `AddForBounds`. |
| `VantagePlaytestLogger`, `VantagePerfCapture` | Session reports (spaces = outside + L1/L2/L3/Finale; times per session plus run time and deaths; alarm and coins logged), F1 overlay, F8 mark, F9 perf capture, F10 occlusion toggle. |
| `VantageEvents`, `VantageKillReporter`, `VantageProceduralAudio` | Shared events (`ActivePlayer()` once per frame; kills, pickups, seed, completion, message, alarm raised/stopped, item, coin, boss spawned, game won; `Prompt`); soldier kill reports; synthesised ambience, drone hum and the alarm siren. |
| `Testing/VantageAutoplayBot`, `Testing/VantageHitLog` | Editor-only automated playtest; see *Workflow*. `VantageHitLog` records every hit the player takes (attacker, damage, time). |

### Editor (`Assets/Vantage/Editor/`, the **Vantage** menu)

| Script | Menu / batch | Role |
|---|---|---|
| `VantageTower` | **Tower → Set Up Tower** / `SetupBatch`; `ReportBatch` | Arrivals: L2 and L3 restart half way (≤ 3.3 m) down the last flight below them (`topOf`), so a checkpoint never starts in view of the floor; bot checks that need the floor use `floorAhead()`. Places the tower (replacing an old `CommandTower`), grounds it at the entrance, hides markers, static flags, door rules (`ClearLevel`; the left corridor door gets the shutter's lockdown; `Doorway` on every door but the shutter and gates), doors out of the NavMesh, **smooth stair surfaces** (flights found from a height map of the step meshes), **side steps** where a flight's foot is against a wall (a landing box over its lowest steps up to the first tread ≥ 0.8 m, visible steps in the stair's material, an invisible ramp through their nosings ≤ 30°; logged "foot against a wall: landing …"), **handrails** on every open side where the model has no rail tops within 0.8 m (posts ≤ 1.1 m apart, top and mid rail on the model rail's line and height, nearest model rail material, a thin collider; logged "Handrails: n added (…)"), the L1→L2 gate, covers (per mesh island of `PROP_*`/`*_PROP_*`), player start, pickups, key + slot, alarm, camera, placeholders, soldier prefab, level manager (levels, areas, arrivals), extraction, boundary (ring plot), intro, door check, NavMesh bake, spawn markers behind an exit (`BehindExit`), covers, coins, then the sentry routes (logged `Sentry route Slab/Helipad: n points, m`; FAIL lines when an area is missing or a leg can't be walked). `ReportBatch` logs every tower object with its Blender properties and the floor heights (read-only). `FindTower`, `LevelHeights`, `EntrancePoint`, `EntranceFacing`. |
| `VantageSentryRoutes` | (used by Setup) | `Trace(search, floorY, include, towerCentre, points, inset, minArea, out area)`: samples the NavMesh on a 0.75 m grid at a floor height, keeps the largest patch, follows its rim facing away from the tower by angle, resamples and insets. Setup keeps samples 1.5 m clear of every `NavMeshObstacle` (runtime carving). |
| `VantagePlaceholders` | **Enemies → Rebuild Placeholder Models** | Robot, boss, helicopter prefabs (boxes merged into one mesh, one material, a `Model` child to replace). Setup only builds missing ones. |
| `VantageEnemyModels` | **Enemies → Use Asset Store Models** (boss); `ShotsBatch -vantageShots dir` | Called by Setup: `Boss()` puts the ISO Mech under the boss prefab's `Model` (scaled to the current model's height, URP material, Animator; Eye on `Top_M`, Muzzle on `ShotgunTop_R`); `SoldierBody()` unpacks the soldier being built and swaps in the Low Poly Soldiers body (`VantagePlayerCharacter.SwapBody`), cutting the model's own rifle submesh. `ShotsBatch`: soldier (rifle pose) and boss beside the player start, logs the rifle-to-hand distance. A missing pack leaves that enemy as it was. |
| `VantageMilitaryBase` | **World → Build Military World** / `RunBatch` | Builds the world at Blender coordinates, fits the plot to `VANTAGE_Tower` (footprint + 8 m, levelled at its ground floor), relaxes the terrain into slopes, places the base and covers, runs the tower setup, checks paths out, bakes the minimap. |
| | **World → Rebake Minimap** / `MinimapBatch`; `LedgeReportBatch` | Minimap only; ledge/slope and NavMesh connectivity maps around the tower. |
| `VantagePlayerCharacter` | **Player → Use Character Model** / `ApplyBatch [-vantageModel path] [-vantageShots dir]` | Puts a humanoid model on the player rig (see the rule on the player body). |
| `VantageAutoplay` | **Test → Run Autoplay Test** / `RunBatch` | Runs the bot in Play mode; batch exit code 0 = passed. |
| `VantageSetup` | **Bake NavMesh For Enemies**, Add Pistol/Rifle Pickup | Also `VantageLayers` (ensures layers 8–11 exist). |
| `VantageFloorPlans` | **Documentation → Render Floor Plans** / `RenderBatch -vantageShots dir [-vantageRoutes file]`; `DoorReportBatch` | Top-down plan per floor (camera inside the floor, near plane cuts above 2.4 m, entrance at the bottom), a collider cross-section (walls/furniture), a JSON of every named object in pixels, the NavMesh triangles by connected region, optional NavMesh routes between named objects. `DoorReportBatch`: per door, NavMesh in the opening and what else is solid there. Used for the hand-in maps (`PaperDocumentation-/Presentation`). |
| `VantageEditorUtil` | **Performance → Bake Occlusion Culling** | `ScenePath`, prefab paths, `Arg()`, `Shot()` (batch screenshots). |

### Prefabs (`Prefabs/`)

- `Soldier.prefab`: **generated by Setup** from the template soldier (unpacked, no longer a variant, once it wears the Low Poly Soldiers body) with its guns in a real `CharacterInventory`, `AlwaysUpdateIK` on, Animator set to always animate, `VantageEnemyAggression` and `VantageKillReporter`.
- `HeavyRobot.prefab`, `Helicopter.prefab`: placeholders by `VantagePlaceholders`; replace their `Model` child with the user's models (keep `Eye`/`Muzzle` on the robot, `Main Rotor`/`Tail Rotor` on the helicopter). `Boss.prefab`: built as a placeholder, then given the ISO Mech by `VantageEnemyModels` on every Setup.
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
5. **Verify gameplay** with `-executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch` (about 5 minutes; 8-minute timeout). Read the lines starting with `[Bot]`. It checks: camera spots the player and springs the trap once; the entrance walk; soldiers fire and hit; a later level's KILL door stays locked; L1 clear stops the alarm and opens shutter and stairs; every L2 spawn marker reachable with the stairwell gate shut (and the stairwell marker excluded); the key sequence and the stairwell gate; the last enemies come (the soldier must walk up itself); E only on L2; covers have room in front; soldiers stay on their floor; stairs climbed from the floor; the robot room opens last; the L3 patrol door; the rifle-only robot; every stair flight; the finale (boss, helicopter, win); checkpoint restore with a kept coin; a shot camera stays quiet while the trigger still springs the trap. Since Oct 21 it also checks the intro (plays on a new run; since Oct 26 the deserter: in the warning band, warning shot, in frame with the slab sentry, dead after the kill shot; frames the slab sentry; skip restores control; the pistol banner; not after a death, again after play again), the safe respawn (dies into L3: protected, gun in hand, no soldier in sight of the arrival within `SafeArrivalRadius`, < 50 damage in 4 s; logs every hit by attacker), the warning ring (zones, grace reset, HUD warning, free zone quiet, warning shots then hits, kill zone, no hits without sight, quiet in a cinematic) and the sentries (spawn and walk, stay on route, hold no lock, stay dead after a death). `-vantageOnly cinematic|ring|sentries|intro|spawns|respawn|robot` runs one group (`robot`: clear L1 and L2, then the robot check with the player's real guns) (`spawns`: clear L1, then the L2 spawn reachability check, about a minute). It reloads the scene four times (the bot is `DontDestroyOnLoad`). With `-vantageShots <dir>` it saves `hud_alarm`, `hud_combat`, `hud_level_cleared`, `hud_key_prompt`, `hud_boss`, `hud_end_screen`, `hud_warning`, `intro_deserter`, `intro_shot2` (game view **with the HUD**). The robot-room check also asks the live NavMesh whether both patrols can be walked to (a softlock check). Diagnostic modes: `-vantageDiag`, `-vantageDiagStairs`, `-vantageDiagPaths`, `-vantageDiagFall`, `-vantageDiagL3` (L3 path legs with the runtime door states). Extend the bot when you add gameplay.
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
- **Damage** arrives as `SendMessage("OnHit", CoverShooter.Hit)`. `CharacterHealth.Hurt` never fires (package bug); use `Changed`. The robot judges the attacker's gun in hand, not `Hit.Type`: the player's `Rifle` (drum gun) has `WeaponType.Pistol` in the rig (don't change it: it picks the hold animation, IK, reload and crosshair).
- **Soldiers must use `Assets/Vantage/Prefabs/Soldier.prefab`.** The template `Soldier.prefab` keeps its guns in the deprecated `CharacterMotor.Weapons`; template soldiers spawn unarmed and run away.
- **`CharacterMotor.AlwaysUpdateIK`** (template edit, marked "VANTAGE change"). Without it, off-screen soldiers can never fire.
- **`AIMovement`** (template edit, marked "VANTAGE change"): a move target more than 1 m above or below is given up instead of snapped to. Without it soldiers teleported through floors toward a player below.
- **Ordering soldiers to move:** `FighterBrain.ToRunTo/ToWalkTo` only *record* a target. Movement needs the broadcast `brain.Message("ToRunTo", pos)` (AIMovement acts on it), and the brain's own fight states (circle, cover) override it unless the brain is set to idle-alerted first. Calling the brain method directly left hunting soldiers circling where they last saw the player.
- **Covers** are placed after the NavMesh bake and only where there is room in front; soldiers don't use them (`CoverRange` 0), the player does.
- **No step-ups:** the template character cannot step up ledges. Every stair needs a smooth walkable surface (Setup builds them over the real width, up to ~31°, flush with the floor at the foot; NavMesh blockers over the flights keep enemies on their level), and the entrance must be flush with the terrain (Setup moves the tower; the world tool levels the plot at the ground floor). Some flights are entered from the side (the L2→L3 flight has its foot against the Armory wall; Setup adds a landing and side steps walked on an invisible ramp). Handrails (model or ours) never go along a side entrance.
- **Static batching and runtime changes:** anything that moves or changes material at runtime must not be batching static (doors, consoles, `LVL1_AlarmBeacons`). Setup sets this; the tower's import marks everything static.
- **Checkpoint state:** `VantageRun` is static and survives the scene reload; it is cleared at each Play session start (`SubsystemRegistration`). New per-run state goes there, per-level state must reset with the scene.
- **Sentries are not level enemies.** They are spawned by `VantageSentries`, not the level brain, so no KILL list, `Remaining` or HUD pip counts them; never add them to `VantageTowerLevels`' lists. The same goes for the intro's deserter (`VantageDeserter`, a body outside the plot). Their routes come from the baked NavMesh (re-run Setup after the slab or platform changes) and must stay clear of `NavMeshObstacle`s (locked doors carve holes at runtime).
- **Camera takeovers go through `VantageCinematic`** (intro, extraction). Don't switch `ThirdPersonInput`/`ThirdPersonCamera` off by hand; a second system doing so would be restored wrongly.
- **`WaitForEndOfFrame` never resumes in batch mode** (nothing renders); wait a frame with `yield return null` in anything the bot runs.
- **One component per file** (Unity serialization): e.g. `VantageKeyItem.cs` and `VantageKeySlot.cs`.
- **Placeholders are built only when missing.** Once the user's model is in a placeholder prefab, Setup won't overwrite it; the menu item does. Exception: the boss's `Model` is replaced by the ISO Mech on every Setup (`VantageEnemyModels.Boss`); to use another boss model, change that step. Put the model under `Model`; keep `Eye`, `Eye Light` (robots) and `Muzzle` beside it, and `Main Rotor`/`Tail Rotor` (helicopter) named as they are. The model may have colliders (hits are forwarded).
- **Build Settings** must list `Assets/Scenes/SampleScene.unity` (index 0); scene reloads go through `VantageRun.ReloadLevel()`.
- **Body swap and Biped skeletons:** the template soldier is a 3ds Max Biped (`Bip001 …`) too, so old and new bones share names. An unmatched old bone (the template's `Spine2`) with matched bones below it is not carried along (the guns rode on the old, unanimated hands). The rifle-to-hand distance in `VantageEnemyModels.ShotsBatch` is the check (0.08 m like the template).
- **Player body:** the guns, holsters, `HitBox`es (`BodyPartHealth`), `Sight` and face helpers hang on the skeleton. Use **Vantage → Player → Use Character Model**, never a hand swap. It matches the humanoid muscle pose (not the T-poses), logs the height change (Cowboy 1.69 m → Adhurim 1.64 m), and switches `CharacterFace` off when the body lacks its blend shapes.
- **Camera:** never fade by size above ~5 m; keep the clearance cast instead. The extraction disables `ThirdPersonCamera` and `VantageCameraFader` to fly the camera.
- **NavMesh agent radius = body radius:** `VantageSetup.BakeNavMesh` sets the Humanoid agent type's radius (`ProjectSettings/NavMeshAreas.asset`) to the soldier prefab's capsule radius (0.3 m) before baking. At Unity's default 0.5 m, gaps the characters walk through (the L2 Office desks, ~0.6 m) were closed for the AI.
- **NavMesh and obstacles:** the bake switches every `NavMeshObstacle` off (`VantageSetup.BakeNavMesh`); enabled carving obstacles would be baked in as unwalkable and stay holes after the door unlocks. They carve at runtime only.
- **Spawns behind an exit:** Setup's `markBehindExit` tests the NavMesh path from each level's arrival to each spawn marker; a path through a door with that `ClearLevel` or the level's stair gate puts the marker in `VantageTowerLevels.BehindExit`. A new spawn marker behind a gate is excluded automatically; check the log line `Spawn markers behind a level's exit`.
- **Check reachability, not just kills:** the bot used to kill enemies directly, so an unreachable area passed. Gameplay that gates an area must have a bot check that paths to it (see the robot-room check). The NavMesh region map (`VantageFloorPlans`) shows islands at a glance.
- **NavMesh:** a NavMesh baked from script must be saved as an asset (`VantageSetup.saveNavMeshAsset`), otherwise the scene silently saves as binary. The gameplay model is excluded from the bake (`NavMeshModifier`).
- **Input:** the player uses the **old Input Manager**; active input handling is "Both". TakeCover is **Space**, Use is **E**, play again is **Enter**.
- **Render pipeline mapping:** `GraphicsSettings` default must be **`PC_RPAsset`**. Don't edit `ProjectSettings/*` while Unity is open.
- **Lighting:** shadow bias in PC_RPAsset must stay around 1/1 (shadow acne at 0.1/0.5). None of our added lights casts shadows.
- **URP materials:** pack vegetation and terrain layers need low/constant smoothness (handled by the world tool).
- **Batch screenshots** need `ShaderUtil.allowAsyncCompilation = false` (`VantageEditorUtil.Shot` does it); the first render after loading can come out garbled, so `Shots` renders the first view twice.
- **Bot and cover:** the bot turns `AutoTakeCover` off and leaves cover before teleporting.
- **Batch Play mode** (the bot): the scripting domain reloads on entering Play mode, so request and result travel via `SessionState`. Batch mode never renders a game view, so anything gated on visibility behaves as off-screen there.

## Performance notes

- **Rendering:** Forward+ (PC_Renderer), soft shadow quality low, additional-light shadows off, shadow distance 50 m with 2 cascades, SSAO at half resolution (medium blur). Lowered on 2026-10-21 for the user's laptop (Intel Iris Xe).
- **Tower:** walls, slabs, stairs occluder + occludee + batching static; glass, props, signs, lamps occludee + batching; doors, consoles, beacons dynamic. Occlusion baked. Marker renderers off (set in the editor, not at runtime).
- **AI:** only the current level's enemies exist. Doors more than one level away from the current level and the player's level sleep; doors sense actors every 0.15 s; kill doors react to kill events.
- **World:** trees are meshes without LODs, so there are few: ~2.6k trees/rocks within 220 m of the plot plus ~3.6k low-poly far conifers (was ~26k full trees), 450 m tree distance, grass at 60 m and density 0.7. The pack's `DistantForestRenderer` draws every instance every frame with no culling, so the horizon forest is thinned to 17.6k (was 70k).
- **Per-frame code:** no `Find*`, `GetComponent`, LINQ, string building or `OnGUI` per frame (the tower package's `OnGUI` and per-frame `FindGameObjectWithTag` were removed). HUD strings rebuild on change; `Remaining` counts once per frame with cached components; the camera checks sight every 0.1 s; robots think every 0.2 s.
- **Measure, don't guess:** F9 in Play mode (once with F10 occlusion on, once off). Batch-mode frame times mean nothing because nothing renders.

## Known gaps and open work

- **Not yet checked by a human in Play mode (new tower):**
  - (2026-10-26) the halved enemy fire on every level; the restart on the stairs below L2/L3 and its 3 s protection; the L3 side steps and landing (look and walk); the new handrails (outside stairs, L1 inner side); the deserter shot (does it read: warning shot, run, shot; is the slab sentry's glint/tracer visible from the ground camera?) and the objective captions and banner
  - the warning ring distances and the 5 s grace; whether the ring shots read (glint, tracer, dust) and the sentries feel fair, incl. the extra gun on the L3 slab and in the finale
  - the intro's pacing and framing (on a fresh run; the bot only checks shot 2)
  - feel and balance of the alarm hunt on L1 with only a pistol, and of the robot and boss fights
  - the camera inside the new rooms and on the outside stairs
  - the helicopter fly-in and camera pull-back, the end screen, Enter to restart
  - the security camera's sweep and cone readability; the key slot's key copy orientation
  - the player character's animation and guns in hand (since 2026-10-08)
  - frame time with the alarm on L1 and during the boss fight (F9, F10 on/off)
- Hunting L1 soldiers can follow the player a few metres out of the entrance during the alarm (the safety net returns them); the bot's "alerted soldiers stay on their level" check fails now and then because of it (2026-10-26, seed 90750).
- Safe arrivals keep L2's four Hall markers and L3's Barracks 0/1 and Comms 4 out of play (they see the restart points); L2 soldiers start in the rooms, the L3 Barracks starts empty.
- The playtest logger writes a negative session length for the session after a death reload.
- The professor's feedback (commit 2026-10-09) is not written down anywhere yet; the concept doc's 2026-10-14 revision row says so.
- **Environmental storytelling** (story props) and **per-space mood volumes** are not built in the new tower. The profiles still exist in `Assets/Vantage/Volumes/`; the scene only uses `Global Volume`.
- **Procedural methods** are only the seeded soldier placement.
- The L3 robot and the helicopter are placeholders. The boss mech is 5.1 × 4.3 m wide on a 0.96 m agent: check it on the helipad platform in Play mode (railings, the charge).
- `MK_Enemy_A_1` and `MK_Enemy_A_2` have no NavMesh on their floor (next to props) and are skipped.
- Licensing: Avaturn terms, Stylized Coin Pack licence, Low Poly Soldiers (demo) and ISO Mech licences, `Assets/Textures` source.
- `README.md` asset table still needs the Asset Store links and publisher check before hand-in.
- `Documentation/tower_*` screenshots show the old tower; render new ones (Setup's `-vantageShots`).
- Ideas: ammo refills per level, story props, per-level post-processing, LODs for trees.

## History (short)

- **2026-10-05:** first-person dropped; third person only (the template "Cowboy").
- **2026-10-06:** generated ProBuilder blockout tower with a procedural level 2, roof waves, compound, HUD and playtest logging.
- **2026-10-07:** the user's Blender military world replaced the bought industrial yard; minimap; the user's six-storey command tower replaced the blockout (four kill-to-unlock levels); playtest fixes found with the new autoplay bot (armed soldiers firing off-screen, smooth stairs, camera clearance, shadow acne, static flags and occlusion); cleanup; PC render pipeline actually used; furniture and UI pass (TMP-style HUD, minimap).
- **2026-10-08:** the player's body is the user's own avatar (`VantagePlayerCharacter`); HUD and minimap on TextMeshPro; hardcoded values removed (`VantagePhysics`, measured footprints); per-frame work cut; hunters stay on their level; spawns need NavMesh on their floor; `-vantageSeed`.
- **2026-10-14:** the user's new three-floor tower with a helipad replaced the command tower (now in `_UnusedAssets`). Design: alarm trap + security camera (L1), key and key slot (L2), patrol kill-door, terminal and rifle-only heavy robot (L3), boss then helicopter extraction (finale), 5 coins with an end screen and achievements, per-level checkpoint. Tower package scripts edited for performance (no `OnGUI`, no per-frame lookups, event-driven kill doors) and bridged to our level brain. New `VantageTower` setup (replaces `VantageCommandTower`), placeholders, bot rewritten (ALL PASSED, random seed and seed 13375). World rebuilt around the new plot (0 ledges).
- **2026-10-26:** after a playtest with 15 deaths (8 within 1–6 s of the L3 restart): enemy fire halved (damage per hit, burst pauses, aim error, drones and turrets); restart points half way down the stairs below L2/L3 with the gun drawn and 3 s protection, no spawns in view of them; the L3 stairs got a landing and side steps; handrails fill the model's gaps; the intro became a briefing (the deserter shot by the slab sentry, objective captions, a pistol banner).
- **2026-10-22:** smaller world (trees, props only within 220 m; lighter shadows/SSAO); L2 softlock fixed (a soldier could spawn behind the stairwell gate: `BehindExit`); NavMesh baked for the real body radius 0.3 m (was 0.5: the L2 Office behind its desks was cut off for the AI); Asset Store soldier body and ISO Mech boss (`VantageEnemyModels`), body-swap fix for Biped skeletons.
- **2026-10-21:** after the presentation: two sentries on the outside of the top and a warning ring around the tower (boundary with a reason), an intro flyover (the professor's cutscene suggestion), `VantageCinematic` shared by the intro and the extraction, bot groups and `-vantageOnly`.
- **2026-10-16 (2):** level 3 softlock fixed (robot room opens after the indoor soldiers), NavMesh baked without obstacles, floor-plan tool, presentation in `PaperDocumentation-/Presentation`.
- **2026-10-15/16:** playtest fixes: stair surfaces flush with the floor (~31°) plus a side entry, NavMesh blockers over flights, `AIMovement` floor-snap edit, soldiers without cover, covers only with room in front, 3 fewer enemies, E only at the L2 key slot, boss on the platform floor; hunt and patrol orders now actually move soldiers (broadcast messages).
