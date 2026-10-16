using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Top-down floor plans of the tower for the documentation and presentations: one orthographic render per level
    /// with everything above the floor's head height cut away (the camera hangs inside the floor, the near plane does
    /// the cut), the entrance at the bottom of the image. Next to each image a JSON lists every named tower and setup
    /// object with its world and pixel position, so maps can be annotated without guessing coordinates.
    /// With -vantageRoutes file (lines "floor1_ground: A > B > C", object names) it also writes the NavMesh
    /// walking path between those objects in pixels (name_routes.json); locked doors don't carve in the editor, so
    /// the paths go through them as the player will once they open.
    /// </summary>
    public static class VantageFloorPlans
    {
        private const int Width = 3200, Height = 2400;
        private const float CutAboveFloor = 2.4f;

        [MenuItem("Vantage/Documentation/Render Floor Plans", priority = 200)]
        public static void RenderFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Floor plans", "Documentation", "");
            if (!string.IsNullOrEmpty(folder)) Render(folder);
        }

        /// <summary>Batch: -executeMethod Vantage.EditorTools.VantageFloorPlans.RenderBatch -vantageShots dir</summary>
        public static void RenderBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                Render(VantageEditorUtil.Arg("-vantageShots") ?? "Documentation/floor_plans");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Batch: for every door, what else is solid in its opening (door colliders ignored), how wide the opening is,
        /// and whether the baked NavMesh crosses it. Read-only.
        /// </summary>
        public static void DoorReportBatch()
        {
            EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
            var tower = VantageTower.FindTower();
            foreach (var door in tower.GetComponentsInChildren<VantageDoor>(true))
            {
                if (!VantageTower.WorldBounds(door.transform, out var b)) continue;
                var own = new HashSet<Collider>(door.GetComponentsInChildren<Collider>(true));
                var floor = b.min.y;
                var hits = new List<string>();
                foreach (var c in Physics.OverlapBox(new Vector3(b.center.x, floor + 0.9f, b.center.z), new Vector3(b.extents.x * 0.6f, 0.6f, b.extents.z * 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (!own.Contains(c)) hits.Add(c.name);
                var thin = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
                var onNav = UnityEngine.AI.NavMesh.SamplePosition(new Vector3(b.center.x, floor, b.center.z), out var nh, 0.4f, UnityEngine.AI.NavMesh.AllAreas);
                var path = new UnityEngine.AI.NavMeshPath();
                var c0 = new Vector3(b.center.x, floor, b.center.z);
                var crosses = UnityEngine.AI.NavMesh.SamplePosition(c0 - thin * 1.2f, out var ha, 0.8f, UnityEngine.AI.NavMesh.AllAreas)
                              && UnityEngine.AI.NavMesh.SamplePosition(c0 + thin * 1.2f, out var hb, 0.8f, UnityEngine.AI.NavMesh.AllAreas)
                              && UnityEngine.AI.NavMesh.CalculatePath(ha.position, hb.position, UnityEngine.AI.NavMesh.AllAreas, path)
                              && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                Debug.Log($"[Vantage] Door {door.name}: size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}, locked {door.locked}, NavMesh in the opening {onNav}, crosses {crosses}, solid in the opening: {(hits.Count > 0 ? string.Join(", ", hits) : "nothing")}");
            }
            EditorApplication.Exit(0);
        }

        public static void Render(string folder)
        {
            Directory.CreateDirectory(folder);
            var tower = VantageTower.FindTower();
            if (tower == null) { Debug.LogError("[Vantage] Floor plans: no VANTAGE_Tower in the scene."); return; }
            var t = tower.transform;
            VantageTower.WorldBounds(t, out var b);
            var facing = VantageTower.EntranceFacing(t);
            var rotation = Quaternion.LookRotation(Vector3.down, -facing);
            var heights = VantageTower.LevelHeights(t);
            var size = Mathf.Max(b.extents.x, b.extents.z) * 1.08f;

            // Shadows off: the ceilings we cut away would still shade the floors.
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var shadows = new LightShadows[lights.Length];
            for (int i = 0; i < lights.Length; i++) { shadows[i] = lights[i].shadows; lights[i].shadows = LightShadows.None; }
            var ambient = RenderSettings.ambientIntensity;
            RenderSettings.ambientIntensity = Mathf.Max(ambient, 1.2f);
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var names = new[] { "floor1_ground", "floor2_armory", "floor3_destroyed" };
                for (int i = 0; i < 3; i++)
                {
                    var eye = new Vector3(b.center.x, heights[i] + CutAboveFloor, b.center.z);
                    if (i == 0) render(folder, names[i], eye, rotation, size, t, false); // first render after loading can be garbled
                    render(folder, names[i], eye, rotation, size, t, true);
                }
                render(folder, "floor4_helipad", new Vector3(b.center.x, b.max.y + 40f, b.center.z), rotation, size * 1.15f, t, false);
            }
            finally
            {
                for (int i = 0; i < lights.Length; i++) lights[i].shadows = shadows[i];
                RenderSettings.ambientIntensity = ambient;
            }
            Debug.Log("[Vantage] Floor plans written to " + folder);
        }

        private static void render(string folder, string name, Vector3 eye, Quaternion rotation, float size, Transform tower, bool withSection)
        {
            var go = new GameObject("Floor Plan Camera");
            var rt = new RenderTexture(Width, Height, 24);
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(eye, rotation);
                cam.orthographic = true;
                cam.orthographicSize = size;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 500f;
                cam.useOcclusionCulling = false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, name + ".json"), points(cam, tower, eye.y));
                var routes = routesFor(cam, name);
                if (routes != null) File.WriteAllText(Path.Combine(folder, name + "_routes.json"), routes);
                if (withSection)
                {
                    File.WriteAllBytes(Path.Combine(folder, name + "_section.png"), section(cam, eye.y - CutAboveFloor));
                    File.WriteAllText(Path.Combine(folder, name + "_navmesh.json"), navmesh(cam, eye.y - CutAboveFloor));
                }
                cam.targetTexture = null;
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// The baked NavMesh triangles of this floor (centre between 0.6 m below and 2 m above the floor) in pixels,
        /// each with the id of its connected region: which parts of the floor can be walked to from which.
        /// </summary>
        private static string navmesh(Camera cam, float floorY)
        {
            var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            var key = new Dictionary<Vector3Int, int>();
            var weld = new int[tri.vertices.Length];
            for (int i = 0; i < weld.Length; i++)
            {
                var k = Vector3Int.RoundToInt(tri.vertices[i] * 20f);
                if (!key.TryGetValue(k, out weld[i])) key[k] = weld[i] = i;
            }
            var parent = new int[tri.vertices.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                parent[find(weld[tri.indices[i]])] = find(weld[tri.indices[i + 1]]);
                parent[find(weld[tri.indices[i + 1]])] = find(weld[tri.indices[i + 2]]);
            }
            var sb = new StringBuilder("{\"triangles\":[");
            var first = true;
            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                var a = tri.vertices[tri.indices[i]]; var b = tri.vertices[tri.indices[i + 1]]; var c = tri.vertices[tri.indices[i + 2]];
                var cy = (a.y + b.y + c.y) / 3f;
                if (cy < floorY - 0.6f || cy > floorY + 2f) continue;
                var pa = cam.WorldToScreenPoint(a); var pb = cam.WorldToScreenPoint(b); var pc = cam.WorldToScreenPoint(c);
                if (!first) sb.Append(',');
                first = false;
                sb.Append(string.Format(CultureInfo.InvariantCulture, "[{0},{1:F0},{2:F0},{3:F0},{4:F0},{5:F0},{6:F0}]", find(weld[tri.indices[i]]),
                                        pa.x, Height - pa.y, pb.x, Height - pb.y, pc.x, Height - pc.y));
            }
            return sb.Append("]}").ToString();
        }

        /// <summary>NavMesh paths for this image's lines in the -vantageRoutes file, as {"routes":[[[px,py],...],...]}.</summary>
        private static string routesFor(Camera cam, string image)
        {
            var file = VantageEditorUtil.Arg("-vantageRoutes");
            if (file == null || !File.Exists(file)) return null;
            var sb = new StringBuilder("{\"routes\":[");
            var firstRoute = true;
            foreach (var line in File.ReadAllLines(file))
            {
                var colon = line.IndexOf(':');
                if (colon < 0 || line.Substring(0, colon).Trim() != image) continue;
                var names = line.Substring(colon + 1).Split('>');
                var corners = new List<Vector3>();
                for (int i = 0; i + 1 < names.Length; i++)
                {
                    var a = locate(names[i].Trim());
                    var b = locate(names[i + 1].Trim());
                    if (a == null || b == null) { Debug.LogWarning($"[Vantage] Route: '{names[i].Trim()}' or '{names[i + 1].Trim()}' not found"); continue; }
                    var path = new UnityEngine.AI.NavMeshPath();
                    UnityEngine.AI.NavMesh.CalculatePath(a.Value, b.Value, UnityEngine.AI.NavMesh.AllAreas, path);
                    if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                        Debug.LogWarning($"[Vantage] Route {image}: {names[i].Trim()} -> {names[i + 1].Trim()} is {path.status}");
                    corners.AddRange(path.corners);
                }
                if (!firstRoute) sb.Append(',');
                firstRoute = false;
                sb.Append('[');
                for (int i = 0; i < corners.Count; i++)
                {
                    var s = cam.WorldToScreenPoint(corners[i]);
                    if (i > 0) sb.Append(',');
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},{2:F2}]", s.x, Height - s.y, corners[i].y));
                }
                sb.Append(']');
            }
            return sb.Append("]}").ToString();
        }

        /// <summary>The NavMesh point nearest a named tower or setup object (its renderer centre, else its pivot).</summary>
        private static Vector3? locate(string name)
        {
            Transform found = null;
            var tower = VantageTower.FindTower();
            if (tower != null) found = VantageTower.Find(tower.transform, name);
            var setup = GameObject.Find(VantageTower.SetupName);
            if (found == null && setup != null) found = VantageTower.Find(setup.transform, name);
            if (found == null) return null;
            var p = VantageTower.WorldBounds(found, out var b) ? new Vector3(b.center.x, b.min.y, b.center.z) : found.position;
            return UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position : (Vector3?)null;
        }

        /// <summary>
        /// Cross-section of the colliders, same framing as the render: red = solid at 2.2 m above the floor (walls,
        /// tall shelves), green = solid only at 0.6 m (furniture, crates, barriers). The rendered walls are cut by the
        /// camera and invisible from above; this mask draws them. Two pixels per sample.
        /// </summary>
        private static byte[] section(Camera cam, float floorY)
        {
            const int step = 2;
            var mask = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[Width * Height];
            var r = cam.orthographicSize * 2f / Height * step * 0.5f;
            for (int py = 0; py < Height; py += step)
                for (int px = 0; px < Width; px += step)
                {
                    var w = cam.ScreenToWorldPoint(new Vector3(px + step * 0.5f, py + step * 0.5f, 1f));
                    var high = Physics.CheckSphere(new Vector3(w.x, floorY + 2.2f, w.z), r, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
                    var low = !high && Physics.CheckSphere(new Vector3(w.x, floorY + 0.6f, w.z), r, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
                    var c = high ? new Color32(255, 0, 0, 255) : low ? new Color32(0, 255, 0, 255) : clear;
                    for (int dy = 0; dy < step && py + dy < Height; dy++)
                        for (int dx = 0; dx < step && px + dx < Width; dx++)
                            pixels[(py + dy) * Width + px + dx] = c;
                }
            mask.SetPixels32(pixels);
            mask.Apply();
            var png = mask.EncodeToPNG();
            Object.DestroyImmediate(mask);
            return png;
        }

        /// <summary>Every named object of the tower and its setup: name, world position, pixel (top-left origin).</summary>
        private static string points(Camera cam, Transform tower, float cutY)
        {
            var roots = new List<Transform> { tower };
            var setup = GameObject.Find(VantageTower.SetupName);
            if (setup != null) roots.Add(setup.transform);
            var sb = new StringBuilder();
            sb.Append("{\"cutY\":").Append(cutY.ToString("F2", CultureInfo.InvariantCulture)).Append(",\"points\":[");
            var first = true;
            foreach (var root in roots)
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    var p = VantageTower.WorldBounds(tr, out var wb) ? wb.center : tr.position;
                    var s = cam.WorldToScreenPoint(p);
                    if (s.x < 0 || s.y < 0 || s.x > Width || s.y > Height) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"name\":\"").Append(tr.name.Replace("\\", "/").Replace("\"", "'")).Append("\",\"parent\":\"")
                      .Append(tr.parent != null ? tr.parent.name.Replace("\"", "'") : "").Append("\",")
                      .Append(string.Format(CultureInfo.InvariantCulture, "\"x\":{0:F2},\"y\":{1:F2},\"z\":{2:F2},\"px\":{3:F1},\"py\":{4:F1},\"minY\":{5:F2},\"maxY\":{6:F2}",
                                            p.x, p.y, p.z, s.x, Height - s.y, wb.min.y, wb.max.y)).Append('}');
                }
            return sb.Append("]}").ToString();
        }
    }
}
