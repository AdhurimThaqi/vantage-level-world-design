# VANTAGE

A third-person action level about climbing an abandoned military watchtower.

HSLU module I.BA_LWD (Level & World Design), HS26. Solo project by Adhurim Thaqi. Unity 6 (6000.0.58f2), URP.

## The idea

The tower stands at the edge of an industrial site by the sea. It was a military position, and it was overrun. Its automated defence (drones and a gun turret on the radio mast) is still running and treats anyone inside as a target. A few soldiers are still holding parts of the building.

You start unarmed in the yard and climb from the ground floor to the roof. The radio mast on the roof is visible from the start, so the goal needs no marker: **the way is always up**. There are no cutscenes, no text logs and no dialogue. The story is told by what you find on the way.

Play time is about 10–15 minutes.

The full design rationale is in [`VANTAGE — Level & World Design Concept.md`](VANTAGE%20—%20Level%20&%20World%20Design%20Concept.md).

## How to play

Open `Vantage_First-Person` in Unity **6000.0.58f2**, open `Assets/Scenes/SampleScene` and press Play.

| Action | Key |
|---|---|
| Move / look | WASD / mouse |
| Run | Left Shift |
| Crouch | Left Ctrl (hold) |
| Take cover at a wall or object | Space (near it) |
| Climb / vault low obstacles | Space (facing it) |
| Roll | Double-tap a direction key |
| Aim | Right mouse (hold) |
| Shoot | Left mouse |
| Reload | R |
| Holster / pistol / rifle | 1 / 2 / 3 (once found) |
| Grenade | Hold G, aim, left mouse to throw |
| Melee | F |

Playtest hotkeys:
- **F8** marks a moment in the playtest log.
- **F9** records a 10-second performance capture.
- **F10** toggles occlusion culling, to compare captures with it on and off.

## The climb

The level is four floors climbed in one direction. Each floor plays differently: the engagement distance changes from **long → close → mixed → open**, and the pacing goes **calm → tense → hardest → release**.

| Space | What happens | How you go up |
|---|---|---|
| **Approach** (yard) | You spawn unarmed in the overrun compound. A lit guard hut is the brightest thing in view; the pistol is on its desk. Two drones guard the tower door and only react when you come close. This is a short tutorial fight. | Through the front door |
| **Level 1 – Lobby** | Wide, double height, long sightlines, pillars and crates for cover. One slow drone teaches what the enemy is and what cover does. | Open stairwell along the east wall, visible from the door |
| **Level 2 – Corridors** | Tight offices and corridors under red emergency light. Three drones in the rooms furthest from the stairs, so close ambushes with a weak pistol. **The layout is different every play** (see *Procedural level 2*). | Out the door onto the exterior fire escape |
| **Fire escape** | The only outdoor stretch: exposed, no cover, the yard below you. | Up the outside stairs to level 3 |
| **Level 3 – Collapsed** | Holes in the floor and ceiling; through one you can look down two floors into the lobby. The rifle lies in a lit alcove, slightly off the path. Two soldiers and two drones, one rising from the collapse shaft below. | A collapsed floor slab forms a ramp up through the ceiling |
| **Roof** | Open deck, no cover. A turret on the mast covers the centre. Stepping onto the roof starts **three waves of drones** (2, 3, then 4) over different edges. Clearing them ends the level: *THE TOWER IS SILENT*. | — |

If you die, the level restarts after three seconds.

### Environmental storytelling

Five props tell the story without a word. A player should be able to work out *they retreated upward, they did not make it, nobody answered*.

1. **Lobby:** sandbags by the door face *inward*. They were defending against something already inside.
2. **Level 2:** a door barricaded from the player's side. They were retreating upward too.
3. **Level 2:** bunks with more gear than beds. More people than the place was built for.
4. **Level 3:** a body next to the rifle. Someone got this far and no further.
5. **Roof:** a radio, still powered, beeping to nobody.

### Guidance

- **The mast** is the landmark. You see it from the spawn, through windows and floor holes, and finally beside you on the roof.
- **Light:** every way up is the brightest spot in its room (the hut, the stairwell, the fire escape door, the ramp).
- **Sound:** wind and drone hum get louder towards each way up.
- **Nothing is sealed behind you.** Going back is always possible; going up is just more interesting. The playtest log counts back-tracking to test this.

### Mood

Each space has its own post-processing Volume, following the concept's lighting table:

| Space | Look | Feeling |
|---|---|---|
| Lobby | Warm | Inviting |
| Corridors | Dark, desaturated, red, film grain | Threat |
| Fire escape | Bright and open | Exposed |
| Level 3 | High contrast with light shafts | Dramatic |
| Roof | Golden hour | Release |

A low golden sun shines in from the south-west. The yard has its own soundscape: wind, the sea, running generators and humming floodlights.

## How it works

### Enemies

- **Drones** (`VantageDrone`) hover around their post until they see you, then circle at a set distance and shoot. Their eye **glows brighter just before each shot**, so you can react. Accuracy drops with distance. Shooting a drone always gives away your position. Destroyed drones explode and fall.
- **Turret:** the same script, but fixed in place. It turns, fires fast and has long range. It sits on the mast.
- **Soldiers** use the template's cover-shooter AI. They take cover, flank, react to gunfire and alert each other.

