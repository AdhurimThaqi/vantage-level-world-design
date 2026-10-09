# VANTAGE — project notes for Claude and agents

This file describes the project **as it is now** and how to change it safely. It is the source of truth for any agent working here (e.g. `.claude/agents/vantage-watchdog.md`). If you change architecture, tools or rules, update this file in the same change.

## The project

- Solo student project by Adhurim Thaqi for the HSLU module **I.BA_LWD (Level & World Design, HS26)**. The module grades **level design** (layout, pacing, guidance, environmental storytelling, procedural methods, performance, playtesting) more than systems code. Weigh every change against that.
- The user may make it a **commercial game** later. So prefer root-cause fixes, verify gameplay changes with the autoplay test, keep the code lean, and flag licensing questions (the course template and Pistol Animset Pro must be licensed for commercial use before shipping).
- **The game:** a third-person shooter level. You start unarmed in front of a six-storey military command tower in a military training base, in a 1 km forested valley. You fight up through **four levels of rising difficulty**: ground + F1, F2 + F3, F4 + F5, then the roof. Every enemy on a level must die before red shutters on the inner stairs and the fire escape open to the next level. Clearing the roof ends the game ("THE TOWER IS SILENT").
- **Design source of truth:** `VANTAGE — Level & World Design Concept.md`. Read it before changing gameplay or layout. When the design changes, add a row to its *Revisions* table (it is a graded deliverable).
- Player-facing documentation is `README.md`; keep it in step.

## Work log

`WORKLOG.md` (workspace root) records every request and what was done about it, newest first. It applies to every Claude session and the `vantage-watchdog` agent:
- **At the start:** read its latest entries (what changed recently, what is still open).
- **After every task**, including small ones, questions answered with a check, and reviews that found nothing: add an entry at the top with **Asked** (the user's words), **Done** (files, tools, decisions), **Verified** (what ran and the result, or "not verified") and **Open** (what's left, what to check in Play mode). The format is at the top of the file.
- The log is history; this file is the current state. Update both when a change alters tools, rules or gameplay.

## Repository

- This folder is a **git repository** (`origin` = `github.com/AdhurimThaqi/vantage-level-world-design`, branch `main`). Commit only when the user asks.
- **Git LFS** is used for models, textures, audio, `.raw` terrain data and `*_TerrainData.asset` (see `.gitattributes`). NavMesh, LightingData, OcclusionCullingData, terrain layers and TerrainData are marked `binary` so line-ending conversion can't corrupt them.
- `_Backups/` holds scene copies taken before every automated change; `_UnusedAssets/` holds asset files moved out of the project, with a list in `MOVED_FILES.txt`. Both are git-ignored and local only.
- `Vantage_First-Person/` is the Unity project: **Unity 6000.0.58f2, URP** (PC_RPAsset). The only level scene is `Assets/Scenes/SampleScene.unity`.
- `Vantage_First-Person/Documentation/` has screenshots and maps for the hand-in (`tower_*`, `world_*`). The `level2_plan_seed_*.png` files are from the removed blockout tower.
- `Vantage_First-Person/Playtests/` is created on play: session reports, CSV position trails and performance captures.
- The lecturer's template original is at `C:\Users\at488\Downloads\LWD-TPCST-6000.3`. Use it as a reference only: its binary LightingData/NavMesh/terrain assets and `Main Camera.prefab` are corrupted at the source.

## Assets

