# VANTAGE — project notes for Claude

Solo student project for the HSLU module I.BA_LWD (Level & World Design, HS26). A third-person level in Unity (it started as first person; that was dropped on 2026-10-05): the player climbs an abandoned military watchtower that is still guarded by its automated defence. The design source of truth is `VANTAGE — Level & World Design Concept.md` in this folder. Read it before changing gameplay or layout. The module assesses level design (layout, pacing, guidance, storytelling, procedural methods, performance, playtesting) more than systems.

## Layout

- `Vantage_First-Person/` is the Unity project (Unity **6000.0.58f2**, URP). The level scene is `Assets/Scenes/SampleScene.unity`.
- `_Backups/` holds scene copies taken before each automated change.
- The professor's template it was built from is at `C:\Users\at488\Downloads\LWD-TPCST-6000.3` (Built-in RP, "Third Person Cover Shooter" package). Use it as a reference only. Its binary `LightingData`/`NavMesh`/terrain assets and `Main Camera.prefab` are corrupted at the source (a git eol rule on binaries), so never copy them.

## What is in the Unity project

- `Assets/ThirdPersonCoverShooter`, `Assets/PistolAnimsetPro`: the template package (enemy AI, health, guns, hit reactions, third-person player). Treat them as third-party code.
- `Assets/Vantage/` contains all of our own code, materials, prefabs and volume profiles:
  - `Scripts/`
    - `VantageArsenal`: makes the "Cowboy" player start unarmed; weapons unlock through pickups.
    - `WeaponPickup`: the pistol and rifle pickups (unlocks them in `VantageArsenal`).
    - `VantageThirdPersonPlayer`: player health (100 HP, delayed regen up to 60 %; replaces the template's 400 HP + 5 HP/s that made him unkillable), death and level restart. Adds `VantagePlayerHUD`.
    - `VantagePlayerHUD`: our uGUI HUD built in code (vitals, weapon/ammo, hostiles/waves, pickup notice, damage flash, low-HP pulse, death screen). Hides the template's HealthBar/GunAmmo for the player.
    - `VantageCameraFader`: fades walls between the third-person camera and the player (URP).
    - `VantageCoverUtil`: adds template-style cover markers to tower pieces, props and level 2 walls.
    - `VantageDrone`: drones and the mast turret.
    - `VantageWaveSpawner`: the roof waves.
    - `VantageLevel2Generator`: seeded procedural layout for level 2.
    - `VantageProceduralAudio`: synthesised wind, hum and radio.
    - `VantagePlaytestLogger`, `VantagePerfCapture`: playtest logs and performance captures.
    - `VantageEvents`, `VantageKillReporter`: shared gameplay events and soldier kill reporting.
    - Unused leftovers from the first-person version (they still compile, nothing in the scene uses them): `VantagePlayer`, `VantageWeapons`, `VantageHUD`, `VantageViewSwitch`. The V view switch is gone from the game. Remove these together with their references in `VantageSetup`, `VantageAutoSetup`, `VantageEnhance` and `VantageThirdPerson`.
  - `Editor/`: the **Vantage** menu.
    - `VantageTowerBuilder` (+ `.Compound.cs`, `.Entrance.cs`, `.Guidance.cs`): builds the tower blockout from ProBuilder shapes, the military compound, the entrance, and the way-up guidance (glowing yellow floor line, chevrons, yellow step edges, framed fire escape doors; no colliders).
    - `VantageUnusedAssets`: moves unused pack files out to `../_UnusedAssets/`.
    - `VantageEnhance`: drones, approach, waves, mood, logging, NavMesh.
    - `VantageAutoSetup`: the full one-shot setup.
    - `VantageAtmosphere`: per-space Volumes, sound sources and occlusion areas.
    - `VantageThirdPerson`: extracts the third-person rig from the template.
    - `VantageSetup`: player setup, pickups, NavMesh bake, particle material fix.
- The tower (`VANTAGE Tower` in the scene) is **generated** by `VantageTowerBuilder`. Rebuilding it replaces manual edits made inside it. Change the builder code, or tell the user first.

## Running setup without the editor

Unity must be **closed** on this project, otherwise batch mode fails on the project lock:

```
"C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe" -batchmode -projectPath "<...>\Vantage_First-Person" -executeMethod Vantage.EditorTools.VantageEnhance.RunBatch -vantageShots "<scratch>\shots" -logFile "<scratch>\unity.log"
```

- `VantageEnhance.RunBatch` rebuilds the tower in place, enemies, mood, logging and NavMesh, bakes occlusion, and saves screenshots plus `Documentation/level2_plan_seed_*.png`.
- `VantageAutoSetup.RunBatch` is the full setup from a bare scene.
- The log lines to look for start with `[Vantage]`.
- Back up `SampleScene.unity` to `_Backups/` before running.

Before launching Unity, check the C# offline with `dotnet build` on a scratch csproj built from `Assembly-CSharp-Editor.csproj`. The steps: drop its `<Compile>`, `<ProjectReference>` and `<Analyzer>` items, make the `Library\...` HintPaths absolute, add `Assets/ThirdPersonCoverShooter/Scripts/**` and `Assets/Vantage/**`, and reference `Library/ScriptAssemblies/Unity.StarterAssets.dll` and `Unity.AI.Navigation.dll`.

## Rules that are easy to get wrong

- The template character **cannot step up ledges** (velocity is kept horizontal on flat ground; the template has ramps, never stairs). Every stair gets an invisible `Walk Ramp` BoxCollider instead of its step MeshCollider (`AddWalkRamp`), and floors the player walks onto must be flush or ramped.
- `VantageEnhance.Apply` keeps existing `Drone.prefab`/`Turret.prefab` (the user tunes them in the editor). Only **Vantage → Drones → Rebuild Drone Prefabs From Code** regenerates them.

- The template AI only targets objects on **layer 10 "Character"** with a `BaseActor` whose `Side` differs from theirs. Enemies are side 0, the player is side 1. Layers 8–11 (Cover, Scope, Character, Zones) are hard-coded in `CoverShooter.Layers`.
- Damage travels as `SendMessage("OnHit", CoverShooter.Hit)` to the hit collider. `CharacterHealth.Hurt` never fires (bug in the package), so use `Changed`.
- A NavMesh baked from script must be saved as an asset (`VantageSetup.saveNavMeshAsset`). Otherwise the scene silently saves as binary.
- The per-space Volumes sit on layer 2 (Ignore Raycast) so they never block bullets or AI sight. Cameras need that layer in their volume mask.
- The template's player uses the old Input Manager. Its 15 custom axes (Fire, Zoom, TakeCover, Roll*, Grenade, and so on) were merged into `ProjectSettings/InputManager.asset`. Active input handling is "Both".
- `Assets/AlterunaFPS/Scripts` is renamed to `Scripts~` (disabled) because the Alteruna package is not installed and it broke all compilation. Rename it back to re-enable it.
- This folder is not a git repository. The surrounding repo is the user's home directory, so don't commit there.

## Status (2026-10-06)

- Done and verified: template merged, tower blockout, first enemy pass. The user approved the result in the editor. (First-person combat and the V view switch were built first, then removed; see below.)
- Applied and verified via batch screenshots: the `VantageEnhance` pass.
  - Guard-hut approach with the pistol, 2 door drones, a slow drone in the lobby.
  - Procedural level 2 with 3 drones; plans for seeds 1234 and 52071 are in `Vantage_First-Person/Documentation/`.
  - 2 soldiers + 2 drones on level 3; turret and 1 soldier on the roof, plus the 3 roof waves.
  - Per-space Volumes, sound guidance, golden-hour sun, occlusion areas (baked), playtest logger and performance capture.
  - The user moved the tower to (65.4, -0.4, 39.5); rebuilds keep it there.
- **Third person only (user decision, 2026-10-05).** The first-person rig, Starter Assets cameras and V switch were removed from the scene. The player is the template's "Cowboy" (in the `Third Person Rig` instance).
  - `VantageArsenal` makes him start unarmed; pickups unlock Pistol, then Rifle (the Sniper stays locked).
  - `VantageThirdPersonPlayer` handles death and restart; `VantageCameraFader` fades blockers for URP.
  - The first-person scripts (`VantagePlayer`, `VantageWeapons`, `VantageHUD`, `VantageViewSwitch`) are unused but still compile. They can be removed together with their editor references.
  - The concept doc was updated to third person on 2026-10-06 (see its *Revisions* section).
- Cover: `VantageCoverUtil` adds template-style covers (layer 8 trigger boxes, forward into the obstacle) to tower cover pieces, props and generated level 2 walls. The roof gets no cover by design.
- Look: the tower uses the industrial pack's concrete textures; industrial props are placed by `dressWithProps`.
- Size (2026-10-06): `VantageUnusedAssets.MoveBatch` moved 1,856 unused pack files (812 MB) to `../_UnusedAssets/` (list in `MOVED_FILES.txt`). It verified missing references stayed at 20 before and after; those 20 already existed. Assets are now 607 MB, of which 576 MB is LFS-tracked. Files our editor tools name by path are kept. To restore a file, move it back with its `.meta`.
  - `.gitignore` excludes `_UnusedAssets/` and `_Backups/`.
  - `.gitattributes` marks binary NavMesh/LightingData/OcclusionCullingData as `binary`.
- The tower stands on the asphalt yard: `footprintGround` sets its base to the median ground height. It was buried 2.5 m after the user moved it onto the raised asphalt.
- Military compound (`VantageTowerBuilder.Compound.cs`): perimeter wall, checkpoint, sandbag nests, depot, tents, floodlights and flag. Every piece is ground-snapped and skipped if blocked; the build log lists why each skip happened.
- Environment sound: template wind bed (2D), procedural sea, generators and floodlight buzz.
- Tower textures use world-space UVs (continuous across pieces).
- **Not yet playtested in Play mode** (batch mode cannot play). Things to watch first: drone movement and wall sliding in tight level 2 rooms, drone accuracy and damage balance, whether soldiers walk through runtime-generated level 2 walls (they rely on NavMeshObstacle carving), and the roof waves triggering.
- 2026-10-06 fixes (code compiled offline, **not yet tested in Play mode**): free-standing drone hum fades when no living drone is on its floor (`VantageDrone.All`); player health; new HUD; stair walk ramps + entrance + hut floor via the patch menu item. Backups: `_Backups/SampleScene_before_stairs_entrance_*.unity`, `_Backups/Drone_user_tuned_*.prefab`.
- 2026-10-06 17:21: tower rebuilt in batch with all of the above. The user found the climb unclear, so the route is now: entrance → yellow line → lobby stairs → level 2 **authored L-shaped hall** (generated rooms only beside it, `VantageLevel2Generator.Halls`) → wide framed door → fire escape stairs → wide framed door → level 3 (shaft railed, fallen slab moved) → ramp. Checked visually with `VantageEnhance.RouteShotsBatch -vantageShots <folder>` (route screenshots, scene untouched). Walking it in Play mode is still untested.
- Documentation renders without touching the scene: `-executeMethod Vantage.EditorTools.VantageEnhance.DocumentationBatch`.
- Open ideas: styled uGUI HUD instead of IMGUI, remove the unused first-person scripts, a full art pass on the roof (vertical slice), Asset Store links for the README asset table.
