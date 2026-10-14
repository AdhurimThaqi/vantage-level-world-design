# VANTAGE Tower: 3 levels + finale (design)

Date: 2026-10-14. Status: awaiting user review. Approach agreed in chat: **C (hybrid)**.

## Goal

Replace the old six-storey `CommandTower` (four kill-to-unlock levels) with the user's new Blender tower `Assets/VANTAGE_Tower` (`VANTAGE_Tower_Level.fbx` + `VANTAGE_Tower_Gameplay.fbx`). The new game: three authored levels and a helipad finale, with an alarm trap, a security camera, a key puzzle, a rifle-only robot, a boss and a helicopter extraction. Coins reward exploration. Everything stays optimized (no per-frame lookups, no OnGUI, only the current level's AI running).

Weighed against the module: this is a level-design change first (pacing curve, guidance, gating, optional paths). Systems are kept as small as the design allows.

## Decisions (from the user)

| Topic | Decision |
|---|---|
| Ending | Boss on the helipad, then helicopter extraction (end scene). Ending A (floor breaks) is not used; the cracked section stays as set dressing. |
| Coins | Exploration reward. HUD counter and end-screen tally plus achievements. **1–2 per level, ~5 total**, spots chosen by Claude, off the critical path. Model: PolyKebap Stylized Coin Pack (gold). |
| Alarm | Full alert: siren, spinning beacons, red lights, shutter closes, L1 enemies hunt the player; stops when L1 is cleared. |
| Security camera | Sweeps with a visible cone. Seeing the player sets off the same alarm early. Can be shot to disable it. |
| Weapons | Pistol pickup just inside the entrance (before the alarm). The L2 Armory gun is the **rifle**. Only the rifle damages the heavy robot ("You need a better gun"). |
| Robot, boss, helicopter models | The user models them in Blender. Until then: placeholder primitives (one combined mesh, one material each) in prefab slots; swapping the model needs no code change. |
| Old tower | Leaves the scene. Its files move to `_UnusedAssets/` once the new tower works. |
| Optimization | Required throughout (see *Performance*). |

## Architecture (approach C)

