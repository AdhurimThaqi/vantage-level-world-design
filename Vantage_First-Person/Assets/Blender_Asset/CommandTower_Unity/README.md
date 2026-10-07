# Command Tower – standalone Unity asset

A 6-storey military command tower with a full interior, doors, lights and gameplay markers.
This package is self-contained, so you can drop it into any Unity project or map.

## Import (2 minutes)
1. Copy this whole folder into your project, e.g. `Assets/CommandTower`.
2. Wait for Unity to import it.
3. Menu **Tools > Command Tower > Create Prefab**.
   This sets the texture/model import settings, creates the materials, and saves `Prefabs/CommandTower.prefab`.
4. Drag the prefab into your scene. Its pivot is the centre of the tower base.
   Or use **Create Prefab + Place In Open Scene**, which puts it where your Scene view is looking.

Works with URP (recommended) and Built-in, on Unity 2021.3 or newer.

## What's inside the prefab
- **Structure**: one mesh per floor (walls, slabs, stairs), roof, facade, fire escape. Every part has a mesh collider.
  Glass is separate per floor. `TWR_StairColliders` are invisible ramps so characters walk up the stairs smoothly.
- **Furniture**: about 520 pieces (desks, server racks, bunks, shelves, sandbags, drones...). They are static and work as cover.
- **Doors**: 32 doors with the `CommandTowerKit.TowerDoor` component (`Open()`, `Close()`, `Toggle()`, `locked`).
- **Lights**: baked point lights. Bake lighting via Window > Rendering > Lighting.
- **GameplayMarkers**: 202 `CommandTowerKit.GameplayMarker` objects, shown as coloured gizmos:
  - EnemySpawn (94, every room, with floor/room names)
  - Patrol (one ordered route per floor)
  - DroneSpawn (roof pads, hangar, workshop) and DronePatrol (3 rings around the tower + one indoor route)
  - PlayerEntry (main door, loading bay, fire escape, roof), Objective (command map table, drone uplink, server room, commander intel)
  - Overwatch, SecurityCamera, Cover

## Floors
| Floor | Rooms |
|---|---|
| Ground | Lobby + security checkpoint, guard room, armory/loading bay, lockers, CCTV, generator |
| 1 | 3 dorms, washroom, canteen, lounge |
| 2 | Operations hall, server room, radio room, office |
| 3 | Storage, armory, equipment, drone workshop |
| 4 | Command centre, briefing, commander office, comms |
| 5 | Drone control + balcony, battery room, drone hangar |
| Roof | 3 drone pads, comms mast, sandbag nests |

Footprint: about 30 x 35 m including the fire escape (north) and the entrance canopy (south). Place it on flat ground.
For AI, bake a NavMesh with the AI Navigation package. Doorways are 1.1 m or wider and the stairs use the ramp colliders.

Tip: if you also imported the full Military_Env_Unity package, both can live in the same project, because this package uses its own namespace (`CommandTowerKit`).
