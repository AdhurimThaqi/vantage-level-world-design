# VANTAGE

A third-person shooter level: get into a locked-down military tower, fight up through its three floors, and get out by helicopter from the helipad.

HSLU module I.BA_LWD (Level & World Design), HS26. Solo project by Adhurim Thaqi. Unity 6 (6000.0.58f2), URP.

## The idea

A military command tower stands in a training base in a forested valley. The base was overrun, the tower's top floor is half destroyed, and soldiers, drones, turrets and a heavy combat robot hold it.

You arrive in front of the main entrance with nothing. A pistol lies by the doors. The moment you step inside, **the alarm goes off**: the shutter in front of you slams down, the siren wails, red lights flash and the whole floor comes for you. From there you fight up through **three levels and a finale on the helipad**, each harder than the last. Every level asks something new: survive the lockdown, find the key, find a gun that can hurt the robot. At the top a final machine waits on the helipad, and when it falls the extraction helicopter comes in.

The full design rationale is in [`VANTAGE — Level & World Design Concept.md`](VANTAGE%20—%20Level%20&%20World%20Design%20Concept.md).

## How to play

Open `Vantage_First-Person` in Unity **6000.0.58f2**, open `Assets/Scenes/SampleScene` and press Play.

| Action | Key |
|---|---|
| Move / look | WASD / mouse |
| Run | Left Shift |
| Crouch | Left Ctrl (hold) |
| Use (the level 2 key slot) | E (the HUD shows "[E] …" when you can) |
| Take cover at a wall or object | Space (near it) |
| Climb / vault low obstacles | Space (facing it) |
| Roll | Double-tap a direction key |
| Aim | Right mouse (hold) |
| Shoot | Left mouse |
| Reload | R |
| Holster / pistol / rifle | 1 / 2 / 3 (once found) |
| Grenade | Hold G, aim, left mouse to throw |
| Melee | F |
| Play again (end screen) | Enter |

Playtest hotkeys:
- **F1** shows the playtest overlay (seed, current space, hotkeys).
- **F8** marks a moment in the playtest log.
- **F9** records a 10-second performance capture.
- **F10** toggles occlusion culling, to compare captures with it on and off.

## The climb

The tower is my own Blender model (`Assets/VANTAGE_Tower`). Its rooms, doors, triggers, consoles and enemy markers all come from the Blender file, with their rules written as custom properties on the objects (which door a console unlocks, which enemies must die to open a door).

| Level | Spaces | Enemies | What it asks of you | How it opens |
|---|---|---|---|---|
| **1 · Ground floor** | entrance, checkpoint, rooms A–C, guard hall, turret hallway, security office, West Wing | 5 soldiers (pistols), a sentry turret | Survive the **lockdown**: the alarm trigger just inside the doors (or the security camera in Room A, if it sees you) drops the maintenance shutter and sends the floor after you. The door on the left of the corridor stays locked during the lockdown, so the only way on is the door to Room A on the right: rooms A → B → C, through the West Wing, on into the turret hallway. | All of them dead: the shutter rolls up, the alarm stops, the red stair shutter opens |
| **2 · Armory** | hall with barricades, Armory, Office, stairwell | 5 soldiers, 2 turrets, 2 drones | Take the **rifle** from the Armory and the **key** from the office desk (it floats and glows; it flies into your hands when you take it). Insert it in the slot by the stairwell gate with **E**: the key turns and the lamp goes green. | Key in **and** everyone dead: the stairwell gate rolls up |
| **3 · Destroyed floor** | barracks, comms room, hall, the blown-out open area and broken slab outside, SE room, robot room | 5 soldiers, 2 outside patrols, the **heavy robot** | Clearing the barracks, comms and hall opens the robot room, the way through to the destroyed area (a breach in its wall). Entering the destroyed area alerts the two patrols on the broken slab; killing both opens the SE room. **Only the rifle hurts the robot** ("You need a better gun"). | Everyone dead: the exit to the outside stairs unlocks |
| **Finale · Helipad** | outside stairs, platform, helipad | 2 soldiers, then the **boss** | Clear the platform, step onto the pad: the boss appears, a bigger red-eyed robot that also **charges** after a visible wind-up. | The boss dies: the helicopter lands, *THE TOWER IS SILENT* |