### Weapons and cover

You start with nothing (`VantageArsenal`). The **pistol** is in the guard hut and the **rifle** is in the level-3 alcove; walking into one equips it. Cover objects (pillars, desks, crates, barriers, sandbags, generated walls) have cover markers, so both you and the soldiers can take cover at them. The roof has none, by design. Walls between the camera and the character fade out so they don't block your view (`VantageCameraFader`).

### Procedural level 2

`VantageLevel2Generator` builds a new room-and-corridor layout from a **seed** each time you press Play:
- The floor is split into rooms and corridors with **binary space partitioning**. Every dividing wall gets at least one doorway, so every room is always reachable.
- The **entry** (stair landing), the **exit** (fire-escape door), the barricaded door and the **enemy count (3 drones)** are fixed. Only the layout changes, so the pacing doesn't.
- Furniture and the bunk room are placed per seed, and the walls become cover and update the enemy pathfinding.
- The seed shows top-left during play and is written to the playtest log, so any layout can be reproduced. To replay a seed, select **Level 2 Generator** in the scene, untick *Random Seed Each Play* and set *Seed*.

`Vantage_First-Person/Documentation/level2_plan_seed_1234.png` and `…_52071.png` show two seeds side by side.

### Playtest logging

Every play session writes a report to `Vantage_First-Person/Playtests/`:
- the seed, session length, whether the level was completed and how far the player got
- time spent and deaths in each space, and kills by enemy type
- **back-tracking** events, plus a list of everything that happened with timestamps and F8 marks
- a *Tester notes* section to fill in after each peer playtest
- a CSV position trail, which you can turn into a heatmap

### Performance

- Each floor is its own occlusion area, and occlusion culling is baked.
- F9 writes frame time (average, 99th percentile, worst), batches, set-pass calls and triangles, plus a screenshot, to `Playtests/Performance/`. Capture once with F10 on and once off to show the difference.

## Project layout

```
Level_World_Design_/
├─ VANTAGE — Level & World Design Concept.md   design document
├─ README.md                                   this file
├─ CLAUDE.md                                   technical notes for AI-assisted work
└─ Vantage_First-Person/                       Unity project
   ├─ Assets/
   │  ├─ Scenes/SampleScene.unity              the level
   │  ├─ Vantage/                              all project-specific work
   │  │  ├─ Scripts/                           gameplay (drones, waves, generator, arsenal, logger, …)
   │  │  ├─ Editor/                            Vantage menu: tower builder, compound, setup tools
   │  │  ├─ Prefabs/                           Drone, Turret, Third Person Rig
   │  │  ├─ Materials/  Volumes/               surfaces and per-space post-processing
   │  ├─ ThirdPersonCoverShooter/              character, AI, weapons (course template)
   │  ├─ PistolAnimsetPro/                     character animations
   │  └─ RPG_FPS_game_assets_industrial/       yard, props, concrete textures
   ├─ Documentation/                           level 2 plans for the documentation
   └─ Playtests/                               session logs and performance captures (created on play)
```

### The tower is generated

The tower, the yard compound, the enemies and the mood are built by the **Vantage** menu in the Unity editor, mainly from `VantageTowerBuilder`. Every wall and floor is an editable **ProBuilder** shape, so the blockout can be changed by hand. Running **Vantage → Enhance Scene** again rebuilds the tower and replaces those hand edits. To keep a change for good, make it in the builder code.

After changing the layout, run **Vantage → 3. Bake NavMesh For Enemies** so the soldiers can walk the new geometry.

## Assets used

Bought-in and provided assets build the bricks. The level layout, floor plans, vertical structure, encounter placement, lighting design, story props, gameplay scripts and the procedural generator are my own work.

| Asset | Publisher | Used for |
|---|---|---|
| *Third Person Cover Shooter* 1.6 (course template, provided by the lecturer) | Eduardas Funka (Unity Asset Store) | Player character, enemy soldier AI, cover system, weapons, hit effects, HUD |
| *Pistol Animset Pro* | Kubold ([kubold.com](https://www.kubold.com)) | Character animations (required by the template) |
| *RPG/FPS Game Assets for PC/Mobile (Industrial Set v2.0)* | Dmitrii Kutsenko (Unity Asset Store) | The industrial yard, props, concrete and asphalt textures |
| Unity *Starter Assets – First Person* | Unity Technologies | Still in the project but not used: the game was first person earlier and is third person only now |

<!-- TODO before hand-in: check the publisher names against the Asset Store pages and add the store links. -->

Unused files from these packs were moved out of the project to keep the repository small. They are kept locally in `_UnusedAssets/`, which is not committed.

## Requirements and notes

- **Unity 6000.0.58f2** with URP. Active input handling is set to *Both*, because the template uses the old Input Manager.
- **Git LFS** is required: run `git lfs install` before cloning. Textures, models and audio are stored in LFS.
- The level has not had a full peer-playtest round yet. Drone balance and the procedural level 2 are the first things to tune.
