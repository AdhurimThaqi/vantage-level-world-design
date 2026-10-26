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
            var start = playerStart(tower);
            pickups(tower, setup, start);
            keyAndSlot(tower);
            alarm(tower, setup);
            securityCamera(tower, setup);
            VantagePlaceholders.Build(false);
            VantageEnemyModels.Boss();
            var levels = levelManager(tower, setup, flights, start);
            extraction(tower, setup, levels);
            boundary(tower, setup);
            intro(tower, setup);
            pointToolsAtTower(tower);
            checkDoors(tower);

            VantageSetup.BakeNavMesh();
            markBehindExit(tower, levels, gates);
            covers(tower, group(setup, "Covers"));
            coins(tower, group(setup, "Coins"), levels);
            sentries(tower, setup, levels);
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
        public struct Flight
        {
            public Vector3 bottom, top; public float width; public string name;
            /// <summary>Side (-1/+1 across the flight) of its side steps, 0 when it is entered from the front.</summary>
            public float entrySide;
            /// <summary>Distance along the flight where its landing (side steps) ends: the handrail starts there.</summary>
            public float entryEnd;
        }

        /// <summary>
        /// The template character can't step up, so every flight of the tower's stairs (LVL1_Stairs,
        /// LVL2_Stairs_To_L3, FINAL_Landing_Stairs) gets an invisible walking surface: the slope-limited upper
        /// envelope of the real steps along the flight (never below a step edge, at most ~31°), meeting floors and
        /// landings flush at both ends. The flights are found
        /// from a height map of the steps; a landing between two flights is flat and belongs to neither. The surface
        /// stays close to the steps' own slope, so it meets the floor flush and doesn't float above the treads.
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
                    var built = buildSurface(tower, parent, f, result.Count);
                    blockForEnemies(tower, parent, built);
                    result.Add(built);
                }
                Debug.Log($"[Vantage] {stairs.name}: {found.Count} flights ({string.Join(", ", found.Select(f => $"{f.bottom.y:F2}->{f.top.y:F2} m, {f.width:F1} m wide"))}).");
            }
            handrails(tower, parent, result);
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

                    // The gradient test misses the flight's sides (its window reaches off the steps), so the real
                    // width is measured: across the flight at several points, as far as the steps keep the flight's
                    // height (a wall or the divider between two flights stops it). Medians of both sides.
                    float heightOf(float along, float across)
                    {
                        float x = alongX ? along : across, z = alongX ? across : along;
                        int a = Mathf.RoundToInt((x - lb.min.x) / cell), b = Mathf.RoundToInt((z - lb.min.z) / cell);
                        return a < 0 || b < 0 || a >= nx || b >= nz ? float.NaN : h[a, b];
                    }
                    var lefts = new List<float>();
                    var rights = new List<float>();
                    for (float along = amin + 0.2f; along <= amax - 0.2f; along += 0.3f)
                    {
                        var expected = Mathf.Lerp(hmin, hmax, Mathf.InverseLerp(low, high, along));
                        bool onFlight(float across) { var y = heightOf(along, across); return !float.IsNaN(y) && Mathf.Abs(y - expected) < 0.3f; }
                        if (!onFlight(mid)) continue;
                        float l = mid, r = mid;
                        while (l - cell > mid - 1.6f && onFlight(l - cell)) l -= cell;
                        while (r + cell < mid + 1.6f && onFlight(r + cell)) r += cell;
                        lefts.Add(l);
                        rights.Add(r);
                    }
                    if (lefts.Count > 0)
                    {
                        lefts.Sort();
                        rights.Sort();
                        cmin = lefts[lefts.Count / 2];
                        cmax = rights[rights.Count / 2];
                        mid = (cmin + cmax) / 2;
                    }
                    Vector3 at(float along, float y) => alongX ? new Vector3(along, y, mid) : new Vector3(mid, y, along);
                    flights.Add(new Flight { bottom = at(low, hmin), top = at(high, hmax), width = cmax - cmin + cell, name = stairs.name });
                }
            return flights;
        }

        /// <summary>
        /// Enemies stay on their level: the NavMesh runs over the real steps, so a hunting soldier could walk onto the
        /// stairs (and was then teleported back by the safety net). A carving NavMesh obstacle over the flight's
        /// steps takes them out of the AI's paths; it doesn't block the player.
        /// </summary>
        private static void blockForEnemies(Transform t, Transform parent, Flight piece)
        {
            var flat = piece.top - piece.bottom; flat.y = 0;
            var dir = flat.normalized;
            var go = new GameObject($"Stair Block {piece.name} {piece.bottom.y:F2}");
            go.transform.SetParent(parent, false);
            var height = piece.top.y - piece.bottom.y + 2f;
            go.transform.SetPositionAndRotation(t.TransformPoint((piece.bottom + piece.top) / 2 + Vector3.up * 0.5f), t.rotation * Quaternion.LookRotation(dir));
            var obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(piece.width + 0.4f, height, flat.magnitude + 0.4f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        /// <summary>The walking surface over one flight (see smoothStairs).</summary>
        private static Flight buildSurface(Transform t, Transform parent, Flight piece, int index)
        {
            // Up to ~31°: the character slows down above 26° and stops at 60° (CharacterMotor.MinSlope/MaxSlope),
            // but at 35° it stalled near the top now and then. 25.6° made the surface start in mid-air before the
            // steep (34°) flights, a 23 cm ledge at the foot, and float half a metre above the treads; 31° starts
            // flush with the floor within the 1.4 m run-out and stays within ~15 cm of the step edges.
            const float slopeCap = 0.6f;
            const float step = 0.1f, extend = 1.4f;
            var mask = VantagePhysics.Solid;
            var flat = piece.top - piece.bottom; flat.y = 0;
            var run = flat.magnitude;
            var dir = flat.normalized;
            var side = Vector3.Cross(Vector3.up, dir);
            var rise = piece.top.y - piece.bottom.y;
            var maxSlope = Mathf.Clamp(rise / run * 1.15f, 0.12f, slopeCap);
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
            var entry = sideSteps(t, parent, piece, index, dir, side, run);
            if (entry.HasValue) { piece.entrySide = entry.Value.side; piece.entryEnd = entry.Value.end; }
            return piece;
        }

        /// <summary>
        /// A flight whose foot is against a wall can only be entered from the side (L2→L3: its foot is against the
        /// Armory wall). It gets a landing over its lowest steps, up to the first tread LandingHeight above the floor,
        /// and real side steps from the room up to it, in the steps' own material. The character can't step up, so
        /// the side steps are walked on an invisible ramp through their nosings (no steeper than ~30°) and the landing
        /// on its box. Returns the side and where the landing ends along the flight (the handrail starts there).
        /// </summary>
        private static (float side, float end)? sideSteps(Transform t, Transform parent, Flight piece, int index, Vector3 dir, Vector3 side, float run)
        {
            const float radius = 0.3f, landingHeight = 0.8f, maxSlope = 0.58f, riser = 0.19f;
            bool free(Vector3 local)
            {
                var w = t.TransformPoint(local);
                return !Physics.CheckCapsule(w + Vector3.up * (radius + 0.15f), w + Vector3.up * 1.6f, radius, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
            }
            var floorY = piece.bottom.y - 0.4f;
            var foot = piece.bottom - dir * 0.7f; foot.y = floorY;
            // Find the floor height in front of the foot; skip if the front is open.
            if (Physics.Raycast(t.TransformPoint(foot + Vector3.up * 1.5f), Vector3.down, out var fh, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                foot.y = t.InverseTransformPoint(fh.point).y;
            if (free(foot)) return null;

            var steps = new HashSet<Collider>(levelModel(t).GetComponentsInChildren<Collider>(true).Where(c => c.name == piece.name));
            // Height of the real steps on the flight's centre line (its own colliders only).
            float treadAt(float k)
            {
                var y = float.NaN;
                foreach (var h in Physics.RaycastAll(t.TransformPoint(piece.bottom + dir * k + Vector3.up * 2.5f), Vector3.down, 4f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    if (steps.Contains(h.collider) && h.normal.y > 0.7f)
                    {
                        var ly = t.InverseTransformPoint(h.point).y;
                        if (float.IsNaN(y) || ly > y) y = ly;
                    }
                return y;
            }
            // Where the wall at the foot is (the landing reaches it).
            var wallK = -0.3f;
            foreach (var h in Physics.RaycastAll(t.TransformPoint(piece.bottom + dir * 0.3f + Vector3.up * 0.6f), t.TransformDirection(-dir), 1.5f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                if (!steps.Contains(h.collider) && !h.collider.transform.IsChildOf(parent))
                    wallK = Mathf.Max(wallK, 0.3f - h.distance);

            foreach (var s in new[] { -1f, 1f })
            {
                // The floor beside the first step on this side must be open.
                var beside = piece.bottom + dir * 0.5f + side * s * (piece.width / 2 + 0.6f);
                if (!Physics.Raycast(t.TransformPoint(beside + Vector3.up * 1.5f), Vector3.down, out var bh, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)) continue;
                beside = t.InverseTransformPoint(bh.point);
                if (beside.y > piece.bottom.y + 0.1f || !free(beside)) continue;
                var floor = beside.y;

                // The landing's top: the first tread at least landingHeight above the floor, to the end of that tread.
                float top = float.NaN, end = float.NaN;
                for (float k = 0f; k < run; k += 0.03f)
                {
                    var y = treadAt(k);
                    if (float.IsNaN(y)) continue;
                    if (float.IsNaN(top)) { if (y - floor >= landingHeight - 0.05f) { top = y; end = k; } continue; }
                    if (Mathf.Abs(y - top) > 0.03f) break;
                    end = k;
                }
                if (float.IsNaN(top)) continue;
                var height = top - floor;
                var count = Mathf.Max(2, Mathf.RoundToInt(height / riser));
                var rise = height / count;
                var depth = Mathf.Max(0.3f, rise / maxSlope);
                var edge = s * piece.width / 2f;
                Vector3 at(float k, float across, float y) { var p = piece.bottom + dir * k + side * across; p.y = y; return t.TransformPoint(p); }

                // The landing over the lowest steps, then the side steps down to the floor (k, across, y ranges).
                var boxes = new List<(Vector3 min, Vector3 max)> { (new Vector3(wallK, -piece.width / 2f, floor), new Vector3(end, piece.width / 2f, top)) };
                for (int i = 1; i < count; i++)
                {
                    float c0 = edge + s * (count - 1 - i) * depth, c1 = edge + s * (count - i) * depth;
                    boxes.Add((new Vector3(wallK, Mathf.Min(c0, c1), floor), new Vector3(end, Mathf.Max(c0, c1), floor + i * rise)));
                }
                var go = new GameObject($"Stair Side Steps {piece.name} {piece.bottom.y:F2}");
                go.transform.SetParent(parent, false);
                var shape = new BoxMesh();
                foreach (var (min, max) in boxes)
                {
                    var o = at(min.x, min.y, min.z);
                    shape.Add(o, at(max.x, min.y, min.z) - o, at(min.x, max.y, min.z) - o, at(min.x, min.y, max.z) - o);
                }
                var mesh = shape.Build(go.name, go.transform);
                saveMesh(mesh, $"Stair Side Steps {index}");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var look = steps.Select(c => c.GetComponent<Renderer>()).FirstOrDefault(r => r != null);
                go.AddComponent<MeshRenderer>().sharedMaterial = look != null ? look.sharedMaterials[0] : null;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

                // Walking: the landing's box, and a ramp through the side steps' nosings.
                var landing = new GameObject("Landing");
                landing.transform.SetParent(go.transform, false);
                landing.transform.SetPositionAndRotation(at((wallK + end) / 2f, 0f, (floor + top) / 2f), t.rotation * Quaternion.LookRotation(dir));
                landing.AddComponent<BoxCollider>().size = new Vector3(piece.width, height, end - wallK);
                var reach = count * depth;
                var ramp = new GameObject("Ramp");
                ramp.transform.SetParent(go.transform, false);
                var corners = new[] { at(wallK, edge, top + 0.01f), at(end, edge, top + 0.01f), at(wallK, edge + s * reach, floor + 0.01f), at(end, edge + s * reach, floor + 0.01f) };
                var rampMesh = new Mesh { name = go.name + " Ramp" };
                rampMesh.SetVertices(corners.Select(ramp.transform.InverseTransformPoint).ToList());
                // Faces up whichever way the corners turn (a mesh collider is one-sided for rays).
                var up = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]).y >= 0;
                rampMesh.SetTriangles(up ? new[] { 0, 1, 2, 2, 1, 3 } : new[] { 0, 2, 1, 2, 3, 1 }, 0);
                rampMesh.RecalculateNormals();
                rampMesh.RecalculateBounds();
                saveMesh(rampMesh, $"Stair Side Steps {index} Ramp");
                ramp.AddComponent<MeshCollider>().sharedMesh = rampMesh;
                Debug.Log($"[Vantage] {piece.name} {piece.bottom.y:F2}-{piece.top.y:F2}: foot against a wall: landing {height:F2} m high to {end:F2} m along the flight, " +
                          $"{count - 1} side steps on the {(s > 0 ? "right" : "left")} ({rise:F2} m rise, {depth:F2} m deep, ramp {Mathf.Atan2(height, reach) * Mathf.Rad2Deg:F0}°).");
                return (s, end);
            }
            return null;
        }

        /// <summary>
        /// Handrails on every open side of every flight, from its foot (or the end of its landing) to its head: the
        /// model's own rails stop part way (the outside stairs to the helipad: a single bar from half way up, no
        /// posts). Where the model has a railing (rail tops above the steps at that edge, within 0.8 m) nothing is
        /// added; each gap gets a railing like it: the top rail on the model's line and height (a lone bar becomes its
        /// top rail), posts at least every 1.1 m, a mid rail, the nearest model rail's material and a thin collider.
        /// A side counts as open when nothing but a rail is beside it.
        /// </summary>
        private static void handrails(Transform t, Transform parent, List<Flight> flights)
        {
            const float sample = 0.25f, reachAlong = 0.8f, minGap = 0.6f, postSpacing = 1.1f, bar = 0.05f;
            var model = levelModel(t);
            var railSources = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && (f.name.Contains("Stairs") || f.name.Contains("Rail"))).ToList();
            var railPoints = railSources.SelectMany(f => f.sharedMesh.vertices.Select(v => t.InverseTransformPoint(f.transform.TransformPoint(v)))).ToList();
            var railLooks = model.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains("Rail")).ToList();
            var group = new GameObject("Handrails").transform;
            group.SetParent(parent, false);
            // Rail meshes are named by their stretch: last run's go first.
            foreach (var guid in AssetDatabase.FindAssets("Handrail_ t:Mesh", new[] { "Assets/Vantage/World/Tower" }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            var report = new List<string>();
            foreach (var f in flights)
            {
                var flat = f.top - f.bottom; flat.y = 0;
                var run = flat.magnitude;
                var dir = flat.normalized;
                var side = Vector3.Cross(Vector3.up, dir);
                float nosing(float k) => f.bottom.y + Mathf.Clamp01(k / run) * (f.top.y - f.bottom.y);
                foreach (var s in new[] { -1f, 1f })
                {
                    // The model's rail at this edge: its points above the steps, as (along, across, height above the steps).
                    var near = railPoints.Select(p => (p, d: p - f.bottom)).Select(x => (k: Vector3.Dot(x.d, dir), c: Vector3.Dot(x.d, side * s), h: x.p.y - nosing(Vector3.Dot(x.d, dir))))
                                         .Where(x => x.k > -0.5f && x.k < run + 0.5f && x.c > f.width / 2f - 0.4f && x.c < f.width / 2f + 0.35f && x.h > 0.15f && x.h < 1.6f).ToList();
                    var tops = near.Where(x => x.h > 0.75f).GroupBy(x => Mathf.RoundToInt(x.k / sample)).Select(g => g.OrderByDescending(x => x.h).First()).ToList();
                    var height = tops.Count > 0 ? Mathf.Clamp(tops.Select(x => x.h).OrderBy(h => h).ElementAt(tops.Count / 2), 0.85f, 1.15f) : 0.95f;
                    var across = tops.Count > 0 ? tops.Select(x => x.c).OrderBy(c => c).ElementAt(tops.Count / 2) : f.width / 2f - 0.04f;

                    bool open(float k)
                    {
                        if (s == f.entrySide && k < f.entryEnd) return false;
                        // The model's railing: rail tops (posts, rail ends) within reach. Its posts stand at most 1.5 m
                        // apart, so a railing covers its whole length; a lone bar only covers its two ends.
                        if (tops.Any(x => Mathf.Abs(x.k - k) < reachAlong)) return false;
                        var from = f.bottom + dir * k; from.y = nosing(k) + 0.9f;
                        foreach (var h in Physics.RaycastAll(t.TransformPoint(from), t.TransformDirection(side * s), f.width / 2f + 0.4f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                            if (!h.collider.transform.IsChildOf(parent) && !h.collider.name.Contains("Rail")) return false;
                        return true;
                    }
                    var count = Mathf.Max(1, Mathf.CeilToInt(run / sample));
                    var gaps = new bool[count + 1];
                    for (int i = 0; i <= count; i++) gaps[i] = open(Mathf.Min(i * sample, run));
                    for (int i = 0; i <= count; i++)
                    {
                        if (!gaps[i]) continue;
                        var j = i;
                        while (j < count && gaps[j + 1]) j++;
                        float k0 = i * sample, k1 = Mathf.Min(j * sample, run);
                        if (k1 - k0 >= minGap)
                        {
                            // Up to the neighbouring rail or landing at either end.
                            var from = i == 0 ? 0f : Mathf.Max(k0 - sample, s == f.entrySide ? f.entryEnd : 0f);
                            var to = j == count ? run : k1 + sample;
                            buildRail(t, group, f, dir, side * s, from, to, across, height, nosing, postSpacing, bar, railLooks);
                            report.Add($"{f.name} {f.bottom.y:F2}-{f.top.y:F2} {(s > 0 ? "right" : "left")} {from:F1}-{to:F1} m");
                        }
                        i = j;
                    }
                }
            }
            Debug.Log($"[Vantage] Handrails: {report.Count} added ({string.Join("; ", report)}).");
        }

        /// <summary>One handrail along a flight from k0 to k1: posts, a top rail and a mid rail following the steps, and a collider.</summary>
        private static void buildRail(Transform t, Transform group, Flight f, Vector3 dir, Vector3 outward, float k0, float k1, float across, float height,
                                      Func<float, float> nosing, float postSpacing, float bar, List<Renderer> looks)
        {
            Vector3 at(float k, float above) { var p = f.bottom + dir * k + outward * across; p.y = nosing(k) + above; return t.TransformPoint(p); }
            var go = new GameObject($"Handrail {f.name} {f.bottom.y:F2} {k0:F1}-{k1:F1}");
            go.transform.SetParent(group, false);
            var shape = new BoxMesh();
            var wDir = t.TransformDirection(dir);
            var wOut = t.TransformDirection(outward);
            var posts = Mathf.Max(1, Mathf.CeilToInt((k1 - k0) / postSpacing));
            for (int i = 0; i <= posts; i++)
            {
                var k = Mathf.Lerp(k0, k1, (float)i / posts);
                var foot = at(k, -0.02f) - (wDir + wOut) * bar / 2f;
                shape.Add(foot, wDir * bar, wOut * bar, Vector3.up * (height + 0.02f));
            }
            foreach (var above in new[] { height, height * 0.5f })
            {
                var from = at(k0, above - bar) - wOut * bar / 2f;
                shape.Add(from, at(k1, above - bar) - at(k0, above - bar), wOut * bar, Vector3.up * bar);
            }
            var mesh = shape.Build(go.name, go.transform);
            saveMesh(mesh, go.name.Replace(" ", "_"));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var centre = at((k0 + k1) / 2f, height / 2f);
            var look = looks.OrderBy(r => (r.bounds.ClosestPoint(centre) - centre).sqrMagnitude).FirstOrDefault();
            go.AddComponent<MeshRenderer>().sharedMaterial = look != null ? look.sharedMaterial : null;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            // Collider: a thin slab from the steps up to the rail, along the slope.
            var block = new GameObject("Collider");
            block.transform.SetParent(go.transform, false);
            var along = at(k1, height / 2f) - at(k0, height / 2f);
            block.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(along, Vector3.up));
            block.AddComponent<BoxCollider>().size = new Vector3(bar * 1.6f, height, along.magnitude);
        }

        /// <summary>
        /// Box meshes with flat faces and UVs in metres (tiling materials keep their scale). A box is a corner and
        /// its three edges from that corner (world space).
        /// </summary>
        private class BoxMesh
        {
            private readonly List<Vector3> _vertices = new List<Vector3>(), _normals = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            public void Add(Vector3 o, Vector3 a, Vector3 b, Vector3 c)
            {
                var far = o + a + b + c;
                var centre = o + (a + b + c) / 2f;
                face(o, a, b, centre); face(o, b, c, centre); face(o, c, a, centre);
                face(far, -a, -b, centre); face(far, -b, -c, centre); face(far, -c, -a, centre);
            }

            private void face(Vector3 start, Vector3 u, Vector3 v, Vector3 centre)
            {
                var normal = Vector3.Cross(u, v).normalized;
                var flip = Vector3.Dot(normal, start + (u + v) / 2f - centre) < 0f;
                if (flip) normal = -normal;
                var tu = u.normalized;
                var tv = Vector3.Cross(normal, tu);
                var first = _vertices.Count;
                foreach (var q in new[] { start, start + u, start + u + v, start + v })
                {
                    _vertices.Add(q);
                    _normals.Add(normal);
                    _uv.Add(new Vector2(Vector3.Dot(q, tu), Vector3.Dot(q, tv)));
                }
                _triangles.AddRange(flip ? new[] { first, first + 2, first + 1, first, first + 3, first + 2 }
                                         : new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            /// <summary>The mesh in the space of `owner` (the object that will show it).</summary>
            public Mesh Build(string name, Transform owner)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices.Select(owner.InverseTransformPoint).ToList());
                mesh.SetNormals(_normals.Select(owner.InverseTransformDirection).ToList());
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
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
                    count += VantageCoverUtil.AddForBounds(n, b, parent, roomInFront);
                    pieces++;
                }
            }
            Debug.Log($"[Vantage] Covers: {count} on {pieces} prop pieces.");
        }

        /// <summary>
        /// A cover face is only kept if a character fits in front of it on walkable floor: faces against a wall made
        /// the AI walk into the wall to take cover (hands and guns showing through). Needs the NavMesh baked.
        /// </summary>
        private static bool roomInFront(Vector3 faceCentre, Vector3 outward)
        {
            const float radius = 0.3f;
            var stand = faceCentre + outward * 0.55f;
            if (!Physics.Raycast(stand + Vector3.up * 0.8f, Vector3.down, out var hit, 3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)) return false;
            var p = hit.point;
            if (Physics.CheckCapsule(p + Vector3.up * (radius + 0.1f), p + Vector3.up * 1.6f, radius + 0.05f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)) return false;
            return UnityEngine.AI.NavMesh.SamplePosition(p, out var nav, 0.3f, UnityEngine.AI.NavMesh.AllAreas);
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
        /// Enemy spawn markers that a level's arrival only reaches through one of the level's exits: a door that
        /// opens once the level is clear (ClearLevel) or the level's stair gate. A soldier spawned there can never be
        /// reached, so the floor can never be cleared (L2: the stairwell marker behind the stairwell gate). The test
        /// is the NavMesh path from the arrival (doors are left out of the bake, so paths run through doorways):
        /// a path that passes through an exit marks the marker. (The NavMesh's triangles can't be flood-filled for
        /// this: tiles don't always share vertices at their borders.)
        /// </summary>
        private static void markBehindExit(Transform tower, VantageTowerLevels levels, List<VantageFloorGate> gates)
        {
            var exits = new List<(int level, Bounds bounds)>();
            foreach (var door in tower.GetComponentsInChildren<VantageDoor>(true))
            {
                if (door.ClearLevel < 0 || !WorldBounds(door.transform, out var b)) continue;
                b.Expand(0.4f);
                exits.Add((door.ClearLevel, b));
            }
            foreach (var gate in gates)
                if (gate.Blocker != null) exits.Add((gate.Level, gate.Blocker.bounds));

            var markers = tower.GetComponentsInChildren<VantageTag>(true).Where(t => t.MarkerType == "EnemySpawn").ToList();
            var path = new UnityEngine.AI.NavMeshPath();
            levels.BehindExit = new List<string>();
            for (int level = 0; level < levels.Levels.Count; level++)
            {
                var lv = levels.Levels[level];
                var cuts = exits.Where(e => e.level == level).Select(e => e.bounds).ToList();
                if (lv.Arrival == null || cuts.Count == 0) continue;
                if (!UnityEngine.AI.NavMesh.SamplePosition(lv.Arrival.position, out var from, 1f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    Debug.LogWarning($"[Vantage] {lv.Name}: arrival not on the NavMesh; exits not checked.");
                    continue;
                }
                foreach (var m in markers.Where(m => m.name.StartsWith(lv.Prefix, StringComparison.Ordinal)))
                {
                    if (!UnityEngine.AI.NavMesh.SamplePosition(m.transform.position, out var to, 1f, UnityEngine.AI.NavMesh.AllAreas)
                        || !UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, path)
                        || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                        continue;
                    var corners = path.corners;
                    var through = false;
                    for (int i = 1; i < corners.Length && !through; i++)
                    {
                        var a = corners[i - 1] + Vector3.up * 0.5f; var b = corners[i] + Vector3.up * 0.5f;
                        foreach (var c in cuts)
                            if (c.Contains(a) || c.Contains(b) || c.IntersectRay(new Ray(a, b - a), out var d) && d <= Vector3.Distance(a, b)) { through = true; break; }
                    }
                    if (through) levels.BehindExit.Add(m.name);
                }
            }
            EditorUtility.SetDirty(levels);
            Debug.Log($"[Vantage] Spawn markers behind a level's exit (never used): {(levels.BehindExit.Count > 0 ? string.Join(", ", levels.BehindExit) : "none")}.");
        }

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

            // The lockdown: the door on the left of the entrance corridor (one-way into the turret hallway) is locked
            // with the same condition as the shutter, so during the alarm Room A on the right is the only way on.
            var shutterT = Find(tower, "SHUTTER_Maintenance");
            var westT = Find(tower, "DOOR_Corridor_West_OneWay");
            var shutter = shutterT != null ? shutterT.GetComponent<VantageDoor>() : null;
            var west = westT != null ? westT.GetComponent<VantageDoor>() : null;
            if (shutter != null && west != null && !string.IsNullOrEmpty(shutter.unlockBy))
            {
                west.type = DoorType.Locked;
                west.locked = true;
                west.unlockBy = shutter.unlockBy;
                EditorUtility.SetDirty(west);
                Debug.Log($"[Vantage] {west.name} locked during the lockdown ({west.unlockBy}).");
            }
            else Debug.LogWarning("[Vantage] Lockdown door rule: SHUTTER_Maintenance or DOOR_Corridor_West_OneWay missing.");

            // Only level 2 uses E (the key slot). Level 1: the West Wing door stands open (the security console and
            // the shutter's wall switch are switched off). Level 3: the override terminal is off; the robot room opens
            // once every other enemy of the floor is dead, so the robot is the floor's last fight.
            foreach (var console in new[] { "OBJ_Security_Console", "SWITCH_Shutter_Inside", "L3_TERMINAL_Override" })
            {
                var c = Find(tower, console);
                var interact = c != null ? c.GetComponent<VantageInteract>() : null;
                if (interact != null) { interact.enabled = false; EditorUtility.SetDirty(interact); }
                else Debug.LogWarning($"[Vantage] No console {console}.");
            }
            void reopen(string name, bool locked, string unlockBy)
            {
                var t = Find(tower, name);
                var door = t != null ? t.GetComponent<VantageDoor>() : null;
                if (door == null) { Debug.LogWarning($"[Vantage] No door {name}."); return; }
                door.locked = locked;
                door.type = locked ? DoorType.Locked : DoorType.Auto;
                door.unlockBy = unlockBy;
                EditorUtility.SetDirty(door);
            }
            reopen("DOOR_SecurityOffice_WestWing_LOCKED", false, "");
            // The robot room is the only way to the destroyed area and the patrols (through the breach in its south
            // wall), so it must open before them: when the indoor soldiers are dead.
            reopen("L3_DOOR_RobotRoom_LOCKED", true, "KILL:L3_MK_Enemy_Barracks*,L3_MK_Enemy_Comms*,L3_MK_Enemy_Hall*");
            Debug.Log("[Vantage] E only at the level 2 key slot: West Wing open, robot room opens when the indoor level 3 soldiers are dead.");

            // Doors are open doorways: open from the start on the route, or once unlocked. The shutter and the gates
            // (bars) roll up.
            var doorways = 0;
            foreach (var door in tower.GetComponentsInChildren<VantageDoor>(true))
            {
                door.Doorway = door.type != DoorType.Shutter && !door.name.Contains("GATE");
                if (door.Doorway) doorways++;
                EditorUtility.SetDirty(door);
            }
            Debug.Log($"[Vantage] Doors: {doorways} open doorways (locked ones open when unlocked), the rest roll up.");
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

            // Restart points: the player start, the upper part of each level's stairs, outside the exit to the helipad.
            var arrivals = group(parent, "Arrivals");
            Transform arrival(string name, Vector3 at, Vector3 facing, float search = 1.5f)
            {
                var a = new GameObject(name).transform;
                a.SetParent(arrivals, false);
                if (Physics.Raycast(at + Vector3.up * search, Vector3.down, out var hit, search + 1.5f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    at = hit.point + Vector3.up * 0.05f;
                facing.y = 0;
                a.SetPositionAndRotation(at, Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.forward));
                return a;
            }
            // A level's restart point is half way down the last flight below it (at most 3.3 m), not on its floor:
            // the floor above hides it, so a checkpoint never starts in a soldier's view (the L3 stairs end in the
            // doorway between Barracks and Comms, in sight of five spawn points, and the Barracks see the upper steps
            // through the stairwell railing), and the player steps up into the room when ready.
            Transform topOf(string name, string stairs)
            {
                var f = flights.Where(x => x.name.StartsWith(stairs)).OrderByDescending(x => x.top.y).FirstOrDefault();
                var flat = f.top - f.bottom; flat.y = 0;
                var back = Mathf.Min(3.3f, flat.magnitude * 0.5f);
                var at = f.top - flat.normalized * back;
                at.y = f.top.y - (f.top.y - f.bottom.y) * back / flat.magnitude;
                return arrival(name, tower.TransformPoint(at), tower.TransformDirection(flat.normalized), 0.8f);
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
                new VantageTowerLevels.Level { Name = "Ground floor", Prefix = "MK_", FloorY = h[0], Soldiers = 5, Drones = 0, SoldierWeapon = "Pistol",
                    SoldierDamage = 6f, SoldierHealth = 0.8f, ClearedMessage = "LOCKDOWN LIFTED  ·  STAIRS OPEN",
                    Arrival = arrival("Arrival Ground floor", start, startLook) },
                new VantageTowerLevels.Level { Name = "Armory", Prefix = "L2_", FloorY = h[1], Soldiers = 5, Drones = 2, SoldierWeapon = "Pistol",
                    SoldierDamage = 7f, SoldierHealth = 1f, DroneAccuracy = 0.3f, DroneFireInterval = 1.8f, ClearedMessage = "ARMORY CLEAR",
                    Arrival = topOf("Arrival Armory", "LVL1") },
                new VantageTowerLevels.Level { Name = "Destroyed floor", Prefix = "L3_", FloorY = h[2], Soldiers = 5, Drones = 0,
                    SoldierDamage = 8f, SoldierHealth = 1.15f, ClearedMessage = "THE WAY TO THE HELIPAD IS OPEN",
                    Arrival = topOf("Arrival Destroyed floor", "LVL2") },
                new VantageTowerLevels.Level { Name = "Helipad", Prefix = "FINAL_", FloorY = h[3], Soldiers = 2, Drones = 0,
                    SoldierDamage = 8f, SoldierHealth = 1.25f, ClearedMessage = "REACH THE HELIPAD",
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
        /// hand, and carries the aggression tuning. With the Low Poly Soldiers pack present it wears that body
        /// (VantageEnemyModels; then it is a plain prefab, not a variant).
        /// </summary>
        private static GameObject soldierPrefab()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabs + "Soldier.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(template);
            try
            {
                VantageEnemyModels.SoldierBody(go);
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

        /// <summary>
        /// The warning ring (VantageBoundary) around the tower plot: the tower's footprint (helipad included) plus
        /// 8 m, measured like the world tool's plot, so the free zone starts where the levelled ground ends.
        /// </summary>
        private static void boundary(Transform tower, Transform parent)
        {
            var model = levelModel(tower) ?? tower;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogWarning("[Vantage] Boundary: the tower has no renderers, no ring."); return; }
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var go = new GameObject("Boundary");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
            var ring = go.AddComponent<VantageBoundary>();
            ring.Plot = Rect.MinMaxRect(b.min.x - 8f, b.min.z - 8f, b.max.x + 8f, b.max.z + 8f);
            Debug.Log($"[Vantage] Boundary: plot {ring.Plot}, free {ring.FreeDistance} m, warning {ring.WarningDistance} m.");
        }

        /// <summary>
        /// The two sentries' routes, traced from the NavMesh just baked: the broken slab (3rd-floor height, outside
        /// the building's footprint and the finale area) and the helipad platform. A missing area logs FAIL and that
        /// sentry is left out; nothing else depends on it.
        /// </summary>
        private static void sentries(Transform tower, Transform parent, VantageTowerLevels levels)
        {
            var go = new GameObject("Sentries");
            go.transform.SetParent(parent, false);
            var spawner = go.AddComponent<VantageSentries>();
            spawner.SoldierPrefab = levels.SoldierPrefab;
            var drone = levels.DronePrefab != null ? levels.DronePrefab.GetComponent<VantageDrone>() : null;
            spawner.TracerMaterial = drone != null && drone.Tracer != null ? drone.Tracer.sharedMaterial : null;
            if (spawner.TracerMaterial == null) Debug.LogWarning("[Vantage] Sentries: no drone tracer material, ring shots show no tracer.");
            var centre = levels.TowerArea.center;
            var finale = levels.FinaleArea;
            bool inFinale(Vector3 p) => p.x >= finale.min.x && p.x <= finale.max.x && p.z >= finale.min.z && p.z <= finale.max.z;
            // Locked doors and stair blockers carve the NavMesh at runtime (the bake ran without them): keep the
            // routes 1.5 m clear of every obstacle, or a sentry stands in a hole (and in a doorway).
            var blocked = new List<Bounds>();
            foreach (var obstacle in Object.FindObjectsByType<UnityEngine.AI.NavMeshObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var o = obstacle.transform;
                var c = obstacle.center; var e = obstacle.size * 0.5f;
                var ob = new Bounds(o.TransformPoint(c), Vector3.zero);
                for (int corner = 0; corner < 8; corner++)
                    ob.Encapsulate(o.TransformPoint(c + new Vector3((corner & 1) == 0 ? -e.x : e.x, (corner & 2) == 0 ? -e.y : e.y, (corner & 4) == 0 ? -e.z : e.z)));
                ob.Expand(new Vector3(3f, 1f, 3f));
                blocked.Add(ob);
            }
            bool clear(Vector3 p) { foreach (var ob in blocked) if (ob.Contains(p)) return false; return true; }

            var floor3 = LevelHeights(tower)[2];
            var building = Find(tower, "LVL2_FloorSlab");
            Bounds footprint = default;
            var hasFootprint = building != null && WorldBounds(building, out footprint);
            // Within the tower's area (its LVL* renderers, the slab included): base rooftops at the same height
            // elsewhere in the world must not count.
            var area = levels.TowerArea;
            bool slab(Vector3 p) => Mathf.Abs(p.y - floor3) < 1f && !inFinale(p) && clear(p)
                                    && p.x >= area.min.x - 1f && p.x <= area.max.x + 1f && p.z >= area.min.z - 1f && p.z <= area.max.z + 1f
                                    && !(hasFootprint && p.x > footprint.min.x && p.x < footprint.max.x && p.z > footprint.min.z && p.z < footprint.max.z);
            var slabSearch = area; slabSearch.Expand(new Vector3(2f, 0f, 2f));
            addRoute(spawner, "Slab", 2, VantageSentryRoutes.Trace(slabSearch, floor3, slab, centre, 4, 1.5f, 12f, out var slabArea), slabArea);

            var platform = Find(tower, "FINAL_Platform");
            var platformY = float.NaN;
            if (platform != null && WorldBounds(platform, out var pb)
                && UnityEngine.AI.NavMesh.SamplePosition(new Vector3(pb.center.x, pb.max.y, pb.center.z), out var top, 6f, UnityEngine.AI.NavMesh.AllAreas))
                platformY = top.position.y;
            var padArea = 0f;
            bool pad(Vector3 p) => !float.IsNaN(platformY) && inFinale(p) && Mathf.Abs(p.y - platformY) < 1f && clear(p);
            addRoute(spawner, "Helipad", levels.Levels.Count - 1, float.IsNaN(platformY) ? new List<Vector3>()
                     : VantageSentryRoutes.Trace(finale, platformY, pad, centre, 4, 1.5f, 12f, out padArea), padArea);
        }

        private static void addRoute(VantageSentries spawner, string name, int level, List<Vector3> points, float area)
        {
            if (points.Count < 2)
            {
                Debug.Log($"[Vantage] Sentry route {name}: FAIL no walkable area big enough (found {area:F0} m²), no sentry there.");
                return;
            }
            var length = 0f;
            var legs = new List<string>();
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
                var path = new UnityEngine.AI.NavMeshPath();
                UnityEngine.AI.NavMesh.CalculatePath(points[i - 1], points[i], UnityEngine.AI.NavMesh.AllAreas, path);
                if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) legs.Add($"leg {i} {path.status}");
            }
            if (legs.Count > 0) Debug.Log($"[Vantage] Sentry route {name}: FAIL not walkable: {string.Join(", ", legs)}.");
            spawner.Routes.Add(new VantageSentries.Route { Name = name, Level = level, Points = points });
            Debug.Log($"[Vantage] Sentry route {name}: {points.Count} points, {length:F1} m (area {area:F0} m²): {string.Join(" ", points.Select(p => p.ToString("F1")))}.");
        }

        /// <summary>The intro cutscene: where the entrance is and which way it faces, and the helipad it ends on.</summary>
        private static void intro(Transform tower, Transform parent)
        {
            var go = new GameObject("Intro");
            go.transform.SetParent(parent, false);
            var intro = go.AddComponent<VantageIntro>();
            intro.EntrancePoint = EntrancePoint(tower) ?? tower.position;
            intro.EntranceFacing = EntranceFacing(tower);
            intro.Helipad = Find(tower, "FINAL_Helipad");
            Debug.Log($"[Vantage] Intro: entrance {intro.EntrancePoint:F1} facing {intro.EntranceFacing:F0}, helipad {(intro.Helipad != null ? "found" : "missing")}.");
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
        /// -executeMethod Vantage.EditorTools.VantageTower.HoleReportBatch (read-only): NavMesh points on each level
        /// with no solid floor under them, where an agent would walk and fall through.
        /// </summary>
        public static void HoleReportBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                var tower = FindTower().transform;
                Physics.SyncTransforms();
                var levels = Object.FindFirstObjectByType<VantageTowerLevels>();
                var area = levels.TowerArea;
                area.Encapsulate(levels.FinaleArea);
                foreach (var level in levels.Levels)
                {
                    var holes = new List<string>();
                    var whatAbove = new Dictionary<string, int>();
                    int checkedPoints = 0;
                    for (float x = area.min.x; x <= area.max.x; x += 0.5f)
                        for (float z = area.min.z; z <= area.max.z; z += 0.5f)
                        {
                            var p = new Vector3(x, level.FloorY, z);
                            if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var nav, 0.25f, UnityEngine.AI.NavMesh.AllAreas) || Mathf.Abs(nav.position.y - level.FloorY) > 0.4f) continue;
                            checkedPoints++;
                            if (Physics.Raycast(nav.position + Vector3.up * 0.3f, Vector3.down, out var floor, 0.8f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore)) continue;
                            holes.Add(nav.position.ToString("F1"));
                            // Which render mesh made the NavMesh there?
                            foreach (var r in tower.GetComponentsInChildren<MeshRenderer>(true))
                                if (r.enabled && r.bounds.Contains(nav.position + Vector3.down * 0.02f) || r.bounds.Contains(nav.position))
                                {
                                    whatAbove.TryGetValue(r.name, out var n);
                                    whatAbove[r.name] = n + 1;
                                }
                        }
                    Debug.Log($"[Vantage] Holes {level.Name}: {holes.Count} of {checkedPoints} NavMesh points have no floor under them; meshes there: {string.Join(", ", whatAbove.OrderByDescending(k => k.Value).Take(8).Select(k => k.Key + " x" + k.Value))}; points {string.Join(" ", holes.Take(25))}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

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
