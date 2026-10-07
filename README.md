# VANTAGE

A third-person shooter level: fight your way up an overrun six-storey military command tower, one level at a time.

HSLU module I.BA_LWD (Level & World Design), HS26. Solo project by Adhurim Thaqi. Unity 6 (6000.0.58f2), URP.

## The idea

A six-storey military command tower stands in a training base in a forested valley. The base was overrun. The tower's automated defence (drones and a gun turret) is still running, and soldiers hold every floor.

You start unarmed in front of the main entrance and fight your way up to the roof in **four levels, each harder than the last**. A level is only finished when **everyone on it is dead**: until then, red security shutters lock the stairs and the fire escape to the next level. Clear the roof and the tower falls silent.

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

The tower (my own Blender model, `Assets/Blender_Asset/CommandTower_Unity`) has a ground floor, five upper floors and a roof. They are grouped into four levels:

| Level | Floors | Rooms | Enemies | Difficulty |
|---|---|---|---|---|
| **1** | Ground + 1 | lobby, security checkpoint, guard room, armory, lockers, dorms, canteen | 4 soldiers, 1 drone | weak soldiers (80 % health, 10 damage), slow inaccurate drone |
| **2** | 2 + 3 | operations hall, server room, radio room, storage, armory, drone workshop | 6 soldiers, 2 drones | normal soldiers (13 damage) |
| **3** | 4 + 5 | command centre, briefing, commander's office, drone control, battery room, hangar | 8 soldiers, 3 drones | tough soldiers (125 % health, 16 damage), accurate drones |
| **4** | Roof | drone pads, comms mast, sandbag nests | 5 soldiers, 4 drones, the turret | strongest soldiers (150 % health, 18 damage), fast accurate drones |

- **Gates.** Each way up to the next level (the inner stairs and the outside fire escape) is closed by a red shutter. When the last enemy of the level dies, all shutters to the next level roll up and turn green, and the HUD shows *LEVEL n CLEAR · STAIRS OPEN*. Going back down is always possible.
- **Rewards.** The pistol lies in front of the entrance. The **rifle** appears at the inner-stairs shutter when level 1 is cleared.
- **HUD.** Top-right shows the level and how many hostiles are left; the minimap (top-left) shows nearby enemies.
- **Lights.** Only the floors around your level are lit; the levels above wait in the dark until you get there.

If you die, the level restarts after three seconds.

### Mood

A low golden sun shines in from the south-west, and the valley has its own soundscape: wind in the forest, running generators and humming floodlights. (The per-floor mood lighting and the five story props belonged to the earlier blockout tower and still have to be rebuilt inside the command tower.)

### The world: the military base

The level is set in a 1 km valley I built in Blender: forested hills and a fenced military training base with headquarters, barracks, garages, vehicle pads, guard towers, roads and a shooting range further out. The command tower stands in the base where I placed it. The whole valley is the playground: you can walk anywhere up to the forest's edge, and from the roof the base opens up around you.

A **minimap** in the top-left corner shows where you are (north up, the arrow turns with you), the tower (pinned to the edge when it is out of view, so the goal is always on screen), nearby enemies as red dots, the distance to the tower and the space you're in.

It is built by **Vantage → World → Build Military World** (`VantageMilitaryBase`), not by hand:
- **The world.** The Blender export (`Assets/Blender_Asset/Military_Env_Unity`) is placed at its Blender coordinates, so anything modelled in place in Blender lines up in Unity.
- **The tower plot.** The plot is fitted to the command tower wherever it stands: its terrain is levelled at the tower's base height and the base props inside its footprint are cleared.
- **Walkable ground.** The template character cannot step up ledges, so around the plot the terrain is relaxed into smooth slopes (a Laplace membrane) out to the hills. Roads and pads are bent to follow it, and those lying flush on the terrain lose their collider, so kerbs can't block you.
- **Collision and limits.** Trees and rocks have colliders, and invisible walls stop you 25 m before the edge of the world.
- **Cover and AI.** The base's cover props (sandbags, barriers, containers, vehicles, …) get Cover Shooter covers turned with each prop, and the whole valley is in the enemies' NavMesh.
- **The way out.** The tool checks that paths lead from the player start into the base and the forest. If a fence piece blocks one, it is switched off as a breach.
- **The minimap image** is baked top-down by the tool (**Vantage → World → Rebake Minimap** after changing the layout).
- `VantageMilitaryBase.LedgeReportBatch` draws a ledge/slope map and a NavMesh connectivity map around the tower, as playtest evidence.