| Part | Owner | Change |
|---|---|---|
| Import: colliders, static flags, doors, shutter, triggers, interactables, pickups, materials from Blender custom properties | `VANTAGE_Tower/Scripts/Editor/VantageTowerPostprocessor.cs` (user's package) | Keep. Small marked edits only if needed (`// VANTAGE change`). |
| Doors, shutter, triggers, consoles, key slot | `VantageDoor`, `VantageTrigger`, `VantageInteract`, `VantagePickup` (user's package) | Keep the Blender-driven logic. Edits, each marked `// VANTAGE change`: player from `VantageEvents.ActivePlayer()` instead of `FindGameObjectWithTag` per frame; `OnGUI` removed, prompts and messages go to `VantagePlayerHUD`; KILL lists re-checked on kill events instead of `InvokeRepeating`; doors disabled when not on the current or next level. |
| `VantageLevelManager` | user's package | Reduced to a bridge: forwards `onAlarm`, helipad reach and `Alert()` to `VantageTowerLevels`; `IsAlive` delegates to it; its own spawning and `OnGUI` removed. `VantageInventory` stays (key, rifle). |
| Level brain: levels, spawning, alive/dead, hunting, alarm alert, coins tally, boss, ending | `Assets/Vantage/Scripts/VantageTowerLevels.cs` | Rewritten for L1, L2, L3, Finale. Spawns from `VantageTag` markers (`EnemySpawn`, `Turret`, `HeavyEnemy`, boss marker) by **marker name prefix** (`MK_`, `L2_`, `L3_`, `FINAL_`), not storey height. Soldiers keep the current rules: our `Soldier.prefab`, NavMesh point on the marker's own floor, safety net. Dead = our health check (`CharacterHealth`/`Changed`, drone/turret death), not `activeInHierarchy`. |
| HUD | `VantagePlayerHUD` | New: `[E]` prompt, short messages ("You need the Key", "You need a better gun"), coin counter, alarm indicator, end screen (time, deaths, coins x/5, achievements). Strings rebuilt only on change. |
| New runtime scripts (`Assets/Vantage/Scripts`) | ours | `VantageCoin` (spin/bob, collect, event), `VantageSecurityCamera` (sweep, view cone, sight ray via `VantagePhysics.Sight`, shootable), `VantageAlarm` (siren via `VantageProceduralAudio`, beacon rotation, red light switch, start/stop), `VantageKeyAnim` (key float/spin, fly-to-player on pickup; key-slot turn + red→green lamp), `VantageHeavyRobot` (slow armoured enemy, weapon filter: rifle only; boss mode with burst + charge/slam), `VantageExtraction` (helicopter lands, camera pull-away, end screen). Robot/boss firing reuses `VantageDrone`'s telegraph-and-fire code where it fits. |
| Editor setup | `Assets/Vantage/Editor/VantageCommandTower.cs` | Retargeted to `VANTAGE_Tower`: removes old setup objects; smooth stair surfaces over `*_Stairs*` / `FINAL_Landing_Stairs`; level gates are the Blender doors (no red shutters of our own unless a flight has none); covers on `*_PROP_*` (crates, sandbags, barriers, desks, rubble); pistol pickup, rifle = `L2_PICKUP_Gun`, coins; marker meshes stripped at setup (not hidden at runtime); static flags (structure occluder/occludee/batching, props occludee/batching, doors/shutter/pickups dynamic); NavMesh bake (doors excluded, carving obstacles while locked); occlusion bake; screenshots; gate/door check. |
| World | `VantageMilitaryBase.cs` | Fits the plot and levels the terrain under the **new** tower footprint (measured from its meshes); path-out check; minimap rebake. |
| Old tower | — | `CommandTower` instance and `VANTAGE Tower Setup` removed from the scene; `CommandTower_Unity` moved to `_UnusedAssets/` after the new tower is verified. |

## Level flow

| Level | Space (Blender names) | Flow | Exit condition |
|---|---|---|---|
| **L1 Ground** | Entrance doors, Room A (camera), Room B, Room C, maintenance shutter, turret hallway (one-way red door), security office, West Wing | Pistol inside the entrance. Camera sweeps Room A; spotted → alarm. `TRIGGER_Alarm_ShutterClose` always sets it off: `SHUTTER_Maintenance` slams, beacons spin, siren, red lights, L1 enemies hunt. Route A → B → C → hallway (turret faces north) → console (E unlocks `DOOR_SecurityOffice_WestWing_LOCKED`) → stairs. **1 coin** in a side room off the route. | All `MK_Enemy_*` + `MK_Turret_Hallway` dead → shutter reopens (Blender `KILL:` list), alarm stops, stairs to L2 open. |
| **L2 Armory & Office** | Hall (crates, sandbags, barriers), Armory, Office, Stairwell | Hall fight, two turrets (`L2_MK_Turret_East/West`), **2–3 drones** added in the hall. Rifle in the Armory (`L2_PICKUP_Gun`). Key on the office desk: floats/spins, flies to the player on pickup. `L2_KEYSLOT` needs E + key: key turns, lamp red → green, `L2_GATE_Stairwell_LOCKED` rolls up. **1–2 coins.** | Key inserted **and** L2 enemies dead (the gate stays shut while enemies live, so the player can't run past). |
| **L3 Destroyed floor** | Arrival, Barracks, Comms, Hall, destroyed open area (broken slabs, outside patrols), SE room, Robot room | `TRIGGER_L3_OpenArea_Enter` alerts both patrols (they walk `L3_PATROL_A/B` before that). Both dead → `L3_DOOR_SERoom_LOCKED` opens. `L3_TERMINAL_Override` (E) opens the robot room. Heavy robot (`L3_MK_EnemyRobot_Heavy`): rifle only. **1 coin** on the broken slab. | Robot and L3 enemies dead → `L3_DOOR_Exit_To_Final` opens to the outside stairs. |
| **Finale Helipad** | `FINAL_Landing_Stairs`, `FINAL_Platform`, `FINAL_Helipad` | 2 enemies (`FINAL_MK_Enemy_0/1`). Stepping on the pad (`FINAL_TRIGGER_EndScene_EndingC`) spawns the **boss** at `FINAL_MK_Boss_EndingB`: armoured, burst fire + a charge/slam with a visible wind-up. | Boss dead → helicopter lands, camera pulls away, "THE TOWER IS SILENT", end screen. |

Pacing: tension rises L1 (trap) → L2 (puzzle under fire) → L3 (open combat, robot) → climax (boss) → release (extraction). Coins sit in optional spaces so exploring is the reward. This goes into the concept doc as the new pacing curve.

## Coins and achievements

- ~5 gold coins (`Coin_Gold_*` prefabs), placed by hand by the setup tool from a short list of positions relative to named Blender rooms, so the spots survive re-exports. Trigger collider, spin/bob, pickup sound and HUD notice.
- HUD: `COINS 2/5` (only shown after the first coin). End screen: time, deaths, coins, achievements **All coins**, **No deaths**, **Under 15 minutes**.
- Session-only (no save file). The playtest logger records coin pickups.

## Performance

- No per-frame `Find*`, `GetComponent`, LINQ or string building in new or edited code. Player via `VantageEvents.ActivePlayer()`; components cached at Awake/spawn.
- `OnGUI` removed from tower scripts (it ran on every console every frame).
- Kill-gated doors listen for kill events; no polling.
- Doors, consoles and cameras on levels other than the current and next one are disabled.
- Only the current level's AI exists (spawn on level start, as now). Drones and turrets per level.
- Tower lights switched per level; alarm beacons are emissive + at most a few non-shadow point lights.
- Marker meshes deleted at setup time.
- Placeholders: one combined mesh and one material each.
- Static flags and baked occlusion as described above; shadow settings unchanged.
- Verified by an F9 capture on L1 (alarm active) and the helipad (boss fight), occlusion on and off (user, in Play mode).

## Error handling

- Missing marker or door name: setup logs `[Vantage]` with the name and skips; runtime never throws on a missing optional object.
- Spawn marker without NavMesh on its floor: skipped and logged (current rule).
- Enemies leaving their level: returned by the safety net (current rule).
- Placeholder prefab slot empty: level runs without that enemy and logs once.
- Player dies: level restart after 3 s, as now; the alarm, shutter, key and door states reset to the level's start state. Picked-up coins and weapons stay picked up (weapons as now).

## Testing

- Offline compile (`dotnet build` scratch csproj) before every batch run.
- `SetupBatch`: setup log, screenshots of each level, door/gate check.
- Autoplay bot (`VantageAutoplay.RunBatch`), rewritten for the new flow:
  - L1: alarm trigger closes the shutter; camera sight sets off the alarm; clearing L1 reopens it and opens the stairs.
  - L2: gate stays shut without the key; key + E opens it once L2 is clear.
  - L3: pistol does no damage to the robot, the rifle does; patrol kills open the SE room.
  - Finale: the boss spawns on the pad; its death starts the extraction and the end screen.
  - Every opened stair flight is climbed without stalling; soldiers fire and deal damage.
  - Shots: `hud_alarm.png`, `hud_key_prompt.png`, `hud_end_screen.png`.
- Not verifiable in batch: feel, balance, camera near walls, F9 frame times. Listed for the user's Play-mode check.

## Docs

`CLAUDE.md` (scene, code map, rules), `README.md` (levels, controls incl. E, asset table: Stylized Coin Pack licence, Poly Haven CC0, placeholder note), concept doc *Revisions* row (new tower, 3 levels + finale, coins, alarm, ending), `WORKLOG.md`.

## Out of scope

- The robot, boss and helicopter models (user, Blender).
- Saving progress or coins between sessions.
- Ending A (falling floor).
- Story props and per-space mood volumes beyond the alarm lighting (separate task).