- **Coins.** Five gold coins are hidden off the main route, one or two per level: a dead end on the ground floor, the far end of the office and a corner of the hall on level 2, the broken slab outside on level 3 (patrol ground), the far edge of the helipad. They are counted on the HUD and the end screen.
- **End screen.** Time, deaths, coins, and three achievements: *All coins*, *No deaths*, *Under 15 minutes*.
- **Dying** restarts the level you were on after three seconds (a checkpoint per level). Weapons and coins you found stay found; the level's enemies, doors and key are back where they started.
- **HUD.** Kept to what you act on:
  - Top centre: the level, its name, one marker per enemy (it dims when that enemy dies), the goal of the moment (*FIND A WEAPON*, *LOCKDOWN · CLEAR THE FLOOR*, *REACH THE HELIPAD*, *DESTROY THE MACHINE*) and the coins found.
  - Bottom left: health, with a tick where regeneration stops. Bottom right: weapon and ammo.
  - Centre: the enemy you aim at, short messages, and "[E] …" when you stand at the key slot.
  - Top left: the minimap, with nearby enemies, the distance to the tower and the level you are on.

### Mood

A low golden sun shines in from the south-west, and the valley has its own soundscape: wind in the forest, running generators and humming floodlights. Inside, the alarm turns the ground floor red: flashing beacons, red lights and a siren until the floor is clear.

### The world: the military base

The level is set in a 1 km valley I built in Blender: forested hills and a fenced military training base with headquarters, barracks, garages, vehicle pads, guard towers, roads and a shooting range further out. The whole valley is the playground: you can walk anywhere up to the forest's edge, and from the helipad the base opens up around you.

A **minimap** in the top-left corner shows where you are (north up, the arrow turns with you), the tower (pinned to the edge when it is out of view, so the goal is always on screen), nearby enemies as red dots, the distance to the tower and the space you're in.

It is built by **Vantage → World → Build Military World** (`VantageMilitaryBase`), not by hand:
- **The world.** The Blender export (`Assets/Blender_Asset/MilitaryEnvUnity`) is placed at its Blender coordinates, so anything modelled in place in Blender lines up in Unity.
- **The tower plot.** The plot is fitted to the tower wherever it stands: its footprint (helipad included) plus 8 m is levelled at the tower's ground floor, so the entrance is flush with the ground, and the base props inside it are cleared.
- **Walkable ground.** The template character cannot step up ledges, so around the plot the terrain is relaxed into smooth slopes (a Laplace membrane) out to the hills. Roads and pads are bent to follow it, and those lying flush on the terrain lose their collider, so kerbs can't block you.
- **Collision and limits.** Trees and rocks have colliders, and invisible walls stop you 25 m before the edge of the world.
- **Cover and AI.** The base's cover props get Cover Shooter covers turned with each prop, and the whole valley is in the enemies' NavMesh.
- **The way out.** The tool checks that paths lead from the player start into the base and the forest. If a fence piece blocks one, it is switched off as a breach.
- **The minimap image** is baked top-down by the tool (**Vantage → World → Rebake Minimap** after changing the layout).
- `VantageMilitaryBase.LedgeReportBatch` draws a ledge/slope map and a NavMesh connectivity map around the tower, as playtest evidence.

## How it works

### Enemies

- **Soldiers** use the template's cover-shooter AI, tuned to **stand and fight** (`VantageEnemyAggression`): they guard their room, use only cover within 9 m, never retreat, and lock on and fire whenever they see you on their floor. The alarm, an alert trigger or a quiet spell sends them after you; the L3 outside patrols walk their Blender routes until then.
- **Drones and turrets** (`VantageDrone`) telegraph each shot: their eye **glows brighter just before they fire**. Accuracy drops with distance.
- **The heavy robot and the boss** (`VantageHeavyRobot`) are slow and armoured: only rifle rounds get through. They fire telegraphed three-shot bursts; the boss also charges after a one-second wind-up. Their look is a placeholder until my Blender models replace it: put the model under the prefab's `Model` child (it may have colliders), and keep the `Eye`, `Eye Light` and `Muzzle` objects beside it.
- **Doors on the route stand open**, so the way on reads at a glance and enemies can follow you through the building. Locked doors stay shut until their console, terminal or kill condition opens them. The shutter and the stairwell gate roll up.

### Weapons and cover

You start with nothing (`VantageArsenal`). The **pistol** lies just outside the main entrance (so you are armed before the trap springs), the **rifle** is in the level 2 Armory. Walking into one equips it. About 80 props of the tower (crates, sandbags, barriers, desks, rubble) and the base's cover props have cover markers, so you can take cover at them (only where there is room in front). Soldiers don't use cover; they fight in the open and come for you. The camera keeps 30 cm clear of walls and only small props between the camera and the character fade out (`VantageCameraFader`).

### Procedural enemy placement

`VantageTowerLevels` places each level's soldiers from a **seed** every time you press Play. Each level has more authored spawn markers than soldiers (13 on the ground floor alone); the level draws its soldiers from them **spread over different rooms first**, so the fights are different every play while the counts, difficulty and pacing stay the same. Markers a door depends on (the L3 patrols) are always used. The seed is written to the playtest log; `-vantageSeed N` on the command line replays a layout.

### Automated playtest

