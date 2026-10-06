using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage
{
    /// <summary>
    /// Procedural level 2 (concept doc): a seeded room-and-corridor layout inside a fixed footprint,
    /// between the hand-placed entry (stair landing) and exit (fire escape door).
    /// The layout varies, the pacing does not: entry, exit and enemy count stay authored.
    ///
    /// The main route is authored too (playtest finding: players got lost in a fully generated floor).
    /// An L-shaped hall runs from the stair landing to the fire escape door and is never split or furnished;
    /// the generated rooms sit beside it, each side block opening onto it through seeded doorways.
    ///
    /// Binary space partitioning: every split wall gets at least one doorway, so every room is
    /// always reachable. Walls are kept out of authored "keep clear" zones and never end inside a doorway.
    /// All coordinates are local to the tower root (this object sits at the tower origin).
    /// </summary>
    public class VantageLevel2Generator : MonoBehaviour
    {
        [Header("Seed")]
        public int Seed = 1;
        [Tooltip("New layout every play session; the seed is shown on screen and written to the playtest log.")]
        public bool RandomSeedEachPlay = true;

        [Header("Footprint (tower local)")]
        public float FloorY = 7.2f;
        public float CeilingY = 10.8f;
        [Tooltip("Side blocks the generator fills with rooms. Shared edges between them get a doorway.")]
        public Rect[] Regions =
        {
            Rect.MinMaxRect(-5.6f, -1.5f, 5.6f, 5.6f),
            Rect.MinMaxRect(-5.6f, -8.6f, 0f, -1.5f),
        };
        [Tooltip("The authored main route (stair landing -> north hall -> west hall -> fire escape door). Never split or furnished; every side block gets a doorway onto it.")]
        public Rect[] Halls =
        {
            Rect.MinMaxRect(-8.6f, 5.6f, 5.6f, 8.6f),
            Rect.MinMaxRect(-8.6f, -8.6f, -5.6f, 5.6f),
        };
        [Tooltip("Authored spots no generated wall or prop may cover.")]
        public Rect[] KeepClear =
        {
            Rect.MinMaxRect(-8.6f, -8.2f, -6.4f, -5.2f),
            Rect.MinMaxRect(-8.6f, 1.3f, -7.2f, 3.9f),
        };
        public Vector2 Entry = new Vector2(7.2f, 6.2f);

        [Header("Rules")]
        public float MinRoom = 3f;
        public float CorridorWidth = 1.8f;
        [Range(0, 1)] public float CorridorChance = 0.35f;
        [Range(0, 1)] public float SecondDoorChance = 0.3f;
        public float DoorWidth = 1.3f;
        public float DoorHeight = 2.4f;
        public float WallThickness = 0.2f;

        [Header("Content")]
        public Material WallMaterial;
        public Material WoodMaterial;
        public Material MetalMaterial;
        public Material FabricMaterial;
        public GameObject DronePrefab;
        [Tooltip("Authored enemy count, placed in the rooms furthest from the entry.")]
        public int DroneCount = 3;

        public int UsedSeed { get; private set; }
        public IReadOnlyList<Rect> Rooms => _rooms;

        private struct Door { public Vector2 Center; }

        private readonly List<Rect> _rooms = new List<Rect>();
        private readonly List<Rect> _corridors = new List<Rect>();
        private readonly List<Door> _doors = new List<Door>();
        private System.Random _rng;
        private Transform _root;

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            var seed = RandomSeedEachPlay ? (System.Environment.TickCount & 0x7fffffff) % 100000 : Seed;
            Generate(seed, true);
        }

        public void Generate(int seed, bool spawnEnemies)
        {
            Clear();
            UsedSeed = seed;
            _rng = new System.Random(seed);
            _rooms.Clear();
            _corridors.Clear();
            _doors.Clear();

            _root = new GameObject("Generated Layout (seed " + seed + ")").transform;
            _root.SetParent(transform, false);
            if (!Application.isPlaying)
                _root.gameObject.hideFlags = HideFlags.DontSave;

            // Doorways on edges shared between regions first, so later walls avoid them.
            for (int a = 0; a < Regions.Length; a++)
            {
                for (int b = a + 1; b < Regions.Length; b++)
                    sharedEdgeWall(Regions[a], Regions[b]);
                foreach (var hall in Halls)
                    sharedEdgeWall(Regions[a], hall);
            }

            foreach (var region in Regions)
                split(region, 0);

            placeProps();

            if (spawnEnemies && DronePrefab != null)
                spawnDrones();

            if (Application.isPlaying)
                VantageEvents.RaiseLayoutGenerated(seed);

            Debug.Log($"[Vantage] Level 2 layout seed {seed}: {_rooms.Count} rooms, {_corridors.Count} corridors, {_doors.Count} doorways.");
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (!child.name.StartsWith("Generated Layout"))
                    continue;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        #region Partitioning

        private void split(Rect r, int depth)
        {
            var canX = r.width >= MinRoom * 2;
            var canZ = r.height >= MinRoom * 2;

            if ((!canX && !canZ) || depth > 5 || (depth >= 2 && _rng.NextDouble() < 0.25))
            {
                _rooms.Add(r);
                return;
            }

            var alongX = canX && (!canZ || (r.width > r.height ? _rng.NextDouble() < 0.8 : _rng.NextDouble() < 0.2));
            var length = alongX ? r.width : r.height;
            var corridor = depth <= 1 && length >= MinRoom * 2 + CorridorWidth && _rng.NextDouble() < CorridorChance;
            var gap = corridor ? CorridorWidth : 0f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var min = (alongX ? r.xMin : r.yMin) + MinRoom + gap / 2;
                var max = (alongX ? r.xMax : r.yMax) - MinRoom - gap / 2;
                if (max < min)
                    break;

                var pos = Mathf.Round(Mathf.Lerp(min, max, (float)_rng.NextDouble()) * 2f) / 2f;
                var cutA = pos - gap / 2;
                var cutB = pos + gap / 2;

                if (!wallAllowed(r, alongX, cutA) || (corridor && !wallAllowed(r, alongX, cutB)))
                    continue;

                Rect first, second;
                if (alongX)
                {
                    first = Rect.MinMaxRect(r.xMin, r.yMin, cutA, r.yMax);
                    second = Rect.MinMaxRect(cutB, r.yMin, r.xMax, r.yMax);
                }
                else
                {
                    first = Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, cutA);
                    second = Rect.MinMaxRect(r.xMin, cutB, r.xMax, r.yMax);
                }

                buildSplitWall(r, alongX, cutA);
                if (corridor)
                {
                    buildSplitWall(r, alongX, cutB);
                    _corridors.Add(alongX ? Rect.MinMaxRect(cutA, r.yMin, cutB, r.yMax) : Rect.MinMaxRect(r.xMin, cutA, r.xMax, cutB));
                }

                split(first, depth + 1);
                split(second, depth + 1);
                return;
            }

            _rooms.Add(r);
        }

        /// <summary>
        /// A wall across 'r' at 'pos' must stay out of keep-clear zones and must not end in an existing doorway.
        /// </summary>
        private bool wallAllowed(Rect r, bool alongX, float pos)
        {
            var wall = alongX
                ? Rect.MinMaxRect(pos - WallThickness, r.yMin, pos + WallThickness, r.yMax)
                : Rect.MinMaxRect(r.xMin, pos - WallThickness, r.xMax, pos + WallThickness);

            foreach (var zone in KeepClear)
                if (zone.Overlaps(wall))
                    return false;

            var endA = alongX ? new Vector2(pos, r.yMin) : new Vector2(r.xMin, pos);
            var endB = alongX ? new Vector2(pos, r.yMax) : new Vector2(r.xMax, pos);
            foreach (var door in _doors)
                if (Vector2.Distance(door.Center, endA) < DoorWidth / 2 + 0.5f || Vector2.Distance(door.Center, endB) < DoorWidth / 2 + 0.5f)
                    return false;

            return true;
        }

        private void buildSplitWall(Rect r, bool alongX, float pos)
        {
            var from = alongX ? r.yMin : r.xMin;
            var to = alongX ? r.yMax : r.xMax;
            var doors = _rng.NextDouble() < SecondDoorChance && to - from > DoorWidth * 3 + 1.5f ? 2 : 1;
            buildWall(!alongX, pos, from, to, doors);
        }

        private void sharedEdgeWall(Rect a, Rect b)
        {
            // Horizontal shared edge (a above b or b above a).
            if (Mathf.Approximately(a.yMin, b.yMax) || Mathf.Approximately(a.yMax, b.yMin))
            {
                var z = Mathf.Approximately(a.yMin, b.yMax) ? a.yMin : a.yMax;
                var x0 = Mathf.Max(a.xMin, b.xMin);
                var x1 = Mathf.Min(a.xMax, b.xMax);
                if (x1 - x0 > DoorWidth + 1f)
                    buildWall(true, z, x0, x1, _rng.NextDouble() < SecondDoorChance ? 2 : 1);
            }
            else if (Mathf.Approximately(a.xMin, b.xMax) || Mathf.Approximately(a.xMax, b.xMin))
            {
                var x = Mathf.Approximately(a.xMin, b.xMax) ? a.xMin : a.xMax;
                var z0 = Mathf.Max(a.yMin, b.yMin);
                var z1 = Mathf.Min(a.yMax, b.yMax);
                if (z1 - z0 > DoorWidth + 1f)
                    buildWall(false, x, z0, z1, _rng.NextDouble() < SecondDoorChance ? 2 : 1);
            }
        }

        /// <summary>
        /// Wall running along X (at fixed z) or along Z (at fixed x), from 'from' to 'to', with doorways.
        /// </summary>
        private void buildWall(bool alongX, float fixedCoord, float from, float to, int doorCount)
        {
            var doors = new List<float>();
            for (int attempt = 0; attempt < 20 && doors.Count < doorCount; attempt++)
            {
                var center = Mathf.Lerp(from + DoorWidth / 2 + 0.4f, to - DoorWidth / 2 - 0.4f, (float)_rng.NextDouble());
                if (doors.Exists(d => Mathf.Abs(d - center) < DoorWidth * 2))
                    continue;
                doors.Add(center);
            }
            doors.Sort();

            var cursor = from;
            foreach (var center in doors)
            {
                var d0 = center - DoorWidth / 2;
                var d1 = center + DoorWidth / 2;
                wallBox(alongX, fixedCoord, cursor, d0, FloorY, CeilingY);
                wallBox(alongX, fixedCoord, d0, d1, FloorY + DoorHeight, CeilingY, "Lintel");
                cursor = d1;
                _doors.Add(new Door { Center = alongX ? new Vector2(center, fixedCoord) : new Vector2(fixedCoord, center) });
            }
            wallBox(alongX, fixedCoord, cursor, to, FloorY, CeilingY);
        }

        private void wallBox(bool alongX, float fixedCoord, float s0, float s1, float y0, float y1, string name = "Wall")
        {
            if (s1 - s0 < 0.05f || y1 - y0 < 0.05f)
                return;

            var size = alongX ? new Vector3(s1 - s0, y1 - y0, WallThickness) : new Vector3(WallThickness, y1 - y0, s1 - s0);
            var center = alongX ? new Vector3((s0 + s1) / 2, (y0 + y1) / 2, fixedCoord) : new Vector3(fixedCoord, (y0 + y1) / 2, (s0 + s1) / 2);
            block(name, center, size, WallMaterial, true);
        }

        #endregion

        #region Content

        private void placeProps()
        {
            if (_rooms.Count == 0)
                return;

            // Story prop 3 goes in the biggest room: more gear than beds.
            var biggest = 0;
            for (int i = 1; i < _rooms.Count; i++)
                if (_rooms[i].width * _rooms[i].height > _rooms[biggest].width * _rooms[biggest].height)
                    biggest = i;

            for (int i = 0; i < _rooms.Count; i++)
            {
                var room = _rooms[i];
                if (i == biggest)
                {
                    placeBunks(room);
                    continue;
                }

                var count = _rng.Next(1, 4);
                for (int p = 0; p < count; p++)
                {
                    switch (_rng.Next(3))
                    {
                        case 0: againstWall(room, "Desk", new Vector3(1.6f, 0.8f, 0.8f), WoodMaterial); break;
                        case 1: againstWall(room, "Cabinet", new Vector3(0.8f, 1.5f, 0.6f), MetalMaterial); break;
                        default: againstWall(room, "Crate", new Vector3(1f, 1f, 1f), WoodMaterial); break;
                    }
                }
            }
        }

        private void placeBunks(Rect room)
        {
            for (int b = 0; b < 2; b++)
            {
                var bunk = againstWall(room, "Bunk", new Vector3(2f, 1.8f, 0.9f), FabricMaterial);
                if (bunk == null)
                    break;
            }

            for (int i = 0; i < 7; i++)
            {
                var x = Mathf.Lerp(room.xMin + 0.5f, room.xMax - 0.5f, (float)_rng.NextDouble());
                var z = Mathf.Lerp(room.yMin + 0.5f, room.yMax - 0.5f, (float)_rng.NextDouble());
                if (nearDoor(new Vector2(x, z), 1.1f))
                    continue;
                block("Backpack", new Vector3(x, FloorY + 0.22f, z), new Vector3(0.5f, 0.45f, 0.35f), FabricMaterial, false);
            }
        }

        /// <summary>
        /// Puts a prop against a random wall of the room, away from doorways. Returns null if it did not fit.
        /// </summary>
        private GameObject againstWall(Rect room, string name, Vector3 size, Material material)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var side = _rng.Next(4);
                var rotated = side >= 2;
                var sx = rotated ? size.z : size.x;
                var sz = rotated ? size.x : size.z;
                var margin = WallThickness / 2 + 0.05f;

                float x, z;
                switch (side)
                {
                    case 0: x = Mathf.Lerp(room.xMin + sx / 2 + 0.3f, room.xMax - sx / 2 - 0.3f, (float)_rng.NextDouble()); z = room.yMin + margin + sz / 2; break;
                    case 1: x = Mathf.Lerp(room.xMin + sx / 2 + 0.3f, room.xMax - sx / 2 - 0.3f, (float)_rng.NextDouble()); z = room.yMax - margin - sz / 2; break;
                    case 2: z = Mathf.Lerp(room.yMin + sz / 2 + 0.3f, room.yMax - sz / 2 - 0.3f, (float)_rng.NextDouble()); x = room.xMin + margin + sx / 2; break;
                    default: z = Mathf.Lerp(room.yMin + sz / 2 + 0.3f, room.yMax - sz / 2 - 0.3f, (float)_rng.NextDouble()); x = room.xMax - margin - sx / 2; break;
                }

                var footprint = new Rect(x - sx / 2, z - sz / 2, sx, sz);
                if (nearDoor(new Vector2(x, z), Mathf.Max(sx, sz) / 2 + DoorWidth))
                    continue;
                var blocked = false;
                foreach (var zone in KeepClear)
                    blocked |= zone.Overlaps(footprint);
                if (blocked)
                    continue;

                return block(name, new Vector3(x, FloorY + size.y / 2, z), new Vector3(sx, size.y, sz), material, true);
            }

            return null;
        }

        private bool nearDoor(Vector2 point, float distance)
        {
            foreach (var door in _doors)
                if (Vector2.Distance(door.Center, point) < distance)
                    return true;
            return false;
        }

        private void spawnDrones()
        {
            var order = new List<Rect>(_rooms);
            order.AddRange(_corridors);
            order.Sort((a, b) => Vector2.Distance(b.center, Entry).CompareTo(Vector2.Distance(a.center, Entry)));

            for (int i = 0; i < DroneCount && i < order.Count; i++)
            {
                var c = order[i].center;
                var position = transform.TransformPoint(new Vector3(c.x, FloorY + 2f, c.y));
                var drone = Instantiate(DronePrefab, position, transform.rotation, _root);
                drone.name = "Level 2 Drone " + (i + 1);
                var ai = drone.GetComponent<VantageDrone>();
                if (ai != null)
                {
                    // Close-quarters: short sight, tight orbit, low hover under the 3.6 m ceiling.
                    ai.DetectRange = 9f;
                    ai.PreferredDistance = 4f;
                    ai.HoverHeight = 1.4f;
                    ai.PatrolRadius = Mathf.Min(order[i].width, order[i].height) * 0.3f;
                }
            }
        }

        private GameObject block(string name, Vector3 localCenter, Vector3 size, Material material, bool carve)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
            if (!Application.isPlaying)
                go.hideFlags = HideFlags.DontSave;

            if (carve && Application.isPlaying)
            {
                // Cuts the baked NavMesh so the soldiers path around the generated walls.
                var obstacle = go.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                obstacle.size = Vector3.one;

                // Generated walls and furniture are cover, like everything else in the tower.
                if (name != "Lintel")
                    VantageCoverUtil.AddFor(go, _root);
            }

            return go;
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            foreach (var r in Regions)
                Gizmos.DrawWireCube(new Vector3(r.center.x, (FloorY + CeilingY) / 2, r.center.y), new Vector3(r.width, CeilingY - FloorY, r.height));
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            foreach (var r in KeepClear)
                Gizmos.DrawWireCube(new Vector3(r.center.x, FloorY + 1f, r.center.y), new Vector3(r.width, 2f, r.height));
        }
    }
}