`Vantage_First-Person/Documentation/world_*.png` shows the result.

## How it works

### Enemies

- **Soldiers** use the template's cover-shooter AI, tuned to **stand and fight** (`VantageEnemyAggression`). Out of the box the template backed away when you came within 4 m, fled to cover at 25 % health, ran to any cover up to 30 m away and started out wandering. Now they guard their room, use only cover within 9 m, never retreat, and lock on and open fire whenever they can see you on their floor.
- **Drones** (`VantageDrone`) hover around their post until they see you, then circle at a set distance and shoot. Their eye **glows brighter just before each shot**, so you can react. Accuracy drops with distance. Destroyed drones explode and fall.
- **Turret:** the same script, fixed in place, on the roof. It turns, fires fast and has long range.

### Weapons and cover

You start with nothing (`VantageArsenal`). The **pistol** is in front of the entrance and the **rifle** is the reward for clearing level 1; walking into one equips it. About 140 pieces of the tower's furniture (desks, racks, lockers, shelves, sandbags) and the base's cover props have cover markers, so both you and the soldiers can take cover at them. The camera keeps 30 cm clear of walls, so it never clips through them, and only small props (furniture, crates) between the camera and the character fade out (`VantageCameraFader`).

### Procedural enemy placement

`VantageTowerLevels` places each level's enemies from a **seed** every time you press Play. The tower has 94 authored enemy spawn points (one or more per room) plus drone pads; each level draws its soldiers from those on its floors, **spread over different rooms first**, so the fights are different every play while the counts, difficulty and pacing stay the same. The seed shows top-left during play and is written to the playtest log. To replay a seed, select *VANTAGE Tower Setup → Tower Levels*, untick *Random Seed Each Play* and set *Seed*.

### Automated playtest

**Vantage → Test → Run Autoplay Test** (or batch: `-executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch`, exit code 0 = passed) plays the real level and checks three things, logging every result with `[Bot]`:
- **Soldiers fight back:** it stands in front of a soldier and counts the shots fired and the damage dealt.
- **Gates:** it clears level 1 and checks that its shutters open and level 2 spawns.
- **Stairs:** it walks every unlocked flight and measures the height climbed and any time spent stuck.

It found the bugs behind "enemies don't shoot back" and "the stairs feel buggy":
- **Soldiers had no gun in hand.** The template's soldier prefab still kept its weapons in a deprecated list, so they spawned unarmed and fled.
- **Soldiers couldn't fire off-screen.** The template only aims (and so only fires) while a character is visible on screen.
- **The steps caught the character.** The tower's step edges stuck up 15 cm above its invisible ramps.
- **The fire escape had a ceiling.** Its invisible ramps run the opposite way to the real steps, which put a sloping ceiling over the stairs.

All four are fixed (see *Setting up the tower*), and the test passes.

### Playtest logging

Every play session writes a report to `Vantage_First-Person/Playtests/`:
- the seed, session length, whether the tower was cleared and how far the player got
- time spent and deaths in each level (and outside in the base), and kills by enemy type
- **back-tracking** events, plus a list of everything that happened with timestamps and F8 marks
- a *Tester notes* section to fill in after each peer playtest
- a CSV position trail, which you can turn into a heatmap

### Performance

- **Rendering:** the game renders with the PC settings at full resolution (Forward+, 4 shadow cascades, SSAO). The two lowest quality levels use a lighter mobile setup.
- **The tower** is static for batching and occlusion culling (doors excepted). Only the lights around the player's level are on (the tower has 108), and occlusion culling is baked.
- **The HUD** builds its texts only when a value changes, so there's no garbage every frame.
- Enemies spawn level by level, so only one level's AI runs at a time.
- F9 writes frame time (average, 99th percentile, worst), batches, set-pass calls and triangles, plus a screenshot, to `Playtests/Performance/`. Capture once with F10 on and once off to show the difference.

## Project layout