| Folder | What | Rules |
|---|---|---|
| `Assets/Blender_Asset/CommandTower_Unity` | **User's own** six-storey tower (structure per storey, furniture, 32 doors, 108 lights, 202 `CommandTowerKit.GameplayMarker`s: 94 EnemySpawn, DroneSpawn, Patrol, PlayerEntry, Objective…). Has its own importer (Tools → Command Tower). | Source art, read-only. **Never edit inside the `CommandTower` prefab instance**; use instance overrides only (lights, static flags, ramp collider off). |
| `Assets/Blender_Asset/AdhurimCharacter` | **User's own** player character (`Adhurim_Avatar.fbx`, an Avaturn avatar exported from Blender: Mixamo-style skeleton, 4 meshes, packed textures). | Source art. Its import settings (Humanoid, no animation/cameras/lights) are set by `VantagePlayerCharacter`; its textures and materials are extracted to `Assets/Vantage/Characters/Adhurim_Avatar/` (tune those, not the FBX). |
| `Assets/Blender_Asset/MilitaryEnvUnity` | **User's own** 1 km world: terrain data, forest, military base, props, tiling textures. Its `MilitaryEnvBuilder` (Tools → Military Env → Build Everything) builds a *separate* scene; harmless, but not our level. | Read-only. Our world is built by `VantageMilitaryBase`, which only calls the pack's material step. |
| `Assets/TextMesh Pro` | TMP Essential Resources (TMP Settings, LiberationSans fallback font, shaders). | Package resources; the HUD needs them. Reimport via Window → TextMeshPro if deleted. |
| `Assets/ThirdPersonCoverShooter`, `Assets/PistolAnimsetPro` | Course template: character, soldier AI, cover, weapons, hit effects, animations. | Third-party. **One deliberate edit**: `CharacterMotor.AlwaysUpdateIK` (see Rules). Re-apply it if the package is ever reimported. |
| `Assets/Vantage` | All our code, prefabs, materials and generated data. | See below. |
| `Assets/Vantage/World` | Generated by tools: terrain data and layers, reshaped road meshes, tree prefabs with colliders, `Sky_GoldenHour.mat`, `Minimap.png`, `Tower/` stair surface meshes. | Regenerated by the tools; don't hand-edit. |

Removed, don't reintroduce:
- the industrial pack (`RPG_FPS_game_assets_industrial`)
- `Starter Assets` (first-person)
- `TutorialInfo` and `Readme.asset`

All of these are in `_UnusedAssets/`.

## Scene (`SampleScene`) root objects

