using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Builds the game world from the user's own Blender "Military Training Environment"
    /// (Assets/Blender_Asset/Military_Env_Unity): 1 km terrain with forest, the training base, roads and the horizon,
    /// at the same coordinates as in Blender, so a tower modelled there in place lands in the right spot.
    ///
    /// - The tower plot is fitted to the user's CommandTower wherever it stands: its terrain is levelled at the
    ///   tower's base height and blended back into the hills, and base props inside its footprint are cleared.
    /// - Then VantageCommandTower.Setup turns the tower into the level (levels, gates, enemies, player start).
    /// - Roads and pads follow the reshaped terrain; flush ones lose their colliders so kerbs can't block the
    ///   character (the template cannot step up ledges). Trees and rocks get colliders.
    /// - The world is playable: it is in the NavMesh, cover props get Cover Shooter covers, invisible walls stop the
    ///   player at the edge, and any fence that cuts the tower off from the base gets a breach.
    /// - A top-down image of the world is baked for the minimap (VantageMinimap).
    /// Re-running replaces the previous result. The pack's own script is left untouched.
    /// </summary>
    public static class VantageMilitaryBase
    {
        public const string RootName = "VANTAGE World - Military Base";
        public const string MinimapName = "VANTAGE Minimap";
        private const string OutFolder = "Assets/Vantage/World";
        private const float Blend = 35f;        // width of the terrain blend from the plot back to the hills

        /// <summary>Scene roots from the old industrial yard, removed when the military world takes over.</summary>
        private static readonly string[] LegacyRoots = { "Map", "Plane" };

        // ---------------- pack data (same schema as MilitaryEnvBuilder) ----------------
#pragma warning disable 0649 // filled by JsonUtility
        [Serializable] private class Item { public string k; public string c; public float[] p; public float[] q; public float[] s; }
        [Serializable] private class TerrainInfo { public float[] size; public float hmin; public float hmax; public int height_res; public int splat_res; public int detail_res; public string[] layers; public float layer_tile_m; }
        [Serializable] private class Placements
        {
            public Item[] props; public Item[] buildings; public Item[] unique;
            public string[] tree_names; public float[] trees; public float[] distant;
            public TerrainInfo terrain;
        }
#pragma warning restore 0649

        private static Vector3 pos(float[] p) => new Vector3(-p[0], p[2], -p[1]);
        private static Quaternion rot(float[] q) => new Quaternion(q[1], -q[3], q[2], q[0]);
        private static Vector3 scl(float[] s) => new Vector3(s[0], s[2], s[1]);

        /// <summary>Everything the build needs about the reshaped terrain, in world space.</summary>
        private class Ground
        {
            public Vector3 Offset;        // pack origin in the world (zero: Blender coordinates)
            public Rect Plot;             // the tower plot (x/z), margin included
            public float PlotHeight;      // flat ground height of the plot
            public Vector3 TowerSite;     // where the tower root goes
            public float Size, HMin, HMax;
            public int Res;
            public float[,] Original, Shaped; // pack-space heights in metres, [z, x]

            public float Delta(float worldX, float worldZ) => sample(Shaped, worldX, worldZ) - sample(Original, worldX, worldZ);
            public float Height(float worldX, float worldZ) => sample(Shaped, worldX, worldZ) + Offset.y;

            private float sample(float[,] h, float worldX, float worldZ)
            {
                var fx = Mathf.Clamp((worldX - Offset.x + Size / 2) / Size * (Res - 1), 0, Res - 1.001f);
                var fz = Mathf.Clamp((worldZ - Offset.z + Size / 2) / Size * (Res - 1), 0, Res - 1.001f);
                int x = (int)fx, z = (int)fz;
                float tx = fx - x, tz = fz - z;
                return Mathf.Lerp(Mathf.Lerp(h[z, x], h[z, x + 1], tx), Mathf.Lerp(h[z + 1, x], h[z + 1, x + 1], tx), tz);
            }

            /// <summary>Distance outside the tower plot, 0 inside.</summary>
            public float OutsidePlot(float x, float z)
            {
                var dx = Mathf.Max(Plot.xMin - x, 0, x - Plot.xMax);
                var dz = Mathf.Max(Plot.yMin - z, 0, z - Plot.yMax);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
        }

        // ---------------- entry points ----------------

        [MenuItem("Vantage/World/Build Military World (tower plot in the base)", priority = 70)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
            try
            {
                Build();
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Re-renders only the minimap image, e.g. after the Blender tower has been placed.</summary>
        [MenuItem("Vantage/World/Rebake Minimap", priority = 72)]
        public static void RebakeMinimapFromMenu()
        {
            bakeMinimap(worldGround());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>-executeMethod Vantage.EditorTools.VantageMilitaryBase.MinimapBatch</summary>
        public static void MinimapBatch()
        {
            var exitCode = 0;
            try
            {
                var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                bakeMinimap(worldGround());
                EditorSceneManager.SaveScene(scene);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>The world's extent without rebuilding anything (the pack sits at Blender coordinates).</summary>
        private static Ground worldGround()
        {
            var info = JsonUtility.FromJson<Placements>(File.ReadAllText(full(findPackRoot() + "/Data/placements.json"))).terrain;
            return new Ground { Size = info.size[0], HMin = info.hmin, HMax = info.hmax, Res = info.height_res, Offset = Vector3.zero };
        }

        [MenuItem("Vantage/World/Remove Military World", priority = 71)]
        public static void RemoveFromMenu()
        {
            foreach (var name in new[] { RootName, MinimapName })
            {
                var old = GameObject.Find(name);
                if (old != null)
                    Undo.DestroyObjectImmediate(old);
            }
        }

        /// <summary>
        /// -executeMethod Vantage.EditorTools.VantageMilitaryBase.RunBatch [-vantageShots folder]
        /// </summary>
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-vantageShots");
            var shots = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            var exitCode = 0;
            try
            {
                VantageLayers.Ensure();
                var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                var ground = Build();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[Vantage] Military world saved into the scene.");

                VantageEditorUtil.BakeOcclusion();
                EditorSceneManager.SaveScene(scene);

                if (!string.IsNullOrEmpty(shots))
                    screenshots(shots, ground);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Playtest evidence, scene untouched. Around the tower plot: a ledge/slope map (ledges the template character
        /// can't step up in red, slopes over 26° in orange), a NavMesh connectivity map (green = reachable from the
        /// player start) and path checks from the start to points across the base.
        /// -executeMethod Vantage.EditorTools.VantageMilitaryBase.LedgeReportBatch -vantageShots folder
        /// </summary>
        public static void LedgeReportBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var a = Array.IndexOf(args, "-vantageShots");
            var folder = a >= 0 && a + 1 < args.Length ? args[a + 1] : "LedgeReport";
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                var tower = VantageCommandTower.FindTower().transform;
                var probe = new Ground();
                plotUnder(probe, tower.gameObject);
                var plot = probe.Plot;
                Physics.SyncTransforms();
                var mask = ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11));
                const float s = 0.5f;
                var area = Rect.MinMaxRect(plot.xMin - 30, plot.yMin - 30, plot.xMax + 30, plot.yMax + 30);
                int w = (int)(area.width / s), h = (int)(area.height / s);
                var y = new float[w, h];
                var maxFloor = tower.position.y + 2.6f;
                for (int i = 0; i < w; i++)
                    for (int j = 0; j < h; j++)
                    {
                        var best = float.NaN;
                        foreach (var hit in Physics.RaycastAll(new Vector3(area.xMin + i * s, maxFloor + 60f, area.yMin + j * s), Vector3.down, 200f, mask, QueryTriggerInteraction.Ignore))
                            if (hit.normal.y > 0.9f && hit.point.y < maxFloor && (float.IsNaN(best) || hit.point.y < best))
                                best = hit.point.y;
                        y[i, j] = best;
                    }
                // Ledge (red): a sudden step between two samples with flatter ground on both sides.
                // Steep (orange): over 26°, where the template starts slowing the character down (stops at 60°).
                var tex = new Texture2D(w, h);
                int ledges = 0, steep = 0;
                float at(int i, int j) => i >= 0 && j >= 0 && i < w && j < h ? y[i, j] : float.NaN;
                for (int i = 0; i < w; i++)
                    for (int j = 0; j < h; j++)
                    {
                        var v = y[i, j];
                        var col = float.IsNaN(v) ? Color.black : Color.Lerp(new Color(0.15f, 0.2f, 0.15f), new Color(0.85f, 0.9f, 0.85f), Mathf.InverseLerp(tower.position.y - 4f, tower.position.y + 2f, v));
                        foreach (var (di, dj) in new[] { (1, 0), (0, 1) })
                        {
                            var d = Mathf.Abs(at(i + di, j + dj) - v);
                            if (float.IsNaN(d) || d >= 1.5f) continue;
                            var before = Mathf.Abs(v - at(i - di, j - dj));
                            var after = Mathf.Abs(at(i + 2 * di, j + 2 * dj) - at(i + di, j + dj));
                            if (d > 0.06f && !(before > d / 3) && !(after > d / 3)) { col = Color.red; ledges++; }
                            else if (d > s * 0.49f && col != Color.red) { col = new Color(1f, 0.6f, 0f); steep++; }
                        }
                        tex.SetPixel(i, j, col);
                    }
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "ledges.png"), tex.EncodeToPNG());

                // NavMesh reach: from the player start to the base centre, the plot's surroundings and the forest.
                var start = Object.FindFirstObjectByType<CoverShooter.ThirdPersonInput>().transform.position;
                foreach (var target in new[] { Vector3.zero, new Vector3(plot.xMin - 20f, 0, plot.center.y), new Vector3(plot.xMax + 20f, 0, plot.center.y),
                             new Vector3(plot.center.x, 0, plot.yMax + 20f), new Vector3(-100f, 0, -80f), new Vector3(0f, 0, -220f), new Vector3(-250f, 0, 200f) })
                {
                    var t = target + Vector3.up * 40f;
                    var hasFrom = UnityEngine.AI.NavMesh.SamplePosition(start, out var from, 3f, UnityEngine.AI.NavMesh.AllAreas);
                    var hasTo = UnityEngine.AI.NavMesh.SamplePosition(t, out var to, 60f, UnityEngine.AI.NavMesh.AllAreas);
                    var path = new UnityEngine.AI.NavMeshPath();
                    var status = hasFrom && hasTo && UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, path)
                        ? path.status.ToString()
                        : $"no navmesh (start {hasFrom}, target {hasTo})";
                    var end = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] : Vector3.zero;
                    Debug.Log($"[Vantage] Path player start {start} -> {to.position}: {status}, ends at {end}");
                }
                navMeshMap(folder, start, area);
                Debug.Log($"[Vantage] Ledge report: {ledges} ledge samples, {steep} steep (>26°) samples over {area}; maps written to {folder}.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Top-down NavMesh map: green = connected to the player start, blue = cut off; black = no NavMesh.
        /// Islands are found by welding triangle corners and joining triangles that share an edge.
        /// </summary>
        private static void navMeshMap(string folder, Vector3 start, Rect area)
        {
            var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            var key = new Dictionary<Vector3Int, int>();
            int weld(Vector3 v)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v.x * 20), Mathf.RoundToInt(v.y * 5), Mathf.RoundToInt(v.z * 20));
                if (!key.TryGetValue(k, out var id)) key[k] = id = key.Count;
                return id;
            }
            var ids = tri.indices.Select(i => weld(tri.vertices[i])).ToArray();
            var parent = Enumerable.Range(0, key.Count).ToArray();
            int find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            for (int t = 0; t < ids.Length; t += 3)
            {
                parent[find(ids[t])] = find(ids[t + 1]);
                parent[find(ids[t + 1])] = find(ids[t + 2]);
            }
            UnityEngine.AI.NavMesh.SamplePosition(start, out var s, 3f, UnityEngine.AI.NavMesh.AllAreas);
            var startRoot = -1; var bestD = float.MaxValue;
            for (int t = 0; t < ids.Length; t++)
            {
                var d = (tri.vertices[tri.indices[t]] - s.position).sqrMagnitude;
                if (d < bestD) { bestD = d; startRoot = find(ids[t]); }
            }

            const float px = 0.5f;
            int w = (int)(area.width / px), h = (int)(area.height / px);
            var tex = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int t = 0; t < ids.Length; t += 3)
            {
                var a = tri.vertices[tri.indices[t]]; var b = tri.vertices[tri.indices[t + 1]]; var c = tri.vertices[tri.indices[t + 2]];
                var col = find(ids[t]) == startRoot ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.2f, 0.4f, 0.9f);
                float minX = Mathf.Min(a.x, b.x, c.x), maxX = Mathf.Max(a.x, b.x, c.x), minZ = Mathf.Min(a.z, b.z, c.z), maxZ = Mathf.Max(a.z, b.z, c.z);
                for (var x = minX; x <= maxX; x += px)
                    for (var z = minZ; z <= maxZ; z += px)
                    {
                        var p = new Vector2(x, z);
                        if (!inTriangle(p, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z))) continue;
                        int i = (int)((x - area.xMin) / px), j = (int)((z - area.yMin) / px);
                        if (i >= 0 && j >= 0 && i < w && j < h) pixels[j * w + i] = col;
                    }
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes(Path.Combine(folder, "navmesh.png"), tex.EncodeToPNG());
        }

        private static bool inTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            float s2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
            float s3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
            return (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        }

        /// <summary>
        /// Makes sure the player can get from the tower compound into the base and out into the forest (the
        /// position was overrun, so breaches fit): while the NavMesh path from the player start to a target stops
        /// short, the fence, wall or gate piece in the way is switched off (not deleted) and the NavMesh rebaked.
        /// Only pieces under 4.5 m are touched, never buildings. Returns the pieces switched off.
        /// </summary>
        private static List<GameObject> openWayOut(Ground ground, Transform baseRoot)
        {
            var opened = new List<GameObject>();
            var player = Object.FindFirstObjectByType<CoverShooter.ThirdPersonInput>();
            if (player == null)
                return opened;
            var mask = ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11));
            // The base on both sides of the plot, and the forest beyond the base fence.
            var site = ground.TowerSite;
            var targets = new[]
            {
                new Vector3(ground.Plot.xMin - 40f, 0, site.z), new Vector3(ground.Plot.xMax + 40f, 0, site.z),
                new Vector3(site.x, 0, ground.Plot.yMax + 40f), new Vector3(site.x - 220f, 0, site.z + 180f),
            };

            foreach (var target in targets)
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var start = player.transform.position;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(start, out var from, 3f, UnityEngine.AI.NavMesh.AllAreas)
                        || !UnityEngine.AI.NavMesh.SamplePosition(target + Vector3.up * 30f, out var to, 60f, UnityEngine.AI.NavMesh.AllAreas))
                        break;
                    var path = new UnityEngine.AI.NavMeshPath();
                    UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, path);
                    if (path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    {
                        Debug.Log($"[Vantage] Path from the player start to {to.position} is open.");
                        break;
                    }

                    // From where the path gets stuck, look towards the target (and a little to each side) for a wall piece.
                    var end = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] : from.position;
                    var toward = Vector3.ProjectOnPlane(to.position - end, Vector3.up).normalized;
                    GameObject piece = null;
                    foreach (var angle in new[] { 0f, 25f, -25f, 50f, -50f, 75f, -75f })
                    {
                        var dir = Quaternion.Euler(0, angle, 0) * toward;
                        if (!Physics.Raycast(end + Vector3.up * 1f, dir, out var hit, 12f, mask, QueryTriggerInteraction.Ignore))
                            continue;
                        piece = wallPiece(hit.collider.transform, baseRoot);
                        if (piece != null) break;
                    }
                    if (piece == null)
                    {
                        Debug.LogWarning($"[Vantage] Path to {to.position} stops at {end}, but no fence or wall piece is in the way there.");
                        break;
                    }

                    Undo.RecordObject(piece, "Breach");
                    piece.SetActive(false);
                    opened.Add(piece);
                    Debug.Log($"[Vantage] Breach: switched off {fullName(piece.transform)} (path stopped at {end}).");
                    VantageSetup.BakeNavMesh();
                }
            return opened;
        }

        /// <summary>The whole fence/wall/gate piece a collider belongs to, or null if it's anything else.</summary>
        private static GameObject wallPiece(Transform t, Transform baseRoot)
        {
            // Walk up to the object directly under a group ("Perimeter Wall", "Perimeter and Defences", ...):
            // that is one placed piece.
            var piece = t;
            while (piece.parent != null && piece.parent.parent != null && !isGroup(piece.parent))
                piece = piece.parent;
            var n = piece.name.ToLowerInvariant();
            var b = bounds(piece.gameObject);
            return (n.Contains("fence") || n.Contains("wall") || n.Contains("gate")) && b.size.y < 4.5f ? piece.gameObject : null;
        }

        private static bool isGroup(Transform t) =>
            t.GetComponent<Renderer>() == null && t.GetComponent<Collider>() == null && t.childCount > 3;

        private static string fullName(Transform t)
        {
            var name = t.name;
            for (var p = t.parent; p != null; p = p.parent) name = p.name + "/" + name;
            return name;
        }

        // ---------------- build ----------------

        private static Ground Build()
        {
            var packRoot = findPackRoot();
            ensurePackMaterials(packRoot);
            ensureReadableWorldMeshes(packRoot);

            // Scene roots, inactive ones included (GameObject.Find only sees active objects).
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != RootName && !LegacyRoots.Contains(go.name)) continue;
                if (go.name != RootName)
                    Debug.Log($"[Vantage] Removed '{go.name}' (the old industrial yard).");
                Object.DestroyImmediate(go);
            }

            var data = JsonUtility.FromJson<Placements>(File.ReadAllText(full(packRoot + "/Data/placements.json")));
            var info = data.terrain;
            var ground = new Ground { Size = info.size[0], HMin = info.hmin, HMax = info.hmax, Res = info.height_res, Offset = Vector3.zero };

            ground.Original = readHeights(packRoot, info);
            var commandTower = VantageCommandTower.FindTower();
            if (commandTower == null)
                throw new Exception("No CommandTower in the scene. Drag Assets/Blender_Asset/CommandTower_Unity/Prefabs/CommandTower.prefab in first; the world is fitted around it.");
            plotUnder(ground, commandTower);
            shapeTerrain(ground);

            var root = new GameObject(RootName).transform;
            root.position = ground.Offset;

            ensureFolder(OutFolder);
            buildTerrain(ground, data, packRoot, group(root, "Terrain"));
            var removed = placeObjects(ground, data, packRoot, root);
            placeWorldMeshes(ground, data, packRoot, group(root, "Roads, Pads and Earthworks"));
            var horizon = group(root, "Distant Forest");
            horizon.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            distantForest(data, packRoot, horizon);
            addCovers(root);
            worldEdge(ground, group(root, "World Edge"));
            atmosphere();
            Debug.Log($"[Vantage] Military world built; tower plot {ground.Plot} at height {ground.PlotHeight:F2}; {removed} props cleared from the plot.");

            // The command tower is the level: levels, gates, enemies, pickups, player start, NavMesh.
            VantageCommandTower.Setup();

            var opened = openWayOut(ground, root);
            Debug.Log($"[Vantage] {opened.Count} fence pieces switched off to connect the tower with the base.");

            bakeMinimap(ground);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return ground;
        }

        private static string findPackRoot()
        {
            var guid = AssetDatabase.FindAssets("MilitaryEnvBuilder t:MonoScript").FirstOrDefault();
            if (guid == null)
                throw new Exception("Military environment pack not found (MilitaryEnvBuilder.cs is missing).");
            var path = AssetDatabase.GUIDToAssetPath(guid); // <root>/Scripts/Editor/MilitaryEnvBuilder.cs
            return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path))).Replace('\\', '/');
        }

        private static string full(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        /// <summary>The pack's step 1 (import settings, materials, material remap) runs once, the first time.</summary>
        private static void ensurePackMaterials(string packRoot)
        {
            var albedos = AssetDatabase.FindAssets("_Albedo t:Texture2D", new[] { packRoot + "/Textures" }).Length;
            var materials = AssetDatabase.IsValidFolder(packRoot + "/Materials")
                ? AssetDatabase.FindAssets("t:Material", new[] { packRoot + "/Materials" }).Length
                : 0;
            if (materials < albedos || albedos == 0)
            {
                Debug.Log($"[Vantage] Military pack: setting up import settings and {albedos} materials (first run, takes a while).");
                MilitaryEnvBuilder.MenuMaterials();
            }
            dullVegetation(packRoot);
        }

        /// <summary>
        /// The pack gives leaf cards and bark smoothness 1 on top of their smoothness maps, so trees mirror the sky
        /// and read blue. Re-applied on every build because the pack's material step resets it.
        /// </summary>
        private static void dullVegetation(string packRoot)
        {
            foreach (var guid in AssetDatabase.FindAssets("_Albedo t:Texture2D", new[] { packRoot + "/Textures/Vegetation" }))
            {
                var key = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)).Replace("_Albedo", "");
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{packRoot}/Materials/{key}.mat");
                if (mat == null || !mat.HasProperty("_Smoothness") || mat.GetFloat("_Smoothness") <= 0.2f)
                    continue;
                mat.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>World meshes (roads, pads) are reshaped to the new terrain, so their vertices must be readable.</summary>
        private static void ensureReadableWorldMeshes(string packRoot)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { packRoot + "/Models/World" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
        }

        private static float[,] readHeights(string packRoot, TerrainInfo info)
        {
            int res = info.height_res;
            var raw = File.ReadAllBytes(full(packRoot + "/Terrain/Heightmap_1025_u16.raw"));
            var h = new float[res, res];
            var range = info.hmax - info.hmin;
            for (int r = 0; r < res; r++)
                for (int c = 0; c < res; c++)
                    h[r, c] = info.hmin + BitConverter.ToUInt16(raw, (r * res + c) * 2) / 65535f * range;
            return h;
        }

        /// <summary>
        /// The plot is wherever the user placed the command tower: its footprint plus 8 m, levelled at the tower's
        /// base height, so the ground meets the entrances and nothing of the base stands inside it.
        /// </summary>
        private static void plotUnder(Ground ground, GameObject tower)
        {
            var renderers = tower.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            ground.Plot = Rect.MinMaxRect(b.min.x - 8f, b.min.z - 8f, b.max.x + 8f, b.max.z + 8f);
            ground.PlotHeight = tower.transform.position.y;
            ground.TowerSite = tower.transform.position;
            Debug.Log($"[Vantage] Tower plot fitted to the command tower at {ground.TowerSite} (footprint {b.size.x:F0} x {b.size.z:F0} m).");
        }

        private static float sampleOriginal(Ground ground, float x, float z)
        {
            int res = ground.Res;
            int c = Mathf.Clamp(Mathf.RoundToInt((x - ground.Offset.x + ground.Size / 2) / ground.Size * (res - 1)), 0, res - 1);
            int r = Mathf.Clamp(Mathf.RoundToInt((z - ground.Offset.z + ground.Size / 2) / ground.Size * (res - 1)), 0, res - 1);
            return ground.Original[r, c] + ground.Offset.y;
        }

        /// <summary>
        /// The plot becomes level ground at the plot height; around it the terrain is relaxed into the smoothest
        /// surface joining the plot to the untouched hills (a Laplace membrane), so there are no ledges for the
        /// template character, which cannot step up.
        /// </summary>
        private static void shapeTerrain(Ground ground)
        {
            int res = ground.Res;
            ground.Shaped = (float[,])ground.Original.Clone();
            var flat = ground.PlotHeight - ground.Offset.y;
            var step = ground.Size / (res - 1);

            // Cells of the region that gets reshaped: plot plus the blend ring.
            int c0 = Mathf.Max(1, (int)((ground.Plot.xMin - Blend - ground.Offset.x + ground.Size / 2) / step));
            int c1 = Mathf.Min(res - 2, (int)((ground.Plot.xMax + Blend - ground.Offset.x + ground.Size / 2) / step) + 1);
            int r0 = Mathf.Max(1, (int)((ground.Plot.yMin - Blend - ground.Offset.z + ground.Size / 2) / step));
            int r1 = Mathf.Min(res - 2, (int)((ground.Plot.yMax + Blend - ground.Offset.z + ground.Size / 2) / step) + 1);
            var pinned = new bool[res, res];

            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                {
                    var x = ground.Offset.x - ground.Size / 2 + c * step;
                    var z = ground.Offset.z - ground.Size / 2 + r * step;
                    var d = ground.OutsidePlot(x, z);
                    if (d >= Blend) { pinned[r, c] = true; continue; }
                    // Start value: flat-and-blend; the relaxation smooths it.
                    ground.Shaped[r, c] = Mathf.Lerp(flat, ground.Original[r, c], Mathf.SmoothStep(0, 1, d / Blend));
                    if (d <= 0) pinned[r, c] = true;
                }

            // Pin the frame of the region too, then over-relax the free cells.
            for (int r = r0 - 1; r <= r1 + 1; r++) { pinned[r, c0 - 1] = true; pinned[r, c1 + 1] = true; }
            for (int c = c0 - 1; c <= c1 + 1; c++) { pinned[r0 - 1, c] = true; pinned[r1 + 1, c] = true; }
            const float omega = 1.9f;
            for (int it = 0; it < 700; it++)
                for (int r = r0; r <= r1; r++)
                    for (int c = c0; c <= c1; c++)
                    {
                        if (pinned[r, c]) continue;
                        var h = ground.Shaped;
                        var avg = (h[r - 1, c] + h[r + 1, c] + h[r, c - 1] + h[r, c + 1]) * 0.25f;
                        h[r, c] += omega * (avg - h[r, c]);
                    }
            Debug.Log("[Vantage] Terrain levelled under the tower plot and relaxed into slopes around it.");
        }

        // ---------------- terrain ----------------

        private static void buildTerrain(Ground ground, Placements data, string packRoot, Transform parent)
        {
            var info = data.terrain;
            int res = ground.Res;
            var range = info.hmax - info.hmin;

            var td = new TerrainData { heightmapResolution = res, size = new Vector3(info.size[0], range, info.size[1]) };
            var h = new float[res, res];
            for (int r = 0; r < res; r++)
                for (int c = 0; c < res; c++)
                    h[r, c] = Mathf.Clamp01((ground.Shaped[r, c] - info.hmin) / range);
            td.SetHeights(0, 0, h);

            // Texture layers and splat map as authored.
            var layers = new List<TerrainLayer>();
            ensureFolder(OutFolder + "/Terrain Layers");
            foreach (var name in info.layers)
            {
                var path = $"{OutFolder}/Terrain Layers/{name}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{packRoot}/Textures/Tiling/{name}_Albedo.png");
                layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{packRoot}/Textures/Tiling/{name}_Normal.png");
                layer.tileSize = new Vector2(info.layer_tile_m, info.layer_tile_m);
                // URP otherwise reads smoothness from the albedo alpha (opaque = 1) and the ground turns into a mirror.
                layer.smoothnessSource = TerrainLayerSmoothnessSource.Constant;
                layer.smoothness = name.Contains("Rock") ? 0.15f : 0.05f;
                EditorUtility.SetDirty(layer);
                layers.Add(layer);
            }
            td.terrainLayers = layers.ToArray();
            int sr = info.splat_res, L = layers.Count;
            td.alphamapResolution = sr;
            var splatRaw = File.ReadAllBytes(full(packRoot + "/Terrain/Splat_1024x1024x5_u8.raw"));
            var alpha = new float[sr, sr, L];
            // The compound ground is gravel, fading into the original ground over 15 m around it.
            var gravel = Array.IndexOf(info.layers, "Terrain_Gravel");
            var splatCell = ground.Size / sr;
            for (int r = 0; r < sr; r++)
                for (int c = 0; c < sr; c++)
                {
                    var x = ground.Offset.x - ground.Size / 2 + (c + 0.5f) * splatCell;
                    var z = ground.Offset.z - ground.Size / 2 + (r + 0.5f) * splatCell;
                    var w = gravel >= 0 ? 0.85f * (1f - Mathf.SmoothStep(0, 1, ground.OutsidePlot(x, z) / 15f)) : 0f;
                    for (int l = 0; l < L; l++)
                        alpha[r, c, l] = Mathf.Lerp(splatRaw[(r * sr + c) * L + l] / 255f, l == gravel ? 1f : 0f, w);
                }
            td.SetAlphamaps(0, 0, alpha);

            // Grass and ferns, cleared on the tower plot.
            var detailModels = new List<GameObject>();
            for (int i = 0; i < 4; i++) addIfFound(detailModels, $"{packRoot}/Models/Vegetation/G_GrassCard_{i}.fbx");
            int grassCount = detailModels.Count;
            for (int i = 0; i < 2; i++) addIfFound(detailModels, $"{packRoot}/Models/Vegetation/G_Fern_{i}.fbx");
            int dr = info.detail_res;
            td.SetDetailResolution(dr, 32);
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            td.detailPrototypes = detailModels.Select(g => new DetailPrototype
            {
                prototype = g, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = 0.8f, maxWidth = 1.3f, minHeight = 0.8f, maxHeight = 1.3f,
                healthyColor = Color.white, dryColor = new Color(0.9f, 0.88f, 0.8f), noiseSpread = 0.3f,
            }).ToArray();
            var grass = File.ReadAllBytes(full(packRoot + "/Terrain/DetailGrass_1024_u8.raw"));
            var fern = File.ReadAllBytes(full(packRoot + "/Terrain/DetailFern_1024_u8.raw"));
            var cell = ground.Size / dr;
            for (int p = 0; p < detailModels.Count; p++)
            {
                var isGrass = p < grassCount;
                int variants = isGrass ? grassCount : detailModels.Count - grassCount, sub = isGrass ? p : p - grassCount;
                var layer = new int[dr, dr];
                for (int r = 0; r < dr; r++)
                    for (int c = 0; c < dr; c++)
                    {
                        int v = isGrass ? grass[r * dr + c] : fern[r * dr + c];
                        if (v == 0 || (r * 7 + c * 13) % variants != sub)
                            continue;
                        var x = ground.Offset.x - ground.Size / 2 + (c + 0.5f) * cell;
                        var z = ground.Offset.z - ground.Size / 2 + (r + 0.5f) * cell;
                        if (ground.OutsidePlot(x, z) < 2f)
                            continue;
                        layer[r, c] = isGrass ? v * 2 : v;
                    }
                td.SetDetailLayer(0, 0, p, layer);
            }

            // Trees, bushes, rocks and debris: dropped on the tower plot, put back on the reshaped ground elsewhere.
            var protos = new List<TreePrototype>();
            var index = new int[data.tree_names.Length];
            for (int i = 0; i < data.tree_names.Length; i++)
            {
                var n = data.tree_names[i];
                var path = n.StartsWith("A_") ? $"{packRoot}/Models/Props/{n.Substring(2)}.fbx" : $"{packRoot}/Models/Vegetation/{n}.fbx";
                var model = solidTree(n, AssetDatabase.LoadAssetAtPath<GameObject>(path));
                index[i] = model != null ? protos.Count : -1;
                if (model != null) protos.Add(new TreePrototype { prefab = model, bendFactor = 0f });
                else Debug.LogWarning("[Vantage] Military pack: missing tree model " + path);
            }
            td.treePrototypes = protos.ToArray();
            var trees = new List<TreeInstance>(data.trees.Length / 6);
            int skipped = 0;
            for (int i = 0; i + 5 < data.trees.Length; i += 6)
            {
                var proto = index[(int)data.trees[i]];
                if (proto < 0) continue;
                float ux = -data.trees[i + 1], uz = -data.trees[i + 2];
                var wx = ux + ground.Offset.x; var wz = uz + ground.Offset.z;
                if (ground.OutsidePlot(wx, wz) < 3f) { skipped++; continue; }
                var y = ground.Height(wx, wz) - ground.Offset.y;
                trees.Add(new TreeInstance
                {
                    prototypeIndex = proto,
                    position = new Vector3((ux + ground.Size / 2) / ground.Size, Mathf.Clamp01((y - info.hmin) / range), (uz + ground.Size / 2) / ground.Size),
                    rotation = -data.trees[i + 4],
                    widthScale = data.trees[i + 5], heightScale = data.trees[i + 5],
                    color = Color.white, lightmapColor = Color.white,
                });
            }
            td.SetTreeInstances(trees.ToArray(), false);

            var dataPath = OutFolder + "/MilitaryBase_TerrainData.asset";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(td, dataPath);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(ground.Offset.x - ground.Size / 2, ground.Offset.y + info.hmin, ground.Offset.z - ground.Size / 2);
            var terrain = go.GetComponent<Terrain>();
            // Performance: the base is backdrop. Trees are plain meshes without LODs, so their draw distance is
            // what keeps the frame cheap; fog and the instanced horizon forest cover the far range.
            terrain.drawInstanced = true;
            terrain.treeDistance = 450f;
            terrain.treeBillboardDistance = 150f;
            terrain.treeMaximumFullLODCount = 300;
            terrain.detailObjectDistance = 80f;
            terrain.detailObjectDensity = 1f;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 250f;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null)
            {
                var matPath = OutFolder + "/MilitaryBase_Terrain.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
                terrain.materialTemplate = mat;
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Debug.Log($"[Vantage] Terrain: {trees.Count} trees/rocks ({skipped} removed under the tower plot), {detailModels.Count} grass/fern types.");
        }

        /// <summary>
        /// Trees (T_), young spruces and rocks (R_) get a capsule so the player can't walk through them; the terrain
        /// turns prototype colliders into tree colliders. Bushes, saplings and debris stay walk-through.
        /// The pack's models can't carry components, so each becomes a small prefab in Assets/Vantage/World/Trees.
        /// </summary>
        private static GameObject solidTree(string name, GameObject model)
        {
            if (model == null || !(name.StartsWith("T_") || name.StartsWith("R_") || name.StartsWith("M_YoungSpruce")))
                return model;

            ensureFolder(OutFolder + "/Trees");
            var path = $"{OutFolder}/Trees/{name}.prefab";
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var b = bounds(go);
                var capsule = go.AddComponent<CapsuleCollider>();
                var rock = name.StartsWith("R_");
                // Trees: a trunk-sized capsule (the canopy stays passable). Rocks: their footprint.
                capsule.radius = rock ? Mathf.Min(b.extents.x, b.extents.z) * 0.9f : name.StartsWith("T_") ? 0.35f : 0.25f;
                capsule.height = Mathf.Max(b.size.y, capsule.radius * 2);
                capsule.center = go.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + capsule.height / 2, b.center.z));
                if (!rock) capsule.center = new Vector3(0, capsule.center.y, 0);
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void addIfFound(List<GameObject> list, string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null) list.Add(model);
        }

        // ---------------- buildings and props ----------------

        private static int placeObjects(Ground ground, Placements data, string packRoot, Transform root)
        {
            var groups = new Dictionary<string, Transform>();
            Transform groupFor(string name) => groups.TryGetValue(name, out var t) ? t : groups[name] = group(root, name);
            var names = new Dictionary<string, string>
            {
                { "03_BUILDINGS", "Buildings" }, { "04_MILITARY_PROPS", "Military Props" }, { "05_DEFENSE", "Perimeter and Defences" },
                { "06_VEHICLES", "Vehicles" }, { "11_TRAINING_AREA", "Training Area" },
            };

            var cache = new Dictionary<string, GameObject>();
            int removed = 0, placed = 0;
            void place(Item it, string folder, string category, bool occluder)
            {
                if (!cache.TryGetValue(it.k, out var model))
                    cache[it.k] = model = AssetDatabase.LoadAssetAtPath<GameObject>($"{packRoot}/Models/{folder}/{it.k}.fbx");
                if (model == null) { Debug.LogWarning("[Vantage] Military pack: missing model " + it.k); return; }

                if (it.k.Contains("Truck") || it.k.Contains("APC") || it.k.Contains("Utility_4x4"))
                    category = "06_VEHICLES";
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, groupFor(names.TryGetValue(category ?? "", out var n) ? n : category ?? "Other"));
                go.transform.localPosition = pos(it.p);
                go.transform.localRotation = rot(it.q);
                go.transform.localScale = scl(it.s);

                // On the tower plot: drop. On reshaped ground: follow it if the change is small, otherwise drop.
                var p = go.transform.position;
                var delta = ground.Delta(p.x, p.z);
                if (intersectsPlot(ground, bounds(go)) || Mathf.Abs(delta) > 1.5f)
                {
                    Object.DestroyImmediate(go);
                    removed++;
                    return;
                }
                go.transform.position += Vector3.up * delta;
                setStatic(go, occluder);
                placed++;
            }

            foreach (var it in data.buildings) place(it, "Buildings", "03_BUILDINGS", true);
            foreach (var it in data.props) place(it, "Props", it.c, false);
            Debug.Log($"[Vantage] Buildings and props: {placed} placed.");
            return removed;
        }

        private static bool intersectsPlot(Ground ground, Bounds b) =>
            b.max.x > ground.Plot.xMin && b.min.x < ground.Plot.xMax && b.max.z > ground.Plot.yMin && b.min.z < ground.Plot.yMax;

        private static Bounds bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void setStatic(GameObject go, bool occluder)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (occluder) flags |= StaticEditorFlags.OccluderStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        // ---------------- roads, pads, berms, trenches, decals, horizon ----------------

        /// <summary>
        /// The pack's world meshes are modelled in world space. Each one is copied, its vertices follow the
        /// reshaped terrain, and triangles on the tower plot are cut away. Unchanged meshes are used as they are.
        /// </summary>
        private static void placeWorldMeshes(Ground ground, Placements data, string packRoot, Transform parent)
        {
            var meshFolder = OutFolder + "/Meshes";
            if (AssetDatabase.IsValidFolder(meshFolder))
                AssetDatabase.DeleteAsset(meshFolder);
            ensureFolder(meshFolder);

            int reshaped = 0, dropped = 0, flush = 0;
            foreach (var it in data.unique)
            {
                var safe = it.k.Replace(".", "_");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{packRoot}/Models/World/{safe}.fbx");
                if (model == null) { Debug.LogWarning("[Vantage] Military pack: missing world mesh " + safe); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                go.name = safe;
                go.transform.localPosition = pos(it.p);
                go.transform.localRotation = rot(it.q);
                go.transform.localScale = scl(it.s);
                var flat = safe.StartsWith("Decal_") || safe.Contains("Markings") || safe.Contains("Distant");

                var keep = false;
                foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null) continue;
                    var result = reshape(ground, filter.transform, mesh, safe == "Terrain_Distant_Horizon");
                    if (result == null) { keep = true; continue; } // untouched
                    if (result.vertexCount == 0)
                    {
                        if (filter.gameObject != go)
                            Object.DestroyImmediate(filter.gameObject);
                        else
                        {
                            Object.DestroyImmediate(go.GetComponent<MeshCollider>());
                            Object.DestroyImmediate(go.GetComponent<Renderer>());
                            Object.DestroyImmediate(filter);
                        }
                        continue;
                    }
                    AssetDatabase.CreateAsset(result, $"{meshFolder}/{filter.name}_{mesh.name}.asset");
                    filter.sharedMesh = result;
                    var collider = filter.GetComponent<MeshCollider>();
                    if (collider != null) collider.sharedMesh = result;
                    keep = true;
                    reshaped++;
                }
                if (!keep) { Object.DestroyImmediate(go); dropped++; continue; }

                // Roads and pads lying flush on the terrain lose their collider: the player walks on the terrain
                // under them, so their kerbs and seams can't stop the template character (no step-up).
                if (!safe.Contains("Berm") && !safe.Contains("Trench"))
                    foreach (var collider in go.GetComponentsInChildren<MeshCollider>())
                        if (collider.sharedMesh != null && heightAboveTerrain(ground, collider.transform, collider.sharedMesh) < 0.35f)
                        {
                            Object.DestroyImmediate(collider);
                            flush++;
                        }
                if (flat || safe.Contains("Distant"))
                    go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

                if (flat)
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                setStatic(go, false);
            }
            Debug.Log($"[Vantage] Roads and pads: {reshaped} meshes fitted to the reshaped terrain, {dropped} dropped (inside the tower plot), {flush} flush colliders removed (walk on terrain).");
        }

        private static float heightAboveTerrain(Ground ground, Transform t, Mesh mesh)
        {
            var max = float.MinValue;
            foreach (var v in mesh.vertices)
            {
                var w = t.TransformPoint(v);
                max = Mathf.Max(max, w.y - ground.Height(w.x, w.z));
            }
            return max;
        }

        /// <summary>Returns null when the mesh needs no change, an empty mesh when nothing is left.</summary>
        private static Mesh reshape(Ground ground, Transform t, Mesh source, bool horizon)
        {
            var vertices = source.vertices;
            var world = new Vector3[vertices.Length];
            var inside = new bool[vertices.Length];
            var changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = t.TransformPoint(vertices[i]);
                if (!horizon)
                {
                    inside[i] = ground.OutsidePlot(w.x, w.z) < 0.5f;
                    var d = ground.Delta(w.x, w.z);
                    if (Mathf.Abs(d) > 0.005f) { w.y += d; changed = true; }
                }
                changed |= inside[i];
                world[i] = w;
            }
            if (!changed)
                return null;

            var mesh = Object.Instantiate(source);
            mesh.name = source.name;
            var local = new Vector3[vertices.Length];
            for (int i = 0; i < local.Length; i++) local[i] = t.InverseTransformPoint(world[i]);
            mesh.vertices = local;

            var total = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s);
                var kept = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                    if (!inside[tris[i]] && !inside[tris[i + 1]] && !inside[tris[i + 2]])
                        kept.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                mesh.SetTriangles(kept, s);
                total += kept.Count;
            }
            if (total == 0)
                return new Mesh();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void distantForest(Placements data, string packRoot, Transform parent)
        {
            var far = AssetDatabase.LoadAssetAtPath<GameObject>($"{packRoot}/Models/Vegetation/T_Far_Conifer.fbx");
            if (far == null || data.distant == null || data.distant.Length == 0)
                return;
            var renderer = parent.gameObject.AddComponent<DistantForestRenderer>();
            renderer.mesh = far.GetComponentInChildren<MeshFilter>().sharedMesh;
            renderer.material = far.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            if (renderer.material != null) renderer.material.enableInstancing = true;
            int n = data.distant.Length / 5;
            var d = new float[n * 5];
            for (int i = 0; i < n; i++)
            {
                int o = i * 5;
                d[o] = -data.distant[o]; d[o + 1] = data.distant[o + 2]; d[o + 2] = -data.distant[o + 1];
                d[o + 3] = -data.distant[o + 3] * Mathf.Rad2Deg; d[o + 4] = data.distant[o + 4];
            }
            renderer.data = d; // positions are local: the root's offset moves the horizon with the base
            renderer.Build();
        }

        /// <summary>Props that make sense to crouch behind, by pack model name.</summary>
        private static readonly string[] CoverModels =
        {
            "SandbagWall", "SandbagPosition", "JerseyBarrier", "ConcreteBlock", "Hesco", "TWall", "Container_20ft",
            "Crate_Wood", "PalletStack", "Dumpster", "Generator", "ObstacleWall", "MetalBarrier", "Bunker", "TireStack",
            "FuelTank", "Truck_6x6", "APC_8x8", "Utility_4x4", "CableReel", "EquipmentCase", "FiringPoint",
        };

        /// <summary>Cover Shooter covers on the base's cover props, turned with each prop.</summary>
        private static void addCovers(Transform root)
        {
            Physics.SyncTransforms();
            var container = group(root, "Covers"); // root is only translated, as the covers need
            int count = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null || t.parent.parent != root || t.name == "Covers")
                    continue; // only the placed models, one level below the category groups
                var name = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? t.name;
                if (CoverModels.Any(m => name.StartsWith(m)))
                    count += VantageCoverUtil.AddForOriented(t.gameObject, container);
            }
            Debug.Log($"[Vantage] Military base: {count} covers.");
        }

        /// <summary>Invisible walls 25 m inside the terrain's edge, so nobody walks off the world.</summary>
        private static void worldEdge(Ground ground, Transform parent)
        {
            const float inset = 25f, height = 150f, thick = 2f;
            var half = ground.Size / 2 - inset;
            var c = new Vector3(ground.Offset.x, ground.PlotHeight + height / 2 - 40f, ground.Offset.z);
            void wall(string name, Vector3 offset, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, true);
                go.transform.position = c + offset;
                go.AddComponent<BoxCollider>().size = size;
                go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            wall("Edge North", new Vector3(0, 0, half), new Vector3(half * 2, height, thick));
            wall("Edge South", new Vector3(0, 0, -half), new Vector3(half * 2, height, thick));
            wall("Edge East", new Vector3(half, 0, 0), new Vector3(thick, height, half * 2));
            wall("Edge West", new Vector3(-half, 0, 0), new Vector3(thick, height, half * 2));
        }

        /// <summary>
        /// Golden-hour haze: the sun and skybox stay as the level sets them; distance fog blends the
        /// 1 km world into the sky and keeps the far forest cheap to look at.
        /// </summary>
        private static void atmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0012f;
            RenderSettings.fogColor = new Color(0.78f, 0.71f, 0.62f);

            // Our own procedural sky (the old one came from the industrial pack). Warm, slightly hazy, and a
            // forest-coloured ground half, so gaps below the horizon read as distant land instead of a blue band.
            var path = OutFolder + "/Sky_GoldenHour.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.25f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.58f, 0.66f));
            sky.SetColor("_GroundColor", new Color(0.24f, 0.26f, 0.2f));
            sky.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
        }

        // ---------------- helpers ----------------

        private static Transform group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void ensureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            ensureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------- minimap ----------------

        private const string MinimapPath = OutFolder + "/Minimap.png";
        private const int MinimapPixels = 2048;

        /// <summary>
        /// Renders the whole terrain straight down (no fog, no post) into Minimap.png and sets up the scene's
        /// VantageMinimap with it, so the corner map costs one texture at runtime instead of a second camera.
        /// </summary>
        private static void bakeMinimap(Ground ground)
        {
            var area = new Rect(ground.Offset.x - ground.Size / 2, ground.Offset.z - ground.Size / 2, ground.Size, ground.Size);
            ShaderUtil.allowAsyncCompilation = false;
            var fog = RenderSettings.fog;
            RenderSettings.fog = false;
            // The golden-hour sun is too low to light a top-down view; use a high neutral sun for the bake only.
            var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            var sunRotation = sun != null ? sun.transform.rotation : Quaternion.identity;
            var sunColor = sun != null ? sun.color : Color.white;
            var sunIntensity = sun != null ? sun.intensity : 1f;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(65f, 30f, 0f);
                sun.color = new Color(1f, 0.97f, 0.92f);
                sun.intensity = 1.3f;
            }
            var go = new GameObject("Minimap Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = ground.Size / 2;
                cam.aspect = 1f;
                cam.transform.position = new Vector3(area.center.x, ground.Offset.y + ground.HMax + 200f, area.center.y);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = ground.HMax - ground.HMin + 400f;
                cam.useOcclusionCulling = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.15f, 0.1f);
                var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
                data.renderPostProcessing = false;

                var rt = new RenderTexture(MinimapPixels, MinimapPixels, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(MinimapPixels, MinimapPixels, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, MinimapPixels, MinimapPixels), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                stretchLevels(tex);
                File.WriteAllBytes(full(MinimapPath), tex.EncodeToPNG());
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            finally
            {
                Object.DestroyImmediate(go);
                RenderSettings.fog = fog;
                if (sun != null)
                {
                    sun.transform.rotation = sunRotation;
                    sun.color = sunColor;
                    sun.intensity = sunIntensity;
                }
            }

            AssetDatabase.ImportAsset(MinimapPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(MinimapPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = MinimapPixels;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            var old = GameObject.Find(MinimapName);
            if (old != null)
                Object.DestroyImmediate(old);
            var map = new GameObject(MinimapName).AddComponent<VantageMinimap>();
            map.Map = AssetDatabase.LoadAssetAtPath<Texture2D>(MinimapPath);
            map.WorldArea = area;
            var tower = VantageCommandTower.FindTower();
            map.Tower = tower != null ? tower.transform : null;
            Debug.Log($"[Vantage] Minimap baked ({MinimapPixels}px over {ground.Size:F0} m) to {MinimapPath}.");
        }

        /// <summary>
        /// Exposure: scales brightness so the median (mostly ground) lands on a mid-tone, gain capped at 2.5,
        /// so the map reads at a glance without washing out. A percentile stretch fails here because almost the
        /// whole image is ground of one brightness.
        /// </summary>
        private static void stretchLevels(Texture2D tex)
        {
            var px = tex.GetPixels();
            var lum = px.Select(c => c.grayscale).OrderBy(v => v).ToArray();
            var median = Mathf.Max(0.01f, lum[lum.Length / 2]);
            var gain = Mathf.Clamp(0.32f / median, 0.6f, 2.5f);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i] * gain;
                px[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b));
            }
            tex.SetPixels(px);
            tex.Apply();
            Debug.Log($"[Vantage] Minimap exposure: median {median:F2}, gain {gain:F2}.");
        }

        private static void screenshots(string folder, Ground ground)
        {
            Directory.CreateDirectory(folder);
            var commandTower = VantageCommandTower.FindTower();
            if (commandTower != null)
            {
                var t = commandTower.transform;
                VantageEditorUtil.Shot(folder, "tower_1_aerial", t.TransformPoint(new Vector3(35f, 45f, 60f)), t.TransformPoint(new Vector3(0, 12f, 0)), false, 0);
                VantageEditorUtil.Shot(folder, "tower_2_entrance", t.TransformPoint(new Vector3(4f, 2f, 22f)), t.TransformPoint(new Vector3(0, 1.2f, 10f)), false, 0);
                VantageEditorUtil.Shot(folder, "tower_3_lobby", t.TransformPoint(new Vector3(0f, 2.1f, 8f)), t.TransformPoint(new Vector3(0, 1.5f, -4f)), false, 0);
                int n = 0;
                foreach (var gate in Object.FindObjectsByType<VantageFloorGate>(FindObjectsSortMode.None).OrderBy(g => g.Level).ThenBy(g => g.name))
                {
                    var panel = gate.Panel;
                    VantageEditorUtil.Shot(folder, $"tower_gate_{++n}_L{gate.Level + 1}_{(gate.name.Contains("Fire") ? "fire_escape" : "stairs")}", panel.position - panel.forward * 4.5f + Vector3.up * 0.6f + panel.right * 1.2f, panel.position, false, 0);
                }
            }
            var tower = commandTower != null ? commandTower.transform : null;
            var towerPos = tower != null ? tower.position : ground.TowerSite;
            var baseCentre = ground.Offset + Vector3.up * ground.PlotHeight;
            var player = Object.FindFirstObjectByType<CoverShooter.ThirdPersonInput>();

            VantageEditorUtil.Shot(folder, "world_1_aerial", towerPos + new Vector3(120f, 110f, -150f), Vector3.Lerp(towerPos, baseCentre, 0.4f), false, 0);
            // From the roof edge across the base towards the headquarters (north-east).
            VantageEditorUtil.Shot(folder, "world_2_roof_view", towerPos + new Vector3(5f, 18.5f, 5f), towerPos + new Vector3(70f, 2f, 90f), false, 0);
            if (player != null)
            {
                var p = player.transform.position;
                VantageEditorUtil.Shot(folder, "world_3_spawn", p - player.transform.forward * 4f + Vector3.up * 2.2f, p + player.transform.forward * 20f + Vector3.up * 1.6f, false, 0);
            }
            VantageEditorUtil.Shot(folder, "world_4_plot_aerial", towerPos + new Vector3(-50f, 55f, -60f), towerPos, false, 0);
            var street = new Vector3(baseCentre.x + 10f, ground.Height(baseCentre.x + 10f, baseCentre.z - 30f) + 1.7f, baseCentre.z - 30f);
            VantageEditorUtil.Shot(folder, "world_5_base_street", street, towerPos + Vector3.up * 8f, false, 0);
            Debug.Log("[Vantage] Military world screenshots written to " + folder);
        }
    }
}
