# Military Training Environment – Unity package

Generated from Blender. Everything here is plain files: FBX models, PNG textures, terrain data and one editor script that rebuilds the full scene in Unity.

## Requirements
- Unity 2022.3 LTS or Unity 6 (tested API level). **URP recommended**; Built-in also works. HDRP: materials need converting.
- About 1.5 GB free for imported textures.

## How to import (5 minutes)
1. Copy this whole folder into your project as `Assets/MilitaryEnv` (any folder name works).
2. Wait for Unity to finish importing.
3. Menu **Tools > Military Env > Build Everything**.
   It sets texture/model import settings, creates materials, assigns them to all models,
   builds the terrain (height, 5 texture layers, grass, ferns, ~26k trees/rocks/debris),
   places all buildings, props, vehicles, roads and lights, and saves
   `Scenes/MilitaryTrainingEnvironment.unity`.
4. Open the scene and press Play, or look through the cameras in `13_CAMERAS`.

If you only change textures later, run **1 - Import Settings + Materials**; to rebuild the level only, run **2 - Build Scene**.

## Folder overview
| Folder | Contents |
|---|---|
| Models/Buildings | 16 buildings (HQ, barracks, warehouse, garage...) – with mesh colliders + lightmap UVs |
| Models/Props | 62 reusable props & vehicles (containers, barriers, fence, towers, trucks, APCs...) |
| Models/World | roads, pads, decals, berms, trenches, distant horizon (world-space meshes) |
| Models/Vegetation | trees, understory, grass cards, ferns, rocks, debris |
| Textures/Assets | baked per-asset atlases: `_Albedo`, `_Normal`, `_MetallicSmoothness` (R = metallic, A = smoothness) |
| Textures/Tiling | seamless tiling materials (asphalt, gravel, dirt road, concrete, mud, terrain layers) |
| Textures/Vegetation | leaf/needle cards (with alpha), bark tiles, rock textures |
| Terrain | 16-bit heightmap (1025²), 5-layer splat map, grass/fern density maps (+ PNG previews) |
| Data | `placements.json` (every object transform), `manifest.json`, `alpha_materials.txt` |
| Scripts | `Editor/MilitaryEnvBuilder.cs` (the importer) and `DistantForestRenderer.cs` (GPU-instanced horizon forest) |

## Notes / known limits
- Terrain: 1000 x 1000 m, centred on world origin. Heights from about -22 m to +77 m.
- Collision: buildings, props and roads get mesh colliders; trees have none (add a CapsuleCollider to the tree models if you need them).
- Fog is done with Unity fog (Exponential Squared). Tweak in Lighting > Environment, or add a URP Volume for colour grading.
- Performance: trees/rocks/debris use the Terrain tree system (instanced), grass uses Terrain details with instancing,
  and the horizon forest uses `DistantForestRenderer`. Lower Terrain > Settings > Detail Distance / Tree Distance on weaker GPUs.
- No LODs were generated. For a shipping game, add LOD Groups to the trees and big props.
- Coordinates in `placements.json` are Blender space; conversion is `Unity = (-x, z, -y)`.