| Object | Made by | Contents |
|---|---|---|
| `CommandTower` (prefab, at (5.2, 4.04, -66), rotated 180°) | placed by the user | The main building. |
| `VANTAGE Tower Setup` | `VantageCommandTower.Setup` | Entrance walk ramps, 18 smooth stair surfaces, 6 level gates, furniture covers, `Tower Levels` (the level manager), pistol and rifle pickups. |
| `VANTAGE World - Military Base` | `VantageMilitaryBase.Build` | Terrain, buildings, props, roads, distant forest, ~600 covers, world-edge walls. |
| `Third Person Rig` (prefab) | template rig, body by `VantagePlayerCharacter` | The player `Adhurim_Avatar` (the template "Cowboy" with the user's body: `VantageArsenal`, `VantageThirdPersonPlayer`), and the camera with `VantageCameraFader`. |
| `VANTAGE Minimap` | `bakeMinimap` | `VantageMinimap` settings (baked map, world rect, tower). |
| `VANTAGE Playtest Tools` | tools | `VantagePlaytestLogger`, `VantagePerfCapture`. |
| `NavMesh` | `VantageSetup.BakeNavMesh` | `NavMeshSurface` (saved as an asset next to the scene). |
| `Global Volume`, `Directional Light` | — | Post-processing; golden-hour sun at 24°. |

## Code map (`Assets/Vantage`)

### Runtime (`Scripts/`)

| Script | Role |
|---|---|
| `VantageTowerLevels` | The four levels. Spawns each level's soldiers and drones (and the roof turret) from the tower's markers with a **seed** (spread over rooms first) when the level starts; markers are chosen by their **height**, not their `floor` field, and enemy spawns only where NavMesh lies on the marker's own floor (others are logged and skipped; soldiers stand on that NavMesh point). `-vantageSeed N` on the command line replays a layout. Opens that level's `VantageFloorGate`s when everyone is dead, activates the level's reward, and switches tower lights per level. **Hunting:** with ≤ `HuntWhenRemaining` enemies left or `HuntAfterQuietSeconds` without a kill, the rest come for the player, but only while the player is on that level. A safety net returns enemies that left their level to a spawn point. The tower footprint is measured from its meshes at Awake (no hand-typed bounds). Exposes `Instance`, `EnemySide`, `Current`, `Remaining`, `Hunting`, `LevelAt()`, `StoreyAt()`, `StoreyName()`, `LevelCleared`. Difficulty per level is in its `Levels` list (Inspector). |
| `VantageFloorGate` | Red stair shutter: BoxCollider over the whole flight plus a carving `NavMeshObstacle`. `Open()` rolls it up and turns it green. |
| `VantageEnemyAggression` | On every soldier: starts idle, avoid distance 1.2 m, never retreats, cover only within 9 m; locks on and fires when it sees the player on its floor. |
| `VantageDrone` | Drones and the turret (`Mobile = false`). Telegraphs shots with its eye; `All` lists live drones. |
| `VantageArsenal`, `WeaponPickup` | Player starts unarmed; pickups unlock Pistol/Rifle in the template inventory. Pickups don't refill ammo for an owned weapon. |
| `VantageThirdPersonPlayer` | Player health (100 HP, delayed regen to 60 %), death, level restart after 3 s. Adds `VantagePlayerHUD`. |
| `VantagePlayerHUD` | uGUI + TextMeshPro built in code, no boxes or labels: health number and bar (damage trail, regen-limit tick) bottom-left; weapon, rounds and reserve bottom-right; level, name and one pip per enemy top-centre; aimed-at enemy's health, kill marker, pickup notice, level-cleared banner in the centre; damage flash, low-health vignette, death screen. |
| `VantageUI` | Shared UI building blocks for the HUD and minimap: palette, canvas/rect/image/text/bar helpers, generated sprites. Texts are **TextMeshPro**: a dynamic TMP font made once at runtime from Bebas (`Resources/VantageFont.ttf`, copied from the template) with TMP's default font as fallback (∞), one shared material with an underlay shadow, and `wordSpacing` because Bebas' own space is too narrow (write single spaces). |
| `VantagePhysics` | Shared ray masks (`Solid` = geometry, `Sight` = geometry + characters, built from `CoverShooter.Layers`) and body points (`Eye`, `AimPoint`) read from the character's collider. Use these instead of layer numbers or fixed heights. |
| `VantageMinimap` | Top-left minimap, built in code: baked image, north up, player arrow, tower pinned to the edge, nearby enemies, distance, current space. |
| `VantageCameraFader` | Execution order 1000. Keeps the camera 0.3 m clear of walls (sphere cast from the head) and fades only objects ≤ 5 m. |
| `VantageCoverUtil` | Adds template cover markers (layer 8 triggers); `AddForOriented` follows the prop's rotation. |
| `VantagePlaytestLogger`, `VantagePerfCapture` | Session reports, spaces = outside + 4 levels, F1 overlay (hidden by default), F8 mark, F9 perf capture, F10 occlusion toggle. |
| `VantageEvents`, `VantageKillReporter`, `VantageProceduralAudio` | Shared events (`ActivePlayer()`, looked up once per frame; kills, pickups, seed, completion); soldier kill reports; synthesised drone hum. |
| `Testing/VantageAutoplayBot` | Editor-only (`#if UNITY_EDITOR`) automated playtest; see *Workflow*. |

### Editor (`Editor/`, the **Vantage** menu)

| Script | Menu / batch | Role |
|---|---|---|
| `VantageCommandTower` | **Tower → Set Up Command Tower** / `SetupBatch` | Turns the tower into the level. It removes old setup objects, then: fixes furniture placement (`placeFurniture`, see Rules), sets static flags (structure: occluder, occludee, batching; furniture: occludee, batching; doors stay dynamic), raises the sun to 24°, makes the tower lights realtime, adds entrance walk ramps, builds the **smooth stair surfaces** (disabling the tower's own ramp collider), builds the gates, adds furniture covers, rebuilds **our soldier prefab**, creates the level manager and pickups, sets the player start, and bakes the NavMesh. `SetupBatch` also bakes occlusion, renders shots and runs a gate check. |
| `VantageMilitaryBase` | **World → Build Military World** / `RunBatch` | Builds the world at Blender coordinates, fits the plot to the `CommandTower` (required), levels the terrain under it and relaxes it into slopes, places the base and covers, then runs the tower setup, checks paths out (breaches fences if needed), and bakes the minimap. |
| | **World → Rebake Minimap** / `MinimapBatch`; `LedgeReportBatch` | Minimap only; ledge/slope and NavMesh connectivity maps around the tower. |
| `VantagePlayerCharacter` | **Player → Use Character Model** / `ApplyBatch [-vantageModel path] [-vantageShots dir]` | Puts a humanoid model on the player rig (default `Adhurim_Avatar.fbx`, or the model selected in the Project window): imports it as Humanoid, extracts textures (adds the file extensions Blender leaves off) and materials, links them from the FBX itself (`FbxTextureLinks`; hair is alpha-clipped), puts the old skeleton in its T-pose and gives the new one the **same humanoid muscle pose** (`HumanPoseHandler`, the mapping animations use), moves every gun, holster, hit box and helper to the matching new bone (humanoid mapping, then bone name) keeping its world rotation and its offset from the bone, returns the new skeleton to its rest pose, remaps references, switches `CharacterFace` off if the body lacks its blend shapes, deletes the old body and sets the avatar. Logs the height change. Re-runnable; existing material files are kept. |
| `VantageAutoplay` | **Test → Run Autoplay Test** / `RunBatch` | Runs the bot in Play mode; batch exit code 0 = passed. |
| `VantageSetup` | **Bake NavMesh For Enemies**, Add Pistol/Rifle Pickup | Also `VantageLayers` (ensures layers 8–11 exist). |
| `VantageEditorUtil` | **Performance → Bake Occlusion Culling** | `ScenePath`, prefab paths (`PlayerRigPath`), `Arg()` (batch arguments), `Shot()` (batch screenshots). |

### Prefabs (`Prefabs/`)

- `Soldier.prefab`: **generated by Setup**, a variant of the template soldier with its guns in a real `CharacterInventory`, `AlwaysUpdateIK` on, Animator set to always animate, `VantageEnemyAggression` and `VantageKillReporter`.
- `Drone.prefab`, `Turret.prefab`: tuned by hand; nothing regenerates them.
- `Third Person Rig.prefab`: the player rig. Its body is set by `VantagePlayerCharacter` (don't hand-edit the bones; re-run the tool). Its template canvas keeps only `Scope`; the template squad bar (two AI portraits with ability icons), health bar, enemy health bar and ammo text were removed. Our HUD replaces them.

## Workflow for changes

1. **Unity must be closed** for batch mode (it fails on the project lock). Check `Vantage_First-Person/Temp/UnityLockfile`. If Unity is open with unsaved changes, ask the user to save and close it.
2. **Back up** `Assets/Scenes/SampleScene.unity` to `_Backups/SampleScene_before_<what>_<date>.unity` before any scene change.
3. **Compile offline first.** Build a scratch csproj from `Assembly-CSharp-Editor.csproj`:
   1. Drop its `<Compile>`, `<ProjectReference>` and `<Analyzer>` items.
   2. Make the `Library\...` HintPaths absolute.
   3. Add `Assets/ThirdPersonCoverShooter/Scripts/**`, `Assets/Vantage/**` and `Assets/Blender_Asset/*/Scripts/**`.
   4. Reference `Library/ScriptAssemblies/Unity.AI.Navigation.dll`.
   5. Run `dotnet build`.
4. **Run the tool in batch mode:**
   ```
   "C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe" -batchmode -projectPath "<...>\Vantage_First-Person" -executeMethod Vantage.EditorTools.VantageCommandTower.SetupBatch -vantageShots "<scratch>\shots" -logFile "<scratch>\unity.log"
   ```
   Our log lines start with `[Vantage]`. Look at the screenshots: batch mode can't play, but it can render.
5. **Verify gameplay** with `-executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch`. It enters Play mode, so start it with `Start-Process … -PassThru` and a timeout of about 8 minutes, then read the lines starting with `[Bot]`. It checks that soldiers fire and deal damage, that clearing level 1 opens its gates and spawns level 2, and that every opened flight is climbed without stalling. With `-vantageShots <dir>` it also saves `hud_combat.png` and `hud_level_cleared.png`, the game view **with the HUD** (the only way to see the UI without a human in Play mode). Extend the bot when you add gameplay.
6. **Re-run what the change requires:**

   | Changed | Re-run |
   |---|---|
   | Tower, gates, stairs, enemies, levels, pickups | Tower → Set Up Command Tower |
   | World, terrain, plot, base props | World → Build Military World (includes the tower setup) |
   | Navigable geometry | Bake NavMesh For Enemies |
   | Layout visible from above | World → Rebake Minimap |
   | Occluders, static flags | Performance → Bake Occlusion Culling |
   | Any gameplay | Test → Run Autoplay Test |
7. **Update docs** in the same change: this file, `README.md`, and the concept doc's *Revisions* table if the design changed. Report what's verified and what isn't.
8. **Log it in `WORKLOG.md`** (see *Work log* above), every task, before reporting back.

## Rules that break silently

- **Targeting:** the template AI only targets objects on **layer 10 "Character"** with a `BaseActor` whose `Side` differs from theirs. Enemies are side 0, the player side 1. Layers 8–11 (Cover, Scope, Character, Zones) are hard-coded in `CoverShooter.Layers`.
- **Layers and heights:** never write layer numbers or "+ 1.6 m" for a head in new code; use `VantagePhysics.Solid` / `Sight` and `Eye()` / `AimPoint()`, so another body height or layer change can't break sight lines.
- **Player body:** the guns, holsters, `HitBox`es (`BodyPartHealth`), `Sight` and face helpers hang on the skeleton. Swapping the body by hand loses them; use **Vantage → Player → Use Character Model**. Don't match the two avatars' own T-poses (their humanDescription skeletons differ: guns ended up floating beside the hands); match the muscle pose. Check with `ShotsBatch` (`player_hand_*` shots in `Pistol_Idle`, skinning forced per render). The new model must be humanoid-mappable; the swap logs the height change (Cowboy 1.69 m → Adhurim 1.64 m), and hit boxes keep their world size. The template's `CharacterFace` sets blend shapes 0–9 by index (Cowboy face shapes); the tool switches it off when the new body has fewer (otherwise "Array index (9) is out of bounds" every frame).
- **Damage** arrives as `SendMessage("OnHit", CoverShooter.Hit)`. `CharacterHealth.Hurt` never fires (package bug); use `Changed`.
- **Soldiers must use `Assets/Vantage/Prefabs/Soldier.prefab`.** The template `Soldier.prefab` keeps its guns in the deprecated `CharacterMotor.Weapons`, while the AI arms from `CharacterInventory`. Template soldiers spawn unarmed and run away.
- **`CharacterMotor.AlwaysUpdateIK`** (template edit, marked "VANTAGE change"). Template IK, and with it `gun.Allow`, only runs while the character is visible on screen. Without the flag, off-screen soldiers can never fire.
- **No step-ups:** the template character cannot step up ledges (its velocity is kept horizontal). Every stair or step needs a smooth walkable surface, and floors must be flush or ramped. The tower's own `TWR_StairColliders` are **disabled on purpose**: their step edges sat 15 cm above them, and on the fire escape they run backwards and formed a ceiling. `smoothStairs` replaces them, using the slope-limited upper envelope of the real steps (≤ 25.6°, the template slows above 26°). Flight direction is decided against the real geometry (`rampPieces` misfit test).
- **Furniture:** the tower export has floating and badly placed pieces. Don't fix them by hand in the prefab. `placeFurniture` (run by Setup) works on instance overrides:
  - floor pieces hovering above the floor are set down;
  - wall pieces (`WallMounted`, `WallBacked` lists) are moved against the nearest wall, measured at three heights so window openings don't count; a wall piece with no wall within 0.6 m is set on the floor;
  - pieces in a doorway or in its door's swing are slid along the wall, else into the room, and are **hidden** if they fit nowhere (logged; revert the override to bring one back).
  Re-running moves nothing. Check the `[Vantage] Furniture:` log line after a setup.
- **Entrance rails:** the fire escape's ground platform had two guard rails across its entrance steps (knee and chest height). `clearEntranceRails` (Setup) casts rays from 0.25 to 1.8 m, from outside *and* inside (mesh colliders are only hit from the front of a face), and cuts any connected piece that is a thin bar ≥ 1 m wide across the way, into `World/Tower/<collider> Entrance Open.asset`. A cut that leaves faces behind still blocks the capsule while rays pass, so always remove whole pieces.
- **Camera:** tower storeys are single meshes. Never fade by size above ~5 m (whole floors went transparent); keep the clearance cast instead.
- **NavMesh:** a NavMesh baked from script must be saved as an asset (`VantageSetup.saveNavMeshAsset`), otherwise the scene silently saves as binary. Gates are excluded from the bake (`NavMeshModifier`) and block agents with carving obstacles.
- **Input:** the player uses the **old Input Manager**; its 15 custom axes are merged into `ProjectSettings/InputManager.asset`, and active input handling is "Both". TakeCover is **Space**.
- **Render pipeline mapping:** `GraphicsSettings` default must be **`PC_RPAsset`**. Quality levels Medium–Ultra use the default; Very Low and Low use `Mobile_RPAsset` (80 % render scale, one shadow cascade). Until 2026-10-07 the default was the mobile asset and "Ultra" pointed at a missing asset, so the game rendered at 80 % with mobile shadows and every PC setting was ignored. Tune the asset that is actually used, and don't edit `ProjectSettings/*` while Unity is open (it can overwrite them).
- **Lighting:** shadow bias in PC_RPAsset must stay around 1/1. At 0.1/0.5 with a low sun you get shadow acne (the "flickering textures" bug). The tower's lights were Baked, which does nothing in URP without a lightmap bake; Setup makes them realtime without shadows.
- **URP materials:** pack vegetation and terrain layers need low/constant smoothness, otherwise they mirror the sky (handled by the world tool).
- **Batch screenshots** need `ShaderUtil.allowAsyncCompilation = false` (`VantageEditorUtil.Shot` does it).
- **Bot and cover:** the player has the template's `AutoTakeCover` (stand still next to cover and you snap into it). The bot turns it off and leaves cover before teleporting; otherwise it ends up crouched out of sight or held in place by the cover.
- **Batch Play mode** (the bot): the scripting domain reloads on entering Play mode, so request and result travel via `SessionState`. Batch mode never renders a game view, so anything gated on visibility (`Renderer.isVisible`, Animator culling) behaves as off-screen there.

## Performance notes

- **Rendering:** Forward+ (PC_Renderer), soft shadow quality medium, additional-light shadows off (none of our lights cast shadows), shadow distance 50 m with 4 cascades, SSAO on.
- **Tower:** the structure is occluder, occludee and batching static, furniture occludee and batching static, doors dynamic; occlusion is baked. Only the lights around the player's level are on.
- **AI:** enemies spawn level by level, so only one level's AI runs at a time.
- **World:** trees are meshes without LODs (~26k) at a 450 m tree distance and 80 m grass distance.
- **Per-frame code:**
  - The HUD rebuilds its strings only when a value changes.
  - `VantageTowerLevels.Remaining` counts once per frame with cached component references.
  - `VantageCameraFader` caches each obstacle's renderers.
  - Keep new code free of per-frame `Find*`, `GetComponent`, LINQ and string building.
- **Measure, don't guess:** F9 in Play mode (once with F10 occlusion on, once off). Batch-mode frame times mean nothing because nothing renders.

## Known gaps and open work

- **Not yet checked by a human in Play mode:**
  - the new player character: animation, guns in the hands, holsters, hit boxes (moved from the Cowboy; the bot only sees it in two HUD shots)
  - camera feel near walls
  - damage balance and ammo across four levels (pickups don't refill ammo once a weapon is owned)
  - drones inside small rooms and around gates
  - minimap readability
  - frame time on the roof and outside (F9)
- **Environmental storytelling** (five story props) and **per-space mood volumes** belonged to the old blockout tower and are not yet rebuilt in the command tower. The profiles still exist in `Assets/Vantage/Volumes/`; the scene only uses `Global Volume`.
- **Procedural methods** are now only the seeded enemy placement. The procedural level-2 generator went with the old tower.
- Twice (seeds 41562, 53953) level-1 soldiers were found on floor 3 seconds after spawning; the safety net returned them. Since spawns need NavMesh on their own floor (`MK_EnemySpawn_F1_Lounge_1`, on a picnic table, is skipped) seed 53953 is clean, but the cause isn't proven; soldier names now include their spawn marker, so a recurrence names it.
- Licensing: check the Avaturn terms for commercial use of the player avatar.
- The fire-escape gate shutter looks bright orange in the editor (its darker tint is applied at runtime).
- `README.md` asset table still needs the Asset Store links and publisher check before hand-in.
- Ideas: ammo pickups or refills per level, story props in the tower, per-level post-processing, LODs for trees.

## History (short)

- **2026-10-05:** first-person dropped; third person only (the template "Cowboy").
- **2026-10-06:** generated ProBuilder blockout tower with a procedural level 2, roof waves, compound, HUD and playtest logging. 1,856 unused pack files moved out.
- **2026-10-07:**
  - The user's Blender **military world** replaced the bought industrial yard (pack removed), and the minimap was added.
  - The user's **command tower** replaced the blockout, with the four-level kill-to-unlock structure.
  - **Playtest fixes**, found with the new autoplay bot:
    - soldiers armed and able to fire off-screen
    - smooth stairs
    - camera clearance instead of floor fading
    - shadow acne fixed
    - static flags and occlusion
  - **Cleanup:** 14 dead scripts removed (first-person, the old tower builder, level 2 generator, waves), along with Starter Assets and the tutorial files.
  - **Efficiency:** the PC render pipeline asset is now actually used; HUD, enemy count and camera fader no longer allocate every frame.
  - **Furniture and UI pass:**
    - furniture fixed by Setup (floating extinguishers and racks, a locker blocking the fire-escape entrance);
    - new HUD and minimap style (`VantageUI`, Bebas);
    - template squad bar removed; playtest overlay behind F1;
    - one-off tools removed: the tower diagnostic batches, `VantageUnusedAssets`, the particle material fix (already applied).
- **2026-10-08:**
  - The player's body is the user's own avatar (`VantagePlayerCharacter`).
  - HUD and minimap moved to TextMeshPro.
  - **Hardcoded values removed:**
    - layer masks moved to `VantagePhysics`;
    - eye and aim heights are read from colliders;
    - the tower footprint is measured;
    - top storey and storey names come from the levels;
    - marker types, enemy side and drone spawn height are named.
  - **Per-frame work cut:**
    - `ActivePlayer()` is cached per frame;
    - the minimap no longer calls `Find*`;
    - the drones' eye material is cached.
  - **Fixes:**
    - hunters only hunt while the player is on their level;
    - drones under the ceiling no longer count as the floor above;
    - spawns are chosen by marker height;
    - the knee-height rail across the fire-escape entrance is gone;
    - the bot tries every soldier for the firing test.
  - `Military_Env_Unity` was renamed to `MilitaryEnvUnity` (GUIDs kept).
  - Second pass: guns no longer float beside the new character's hands (muscle-pose matching); `CharacterFace` switched off (no blend shapes); spawns need NavMesh on their floor; bot is immune to auto-cover; `-vantageSeed`.