```
Level_World_Design_/
├─ VANTAGE — Level & World Design Concept.md   design document
├─ README.md                                   this file
├─ CLAUDE.md                                   technical guide for development and AI agents
├─ .claude/agents/                             project agents (e.g. vantage-watchdog, a QA and level-design reviewer)
└─ Vantage_First-Person/                       Unity project
   ├─ Assets/
   │  ├─ Scenes/SampleScene.unity              the level
   │  ├─ Vantage/                              all project-specific work
   │  │  ├─ Scripts/                           gameplay (levels, gates, drones, AI tuning, arsenal, HUD, minimap, logger, …)
   │  │  │  └─ Testing/                        the automated playtest bot (editor only)
   │  │  ├─ Editor/                            Vantage menu: tower setup, world builder, autoplay test, tools
   │  │  ├─ Prefabs/                           Soldier (generated), Drone, Turret, Third Person Rig
   │  │  ├─ World/                             generated: terrain data, stair surfaces, tree colliders, sky, minimap image
   │  │  ├─ Materials/  Volumes/               surfaces and post-processing profiles
   │  ├─ ThirdPersonCoverShooter/              character, AI, weapons (course template)
   │  ├─ PistolAnimsetPro/                     character animations
   │  ├─ Blender_Asset/Military_Env_Unity/     my Blender world: terrain, forest, the military base, props and textures
   │  └─ Blender_Asset/CommandTower_Unity/     my Blender command tower: 6 floors, interiors, doors, lights, gameplay markers
   ├─ Documentation/                           screenshots and maps for the documentation
   └─ Playtests/                               session logs and performance captures (created on play)
```

### Setting up the tower

The command tower prefab is mine and stays untouched. **Vantage → Tower → Set Up Command Tower** adds everything the game needs around it (under *VANTAGE Tower Setup*), and it is safe to run again:
- static flags (the prefab had none, so the whole tower was drawn every frame and nothing was culled), and the realtime lights
- walk ramps over the entrance steps, and **smooth stair surfaces** over all 18 flights, which replace the tower's own stair ramps: those let the step edges catch the character and run backwards on the fire escape. The surfaces are built from the real steps and never steeper than 25.6°
- the six gate shutters, found from the tower's stair ramps
- the furniture cover, **our soldier prefab** (`Assets/Vantage/Prefabs/Soldier.prefab`, a variant of the template's soldier with its weapons moved into a real inventory and off-screen aiming on), and the level manager with the four levels
- the pistol and rifle, and the player start in front of the main entrance
- a fresh NavMesh bake

**Vantage → World → Build Military World** also runs it after fitting the world around the tower.

## For developers

- **Technical guide:** [`CLAUDE.md`](CLAUDE.md) explains how everything fits together: the code map, every menu tool and batch command, the change workflow, and the rules that break silently (for example the template character can't step up ledges, and soldiers must use our soldier prefab). Read it before changing anything; AI agents in `.claude/agents/` read it first too.
- **Everything generated comes from tools, not hand edits.** Change the tool code and re-run:
  - **Vantage → Tower → Set Up Command Tower**: gates, stairs, soldiers, levels, pickups, NavMesh.
  - **Vantage → World → Build Military World**: the world, then the tower setup.
- **After every gameplay change**, run **Vantage → Test → Run Autoplay Test** and check the `[Bot]` lines in the Console.
- **Template change:** one deliberate edit to the template, `CharacterMotor.AlwaysUpdateIK` (marked "VANTAGE change"). Re-apply it if the package is ever reimported.

## Assets used

Bought-in and provided assets build the bricks. The level layout, floor plans, vertical structure, encounter placement, lighting design, story props, gameplay scripts and the procedural enemy placement are my own work.

| Asset | Publisher | Used for |
|---|---|---|
| *Third Person Cover Shooter* 1.6 (course template, provided by the lecturer) | Eduardas Funka (Unity Asset Store) | Player character, enemy soldier AI, cover system, weapons, hit effects, HUD |
| *Pistol Animset Pro* | Kubold ([kubold.com](https://www.kubold.com)) | Character animations (required by the template) |
| *Command Tower* (`Assets/Blender_Asset/CommandTower_Unity`) | My own work, built in Blender | The main building: structure, interiors, furniture, doors, lights, spawn markers |
| *Military Training Environment* (`Assets/Blender_Asset/Military_Env_Unity`) | My own work, built in Blender | The whole world: terrain, forest, the military base, props, and the concrete textures on the tower |

<!-- TODO before hand-in: check the publisher names against the Asset Store pages and add the store links. -->

Unused files from these packs were moved out of the project to keep the repository small. They are kept locally in `_UnusedAssets/`, which is not committed.

## Requirements and notes

- **Unity 6000.0.58f2** with URP. Active input handling is set to *Both*, because the template uses the old Input Manager.
- **Git LFS** is required: run `git lfs install` before cloning. Textures, models, audio and terrain data are stored in LFS.
- If the game might be sold later: check that the licences of the course template (*Third Person Cover Shooter*) and *Pistol Animset Pro* cover commercial use.
- The level has not had a full peer-playtest round yet. Enemy counts and damage per level are the first things to tune (Inspector: *VANTAGE Tower Setup → Tower Levels*).
