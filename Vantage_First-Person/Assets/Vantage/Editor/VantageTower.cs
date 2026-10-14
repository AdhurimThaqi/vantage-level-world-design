using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Sets up the user's Blender tower (Assets/VANTAGE_Tower: VANTAGE_Tower_Level.fbx + VANTAGE_Tower_Gameplay.fbx)
    /// as the level: three levels and the helipad finale. The tower package's own import script already turns the
    /// Blender custom properties into doors, triggers, consoles and pickups; this adds what our game needs on top.
    /// Safe to re-run: everything it adds lives under "VANTAGE Tower Setup".
    /// </summary>
    public static class VantageTower
    {
        public const string InstanceName = "VANTAGE_Tower";
        public const string SetupName = "VANTAGE Tower Setup";
        private const string LevelModel = "VANTAGE_Tower_Level", GameplayModel = "VANTAGE_Tower_Gameplay";
        private const string OldTowerName = "CommandTower";

        /// <summary>
        /// Objects that give the walking floor of each level (L1, L2, L3, Finale): the bottom of the entrance door,
        /// then the top of the slab each level stands on.
        /// </summary>
        public static readonly (string name, bool top)[] FloorObjects =
            { ("DOOR_Entrance_L", false), ("LVL2_FloorSlab", true), ("LVL2_Ceiling", true), ("FINAL_Platform", true) };

        public static GameObject FindTower()
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == InstanceName)
                    return root;
            return null;
        }

        /// <summary>First object with this exact name under the tower (inactive ones included).</summary>
        public static Transform Find(Transform tower, string name)
        {
            foreach (var t in tower.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        /// <summary>World bounds of an object's renderers (children included); false if it has none.</summary>
        public static bool WorldBounds(Transform t, out Bounds b)
        {
            b = default;
            var first = true;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return !first;
        }

        /// <summary>World y of each level's floor (FloorObjects order); NaN where the object is missing.</summary>
        public static float[] LevelHeights(Transform tower)
        {
            var heights = new float[FloorObjects.Length];
            for (int i = 0; i < FloorObjects.Length; i++)
            {
                var o = Find(tower, FloorObjects[i].name);
                heights[i] = o != null && WorldBounds(o, out var b) ? (FloorObjects[i].top ? b.max.y : b.min.y) : float.NaN;
            }
            return heights;
        }

        [MenuItem("Vantage/Tower/Set Up Tower (levels, doors, enemies)", priority = 80)]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Places the tower (replacing the old CommandTower) and builds everything under "VANTAGE Tower Setup".</summary>
        public static void Setup()
        {
            var tower = PlaceTower().transform;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == SetupName)
                    Object.DestroyImmediate(root);
            var setup = new GameObject(SetupName).transform;
            groundTower(tower);
            Physics.SyncTransforms();
            hideMarkers(tower);
            staticFlags(tower);
            levelDoors(tower);
            doorsOffNavMesh(tower);
            var flights = smoothStairs(tower, group(setup, "Stair Surfaces"));
            var gates = buildGates(tower, group(setup, "Level Gates"), flights);
            covers(tower, group(setup, "Covers"));
            var start = playerStart(tower);
            pickups(tower, setup, start);
            keyAndSlot(tower);
            alarm(tower, setup);
            securityCamera(tower, setup);
            VantagePlaceholders.Build(false);
            var levels = levelManager(tower, setup, flights, start);
            extraction(tower, setup, levels);
            pointToolsAtTower(tower);
            checkDoors(tower);

            VantageSetup.BakeNavMesh();
            coins(tower, group(setup, "Coins"), levels);
            EditorSceneManager.MarkSceneDirty(tower.gameObject.scene);
            Debug.Log($"[Vantage] Tower set up: {flights.Count} stair flights, {gates.Count} gates, {levels.Levels.Count} levels.");
        }

        /// <summary>
        /// -executeMethod Vantage.EditorTools.VantageTower.SetupBatch [-vantageShots folder]
        /// Re-runs the tower setup (not the world), saves, bakes occlusion and renders views of every level.
        /// </summary>
        public static void SetupBatch()
        {
            var folder = VantageEditorUtil.Arg("-vantageShots");
            var exitCode = 0;
            try
            {
                var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                Setup();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                if (folder != null)
                    Shots(folder);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Outside views and one look across each level, for checking the setup without playing.</summary>
        public static void Shots(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            var t = FindTower().transform;
            WorldBounds(t, out var b);
            var facing = EntranceFacing(t);
            var entrance = EntrancePoint(t) ?? b.center;
            // The first render after loading comes out garbled (shader warm-up); render it twice.
            VantageEditorUtil.Shot(folder, "tower_1_approach", entrance + facing * 22f + Vector3.up * 4f, entrance + Vector3.up * 3f, false, 0);
            VantageEditorUtil.Shot(folder, "tower_1_approach", entrance + facing * 22f + Vector3.up * 4f, entrance + Vector3.up * 3f, false, 0);
            VantageEditorUtil.Shot(folder, "tower_2_overview", b.center + facing * 35f + Vector3.Cross(Vector3.up, facing) * 25f + Vector3.up * 25f, b.center, false, 0);
            var h = LevelHeights(t);
            for (int i = 0; i < 3; i++)
                VantageEditorUtil.Shot(folder, $"tower_3_level{i + 1}", entrance - facing * 1.5f + Vector3.up * (h[i] - h[0] + 1.7f),
                    new Vector3(b.center.x, h[i] + 1.2f, b.center.z), false, 0);
            var platform = Find(t, "FINAL_Platform");
            if (platform != null && WorldBounds(platform, out var pb))
                VantageEditorUtil.Shot(folder, "tower_4_helipad", pb.center + facing * 12f + Vector3.up * 8f, pb.center, false, 0);
            Debug.Log("[Vantage] Tower screenshots written to " + folder);
        }

        #region Placement

        /// <summary>
        /// Replaces the old CommandTower with the new tower at the same spot: the new footprint's centre on the old
        /// one, the ground floor at the old ground floor, the entrance facing the same way. Re-running keeps an
        /// existing VANTAGE_Tower where it is.
        /// </summary>
        public static GameObject PlaceTower()
        {
            var existing = FindTower();
            Vector3? oldCentre = null, oldFacing = null;
            float? oldFloor = null;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != OldTowerName && root.name != SetupName) continue;
                if (root.name == OldTowerName && WorldBounds(root.transform, out var ob))
                {
                    oldCentre = ob.center;
                    // The old tower's entrance is its local +z.
                    oldFacing = root.transform.forward;
                    // Ground floor surface of the old tower (local y 0.45, see VantageTowerLevels.GroundFloor).
                    oldFloor = root.transform.position.y + 0.45f;
                }
                Debug.Log($"[Vantage] Removed old '{root.name}'.");
                Object.DestroyImmediate(root);
            }
            if (existing != null)
                return existing;

            var tower = new GameObject(InstanceName);
            var level = instantiate(LevelModel);
            var gameplay = instantiate(GameplayModel);
            if (level == null || gameplay == null)
                throw new Exception("VANTAGE_Tower_Level.fbx / VANTAGE_Tower_Gameplay.fbx not found under Assets/VANTAGE_Tower.");
            level.transform.SetParent(tower.transform, false);
            gameplay.transform.SetParent(tower.transform, false);

            if (oldCentre.HasValue)
            {
                // Turn the tower so its entrance faces where the old entrance faced.
                var facing = EntranceFacing(tower.transform);
                var want = Vector3.ProjectOnPlane(oldFacing.Value, Vector3.up).normalized;
                tower.transform.rotation = Quaternion.FromToRotation(facing, want);
                tower.transform.rotation = Quaternion.Euler(0, tower.transform.eulerAngles.y, 0);
                Physics.SyncTransforms();
                WorldBounds(level.transform, out var nb);
                var floorY = LevelHeights(tower.transform)[0];
                if (float.IsNaN(floorY))
                    throw new Exception("The tower has no DOOR_Entrance_L: can't find its ground floor.");
                Debug.Log($"[Vantage] Old entrance faced {want:F2}, new entrance faced {facing:F2}.");
                var shift = new Vector3(oldCentre.Value.x - nb.center.x, (oldFloor ?? floorY) - floorY, oldCentre.Value.z - nb.center.z);
                tower.transform.position += shift;
            }
            Debug.Log($"[Vantage] Tower placed at {tower.transform.position:F2}, yaw {tower.transform.eulerAngles.y:F0}.");
            return tower;
        }

        /// <summary>
        /// The character can't step up, so the ground floor must be flush with the terrain at the main entrance:
        /// the tower is moved up or down so its floor is 2 cm above the terrain just outside the doors.
        /// </summary>
        private static void groundTower(Transform tower)
        {
            var terrain = Terrain.activeTerrain;
            var entrance = EntrancePoint(tower);
            if (terrain == null || entrance == null) return;
            var outside = entrance.Value + EntranceFacing(tower) * 1.5f;
            var ground = terrain.SampleHeight(outside) + terrain.transform.position.y;
            var dy = ground + 0.02f - entrance.Value.y;
            if (Mathf.Abs(dy) < 0.005f) return;
            tower.position += Vector3.up * dy;
            Physics.SyncTransforms();
            Debug.Log($"[Vantage] Tower moved {dy:+0.00;-0.00} m so the entrance is flush with the ground.");
        }

        /// <summary>Outward direction of the main entrance (from the tower's centre through the entrance doors).</summary>
        public static Vector3 EntranceFacing(Transform tower)
        {
            var l = Find(tower, "DOOR_Entrance_L");
            var r = Find(tower, "DOOR_Entrance_R");
            if (l == null || r == null || !WorldBounds(tower, out var b)) return tower.forward;
            var door = (l.position + r.position) * 0.5f;
            var outDir = door - new Vector3(b.center.x, door.y, b.center.z);
            return Mathf.Abs(outDir.x) > Mathf.Abs(outDir.z) ? new Vector3(Mathf.Sign(outDir.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(outDir.z));
        }

        /// <summary>Midpoint between the two entrance door leaves, or null.</summary>
        public static Vector3? EntrancePoint(Transform tower)
        {
            var l = Find(tower, "DOOR_Entrance_L");
            var r = Find(tower, "DOOR_Entrance_R");
            if (l == null || r == null) return null;
            WorldBounds(l, out var lb);
            WorldBounds(r, out var rb);
            var c = (lb.center + rb.center) * 0.5f;
            return new Vector3(c.x, Mathf.Min(lb.min.y, rb.min.y), c.z);
        }

        private static GameObject instantiate(string model)
        {
            foreach (var guid in AssetDatabase.FindAssets(model + " t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(model + ".fbx")) continue;
                return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            return null;
        }

        #endregion

        private static Transform group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform levelModel(Transform tower) => tower.Find(LevelModel);
        private static Transform gameplayModel(Transform tower) => tower.Find(GameplayModel);

        #region Markers, static flags, doors

        /// <summary>
        /// The gameplay model's markers (red capsules, icons, patrol lines, trigger boxes) are for the editor only:
        /// their renderers are switched off here once, instead of being searched for and hidden at every start.
        /// The key stays visible. Nothing of the gameplay model goes into the NavMesh.
        /// </summary>
        private static void hideMarkers(Transform tower)
        {
            var gameplay = gameplayModel(tower);
            if (gameplay == null) return;
            var hidden = 0;
            foreach (var r in gameplay.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.name == "L2_PICKUP_Key") continue;
                if (r.enabled) { r.enabled = false; EditorUtility.SetDirty(r); }
                hidden++;
            }
            if (gameplay.GetComponent<Unity.AI.Navigation.NavMeshModifier>() == null)
                gameplay.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
            Debug.Log($"[Vantage] Markers stripped: {hidden} marker renderers off.");
        }

        /// <summary>
        /// The tower's import makes every level mesh occluder static. Only walls, slabs and stairs should hide what is
        /// behind them: glass, props, signs and lamps are only occludees, and the alarm beacons change at runtime.
        /// </summary>
        private static void staticFlags(Transform tower)
        {
            var level = levelModel(tower);
            if (level == null) return;
            int solid = 0, minor = 0, dynamic = 0;
            foreach (var r in level.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = r.gameObject;
                var n = go.name;
                StaticEditorFlags flags;
                if (go.GetComponent<VantageDoor>() || go.GetComponent<VantageInteract>() || go.GetComponent<VantageBreakable>() || n.Contains("AlarmBeacons"))
                { flags = 0; dynamic++; }
                else if (n.Contains("Glass") || n.Contains("PROP") || n.Contains("Props") || n.Contains("SIGN") || n.Contains("TXT") || n.Contains("Lights")
                         || n.Contains("Lamps") || n.Contains("Railings") || n.Contains("KEYPAD") || n.Contains("Chunk") || n.Contains("Helipad"))
                { flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic; minor++; }
                else
                { flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic; solid++; }
                if (GameObjectUtility.GetStaticEditorFlags(go) != flags)
                    GameObjectUtility.SetStaticEditorFlags(go, flags);
            }
            Debug.Log($"[Vantage] Static flags: {solid} occluders, {minor} occludee-only, {dynamic} dynamic.");
        }

        /// <summary>
        /// Doors are left out of the NavMesh bake. Locked doors and the maintenance shutter get a carving obstacle
        /// (on while they block; VantageDoor switches it), so soldiers don't path through a shut door.
        /// </summary>
        private static void doorsOffNavMesh(Transform tower)
        {
            int obstacles = 0;
            foreach (var door in tower.GetComponentsInChildren<VantageDoor>(true))
            {
                var go = door.gameObject;
                if (go.GetComponent<Unity.AI.Navigation.NavMeshModifier>() == null)
                    go.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
                if (!door.locked && door.type != DoorType.Shutter) continue;
                var box = go.GetComponent<BoxCollider>();
                var obstacle = go.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                if (obstacle == null) obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                if (box != null)
                {
                    obstacle.center = box.center;
                    obstacle.size = box.size + new Vector3(0.1f, 0f, 0.1f);
                }
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                // The shutter starts rolled up: its obstacle comes on when it closes.
                obstacle.enabled = door.type != DoorType.Shutter;
                obstacles++;
            }
            Debug.Log($"[Vantage] Doors: left out of the NavMesh, {obstacles} locked doors/shutters with carving obstacles.");
        }

        #endregion

        #region Stairs and gates

        /// <summary>One stair flight in tower-local coordinates: foot and head (centre of each end) and width.</summary>
        public struct Flight { public Vector3 bottom, top; public float width; public string name; }

        /// <summary>
        /// The template character can't step up, so every flight of the tower's stairs (LVL1_Stairs,
        /// LVL2_Stairs_To_L3, FINAL_Landing_Stairs) gets an invisible walking surface: the slope-limited upper
        /// envelope of the real steps along the flight (never below a step edge, never steeper than 25.6°, under the
        /// 26° where the template slows down), meeting floors and landings flush at both ends. The flights are found
        /// from a height map of the steps; a landing between two flights is flat and belongs to neither.
        /// </summary>
        private static List<Flight> smoothStairs(Transform tower, Transform parent)
        {
            var result = new List<Flight>();
            var level = levelModel(tower);
            if (level == null) return result;
            foreach (var stairs in level.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.Contains("Stairs")))
            {
                var found = findFlights(tower, stairs.transform);
                foreach (var f in found)
                {
                    buildSurface(tower, parent, f, result.Count);
                    result.Add(f);
                }
                Debug.Log($"[Vantage] {stairs.name}: {found.Count} flights ({string.Join(", ", found.Select(f => $"{f.bottom.y:F2}->{f.top.y:F2} m, {f.width:F1} m wide"))}).");
            }
            return result;
        }

        private static List<Flight> findFlights(Transform tower, Transform stairs)
        {
            const float cell = 0.1f;
            const int window = 4;
            var own = new HashSet<Collider>(stairs.GetComponentsInChildren<Collider>(true));
            WorldBounds(stairs, out var wb);
            var lb = new Bounds(tower.InverseTransformPoint(wb.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                lb.Encapsulate(tower.InverseTransformPoint(wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            int nx = Mathf.CeilToInt(lb.size.x / cell) + 1, nz = Mathf.CeilToInt(lb.size.z / cell) + 1;
            var h = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    h[i, j] = float.NaN;
                    var from = tower.TransformPoint(new Vector3(lb.min.x + i * cell, lb.max.y + 0.5f, lb.min.z + j * cell));
                    foreach (var hit in Physics.RaycastAll(from, Vector3.down, lb.size.y + 1f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    {
                        if (!own.Contains(hit.collider) || hit.normal.y < 0.7f) continue;
                        var y = tower.InverseTransformPoint(hit.point).y;
                        if (float.IsNaN(h[i, j]) || y > h[i, j]) h[i, j] = y;
                    }
                }

            // Class per cell: 0 up along +x, 1 up along -x, 2 up along +z, 3 up along -z, -1 flat or unknown.
            var cls = new int[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    cls[i, j] = -1;
                    if (i < window || j < window || i >= nx - window || j >= nz - window) continue;
                    float gx = (h[i + window, j] - h[i - window, j]) / (2 * window * cell);
                    float gz = (h[i, j + window] - h[i, j - window]) / (2 * window * cell);
                    if (float.IsNaN(gx) || float.IsNaN(gz)) continue;
                    if (Mathf.Abs(gx) > 0.3f && Mathf.Abs(gz) < 0.15f) cls[i, j] = gx > 0 ? 0 : 1;
                    else if (Mathf.Abs(gz) > 0.3f && Mathf.Abs(gx) < 0.15f) cls[i, j] = gz > 0 ? 2 : 3;
                }

            var flights = new List<Flight>();
            var seen = new bool[nx, nz];
            var stack = new Stack<(int, int)>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (seen[i, j] || cls[i, j] < 0) continue;
                    var c = cls[i, j];
                    var cells = new List<(int, int)>();
                    stack.Push((i, j));
                    seen[i, j] = true;
                    while (stack.Count > 0)
                    {
                        var (a, b) = stack.Pop();
                        cells.Add((a, b));
                        foreach (var (da, db) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int x = a + da, z = b + db;
                            if (x < 0 || z < 0 || x >= nx || z >= nz || seen[x, z] || cls[x, z] != c) continue;
                            seen[x, z] = true;
                            stack.Push((x, z));
                        }
                    }
                    if (cells.Count * cell * cell < 0.8f) continue;

                    var alongX = c < 2;
                    float amin = float.MaxValue, amax = float.MinValue, cmin = float.MaxValue, cmax = float.MinValue, hmin = float.MaxValue, hmax = float.MinValue;
                    foreach (var (a, b) in cells)
                    {
                        float x = lb.min.x + a * cell, z = lb.min.z + b * cell;
                        float along = alongX ? x : z, across = alongX ? z : x;
                        amin = Mathf.Min(amin, along); amax = Mathf.Max(amax, along);
                        cmin = Mathf.Min(cmin, across); cmax = Mathf.Max(cmax, across);
                        hmin = Mathf.Min(hmin, h[a, b]); hmax = Mathf.Max(hmax, h[a, b]);
                    }
                    var upPositive = c == 0 || c == 2;
                    float low = upPositive ? amin : amax, high = upPositive ? amax : amin, mid = (cmin + cmax) / 2;
                    Vector3 at(float along, float y) => alongX ? new Vector3(along, y, mid) : new Vector3(mid, y, along);
                    flights.Add(new Flight { bottom = at(low, hmin), top = at(high, hmax), width = cmax - cmin + cell, name = stairs.name });
                }
            return flights;
        }

        /// <summary>The walking surface over one flight (see smoothStairs).</summary>
        private static void buildSurface(Transform t, Transform parent, Flight piece, int index)
        {
            const float slopeCap = 0.48f;  // 25.6°
            const float step = 0.1f, extend = 1.4f;
            var mask = VantagePhysics.Solid;
            var flat = piece.top - piece.bottom; flat.y = 0;
            var run = flat.magnitude;
            var dir = flat.normalized;
            var side = Vector3.Cross(Vector3.up, dir);
            var rise = piece.top.y - piece.bottom.y;
            var maxSlope = Mathf.Clamp(rise / run * 1.2f, 0.12f, slopeCap);
            int nk = Mathf.CeilToInt((run + 2 * extend) / step) + 1;
            var lanes = new[] { -0.45f, -0.15f, 0.15f, 0.45f };
            var heights = new float[lanes.Length, nk];
            var hole = new bool[lanes.Length, nk];

            for (int j = 0; j < lanes.Length; j++)
            {
                var raw = new float[nk];
                for (int i = 0; i < nk; i++)
                {
                    var k = -extend + i * step;
                    var plane = piece.bottom.y + Mathf.Clamp01(k / run) * rise;
                    var local = piece.bottom + dir * k + side * (lanes[j] * piece.width);
                    local.y = plane;
                    raw[i] = float.NegativeInfinity;
                    foreach (var h in Physics.RaycastAll(t.TransformPoint(local + Vector3.up * 1.2f), Vector3.down, 3.5f, mask, QueryTriggerInteraction.Ignore))
                    {
                        if (h.normal.y < 0.6f || h.collider.transform.IsChildOf(parent)) continue;
                        var y = t.InverseTransformPoint(h.point).y;
                        if (y <= plane + 0.3f && y > raw[i]) raw[i] = y;
                    }
                }
                var env = (float[])raw.Clone();
                for (int i = 1; i < nk; i++) env[i] = Mathf.Max(env[i], env[i - 1] - maxSlope * step);
                for (int i = nk - 2; i >= 0; i--) env[i] = Mathf.Max(env[i], env[i + 1] - maxSlope * step);
                for (int i = 0; i < nk; i++) { heights[j, i] = env[i]; hole[j, i] = float.IsNegativeInfinity(raw[i]); }
            }

            var vertices = new List<Vector3>();
            var vertexHole = new List<bool>();
            var triangles = new List<int>();
            var edges = new[] { -0.5f, -0.25f, 0f, 0.25f, 0.5f };
            for (int i = 0; i < nk; i++)
                for (int e = 0; e < edges.Length; e++)
                {
                    var j = Mathf.Clamp(Mathf.RoundToInt((edges[e] + 0.45f) / 0.3f), 0, lanes.Length - 1);
                    var y = heights[j, i];
                    if (float.IsNegativeInfinity(y)) y = Enumerable.Range(0, lanes.Length).Select(l => heights[l, i]).Max();
                    if (float.IsNegativeInfinity(y)) y = piece.bottom.y + Mathf.Clamp01((-extend + i * step) / run) * rise;
                    var local = piece.bottom + dir * (-extend + i * step) + side * (edges[e] * (piece.width - 0.1f));
                    local.y = y + 0.01f;
                    vertices.Add(t.TransformPoint(local));
                    vertexHole.Add(hole[j, i]);
                }
            for (int i = 0; i < nk - 1; i++)
                for (int e = 0; e < edges.Length - 1; e++)
                {
                    int a = i * edges.Length + e, b = a + 1, c = a + edges.Length, d = c + 1;
                    if (vertexHole[a] || vertexHole[b] || vertexHole[c] || vertexHole[d]) continue;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }

            var go = new GameObject($"Stair Surface {piece.name} {piece.bottom.y:F2}-{piece.top.y:F2}");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = go.name };
            var root = go.transform;
            mesh.SetVertices(vertices.Select(v => root.InverseTransformPoint(v)).ToList());
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            saveMesh(mesh, $"Stair Surface {index}");
        }

        private static void saveMesh(Mesh mesh, string name)
        {
            const string folder = "Assets/Vantage/World/Tower";
            if (!AssetDatabase.IsValidFolder("Assets/Vantage/World")) AssetDatabase.CreateFolder("Assets/Vantage", "World");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Vantage/World", "Tower");
            var path = $"{folder}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        /// <summary>
        /// Level 1 → 2 has no door in the model, so the flight that leaves the ground floor gets our red shutter
        /// (VantageFloorGate, level 0). Level 2 → 3 and 3 → finale are the Blender doors (key gate, exit door).
        /// </summary>
        private static List<VantageFloorGate> buildGates(Transform tower, Transform parent, List<Flight> flights)
        {
            var gates = new List<VantageFloorGate>();
            var ground = LevelHeights(tower)[0] - tower.position.y;
            var material = gateMaterial();
            foreach (var f in flights)
                if (f.name.StartsWith("LVL1") && Mathf.Abs(f.bottom.y - ground) < 0.8f)
                    gates.Add(gate(tower, parent, f, 0, material));
            Debug.Log($"[Vantage] Gates: {gates.Count} ({string.Join(", ", gates.Select(g => g.name))}).");
            return gates;
        }

        private static VantageFloorGate gate(Transform t, Transform parent, Flight piece, int level, Material material)
        {
            var go = new GameObject($"Gate {level + 1}->{level + 2} (Stairs)");
            go.transform.SetParent(parent, false);
            go.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;

            var flat = piece.top - piece.bottom; flat.y = 0;
            var dir = flat.normalized;
            var run = flat.magnitude;
            var rise = piece.top.y - piece.bottom.y;
            var frame = t.rotation * Quaternion.LookRotation(dir);

            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(go.transform, false);
            var height = rise + 3f;
            blocker.transform.SetPositionAndRotation(t.TransformPoint(piece.bottom + dir * (run / 2) + Vector3.up * (height / 2 - 0.15f)), frame);
            var box = blocker.AddComponent<BoxCollider>();
            box.size = new Vector3(piece.width + 0.3f, height, run + 1f);
            var obstacle = blocker.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Shutter";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(go.transform, false);
            panel.transform.SetPositionAndRotation(t.TransformPoint(piece.bottom - dir * 0.4f + Vector3.up * 1.3f), frame);
            panel.transform.localScale = new Vector3(piece.width + 0.4f, 2.6f, 0.08f);
            var renderer = panel.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.position = t.TransformPoint(piece.bottom - dir * 1.2f + Vector3.up * 2.4f);
            lamp.type = LightType.Point;
            lamp.range = 6f;
            lamp.intensity = 2.5f;
            lamp.shadows = LightShadows.None;

            var gate = go.AddComponent<VantageFloorGate>();
            gate.Level = level;
            gate.Blocker = box;
            gate.Obstacle = obstacle;
            gate.Panel = panel.transform;
            gate.PanelRenderer = renderer;
            gate.Lamp = lamp;
            return gate;
        }

        private static Material gateMaterial()
        {
            const string path = "Assets/Vantage/Materials/Gate Shutter.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        #endregion

        #region Cover, start, pickups, tools

        /// <summary>
        /// Cover Shooter covers on the props big enough to hide behind. Several props are one combined mesh
        /// (rubble, the helipad crates), so covers are made per connected piece of each prop mesh.
        /// </summary>
        private static void covers(Transform tower, Transform parent)
        {
            var level = levelModel(tower);
            if (level == null) return;
            int count = 0, pieces = 0;
            foreach (var mf in level.GetComponentsInChildren<MeshFilter>(true))
            {
                var n = mf.name;
                if (!(n.StartsWith("PROP_") || n.Contains("_PROP_") || n == "FINAL_Props_Cover") || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                foreach (var b in islands(mf))
                {
                    if (b.size.y < 0.75f || b.size.y > 2.4f) continue;
                    var wide = Mathf.Max(b.size.x, b.size.z);
                    if (wide < 0.9f || wide > 5f) continue;
                    count += VantageCoverUtil.AddForBounds(n, b, parent);
                    pieces++;
                }
            }
            Debug.Log($"[Vantage] Covers: {count} on {pieces} prop pieces.");
        }

        /// <summary>World bounds of each connected piece of a mesh.</summary>
        private static List<Bounds> islands(MeshFilter mf)
        {
            var v = mf.sharedMesh.vertices;
            var tri = mf.sharedMesh.triangles;
            var parent = Enumerable.Range(0, v.Length).ToArray();
            int find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            var weld = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var k = Vector3Int.RoundToInt(v[i] * 100);
                if (weld.TryGetValue(k, out var j)) parent[find(i)] = find(j); else weld[k] = i;
            }
            for (int i = 0; i < tri.Length; i += 3) { parent[find(tri[i])] = find(tri[i + 1]); parent[find(tri[i + 1])] = find(tri[i + 2]); }
            var result = new List<Bounds>();
            foreach (var g in Enumerable.Range(0, v.Length).GroupBy(find))
            {
                var first = true;
                var b = default(Bounds);
                foreach (var i in g)
                {
                    var p = mf.transform.TransformPoint(v[i]);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
                result.Add(b);
            }
            return result;
        }

        /// <summary>Player start 8 m out from the main entrance, on the ground, facing the doors.</summary>
        private static Vector3 playerStart(Transform tower)
        {
            var facing = EntranceFacing(tower);
            var entrance = EntrancePoint(tower) ?? tower.position;
            var world = entrance + facing * 8f;
            if (Physics.Raycast(world + Vector3.up * 20f, Vector3.down, out var hit, 40f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                world = hit.point;
            var look = Quaternion.LookRotation(-facing);
            foreach (var input in Object.FindObjectsByType<CoverShooter.ThirdPersonInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var character = input.GetComponent<CoverShooter.CharacterMotor>();
                if (character == null) continue;
                character.transform.SetPositionAndRotation(world + Vector3.up * 0.05f, look);
                var cam = character.transform.root.GetComponentInChildren<CoverShooter.ThirdPersonCamera>(true);
                if (cam != null)
                    cam.transform.SetPositionAndRotation(world + facing * 4f + Vector3.up * 2.2f, look);
            }
            Debug.Log($"[Vantage] Player start {world:F1}.");
            return world;
        }

        /// <summary>
        /// Pistol just outside the main entrance (the alarm trigger is right inside it, so the player is armed
        /// before the trap). The Armory gun (L2_PICKUP_Gun) becomes our rifle pickup.
        /// </summary>
        private static void pickups(Transform tower, Transform parent, Vector3 start)
        {
            var facing = EntranceFacing(tower);
            var entrance = EntrancePoint(tower) ?? start;
            var side = Vector3.Cross(Vector3.up, facing);
            var pistol = VantageSetup.AddPickupAt("Pistol", entrance + facing * 2.5f + side * 1.6f + Vector3.up * 1f, parent);

            var gun = Find(tower, "L2_PICKUP_Gun");
            if (gun != null)
            {
                foreach (var c in gun.GetComponents<Behaviour>()) { c.enabled = false; EditorUtility.SetDirty(c); }
                foreach (var c in gun.GetComponents<Collider>()) { c.enabled = false; EditorUtility.SetDirty(c); }
                var rifle = VantageSetup.AddPickupAt("Rifle", gun.position + Vector3.up * 0.3f, parent);
                Debug.Log($"[Vantage] Pickups: pistol at {pistol.transform.position:F1}, rifle at {rifle.transform.position:F1} (Armory).");
            }
            else
                Debug.LogWarning("[Vantage] No L2_PICKUP_Gun: no rifle pickup.");
        }

        /// <summary>Playtest logger and minimap point at the tower.</summary>
        private static void pointToolsAtTower(Transform t)
        {
            foreach (var logger in Object.FindObjectsByType<VantagePlaytestLogger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                logger.Tower = t;
            foreach (var map in Object.FindObjectsByType<VantageMinimap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                map.Tower = t;
        }

        #endregion

        #region Levels

        /// <summary>
        /// Doors between levels: the stairwell gate (key) also waits for level 2 to be cleared, and the exit to the
        /// helipad is locked until level 3 is cleared (it has no condition in the model).
        /// </summary>
        private static void levelDoors(Transform tower)
        {
            void rule(string name, int level, bool lockIt)
            {
                var t = Find(tower, name);
                var door = t != null ? t.GetComponent<VantageDoor>() : null;
                if (door == null) { Debug.LogWarning($"[Vantage] No door {name}."); return; }
                door.ClearLevel = level;
                if (lockIt) { door.locked = true; door.type = DoorType.Locked; }
                EditorUtility.SetDirty(door);
            }
            rule("L2_GATE_Stairwell_LOCKED", 1, false);
            rule("L3_DOOR_Exit_To_Final", 2, true);
        }

        /// <summary>The key glows and floats (VantageKeyItem); the slot shows the key turning and a lamp (VantageKeySlot).</summary>
        private static void keyAndSlot(Transform tower)
        {
            var key = Find(tower, "L2_PICKUP_Key");
            var slot = Find(tower, "L2_KEYSLOT_InsertKey");
            if (key == null || slot == null) { Debug.LogWarning("[Vantage] Key or key slot missing."); return; }
            if (key.GetComponent<VantageKeyItem>() == null) key.gameObject.AddComponent<VantageKeyItem>();
            if (key.GetComponentInChildren<Light>(true) == null)
            {
                var glow = new GameObject("Key Glow").AddComponent<Light>();
                glow.transform.SetParent(key, false);
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.85f, 0.4f);
                glow.range = 2.5f;
                glow.intensity = 1.5f;
                glow.shadows = LightShadows.None;
            }

            var keySlot = slot.GetComponent<VantageKeySlot>();
            if (keySlot == null) keySlot = slot.gameObject.AddComponent<VantageKeySlot>();
            var keyMesh = key.GetComponent<MeshFilter>();
            var keyRenderer = key.GetComponent<Renderer>();
            keySlot.KeyMesh = keyMesh != null ? keyMesh.sharedMesh : null;
            keySlot.KeyMaterial = keyRenderer != null ? keyRenderer.sharedMaterial : null;
            if (keySlot.Lamp == null)
            {
                WorldBounds(slot, out var sb);
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.name = "Key Slot Lamp";
                Object.DestroyImmediate(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(slot, true);
                lamp.transform.position = new Vector3(sb.center.x, sb.max.y + 0.08f, sb.center.z);
                lamp.transform.localScale = Vector3.one * 0.08f;
                lamp.GetComponent<Renderer>().sharedMaterial = lampMaterial();
                keySlot.Lamp = lamp.GetComponent<Renderer>();
                var light = new GameObject("Key Slot Light").AddComponent<Light>();
                light.transform.SetParent(lamp.transform, false);
                light.type = LightType.Point;
                light.range = 2f;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
                keySlot.LampLight = light;
            }
            EditorUtility.SetDirty(keySlot);
            Debug.Log("[Vantage] Key and key slot set up.");
        }

        /// <summary>
        /// The ground floor alarm: the beacon meshes get an emissive material, up to four red point lights go at
        /// beacons spread over the floor, and the siren sits in the middle of level 1 (inactive until the alarm).
        /// </summary>
        private static void alarm(Transform tower, Transform parent)
        {
            var go = new GameObject("Alarm");
            go.transform.SetParent(parent, false);
            var alarm = go.AddComponent<VantageAlarm>();
            var beacons = Find(tower, "LVL1_AlarmBeacons");
            var lights = new List<Light>();
            if (beacons != null && beacons.GetComponent<MeshFilter>() is MeshFilter mf && mf.sharedMesh != null && mf.sharedMesh.isReadable)
            {
                var renderer = beacons.GetComponent<Renderer>();
                renderer.sharedMaterial = beaconMaterial();
                alarm.Beacons = new[] { renderer };
                // Spread the lights: farthest-point picks over the beacon pieces.
                var centres = islands(mf).Select(b => b.center).ToList();
                var picked = new List<Vector3>();
                while (picked.Count < 4 && picked.Count < centres.Count)
                    picked.Add(picked.Count == 0 ? centres[0] : centres.OrderByDescending(c => picked.Min(p => (p - c).sqrMagnitude)).First());
                foreach (var p in picked)
                {
                    var l = new GameObject("Alarm Light").AddComponent<Light>();
                    l.transform.SetParent(go.transform, false);
                    l.transform.position = p + Vector3.down * 0.3f;
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.08f, 0.04f);
                    l.range = 9f;
                    l.shadows = LightShadows.None;
                    lights.Add(l);
                }
            }
            alarm.Lights = lights.ToArray();

            var siren = new GameObject("Siren");
            siren.transform.SetParent(go.transform, false);
            WorldBounds(Find(tower, "LVL1_Structure") ?? tower, out var floor);
            siren.transform.position = new Vector3(floor.center.x, LevelHeights(tower)[0] + 3f, floor.center.z);
            var audio = siren.AddComponent<VantageProceduralAudio>();
            audio.Sound = VantageProceduralAudio.Kind.Siren;
            audio.Volume = 0.45f;
            audio.MinDistance = 8f;
            audio.MaxDistance = 45f;
            siren.SetActive(false);
            alarm.Siren = siren;
            Debug.Log($"[Vantage] Alarm: {alarm.Lights.Length} red lights, beacons {(alarm.Beacons != null ? "on" : "missing")}, siren.");
        }

        private static Material beaconMaterial()
        {
            const string path = "Assets/Vantage/Materials/Alarm Beacon.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.35f, 0.03f, 0.02f));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.2f, 0.02f, 0.01f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// The security camera at MK_SecurityCamera_RoomA: a head (box and lens) that sweeps across Room A with a
        /// spot light as its view cone. Its collider lets bullets knock it out.
        /// </summary>
        private static void securityCamera(Transform tower, Transform parent)
        {
            var marker = Find(tower, "MK_SecurityCamera_RoomA");
            if (marker == null) { Debug.LogWarning("[Vantage] No MK_SecurityCamera_RoomA."); return; }
            // Looks into Room A: towards the middle of its enemy markers, tilted down.
            var room = tower.GetComponentsInChildren<VantageTag>(true).Where(t => t.name.StartsWith("MK_Enemy_A_")).Select(t => t.transform.position).ToList();
            var into = room.Count > 0 ? room.Aggregate(Vector3.zero, (a, p) => a + p) / room.Count - marker.position : -marker.forward;
            into.y = 0;

            var go = new GameObject("Security Camera");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(marker.position, Quaternion.LookRotation(into.normalized));
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.45f, 0.4f, 0.6f);
            collider.center = new Vector3(0, 0, 0.1f);

            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localRotation = Quaternion.Euler(20f, 0, 0);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(head, false);
            body.transform.localScale = new Vector3(0.22f, 0.18f, 0.42f);
            body.GetComponent<Renderer>().sharedMaterial = cameraMaterial();
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "Lens";
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.SetParent(head, false);
            lens.transform.localPosition = new Vector3(0, 0, 0.22f);
            lens.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            lens.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);
            lens.GetComponent<Renderer>().sharedMaterial = lampMaterial();
            var cone = new GameObject("View Cone").AddComponent<Light>();
            cone.transform.SetParent(head, false);
            cone.transform.localPosition = new Vector3(0, 0, 0.25f);
            cone.type = LightType.Spot;
            cone.spotAngle = 60f;
            cone.innerSpotAngle = 40f;
            cone.range = 12f;
            cone.intensity = 3f;
            cone.shadows = LightShadows.None;

            var cam = go.AddComponent<VantageSecurityCamera>();
            cam.Head = head;
            cam.Cone = cone;
            cam.Lens = lens.GetComponent<Renderer>();
            Debug.Log($"[Vantage] Security camera at {marker.position:F1}, looking {into.normalized:F2}.");
        }

        private static Material cameraMaterial()
        {
            const string path = "Assets/Vantage/Materials/Security Camera.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.14f));
            m.SetFloat("_Smoothness", 0.5f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material lampMaterial()
        {
            const string path = "Assets/Vantage/Materials/Key Slot Lamp.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// The level brain: four levels (ground, armory, destroyed floor, helipad) with their marker prefixes, floor
        /// heights, areas, restart points and difficulty, plus the enemy prefabs.
        /// </summary>
        private static VantageTowerLevels levelManager(Transform tower, Transform parent, List<Flight> flights, Vector3 start)
        {
            var go = new GameObject("Tower Levels");
            go.transform.SetParent(parent, false);
            var levels = go.AddComponent<VantageTowerLevels>();
            go.AddComponent<VantageLevelManager>();
            levels.Tower = tower;
            levels.SoldierPrefab = soldierPrefab();
            levels.DronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VantageEditorUtil.DronePrefabPath);
            levels.TurretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VantageEditorUtil.TurretPrefabPath);
            levels.HeavyRobotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeavyRobotPrefabPath);
            levels.BossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);

            // Areas: the tower's rooms (everything named LVL*), and the outside stairs, platform and helipad.
            var level = levelModel(tower);
            Bounds? area = null, finale = null;
            foreach (var r in level.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.name;
                if (n.StartsWith("LVL")) { if (area == null) area = r.bounds; else { var b = area.Value; b.Encapsulate(r.bounds); area = b; } }
                if (n == "FINAL_Platform" || n == "FINAL_Landing_Stairs" || n == "FINAL_Helipad" || n == "FINAL_Props_Cover")
                { if (finale == null) finale = r.bounds; else { var b = finale.Value; b.Encapsulate(r.bounds); finale = b; } }
            }
            var h = LevelHeights(tower);
            levels.TowerArea = area ?? default;
            if (finale != null)
            {
                var f = finale.Value;
                f.SetMinMax(new Vector3(f.min.x - 0.3f, h[2] - 0.5f, f.min.z - 0.3f), new Vector3(f.max.x + 0.3f, f.max.y + 5f, f.max.z + 0.3f));
                levels.FinaleArea = f;
            }

            // Restart points: the player start, the top of each level's stairs, outside the exit to the helipad.
            var arrivals = group(parent, "Arrivals");
            Transform arrival(string name, Vector3 at, Vector3 facing)
            {
                var a = new GameObject(name).transform;
                a.SetParent(arrivals, false);
                if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var hit, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    at = hit.point + Vector3.up * 0.05f;
                facing.y = 0;
                a.SetPositionAndRotation(at, Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.forward));
                return a;
            }
            Transform topOf(string name, string stairs)
            {
                var f = flights.Where(x => x.name.StartsWith(stairs)).OrderByDescending(x => x.top.y).FirstOrDefault();
                var dir = tower.TransformDirection((f.top - f.bottom).normalized);
                return arrival(name, tower.TransformPoint(f.top) + dir * 1.6f, dir);
            }
            var startLook = (EntrancePoint(tower) ?? tower.position) - start;
            var exit = Find(tower, "L3_DOOR_Exit_To_Final");
            var landing = Find(tower, "FINAL_Landing_Stairs");
            Transform finaleArrival = null;
            if (exit != null && landing != null && WorldBounds(exit, out var eb) && WorldBounds(landing, out var lb))
            {
                var outward = lb.center - eb.center; outward.y = 0;
                outward = Mathf.Abs(outward.x) > Mathf.Abs(outward.z) ? new Vector3(Mathf.Sign(outward.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(outward.z));
                finaleArrival = arrival("Arrival Helipad", new Vector3(eb.center.x, eb.min.y, eb.center.z) + outward * 1.2f, outward);
            }

            levels.Levels = new List<VantageTowerLevels.Level>
            {
                new VantageTowerLevels.Level { Name = "Ground floor", Prefix = "MK_", FloorY = h[0], Soldiers = 6, Drones = 0, SoldierWeapon = "Pistol",
                    SoldierDamage = 8f, SoldierHealth = 0.8f, ClearedMessage = "LOCKDOWN LIFTED  ·  STAIRS OPEN",
                    Arrival = arrival("Arrival Ground floor", start, startLook) },
                new VantageTowerLevels.Level { Name = "Armory", Prefix = "L2_", FloorY = h[1], Soldiers = 6, Drones = 3, SoldierWeapon = "Pistol",
                    SoldierDamage = 10f, SoldierHealth = 1f, DroneAccuracy = 0.4f, DroneFireInterval = 1.5f, ClearedMessage = "ARMORY CLEAR",
                    Arrival = topOf("Arrival Armory", "LVL1") },
                new VantageTowerLevels.Level { Name = "Destroyed floor", Prefix = "L3_", FloorY = h[2], Soldiers = 5, Drones = 0,
                    SoldierDamage = 12f, SoldierHealth = 1.15f, ClearedMessage = "THE WAY TO THE HELIPAD IS OPEN",
                    Arrival = topOf("Arrival Destroyed floor", "LVL2") },
                new VantageTowerLevels.Level { Name = "Helipad", Prefix = "FINAL_", FloorY = h[3], Soldiers = 2, Drones = 0,
                    SoldierDamage = 12f, SoldierHealth = 1.25f, ClearedMessage = "REACH THE HELIPAD",
                    Arrival = finaleArrival },
            };
            Debug.Log($"[Vantage] Levels: {string.Join(", ", levels.Levels.Select(l => $"{l.Name} y {l.FloorY:F2} arrival {(l.Arrival != null ? l.Arrival.position.ToString("F1") : "none")}"))}; tower area {levels.TowerArea.size:F1}, finale area {levels.FinaleArea.size:F1}.");
            return levels;
        }

        public const string SoldierPrefabPath = "Assets/Vantage/Prefabs/Soldier.prefab";
        public const string HeavyRobotPrefabPath = "Assets/Vantage/Prefabs/HeavyRobot.prefab";
        public const string BossPrefabPath = "Assets/Vantage/Prefabs/Boss.prefab";
        private const string TemplatePrefabs = "Assets/ThirdPersonCoverShooter/Assets/Prefabs/";

        /// <summary>
        /// Our soldier: a variant of the template soldier that can actually fight. The template prefab still keeps
        /// its guns in CharacterMotor's old Weapons list, but this version of the template's AI arms itself from a
        /// CharacterInventory component, which the prefab never got, so every soldier spawned unarmed and the
        /// fighter brain fell back to running away. The variant gets the inventory, starts with its first gun in
        /// hand, and carries the aggression tuning.
        /// </summary>
        private static GameObject soldierPrefab()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabs + "Soldier.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(template);
            try
            {
                var motor = go.GetComponent<CoverShooter.CharacterMotor>();
                var inventory = go.GetComponent<CoverShooter.CharacterInventory>();
                if (inventory == null) inventory = go.AddComponent<CoverShooter.CharacterInventory>();
#pragma warning disable 0618 // the template's deprecated weapon list is exactly what we migrate
                inventory.Weapons = (motor.Weapons ?? new CoverShooter.WeaponDescription[0]).Where(w => w.RightItem != null).ToArray();
#pragma warning restore 0618
                var first = inventory.Weapons.FirstOrDefault(w => w.Gun != null);
                motor.Weapon = first;
                motor.IsEquipped = first.Gun != null;
                // The template only aims (and so only fires) while the character is on screen; ours must be able
                // to shoot from outside the camera view too.
                motor.AlwaysUpdateIK = true;
                foreach (var animator in go.GetComponentsInChildren<Animator>(true))
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (go.GetComponent<VantageEnemyAggression>() == null) go.AddComponent<VantageEnemyAggression>();
                if (go.GetComponent<VantageKillReporter>() == null) go.AddComponent<VantageKillReporter>().EnemyName = "Soldier";
                var saved = PrefabUtility.SaveAsPrefabAsset(go, SoldierPrefabPath);
                Debug.Log($"[Vantage] Soldier prefab: {inventory.Weapons.Length} weapons ({string.Join(", ", inventory.Weapons.Select(w => w.RightItem.name))}).");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        #endregion

        /// <summary>The ending: the helicopter lands on the middle of the helipad, flying in from the side away from the tower.</summary>
        private static void extraction(Transform tower, Transform parent, VantageTowerLevels levels)
        {
            var pad = Find(tower, "FINAL_Helipad");
            if (pad == null || !WorldBounds(pad, out var pb)) { Debug.LogWarning("[Vantage] No FINAL_Helipad: no extraction."); return; }
            var go = new GameObject("Extraction");
            go.transform.SetParent(parent, false);
            var landing = new GameObject("Landing Point").transform;
            landing.SetParent(go.transform, false);
            landing.position = new Vector3(pb.center.x, pb.max.y, pb.center.z);
            var e = go.AddComponent<VantageExtraction>();
            e.Pad = landing;
            e.HelicopterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VantagePlaceholders.HelicopterPrefabPath);
            var away = landing.position - levels.TowerArea.center; away.y = 0;
            e.ApproachFrom = away.normalized;
            Debug.Log($"[Vantage] Extraction: lands at {landing.position:F1}, comes from {e.ApproachFrom:F2}.");
        }

        #region Coins

        private const string CoinPrefab = "Assets/PolyKebap/Stylized Coin Pack/Prefabs/Gold Coins/Coin_Gold_Crown.prefab";

        /// <summary>
        /// Five gold coins, each in a space the main route doesn't pass through, so finding one rewards looking
        /// around: the north-west dead end of the ground floor, the far end of the office and the south-west corner
        /// of the hall on level 2, the broken slab outside on level 3 (patrol ground), the far edge of the helipad.
        /// Each goes on the walkable floor beside its anchor object, on the side away from the tower's middle.
        /// </summary>
        private static void coins(Transform tower, Transform parent, VantageTowerLevels levels)
        {
            var spots = new (string id, string anchor)[]
            {
                ("coin_L1_NorthWest", "PROP_Crate_Top1"),
                ("coin_L2_Office", "L2_PROP_Shelf_Office"),
                ("coin_L2_HallCorner", "L2_PROP_Crate_H1"),
                ("coin_L3_BrokenSlab", "LVL3_BrokenSlab_Joint"),
                ("coin_Helipad", "FINAL_Helipad"),
            };
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
            var middle = levels.TowerArea.center;
            var placed = new List<string>();
            foreach (var (id, anchorName) in spots)
            {
                var anchor = Find(tower, anchorName);
                if (anchor == null || !WorldBounds(anchor, out var b)) { Debug.LogWarning($"[Vantage] Coin {id}: no {anchorName}."); continue; }
                var floor = b.min.y;
                Vector3? best = null;
                var bestScore = float.MinValue;
                var reach = Mathf.Max(b.extents.x, b.extents.z);
                for (float r = Mathf.Min(reach, 3f) + 0.6f; r <= Mathf.Min(reach, 3f) + 1.6f; r += 0.5f)
                    for (int a = 0; a < 16; a++)
                    {
                        var p = new Vector3(b.center.x, floor, b.center.z) + Quaternion.Euler(0, a * 22.5f, 0) * Vector3.forward * r;
                        if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 0.4f, UnityEngine.AI.NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floor) > 0.6f) continue;
                        // Same room as the anchor: nothing solid (a wall) between them, the anchor itself aside.
                        var from = new Vector3(b.center.x, floor + 0.5f, b.center.z);
                        var to = hit.position + Vector3.up * 0.5f;
                        var blocked = false;
                        foreach (var h in Physics.RaycastAll(from, to - from, (to - from).magnitude, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                            if (!h.collider.transform.IsChildOf(anchor)) { blocked = true; break; }
                        if (blocked) continue;
                        var score = new Vector2(hit.position.x - middle.x, hit.position.z - middle.z).magnitude;
                        if (score > bestScore) { bestScore = score; best = hit.position; }
                    }
                if (best == null) { Debug.LogWarning($"[Vantage] Coin {id}: no walkable floor beside {anchorName}."); continue; }

                var go = new GameObject("Coin " + id);
                go.transform.SetParent(parent, false);
                go.transform.position = best.Value + Vector3.up * 0.45f;
                go.AddComponent<SphereCollider>().radius = 0.7f;
                var coin = go.AddComponent<VantageCoin>();
                coin.Id = id;
                if (prefab != null)
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                    model.transform.localPosition = Vector3.zero;
                    foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    if (WorldBounds(model.transform, out var mb))
                        model.transform.localScale *= 0.35f / Mathf.Max(0.01f, Mathf.Max(mb.size.x, mb.size.y, mb.size.z));
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var glow = new GameObject("Coin Glow").AddComponent<Light>();
                glow.transform.SetParent(go.transform, false);
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.8f, 0.35f);
                glow.range = 1.8f;
                glow.intensity = 1.2f;
                glow.shadows = LightShadows.None;
                placed.Add($"{id} at {go.transform.position:F1} (level {levels.LevelAt(go.transform.position) + 1})");
            }
            Debug.Log($"[Vantage] Coins: {placed.Count} ({string.Join("; ", placed)}).");
        }

        #endregion

        #region Checks

        /// <summary>
        /// Every door and trigger named in the Blender properties must exist: "unlocks" and "close:" name doors,
        /// "unlock_by" names a console (or a KILL list of enemy markers).
        /// </summary>
        private static void checkDoors(Transform tower)
        {
            var doors = new HashSet<string>(tower.GetComponentsInChildren<VantageDoor>(true).Select(d => d.name));
            var interacts = new HashSet<string>(tower.GetComponentsInChildren<VantageInteract>(true).Select(i => i.name));
            var markers = tower.GetComponentsInChildren<VantageTag>(true).Where(t => t.MarkerType == "EnemySpawn" || t.MarkerType == "Turret" || t.MarkerType == "HeavyEnemy").Select(t => t.name).ToList();
            int ok = 0, fail = 0;
            void check(string owner, string what, bool good)
            {
                if (good) ok++; else fail++;
                Debug.Log($"[Vantage] Check: {owner} -> {what} {(good ? "ok" : "FAIL")}");
            }
            foreach (var tag in tower.GetComponentsInChildren<VantageTag>(true))
            {
                var unlocks = tag.Get("unlocks");
                if (!string.IsNullOrEmpty(unlocks)) check(tag.name, "door " + unlocks, doors.Contains(unlocks));
                var by = tag.Get("unlock_by") ?? tag.Get("open_by");
                if (string.IsNullOrEmpty(by)) continue;
                if (by.StartsWith("KILL:"))
                    foreach (var k in by.Substring(5).Split(','))
                    {
                        var key = k.Trim();
                        check(tag.name, "kill " + key, key.EndsWith("*") ? markers.Any(m => m.StartsWith(key.TrimEnd('*'))) : markers.Contains(key));
                    }
                else
                    check(tag.name, "console " + by, interacts.Contains(by));
            }
            foreach (var trigger in tower.GetComponentsInChildren<VantageTrigger>(true))
                if (trigger.action != null && trigger.action.StartsWith("close:"))
                    check(trigger.name, "door " + trigger.action.Substring(6), doors.Contains(trigger.action.Substring(6)));
            Debug.Log($"[Vantage] Door check: {ok} ok, {fail} FAIL.");
        }

        #endregion

        #region Report

        /// <summary>
        /// -executeMethod Vantage.EditorTools.VantageTower.ReportBatch
        /// Logs every object of the tower (path, position, size, Blender properties) and the level heights. Read-only.
        /// </summary>
        public static void ReportBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                var tower = FindTower();
                var placedHere = tower == null;
                if (placedHere)
                    tower = PlaceTower();   // only in memory: the scene is not saved
                Report(tower.transform);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        public static void Report(Transform tower)
        {
            var sb = new StringBuilder();
            var count = 0;
            foreach (var t in tower.GetComponentsInChildren<Transform>(true))
            {
                if (t == tower) continue;
                var path = t.name;
                for (var p = t.parent; p != null && p != tower; p = p.parent) path = p.name + "/" + path;
                var size = WorldBounds(t, out var b) ? $"{b.size:F1} min {b.min:F1}" : "-";
                var tag = t.GetComponent<VantageTag>();
                var props = tag != null ? string.Join(";", tag.keys.Select((k, i) => k + "=" + tag.values[i])) : "";
                sb.AppendLine($"[Vantage] Node {path} pos {t.position:F2} size {size} tags {props}");
                count++;
            }
            Debug.Log(sb.ToString());
            var h = LevelHeights(tower);
            Debug.Log("[Vantage] Level heights: " + string.Join(", ", FloorObjects.Select((n, i) => $"{n.name} {h[i].ToString("F2", CultureInfo.InvariantCulture)}")) + $" ({count} nodes).");
        }

        #endregion
    }
}
