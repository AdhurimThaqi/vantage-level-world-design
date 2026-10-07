using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Sets up the user's Blender command tower (Assets/Blender_Asset/CommandTower_Unity) as the level's main building.
    /// </summary>
    public static class VantageCommandTower
    {
        public const string InstanceName = "CommandTower";

        public const string SetupName = "VANTAGE Tower Setup";
        /// <summary>Root name of the old generated blockout tower; removed if a scene still has it.</summary>
        public const string LegacyTowerName = "VANTAGE Tower";
        private const string Prefabs = "Assets/ThirdPersonCoverShooter/Assets/Prefabs/";
        private const float GroundFloor = 0.45f, Storey = 4f;

        public static GameObject FindTower()
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == InstanceName || root.name.StartsWith(InstanceName))
                    return root;
            return null;
        }

        [MenuItem("Vantage/Tower/Set Up Command Tower (levels, gates, enemies)", priority = 80)]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Makes the command tower the level: removes the old generated tower, sets up lights, entrance ramps,
        /// gates between the four levels, furniture cover, the level manager, pickups and the player start, and
        /// bakes the NavMesh. Safe to re-run: everything it adds lives under "VANTAGE Tower Setup".
        /// </summary>
        public static void Setup()
        {
            var tower = FindTower();
            if (tower == null)
                throw new Exception("No CommandTower in the scene. Drag Assets/Blender_Asset/CommandTower_Unity/Prefabs/CommandTower.prefab in first.");
            var t = tower.transform;

            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == LegacyTowerName || root.name == "VANTAGE Enemies" || root.name == SetupName)
                {
                    Debug.Log($"[Vantage] Removed '{root.name}'.");
                    Object.DestroyImmediate(root);
                }

            var setup = new GameObject(SetupName).transform;
            staticFlags(t);
            sunAngle();
            realtimeLights(tower);
            entranceRamps(t, group(setup, "Entrance Ramps"));
            smoothStairs(t, group(setup, "Stair Surfaces"));
            var gates = buildGates(t, group(setup, "Level Gates"));
            furnitureCover(t, group(setup, "Furniture Covers"));
            var start = playerStart(t);
            var levels = levelManager(t, setup, gates, start);
            pickups(setup, start, gates, levels);
            pointToolsAtTower(t);

            VantageSetup.BakeNavMesh();
            EditorSceneManager.MarkSceneDirty(tower.scene);
            Debug.Log($"[Vantage] Command tower set up: {gates.Count} gates, {levels.Levels.Count} levels.");
        }

        /// <summary>
        /// -executeMethod Vantage.EditorTools.VantageCommandTower.SetupBatch [-vantageShots folder]
        /// Re-runs the tower setup only (not the world), saves, and renders the entrance, lobby and every gate.
        /// </summary>
        public static void SetupBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-vantageShots");
            var folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            var exitCode = 0;
            try
            {
                var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                Setup();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                VantageEditorUtil.BakeOcclusion();
                EditorSceneManager.SaveScene(scene);
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

        /// <summary>Entrance, lobby and a view of every gate, for checking the setup without playing.</summary>
        public static void Shots(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            var t = FindTower().transform;
            VantageEditorUtil.Shot(folder, "tower_2_entrance", t.TransformPoint(new Vector3(4f, 2f, 22f)), t.TransformPoint(new Vector3(0, 1.2f, 10f)), false, 0);
            VantageEditorUtil.Shot(folder, "tower_3_lobby", t.TransformPoint(new Vector3(0f, 2.1f, 8f)), t.TransformPoint(new Vector3(0, 1.5f, -4f)), false, 0);
            int n = 0;
            Physics.SyncTransforms();
            foreach (var gate in Object.FindObjectsByType<VantageFloorGate>(FindObjectsSortMode.None).OrderBy(g => g.Level).ThenBy(g => g.name))
            {
                var panel = gate.Panel;
                var dir = panel.forward;
                var bottom = panel.position + dir * 0.4f - Vector3.up * 1.3f;
                // From the floor before the flight, at eye height, looking up the stairs.
                VantageEditorUtil.Shot(folder, $"tower_gate_{++n}_L{gate.Level + 1}_{(gate.name.Contains("Fire") ? "fire_escape" : "stairs")}",
                    bottom - dir * 3.5f + Vector3.up * 1.7f, bottom + dir * 2f + Vector3.up * 1.2f, false, 0);

                // Check: the walking surface of the flight is blocked, the floor in front of it is not.
                var box = gate.Blocker;
                bool inside(Vector3 p) => (box.ClosestPoint(p) - p).sqrMagnitude < 1e-6f;
                int blocked = 0, samples = 0;
                var run = box.size.z - 1f;
                for (float k = 0.3f; k < run; k += 1f)
                {
                    var probe = bottom + dir * k + Vector3.up * 6f;
                    var hits = Physics.RaycastAll(probe, Vector3.down, 12f).Where(h => h.collider != box && h.normal.y > 0.5f).OrderBy(h => h.distance).ToList();
                    if (hits.Count == 0) continue;
                    var p = hits.Select(h => h.point).FirstOrDefault(q => q.y < bottom.y + (box.size.y - 3f) + 0.3f && q.y > bottom.y - 0.3f);
                    if (p == default) continue;
                    samples++;
                    if (inside(p + Vector3.up * 0.9f)) blocked++;
                }
                var before = bottom - dir * 1.5f + Vector3.up * 0.9f;
                Debug.Log($"[Vantage] Check {gate.name}: flight blocked at {blocked}/{samples} samples, floor before the flight {(inside(before) ? "BLOCKED (wrong)" : "free")}, bottom at local y {t.InverseTransformPoint(bottom).y:F2}.");
            }
            Debug.Log("[Vantage] Tower screenshots written to " + folder);
        }

        /// <summary>
        /// Stair profile: along each ramp piece, how far the other geometry (the real steps, landings) sticks up above
        /// the smooth ramp surface. Anything above ~3 cm can catch the template character's capsule.
        /// -executeMethod Vantage.EditorTools.VantageCommandTower.StairProfileBatch
        /// </summary>
        public static void StairProfileBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                var t = FindTower().transform;
                Physics.SyncTransforms();
                var stairs = t.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.Contains("StairColliders"));
                var rampCollider = stairs.GetComponent<Collider>();
                Debug.Log($"[Vantage] Ramp collider: {(rampCollider != null ? rampCollider.GetType().Name + " enabled=" + rampCollider.enabled + " layer " + stairs.gameObject.layer : "NONE")}");
                var fe = t.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.Contains("FireEscape"));
                var feCol = fe.GetComponent<MeshCollider>();
                Debug.Log($"[Vantage] FireEscape: render tris {fe.sharedMesh.triangles.Length / 3}, collider {(feCol != null ? $"{(feCol.sharedMesh != null ? feCol.sharedMesh.triangles.Length / 3 : -1)} tris convex={feCol.convex} enabled={feCol.enabled} same mesh={feCol.sharedMesh == fe.sharedMesh}" : "NONE")}, bounds r {fe.GetComponent<Renderer>().bounds.size} c {(feCol != null ? feCol.bounds.size : Vector3.zero)}");
                foreach (var probe in new[] { new Vector3(10.5f, 4.47f, -14f), new Vector3(11.5f, 4.47f, -12.4f), new Vector3(-10.5f, 4.47f, -12.4f), new Vector3(-10.5f, 4.47f, -14f), new Vector3(0f, 2.5f, -12.4f), new Vector3(0f, 6.5f, -14f) })
                {
                    var hits = Physics.RaycastAll(t.TransformPoint(probe + Vector3.up * 1f), Vector3.down, 2f).OrderBy(h => h.distance).Select(h => $"{t.InverseTransformPoint(h.point).y:F2} {h.collider.name}");
                    Debug.Log($"[Vantage]   probe {probe}: {string.Join(" | ", hits)}");
                }
                foreach (var piece in rampPieces(t, stairs).Where(p => p.bottom.y < 9f).OrderBy(p => p.bottom.y))
                {
                    var flat = piece.top - piece.bottom; flat.y = 0;
                    var dir = flat.normalized;
                    var side = Vector3.Cross(Vector3.up, dir);
                    var rise = piece.top.y - piece.bottom.y;
                    float worst = -1f; string worstWhat = "";
                    int above = 0, samples = 0;
                    var rows = new List<string>();
                    for (float k = -0.6f; k <= flat.magnitude + 0.6f; k += 0.1f)
                        foreach (var lateral in new[] { -0.35f, 0f, 0.35f })
                        {
                            var local = piece.bottom + dir * k + side * lateral * piece.width;
                            var rampY = piece.bottom.y + Mathf.Clamp01(k / flat.magnitude) * rise;
                            var world = t.TransformPoint(local + Vector3.up * 1.5f);
                            var hits = Physics.RaycastAll(world, Vector3.down, 3f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore);
                            var top = hits.Where(h => h.collider != rampCollider).OrderBy(h => h.distance).FirstOrDefault();
                            if (top.collider == null) continue;
                            samples++;
                            var dy = t.InverseTransformPoint(top.point).y - rampY;
                            if (dy > 0.03f) above++;
                            if (dy > worst) { worst = dy; worstWhat = $"{top.collider.name} at k={k:F1} lateral={lateral:F2}"; }
                            if (lateral == 0f && Mathf.Abs(k * 10 - Mathf.Round(k * 10)) < 0.01f && Mathf.RoundToInt(k * 10) % 3 == 0)
                                rows.Add($"{k:F1}:{dy:+0.00;-0.00}");
                        }
                    Debug.Log($"[Vantage] {piece.side} flight y {piece.bottom.y:F2}->{piece.top.y:F2} run {flat.magnitude:F1} width {piece.width:F1}: geometry above ramp at {above}/{samples} samples, worst +{worst:F2} m ({worstWhat})");
                    Debug.Log($"[Vantage]   centre line (distance:height above ramp): {string.Join(" ", rows)}");
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
        /// Finds z-fighting: surfaces that lie on top of another surface (within 3 mm, facing the same way), in the
        /// tower and in the world's roads/decals/pads (against the terrain). Logs the worst offenders.
        /// -executeMethod Vantage.EditorTools.VantageCommandTower.ZFightBatch
        /// </summary>
        public static void ZFightBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                // Roads, markings and decals have no colliders (removed for walking); give them temporary ones so
                // they take part in the scan. The scene is not saved.
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (r.GetComponent<Collider>() == null && r.GetComponent<MeshFilter>()?.sharedMesh != null && fullName(r.transform).Contains("Roads, Pads"))
                        r.gameObject.AddComponent<MeshCollider>();
                Physics.SyncTransforms();
                var results = new List<(string name, int hits, int samples, string with)>();
                var terrain = Object.FindFirstObjectByType<Terrain>();
                foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    var mesh = mf.sharedMesh;
                    var r = mf.GetComponent<Renderer>();
                    if (mesh == null || r == null || !r.enabled) continue;
                    Vector3[] v; int[] tri;
                    try { v = mesh.vertices; tri = mesh.triangles; } catch { continue; }
                    if (tri.Length == 0) continue;
                    var own = mf.GetComponent<Collider>();
                    int samples = 0, fights = 0;
                    var with = new Dictionary<string, int>();
                    var stride = Mathf.Max(1, tri.Length / 3 / 400) * 3;
                    for (int i = 0; i < tri.Length; i += stride)
                    {
                        var a = mf.transform.TransformPoint(v[tri[i]]);
                        var b = mf.transform.TransformPoint(v[tri[i + 1]]);
                        var c = mf.transform.TransformPoint(v[tri[i + 2]]);
                        var n = Vector3.Cross(b - a, c - a);
                        if (n.sqrMagnitude < 1e-8f) continue;
                        n.Normalize();
                        var centre = (a + b + c) / 3f;
                        samples++;
                        // Another collider's surface at the same spot facing the same way?
                        foreach (var h in Physics.RaycastAll(centre + n * 0.02f, -n, 0.025f, ~0, QueryTriggerInteraction.Ignore))
                            if (h.collider != own && Mathf.Abs(h.distance - 0.02f) < 0.003f && Vector3.Dot(h.normal, n) > 0.95f)
                            {
                                fights++;
                                var sameInstance = PrefabUtility.GetOutermostPrefabInstanceRoot(h.collider.gameObject) == PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject)
                                                   && PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject) != null;
                                var key = (sameInstance ? "SAME MODEL " : "") + h.collider.name + "/" + submeshMaterial(h);
                                key += " <- " + r.sharedMaterials[Mathf.Min(submeshOf(mesh, i), r.sharedMaterials.Length - 1)]?.name;
                                with[key] = with.TryGetValue(key, out var w) ? w + 1 : 1;
                                break;
                            }
                        // Flat pieces lying on the terrain.
                        if (terrain != null && n.y > 0.95f && !mf.transform.IsChildOf(terrain.transform))
                        {
                            var ty = terrain.SampleHeight(centre) + terrain.transform.position.y;
                            if (Mathf.Abs(centre.y - ty) < 0.02f) { fights++; with["Terrain"] = with.TryGetValue("Terrain", out var w) ? w + 1 : 1; }
                        }
                    }
                    if (fights > 0) results.Add((fullName(mf.transform), fights, samples, string.Join(", ", with.OrderByDescending(x => x.Value).Take(3).Select(x => x.Key + " x" + x.Value))));
                }
                // Group by object type (name without parents and clone numbers).
                foreach (var g in results.GroupBy(r => System.Text.RegularExpressions.Regex.Replace(r.name.Split('/').Last(), @"( \(\d+\)|_\d+)$", ""))
                                         .OrderByDescending(g => g.Sum(r => r.hits)).Take(30))
                    Debug.Log($"[Vantage] Z-fight {g.Key}: {g.Count()} objects, {g.Sum(r => r.hits)}/{g.Sum(r => r.samples)} samples; e.g. {g.First().name} overlaps {g.First().with}");
                Debug.Log($"[Vantage] Z-fight scan: {results.Count} meshes with coincident surfaces.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        private static int submeshOf(Mesh mesh, int triangleStart)
        {
            int index = triangleStart;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var d = mesh.GetSubMesh(s);
                if (index >= d.indexStart && index < d.indexStart + d.indexCount) return s;
            }
            return 0;
        }

        private static string submeshMaterial(RaycastHit h)
        {
            var mc = h.collider as MeshCollider;
            var r = h.collider.GetComponent<Renderer>();
            if (mc == null || mc.sharedMesh == null || r == null || h.triangleIndex < 0) return "?";
            var s = submeshOf(mc.sharedMesh, h.triangleIndex * 3);
            return r.sharedMaterials.Length > s && r.sharedMaterials[s] != null ? r.sharedMaterials[s].name : "?";
        }

        private static string fullName(Transform t)
        {
            var name = t.name;
            for (var p = t.parent; p != null && name.Length < 160; p = p.parent) name = p.name + "/" + name;
            return name;
        }

        /// <summary>
        /// Walk-line check over the finished setup: along the centre of every gated flight (foot to top, plus a
        /// metre either side) the top walkable surface every 5 cm, listing every step up of more than 4 cm.
        /// -executeMethod Vantage.EditorTools.VantageCommandTower.WalkLineBatch
        /// </summary>
        public static void WalkLineBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                var t = FindTower().transform;
                foreach (var g in Object.FindObjectsByType<VantageFloorGate>(FindObjectsSortMode.None)) g.Blocker.enabled = false;
                Physics.SyncTransforms();
                var stairs = t.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.Contains("StairColliders"));
                foreach (var piece in rampPieces(t, stairs).Where(p => p.bottom.y < 9f).OrderBy(p => p.bottom.y))
                {
                    var flat = piece.top - piece.bottom; flat.y = 0;
                    var dir = flat.normalized;
                    var steps = new List<string>();
                    float? last = null; var lastK = 0f;
                    var line = new List<string>();
                    for (float k = -1f; k <= flat.magnitude + 1f; k += 0.05f)
                    {
                        var local = piece.bottom + dir * k;
                        var expected = piece.bottom.y + Mathf.Clamp01(k / flat.magnitude) * (piece.top.y - piece.bottom.y);
                        float best = float.NegativeInfinity; string what = "";
                        foreach (var h in Physics.RaycastAll(t.TransformPoint(new Vector3(local.x, expected + 1.2f, local.z)), Vector3.down, 3f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
                        {
                            if (h.normal.y < 0.5f) continue;
                            var y = t.InverseTransformPoint(h.point).y;
                            if (y <= expected + 0.6f && y > best) { best = y; what = h.collider.name; }
                        }
                        if (float.IsNegativeInfinity(best)) { line.Add($"{k:F2}:hole"); last = null; continue; }
                        if (last.HasValue && best - last.Value > 0.04f) steps.Add($"k={k:F2} +{best - last.Value:F2} onto {what}");
                        if (Mathf.Abs(k * 4 - Mathf.Round(k * 4)) < 0.01f) line.Add($"{k:F2}:{best:F2}");
                        last = best; lastK = k;
                    }
                    Debug.Log($"[Vantage] Walk {piece.side} {piece.bottom.y:F2}->{piece.top.y:F2} (foot {piece.bottom:F1}): {(steps.Count == 0 ? "no steps" : string.Join("; ", steps.Take(8)))}");
                    Debug.Log($"[Vantage]   {string.Join(" ", line)}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        private static Transform group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// The prefab has no static flags, so Unity drew every storey every frame. Structure and glass occlude and
        /// are occluded, furniture is occluded; all three batch statically. Doors move, so they stay dynamic.
        /// </summary>
        private static void staticFlags(Transform t)
        {
            int count = 0;
            void mark(string group, StaticEditorFlags flags)
            {
                var g = t.Find(group);
                if (g == null) return;
                foreach (var x in g.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(x.gameObject, flags);
                    count++;
                }
            }
            mark("Structure", StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            mark("Furniture", StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            Debug.Log($"[Vantage] Static flags on {count} tower objects (doors stay dynamic).");
        }

        /// <summary>
        /// Golden hour at 24° instead of 14°: at grazing angles the shadow map smears across walls and floors
        /// (shadow acne, the flickering "shattering" on surfaces). Same direction, a little higher.
        /// </summary>
        private static void sunAngle()
        {
            var sun = RenderSettings.sun != null ? RenderSettings.sun
                : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            if (sun == null) return;
            var e = sun.transform.eulerAngles;
            sun.transform.rotation = Quaternion.Euler(24f, e.y, 0f);
            sun.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(sun);
        }

        /// <summary>The tower's lights are Baked; without a lightmap bake they light nothing in URP. Realtime, no shadows.</summary>
        private static void realtimeLights(GameObject tower)
        {
            var lights = tower.GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.shadows = LightShadows.None;
                EditorUtility.SetDirty(l);
            }
            Debug.Log($"[Vantage] {lights.Length} tower lights set to realtime (VantageTowerLevels switches them per level).");
        }

        // ---------------- entrances ----------------

        /// <summary>
        /// The entrances have 15 cm steps up to the ground floor; the template character can't step up, so each
        /// ground-level PlayerEntry gets an invisible walk ramp (≤ 18°) from the ground to the floor.
        /// </summary>
        private static void entranceRamps(Transform t, Transform parent)
        {
            Physics.SyncTransforms();
            foreach (var m in t.GetComponentsInChildren<CommandTowerKit.GameplayMarker>(true))
            {
                if (m.markerType != "PlayerEntry" || m.floor >= 0) continue;
                var p = t.InverseTransformPoint(m.transform.position);
                // Inward: towards the tower centre, snapped to the main axes.
                var inward = Mathf.Abs(p.x) > Mathf.Abs(p.z) ? new Vector3(-Mathf.Sign(p.x), 0, 0) : new Vector3(0, 0, -Mathf.Sign(p.z));
                var profile = new List<(float d, float y)>();
                for (float d = -2f; d <= 8f; d += 0.1f)
                    profile.Add((d, surface(t, p + inward * d)));

                var floor = profile.FindIndex(s => s.y >= GroundFloor - 0.05f);
                if (floor < 0) { Debug.Log($"[Vantage] {m.name}: no floor within 8 m inward."); continue; }
                var ground = profile.FindLastIndex(floor, s => s.y <= 0.06f);
                if (ground < 0) continue;
                var rise = profile[floor].y - profile[ground].y;
                if (rise < 0.05f) continue;

                const float maxSlope = 18f;
                var run = Mathf.Max(profile[floor].d - profile[ground].d, rise / Mathf.Tan(maxSlope * Mathf.Deg2Rad));
                var top = p + inward * profile[floor].d + Vector3.up * profile[floor].y;
                var bottom = top - inward * run - Vector3.up * rise;
                ramp(t, parent, m.name.Replace("MK_PlayerEntry_", "Walk Ramp - "), bottom, top, 3.4f);
                Debug.Log($"[Vantage] {m.name}: walk ramp, rise {rise:F2} m over {run:F2} m.");
            }
        }

        /// <summary>Top walkable surface (local y) under a tower-local point, ignoring roofs above 1.3 m.</summary>
        private static float surface(Transform t, Vector3 local)
        {
            var best = 0f;
            var found = false;
            foreach (var h in Physics.RaycastAll(t.TransformPoint(local + Vector3.up * 1.3f), Vector3.down, 3f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
            {
                if (h.normal.y < 0.7f) continue;
                var y = t.InverseTransformPoint(h.point).y;
                if (!found || y > best) { best = y; found = true; }
            }
            return found ? best : 0f;
        }

        /// <summary>An invisible box whose top face runs from 'bottom' to 'top' (tower-local).</summary>
        private static void ramp(Transform t, Transform parent, string name, Vector3 bottom, Vector3 top, float width)
        {
            const float thickness = 0.3f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var along = top - bottom;
            go.transform.rotation = t.rotation * Quaternion.LookRotation(along.normalized);
            var up = go.transform.up;
            go.transform.position = t.TransformPoint((bottom + top) / 2) - up * (thickness / 2);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, thickness, along.magnitude + 0.2f);
        }

        // ---------------- stairs ----------------

        /// <summary>
        /// The tower's own stair ramps sit in the inner corners of the steps, so every step edge sticks up to 15 cm
        /// above them and catches the template character (measured by StairProfileBatch; the autoplay bot stalled
        /// 2–5 s per flight). Over each flight this lays an invisible surface that is the slope-limited upper envelope
        /// of the real walkable geometry: it is never below a step edge, never steeper than MaxSlope (under the 26°
        /// where the template starts slowing down), and meets the floors and landings flush at both ends.
        /// </summary>
        private static void smoothStairs(Transform t, Transform parent)
        {
            const float slopeCap = 0.48f;  // 25.6°, under the 26° where the template starts slowing down
            const float step = 0.1f, extend = 1.4f;
            var stairs = t.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.Contains("StairColliders"));
            if (stairs == null) return;
            // The tower's own ramp colliders are not used: on the fire escape they run the opposite way to the real
            // steps, so their undersides form a sloping "ceiling" 1.7 m above the steps that the character's head
            // runs into (found by the autoplay bot). The surfaces built here replace them everywhere.
            var towerRamps = stairs.GetComponent<Collider>();
            Physics.SyncTransforms();
            var mask = ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11));
            int flights = 0;

            foreach (var piece in rampPieces(t, stairs))
            {
                var flat = piece.top - piece.bottom; flat.y = 0;
                var run = flat.magnitude;
                var dir = flat.normalized;
                var side = Vector3.Cross(Vector3.up, dir);
                var rise = piece.top.y - piece.bottom.y;
                // Fit the envelope's slope to this flight (a little steeper than its average) so it comes out as a
                // straight ramp over the step edges instead of a sawtooth of short steep pieces.
                var maxSlope = Mathf.Clamp(rise / run * 1.2f, 0.12f, slopeCap);
                int nk = Mathf.CeilToInt((run + 2 * extend) / step) + 1;
                var lanes = new[] { -0.45f, -0.15f, 0.15f, 0.45f };
                var heights = new float[lanes.Length, nk];
                var hole = new bool[lanes.Length, nk];

                for (int j = 0; j < lanes.Length; j++)
                {
                    // Walkable top surface: the highest upward-facing hit no more than 0.3 m above the ramp plane
                    // (ignores rails, walls and the floor above).
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
                            if (h.normal.y < 0.6f || h.collider == towerRamps) continue;
                            var y = t.InverseTransformPoint(h.point).y;
                            if (y <= plane + 0.3f && y > raw[i]) raw[i] = y;
                        }
                    }
                    // Slope-limited upper envelope (two passes: forward and back).
                    var env = (float[])raw.Clone();
                    for (int i = 1; i < nk; i++) env[i] = Mathf.Max(env[i], env[i - 1] - maxSlope * step);
                    for (int i = nk - 2; i >= 0; i--) env[i] = Mathf.Max(env[i], env[i + 1] - maxSlope * step);
                    for (int i = 0; i < nk; i++) { heights[j, i] = env[i]; hole[j, i] = float.IsNegativeInfinity(raw[i]); }
                }

                // Strip mesh across the flight; holes (no geometry under a column) take the neighbouring lane's height.
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
                        // No floor over voids (stairwell gaps beside landings): skip cells over a hole.
                        if (vertexHole[a] || vertexHole[b] || vertexHole[c] || vertexHole[d]) continue;
                        triangles.AddRange(new[] { a, c, b, b, c, d });
                    }

                var go = new GameObject($"Stair Surface {piece.side} {piece.bottom.y:F2}-{piece.top.y:F2}");
                go.transform.SetParent(parent, false);
                var mesh = new Mesh { name = go.name };
                var root = go.transform;
                mesh.SetVertices(vertices.Select(v => root.InverseTransformPoint(v)).ToList());
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                saveMesh(mesh, $"Stair Surface {flights}");
                flights++;
            }
            if (towerRamps != null)
            {
                towerRamps.enabled = false;
                EditorUtility.SetDirty(towerRamps);
            }
            Debug.Log($"[Vantage] Smooth stair surfaces over {flights} flights; the tower's own stair ramp collider is switched off.");
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

        // ---------------- gates ----------------

        /// <summary>
        /// One gate per stair flight that leaves a level upwards (inner stairs and fire escape): the flights whose
        /// bottom is on storeys 1, 3 and 5 (top floor of levels 1–3). Found from the tower's TWR_StairColliders.
        /// </summary>
        private static List<VantageFloorGate> buildGates(Transform t, Transform parent)
        {
            var gates = new List<VantageFloorGate>();
            var stairs = t.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.Contains("StairColliders"));
            if (stairs == null || stairs.sharedMesh == null) { Debug.LogWarning("[Vantage] No TWR_StairColliders in the tower; no gates."); return gates; }

            var material = gateMaterial();
            foreach (var piece in rampPieces(t, stairs))
            {
                var storey = Mathf.RoundToInt((piece.bottom.y - GroundFloor) / Storey);
                if (Mathf.Abs(piece.bottom.y - (GroundFloor + storey * Storey)) > 0.2f) continue; // mid-landing flights
                if (storey != 1 && storey != 3 && storey != 5) continue;
                var level = (storey - 1) / 2;
                gates.Add(gate(t, parent, piece, level, material));
            }
            Debug.Log($"[Vantage] Gates: {gates.Count} ({string.Join(", ", gates.Select(g => g.name))}).");
            return gates;
        }

        private struct RampPiece { public Vector3 bottom, top; public float width; public string side; }

        /// <summary>Connected pieces of the stair ramp mesh, with their bottom and top edge centres (tower-local).</summary>
        private static List<RampPiece> rampPieces(Transform t, MeshFilter mf)
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

            var pieces = new List<RampPiece>();
            foreach (var g in Enumerable.Range(0, v.Length).GroupBy(find))
            {
                var pts = g.Select(i => t.InverseTransformPoint(mf.transform.TransformPoint(v[i]))).ToList();
                float lo = pts.Min(p => p.y), hi = pts.Max(p => p.y);
                var b = new Bounds(pts[0], Vector3.zero);
                foreach (var p in pts) b.Encapsulate(p);
                // Which end is uphill is decided by the real steps, not the ramp mesh (its shape misleads on the
                // fire escape): try both directions and keep the one whose ramp line runs closest to the actual
                // walking surface (structure geometry, ignoring the ramp collider and our own additions).
                var alongX = b.size.x >= b.size.z;
                float min = alongX ? b.min.x : b.min.z, max = alongX ? b.max.x : b.max.z;
                var rampCollider = mf.GetComponent<Collider>();
                float misfit(bool footAtMin)
                {
                    float total = 0; int n = 0;
                    for (float f = 0.1f; f <= 0.9f; f += 0.1f)
                    {
                        var a = Mathf.Lerp(min, max, f);
                        var expected = Mathf.Lerp(lo, hi, footAtMin ? f : 1f - f);
                        var probe = alongX ? new Vector3(a, hi + 1f, b.center.z) : new Vector3(b.center.x, hi + 1f, a);
                        var best = float.MaxValue;
                        foreach (var h in Physics.RaycastAll(t.TransformPoint(probe), Vector3.down, hi - lo + 3f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
                        {
                            if (h.collider == rampCollider || h.collider.transform.root.name == SetupName || h.normal.y < 0.5f) continue;
                            best = Mathf.Min(best, Mathf.Abs(t.InverseTransformPoint(h.point).y - expected));
                        }
                        if (best < 1.5f) { total += best; n++; }
                    }
                    return n > 0 ? total / n : float.MaxValue;
                }
                Physics.SyncTransforms();
                var footAtMinMisfit = misfit(true);
                var footAtMaxMisfit = misfit(false);
                var lowEnd = footAtMinMisfit <= footAtMaxMisfit ? min : max;
                var highEnd = footAtMinMisfit <= footAtMaxMisfit ? max : min;
                Vector3 at(float a, float y) => alongX ? new Vector3(a, y, b.center.z) : new Vector3(b.center.x, y, a);
                pieces.Add(new RampPiece
                {
                    bottom = at(lowEnd, lo), top = at(highEnd, hi),
                    width = alongX ? b.size.z : b.size.x,
                    side = Mathf.Max(b.size.x, b.size.z) > 12f ? "Fire Escape" : "Stairs",
                });
            }
            return pieces;
        }

        private static VantageFloorGate gate(Transform t, Transform parent, RampPiece piece, int level, Material material)
        {
            var go = new GameObject($"Gate {level + 1}->{level + 2} ({piece.side})");
            go.transform.SetParent(parent, false);
            // Nothing of the gate goes into the NavMesh; the carving obstacle blocks agents while it's locked.
            go.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;

            var flat = piece.top - piece.bottom; flat.y = 0;
            var dir = flat.normalized;
            var run = flat.magnitude;
            var rise = piece.top.y - piece.bottom.y;
            var frame = t.rotation * Quaternion.LookRotation(dir);

            // Blocker over the whole flight, from just below its foot to well above its head.
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

            // Shutter panel across the foot of the flight, and a warning lamp.
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
            m.SetColor("_BaseColor", new Color(0.12f, 0.025f, 0.02f));
            m.SetFloat("_Metallic", 0.6f);
            m.SetFloat("_Smoothness", 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.9f, 0.12f, 0.06f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------- cover ----------------

        /// <summary>Cover Shooter covers on the furniture big enough to hide behind (desks, racks, lockers, sandbags…).</summary>
        private static void furnitureCover(Transform t, Transform parent)
        {
            Physics.SyncTransforms();
            var furniture = t.Find("Furniture");
            if (furniture == null) return;
            int count = 0, pieces = 0;
            foreach (Transform piece in furniture)
            {
                var colliders = piece.GetComponentsInChildren<Collider>();
                if (colliders.Length == 0) continue;
                var b = colliders[0].bounds;
                foreach (var c in colliders) b.Encapsulate(c.bounds);
                if (b.size.y < 0.75f || b.size.y > 2.4f || Mathf.Max(b.size.x, b.size.z) < 0.9f) continue;
                var n = piece.name.ToLowerInvariant();
                if (n.Contains("chair") || n.Contains("light") || n.Contains("sign") || n.Contains("extinguisher") || n.Contains("trash") || n.Contains("drone_quad")) continue;
                count += VantageCoverUtil.AddForOriented(piece.gameObject, parent);
                pieces++;
            }
            Debug.Log($"[Vantage] Furniture cover: {count} covers on {pieces} pieces.");
        }

        // ---------------- levels ----------------

        private static Vector3 playerStart(Transform t)
        {
            var entry = t.GetComponentsInChildren<CommandTowerKit.GameplayMarker>(true).FirstOrDefault(m => m.name.Contains("MainEntrance"));
            var local = entry != null ? t.InverseTransformPoint(entry.transform.position) : new Vector3(0, 0, 14f);
            var outward = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? new Vector3(Mathf.Sign(local.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(local.z));
            var world = t.TransformPoint(local + outward * 7f);
            if (Physics.Raycast(world + Vector3.up * 20f, Vector3.down, out var hit, 40f, ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11)), QueryTriggerInteraction.Ignore))
                world = hit.point;

            // Face the door.
            var facing = Quaternion.LookRotation(t.TransformDirection(-outward));
            foreach (var input in Object.FindObjectsByType<CoverShooter.ThirdPersonInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var character = input.GetComponent<CoverShooter.CharacterMotor>();
                if (character == null) continue;
                character.transform.SetPositionAndRotation(world + Vector3.up * 0.05f, facing);
                var cam = character.transform.root.GetComponentInChildren<CoverShooter.ThirdPersonCamera>(true);
                if (cam != null)
                    cam.transform.SetPositionAndRotation(world - facing * Vector3.forward * 4f + Vector3.up * 2.2f, facing);
            }
            return world;
        }

        private static VantageTowerLevels levelManager(Transform t, Transform parent, List<VantageFloorGate> gates, Vector3 start)
        {
            var go = new GameObject("Tower Levels");
            go.transform.SetParent(parent, false);
            var levels = go.AddComponent<VantageTowerLevels>();
            levels.Tower = t;
            levels.SoldierPrefab = soldierPrefab();
            levels.DronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VantageEditorUtil.DronePrefabPath);
            levels.TurretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VantageEditorUtil.TurretPrefabPath);
            levels.GroundFloor = GroundFloor;
            levels.StoreyHeight = Storey;
            // Difficulty rises level by level: more enemies, tougher soldiers, harder-hitting guns, sharper drones.
            levels.Levels = new List<VantageTowerLevels.Level>
            {
                new VantageTowerLevels.Level { Name = "Ground floor & barracks", Storeys = new[] { 0, 1 }, Soldiers = 4, Drones = 1, SoldierWeapon = "Pistol",
                    SoldierDamage = 10f, SoldierHealth = 0.8f, DroneAccuracy = 0.35f, DroneFireInterval = 1.6f,
                    ClearedMessage = "LEVEL 1 CLEAR  ·  RIFLE UNLOCKED  ·  STAIRS OPEN" },
                new VantageTowerLevels.Level { Name = "Operations & armory", Storeys = new[] { 2, 3 }, Soldiers = 6, Drones = 2, SoldierWeapon = "Pistol",
                    SoldierDamage = 13f, SoldierHealth = 1f, DroneAccuracy = 0.45f, DroneFireInterval = 1.4f,
                    ClearedMessage = "LEVEL 2 CLEAR  ·  STAIRS OPEN" },
                new VantageTowerLevels.Level { Name = "Command & drone control", Storeys = new[] { 4, 5 }, Soldiers = 8, Drones = 3,
                    SoldierDamage = 16f, SoldierHealth = 1.25f, DroneAccuracy = 0.55f, DroneFireInterval = 1.2f,
                    ClearedMessage = "LEVEL 3 CLEAR  ·  THE ROOF IS OPEN" },
                new VantageTowerLevels.Level { Name = "Roof", Storeys = new[] { 6 }, Soldiers = 5, Drones = 4, Turret = true,
                    SoldierDamage = 18f, SoldierHealth = 1.5f, DroneAccuracy = 0.65f, DroneFireInterval = 1.0f,
                    ClearedMessage = "THE TOWER IS SILENT" },
            };
            return levels;
        }

        public const string SoldierPrefabPath = "Assets/Vantage/Prefabs/Soldier.prefab";

        /// <summary>
        /// Our soldier: a variant of the template soldier that can actually fight. The template prefab still keeps
        /// its guns in CharacterMotor's old Weapons list, but this version of the template's AI arms itself from a
        /// CharacterInventory component, which the prefab never got, so every soldier spawned unarmed and the
        /// fighter brain fell back to running away (measured by the autoplay bot: state runAway, weapon none).
        /// The variant gets the inventory, starts with its first gun in hand, and carries the aggression tuning.
        /// </summary>
        private static GameObject soldierPrefab()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Soldier.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(template);
            try
            {
                var motor = go.GetComponent<CoverShooter.CharacterMotor>();
                var inventory = go.GetComponent<CoverShooter.CharacterInventory>() ?? go.AddComponent<CoverShooter.CharacterInventory>();
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
                Debug.Log($"[Vantage] Soldier prefab: {inventory.Weapons.Length} weapons in its inventory ({string.Join(", ", inventory.Weapons.Select(w => w.RightItem.name))}), starts with {(first.Gun != null ? first.Gun.name : "nothing")}.");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Pistol beside the start; the rifle appears at the inner-stairs gate when level 1 is cleared.</summary>
        private static void pickups(Transform parent, Vector3 start, List<VantageFloorGate> gates, VantageTowerLevels levels)
        {
            var pistol = VantageSetup.AddPickupAt("Pistol", start + Vector3.up * 1f + (start - levels.Tower.position).normalized * -2.5f + Vector3.Cross(Vector3.up, (start - levels.Tower.position).normalized) * 1.5f, parent);
            var gate = gates.FirstOrDefault(g => g.Level == 0 && g.name.Contains("Stairs")) ?? gates.FirstOrDefault(g => g.Level == 0);
            if (gate != null)
            {
                var panel = gate.Panel;
                var rifle = VantageSetup.AddPickupAt("Rifle", panel.position - panel.forward * 1.6f - Vector3.up * 0.3f, parent);
                levels.Levels[0].Reward = rifle;
            }
            Debug.Log($"[Vantage] Pickups: pistol at {pistol.transform.position:F1}, rifle as the level 1 reward.");
        }

        /// <summary>Playtest logger, minimap and anything else that pointed at the old tower now use the command tower.</summary>
        private static void pointToolsAtTower(Transform t)
        {
            foreach (var logger in Object.FindObjectsByType<VantagePlaytestLogger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                logger.Tower = t;
            foreach (var map in Object.FindObjectsByType<VantageMinimap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                map.Tower = t;
            if (Object.FindFirstObjectByType<VantagePlaytestLogger>(FindObjectsInactive.Include) == null)
            {
                var tools = new GameObject("VANTAGE Playtest Tools");
                tools.AddComponent<VantagePlaytestLogger>().Tower = t;
                tools.AddComponent<VantagePerfCapture>();
            }
        }

        /// <summary>
        /// Logs the tower's stairs (ramp collider pieces), doors, markers and entrance ground, in tower-local metres.
        /// -executeMethod Vantage.EditorTools.VantageCommandTower.AnalyseBatch
        /// </summary>
        public static void AnalyseBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                var tower = FindTower();
                if (tower == null) throw new Exception("No CommandTower in the scene.");
                var t = tower.transform;
                Debug.Log($"[Vantage] Tower at {t.position} rot {t.eulerAngles} children {t.childCount}");
                foreach (Transform c in t)
                    Debug.Log($"[Vantage]  child {c.name} ({c.childCount}) local {c.localPosition}");

                foreach (var mf in tower.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.name.Contains("Stair") && !mf.name.Contains("FireEscape")) continue;
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    Vector3[] v;
                    try { v = mesh.vertices; } catch { Debug.Log("[Vantage] unreadable " + mf.name); continue; }
                    var tri = mesh.triangles;
                    var parent = Enumerable.Range(0, v.Length).ToArray();
                    int find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
                    var weld = new Dictionary<Vector3Int, int>();
                    for (int i = 0; i < v.Length; i++)
                    {
                        var k = Vector3Int.RoundToInt(v[i] * 100);
                        if (weld.TryGetValue(k, out var j)) parent[find(i)] = find(j); else weld[k] = i;
                    }
                    for (int i = 0; i < tri.Length; i += 3) { parent[find(tri[i])] = find(tri[i + 1]); parent[find(tri[i + 1])] = find(tri[i + 2]); }
                    var groups = new Dictionary<int, Bounds>();
                    for (int i = 0; i < v.Length; i++)
                    {
                        var p = t.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                        var r = find(i);
                        if (groups.TryGetValue(r, out var b)) { b.Encapsulate(p); groups[r] = b; } else groups[r] = new Bounds(p, Vector3.zero);
                    }
                    Debug.Log($"[Vantage] {mf.name}: {groups.Count} pieces");
                    if (mf.name.Contains("Stair"))
                        foreach (var b in groups.Values.OrderBy(b => b.min.y).ThenBy(b => b.center.x))
                            Debug.Log($"[Vantage]   ramp x {b.min.x:F1}..{b.max.x:F1} y {b.min.y:F2}..{b.max.y:F2} z {b.min.z:F1}..{b.max.z:F1}");
                    else
                    {
                        var all = groups.Values.Aggregate((a, b) => { a.Encapsulate(b); return a; });
                        Debug.Log($"[Vantage]   fire escape bounds {all.min:F1}..{all.max:F1}");
                    }
                }

                foreach (var d in tower.GetComponentsInChildren<CommandTowerKit.TowerDoor>(true))
                {
                    var p = t.InverseTransformPoint(d.transform.position);
                    Debug.Log($"[Vantage] Door {d.name} parent {d.transform.parent.name} local {p:F1} open {d.isOpen} locked {d.locked}");
                }

                var markers = tower.GetComponentsInChildren<CommandTowerKit.GameplayMarker>(true);
                foreach (var m in markers.Where(m => m.markerType == "PlayerEntry" || m.markerType == "Objective"))
                    Debug.Log($"[Vantage] Marker {m.name} {m.markerType} floor {m.floor} local {t.InverseTransformPoint(m.transform.position):F1}");

                // Floor heights: ground under the tower centre per storey, and the ground just outside the main entrance.
                Physics.SyncTransforms();
                for (float y = 0; y < 34; y += 0.5f)
                {
                    var o = t.TransformPoint(new Vector3(0, y + 0.45f, 0));
                    if (Physics.Raycast(o, Vector3.down, out var h, 0.5f))
                        Debug.Log($"[Vantage] centre floor surface at local y {t.InverseTransformPoint(h.point).y:F2} ({h.collider.name})");
                }
                var entry = markers.FirstOrDefault(m => m.name.Contains("MainEntrance"));
                if (entry != null)
                {
                    var e = t.InverseTransformPoint(entry.transform.position);
                    for (float dz = -4f; dz <= 6f; dz += 0.5f)
                    {
                        var p = t.TransformPoint(new Vector3(e.x, 3f, e.z + dz));
                        var hits = Physics.RaycastAll(p, Vector3.down, 8f).OrderBy(h => h.distance).Select(h => $"{t.InverseTransformPoint(h.point).y:F2} {h.collider.name}");
                        Debug.Log($"[Vantage] entrance z {e.z + dz:F1}: {string.Join(" | ", hits)}");
                    }
                }
                var lights = tower.GetComponentsInChildren<Light>(true);
                Debug.Log($"[Vantage] Lights {lights.Length}, modes {string.Join(",", lights.Select(l => l.lightmapBakeType.ToString()).Distinct())}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }
    }
}
