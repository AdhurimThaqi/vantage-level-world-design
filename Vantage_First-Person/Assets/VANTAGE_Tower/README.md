# VANTAGE Tower – Unity import

Unity 6. Works with URP or the Built-in pipeline.

## Import
1. Drag the whole `VANTAGE_Tower` folder into your project's `Assets` folder.
2. Wait for the scripts to compile. Then use **VANTAGE ▸ Reimport Tower Models** once.
   This makes sure the import script ran on both FBX files.
3. Open your map scene and use **VANTAGE ▸ Add Tower To Open Scene**.
   This adds the tower, the gameplay markers, a `VANTAGE_LevelManager` and a `PlayerStart`
   just outside the main entrance.
4. Move the `VANTAGE_Tower` root onto your terrain. Put your player at `PlayerStart`.
   The player needs the tag **Player**. The Starter Assets player already has it.
5. Press Play.

## What gets set up automatically
| Thing | Result |
|---|---|
| Walls, floors, stairs, props, glass, broken slabs | Mesh colliders and static flags |
| Doors | Slide up when you get close. Locked doors stay shut until unlocked. |
| Maintenance shutter | Starts open. Slams shut at the alarm trigger. Opens again when the Level 1 enemies and the turret are dead. |
| One-way red door (L1) | Opens only from the turret hallway side |
| Security console (L1) | Press **E** to unlock the West Wing door |
| Gun / Key (L2) | Walk into them to pick them up |
| Key slot (L2) | Press **E**, needs the Key, opens the stairwell gate |
| Override terminal (L3) | Press **E** to unlock the robot room |
| Bottom-right room door (L3) | Opens when both outside patrol enemies are dead |
| Helipad trigger | Runs the ending you pick on the LevelManager |
| Markers (red capsules, yellow icons, paths) | Visible in the editor, hidden in Play mode |

## LevelManager
- **Enemy / Turret / Heavy Robot / Boss prefab**: these spawn on the markers with the same name as the
  marker. Leave them empty to test the layout without enemies.
  - With no enemies, the "kill to open" doors open right away.
  - If spawned enemies face backwards, untick *Flip Marker Facing*.
- **Ending**:
  - **A**: the east part of the helipad drops.
  - **B**: the boss prefab spawns at the boss marker.
  - **C**: shows MISSION COMPLETE, or runs your own `onEndingC` event.
- Events: `onAlarm`, `onEndingA/B/C`.
- Your enemy script can implement `OnAlert()` to react when the player enters the destroyed area.

## For your own scripts
- Every object keeps its Blender properties in a `VantageTag` component.
  Examples: `marker_type`, `note`, `requires`, `unlock_by`.
- `VantageInventory.Has("Gun")` tells you if the player has the Armory gun. The robot is tagged `requires = Gun`.
- `VantageDoor.Find("L3_DOOR_SERoom_LOCKED").Unlock()` opens a door from code.
- `VantagePatrolPath.GetPoints()` returns the outside patrol routes in world space.

## Textures
The textures are CC0 from Poly Haven and sit in the `.fbm` folders next to the FBX files.
Unity picks them up on import. Roughness maps are included but Unity's standard import
doesn't use them, so smoothness is set to 0.15.