**Vantage → Test → Run Autoplay Test** (or batch: `-executeMethod Vantage.EditorTools.VantageAutoplay.RunBatch`, exit code 0 = passed) plays the real level and logs every result with `[Bot]`. It checks, in order:
- the security camera spots the player and springs the trap; walking through the trigger afterwards doesn't spring it twice
- the main entrance can be walked through without getting stuck
- soldiers fire and deal damage
- a door of a later level stays locked while its enemies haven't spawned
- clearing level 1 stops the alarm, reopens the shutter and opens the stairs
- the key: the slot refuses without it, picking it up works, and the gate waits until level 2 is clear
- the last enemies come for the player
- the L3 patrol door opens after both patrols (not one)
- the robot ignores pistol rounds (one warning, not one per bullet) and dies to the rifle
- every stair flight can be climbed without stalling
- the finale: the boss appears on the pad, its death brings the helicopter and the win
- dying on level 2 restarts there with the pistol and a collected coin kept
- a shot-out camera stays quiet, while the trigger still springs the trap

With `-vantageShots <folder>` it also saves screenshots of the game **with the HUD**: combat, alarm, key prompt, level cleared, boss, end screen.

### Playtest logging

Every play session writes a report to `Vantage_First-Person/Playtests/`:
- the seed, session length (and the whole run's time and deaths), whether the tower was cleared and how far the player got
- time spent and deaths in each level (and outside in the base), and kills by enemy type
- **back-tracking** events, plus everything that happened with timestamps (alarm, coins, kills, F8 marks)
- a *Tester notes* section to fill in after each peer playtest
- a CSV position trail, which you can turn into a heatmap

### Performance

- **Rendering:** the game renders with the PC settings at full resolution (Forward+, 4 shadow cascades, SSAO). The two lowest quality levels use a lighter mobile setup.
- **The tower:** walls, slabs and stairs are occluder and batching static; glass, props and signs only occludee; doors, consoles and the alarm beacons dynamic. Occlusion culling is baked. Marker meshes are switched off once in the editor.
- **Logic:** only the current level's enemies exist; doors on levels far from the player sleep; doors sense who is near a few times a second; kill-locked doors react to kills instead of polling; the HUD builds its texts only when a value changes. No script looks objects up by name or tag every frame.
- **Lights:** none of the added lights casts shadows (alarm, key, coins, camera cone).
- F9 writes frame time (average, 99th percentile, worst), batches, set-pass calls and triangles, plus a screenshot, to `Playtests/Performance/`. Capture once with F10 on and once off to show the difference.

## Project layout

```
Level_World_Design_/
├─ VANTAGE — Level & World Design Concept.md   design document
├─ README.md                                   this file
├─ CLAUDE.md                                   technical guide for development and AI agents
├─ WORKLOG.md                                  every change request and what was done
├─ docs/superpowers/                           design spec and build plan of the 3-level tower
├─ .claude/agents/                             project agents (e.g. vantage-watchdog, a QA and level-design reviewer)
└─ Vantage_First-Person/                       Unity project
   ├─ Assets/
   │  ├─ Scenes/SampleScene.unity              the level
   │  ├─ VANTAGE_Tower/                        my Blender tower: level and gameplay models, textures, import scripts
   │  ├─ Vantage/                              all project-specific work
   │  │  ├─ Scripts/                           gameplay (levels, alarm, camera, key, robot, coins, extraction, HUD, …)
   │  │  │  └─ Testing/                        the automated playtest bot (editor only)
   │  │  ├─ Editor/                            Vantage menu: tower setup, world builder, placeholders, autoplay test
   │  │  ├─ Prefabs/                           Soldier (generated), Drone, Turret, HeavyRobot, Boss, Helicopter, Third Person Rig
   │  │  ├─ World/                             generated: terrain data, stair surfaces, placeholder meshes, sky, minimap
   │  │  ├─ Materials/  Volumes/               surfaces and post-processing profiles
   │  ├─ PolyKebap/Stylized Coin Pack/         the coins
   │  ├─ ThirdPersonCoverShooter/              character, AI, weapons (course template)
   │  ├─ PistolAnimsetPro/                     character animations
   │  ├─ Blender_Asset/MilitaryEnvUnity/       my Blender world: terrain, forest, the military base, props and textures
   │  └─ Blender_Asset/AdhurimCharacter/       my player character (Avaturn avatar, exported from Blender)
   ├─ Documentation/                           screenshots and maps for the documentation
   └─ Playtests/                               session logs and performance captures (created on play)
```

### Setting up the tower

The tower models stay as exported from Blender; their import script (`VANTAGE_Tower/Scripts/Editor/VantageTowerPostprocessor.cs`) turns the Blender custom properties into doors, triggers, consoles and pickups. **Vantage → Tower → Set Up Tower** (`VantageTower`) adds everything the game needs around it, under *VANTAGE Tower Setup*, and is safe to run again:
- places the tower (where the old tower stood, entrance flush with the ground) and hides the marker meshes
- static flags, doors out of the NavMesh (locked ones block it with obstacles), the level rules of the stairwell gate and the helipad exit
- **smooth stair surfaces** over every flight, found from the real steps, as wide as the flight and flush with the floor at its foot (about 31°, a side entry where a wall blocks the foot), with NavMesh blockers so enemies stay on their floor, and the red shutter on the ground-floor stairs
- cover on the props, the pistol and the rifle, the key and key slot, the alarm (beacons, lights, siren), the security camera, the extraction
- **our soldier prefab** and the placeholder robot, boss and helicopter (built only when missing, so a real model put into them stays)
- the level manager with the four levels, their restart points and difficulty
- a NavMesh bake, then the five coins on walkable floor

**Vantage → World → Build Military World** also runs it after fitting the world around the tower. (The tower package's own *VANTAGE ▸ Add Tower To Open Scene* is not needed here.)

## For developers

- **Work log:** [`WORKLOG.md`](WORKLOG.md) lists every change request and what was done, verified and left open, newest first.
- **Technical guide:** [`CLAUDE.md`](CLAUDE.md) explains how everything fits together: the code map, every menu tool and batch command, the change workflow, and the rules that break silently. Read it before changing anything; AI agents in `.claude/agents/` read it first too.
- **Everything generated comes from tools, not hand edits.** Change the tool code and re-run:
  - **Vantage → Tower → Set Up Tower**: stairs, gates, doors, soldiers, levels, pickups, coins, NavMesh.
  - **Vantage → World → Build Military World**: the world, then the tower setup.
  - **Vantage → Enemies → Rebuild Placeholder Models**: the robot, boss and helicopter placeholders (overwrites them).
- **After every gameplay change**, run **Vantage → Test → Run Autoplay Test** and check the `[Bot]` lines in the Console.
- **Package edits:** `CharacterMotor.AlwaysUpdateIK` in the template, and the runtime scripts of `Assets/VANTAGE_Tower` (doors, triggers, consoles, pickups, level-manager bridge), are marked "VANTAGE change". Re-apply them if either package is ever re-imported.

## Assets used

Bought-in and provided assets build the bricks. The level layout, floor plans, encounter placement, gating, lighting design, gameplay scripts and the procedural enemy placement are my own work.

| Asset | Publisher | Used for |
|---|---|---|
| *Third Person Cover Shooter* 1.6 (course template, provided by the lecturer) | Eduardas Funka (Unity Asset Store) | Player character, enemy soldier AI, cover system, weapons, hit effects |
| *Pistol Animset Pro* | Kubold ([kubold.com](https://www.kubold.com)) | Character animations (required by the template) |
| *VANTAGE Tower* (`Assets/VANTAGE_Tower`) | My own work, built in Blender | The main building: three floors and the helipad, doors, signs, gameplay markers and rules |
| Tower textures (in `VANTAGE_Tower/Models/*.fbm`) | [Poly Haven](https://polyhaven.com), CC0 | Concrete, metal and asphalt surfaces of the tower |
| *Stylized Coin Pack* (`Assets/PolyKebap`) | PolyKebap | The collectible coins (check the pack's licence before a commercial release) |
| *Military Training Environment* (`Assets/Blender_Asset/MilitaryEnvUnity`) | My own work, built in Blender | The whole world: terrain, forest, the military base, props |
| *Player character* (`Assets/Blender_Asset/AdhurimCharacter`) | My own avatar, made with Avaturn and exported from Blender | The player's body, animated by the template through Unity's humanoid retargeting (Vantage → Player → Use Character Model). Check Avaturn's terms before a commercial release. |
| Robot, boss, helicopter | Placeholders built from boxes by `VantagePlaceholders` | Until my Blender models replace them | The placeholders will be replaced with real models which will be modeled in blender
| *TextMesh Pro* (Unity, Essential Resources) | Unity Technologies | Text rendering for the HUD and minimap |

<!-- TODO before hand-in: check the publisher names against the Asset Store pages and add the store links. -->

Unused files (including the earlier six-storey command tower) were moved out of the project to keep the repository small. They are kept locally in `_UnusedAssets/`, which is not committed.

## Requirements and notes

- **Unity 6000.0.58f2** with URP. Active input handling is set to *Both*, because the template uses the old Input Manager.
- **Git LFS** is required: run `git lfs install` before cloning. Textures, models, audio and terrain data are stored in LFS.
- If the game might be sold later: check that the licences of the course template (*Third Person Cover Shooter*), *Pistol Animset Pro*, the *Stylized Coin Pack* and Avaturn cover commercial use.
- The new tower has not had a peer-playtest round yet. Enemy counts and damage per level are the first things to tune (Inspector: *VANTAGE Tower Setup → Tower Levels*).
