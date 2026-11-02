using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vantage.EditorTools
{
    /// <summary>
    /// The level's time of day: dusk. The sun just above the horizon (warm, long shadows), a dusk sky, cool trilight
    /// ambient so shadowed sides stay readable, haze in the sky's colour, and the base's own lights switched on:
    /// sodium lamps on the light poles and white floodlights near the tower, and blinking red aircraft-warning
    /// lights on the tower's top corners and the helipad. Everything it adds lives under one root and is rebuilt on
    /// every run; the world tool calls Atmosphere() so a rebuild keeps dusk.
    /// </summary>
    public static class VantageDusk
    {
        public const string RootName = "VANTAGE Dusk Lights";
        private const string OutFolder = "Assets/Vantage/World";
        private const float SunElevation = 5f;
        private const float PoleReach = 130f;   // poles further from the tower stay dark (the far base is barely seen)
        private const int MaxPoleLights = 28;

        [MenuItem("Vantage/World/Apply Dusk Lighting", priority = 12)]
        public static void Apply()
        {
            Atmosphere();
            Lights();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Batch: -executeMethod Vantage.EditorTools.VantageDusk.ApplyBatch [-vantageShots folder]</summary>
        public static void ApplyBatch()
        {
            var exitCode = 0;
            try
            {
                var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                Apply();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                var folder = VantageEditorUtil.Arg("-vantageShots");
                if (folder != null) VantageTower.Shots(folder);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Sun, sky, ambient and haze.</summary>
        public static void Atmosphere()
        {
            var sun = RenderSettings.sun != null ? RenderSettings.sun
                : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                Undo.RecordObject(sun, "Dusk sun");
                Undo.RecordObject(sun.transform, "Dusk sun");
                var yaw = sun.transform.eulerAngles.y;   // keep the direction the level was lit from
                sun.transform.rotation = Quaternion.Euler(SunElevation, yaw, 0f);
                sun.useColorTemperature = false;
                sun.color = new Color(1f, 0.56f, 0.32f);
                sun.intensity = 1.5f;
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
                Debug.Log($"[Vantage] Dusk: sun at {SunElevation}° elevation, yaw {yaw:F0}°.");
            }
            else Debug.Log("[Vantage] Dusk: FAIL no directional light in the scene.");

            var path = OutFolder + "/Sky_Dusk.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 1.2f);
            sky.SetColor("_SkyTint", new Color(0.48f, 0.47f, 0.62f));
            sky.SetColor("_GroundColor", new Color(0.16f, 0.16f, 0.14f));
            sky.SetFloat("_Exposure", 0.95f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            // Trilight rather than the sky: a low sun makes the sky's ambient dim and orange, and enemies in shade
            // must still read against the walls.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.42f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.37f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.14f, 0.13f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0014f;
            RenderSettings.fogColor = new Color(0.55f, 0.45f, 0.45f);
        }

        /// <summary>The base's lamps near the tower and the tower's warning lights, under one rebuilt root.</summary>
        public static void Lights()
        {
            foreach (var old in EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == RootName).ToList())
                Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            var tower = VantageTower.FindTower();
            var centre = tower != null && VantageTower.WorldBounds(tower.transform, out var tb) ? tb.center : Vector3.zero;

            // Poles of the base (the world's LightPole and FloodlightPole props), nearest first.
            var world = GameObject.Find(VantageMilitaryBase.RootName);
            var poles = new List<(Transform t, bool flood, float d)>();
            if (world != null)
                foreach (var t in world.GetComponentsInChildren<Transform>())
                {
                    var flood = t.name.StartsWith("FloodlightPole");
                    if (!flood && !t.name.StartsWith("LightPole")) continue;
                    if (t.parent != null && (t.parent.name.StartsWith("FloodlightPole") || t.parent.name.StartsWith("LightPole"))) continue;
                    var d = Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(centre.x, centre.z));
                    if (d <= PoleReach) poles.Add((t, flood, d));
                }
            var lit = 0;
            foreach (var (t, flood, _) in poles.OrderBy(p => p.d).Take(MaxPoleLights))
            {
                if (!VantageTower.WorldBounds(t, out var pb)) continue;
                var top = new Vector3(pb.center.x, pb.max.y - 0.35f, pb.center.z);
                var go = new GameObject((flood ? "Floodlight " : "Lamp ") + t.name);
                go.transform.SetParent(root, false);
                go.transform.position = top;
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.shadows = LightShadows.None;
                if (flood)
                {
                    // Floodlights face the tower, tilted down onto the ground in front of it.
                    var toTower = centre - top; toTower.y = 0;
                    var dir = toTower.sqrMagnitude > 0.01f ? toTower.normalized : Vector3.forward;
                    go.transform.rotation = Quaternion.LookRotation(dir + Vector3.down * 0.9f);
                    light.color = new Color(0.86f, 0.9f, 1f);
                    light.range = 38f;
                    light.spotAngle = 70f;
                    light.intensity = 9f;
                }
                else
                {
                    go.transform.rotation = Quaternion.LookRotation(Vector3.down);
                    light.color = new Color(1f, 0.72f, 0.42f);
                    light.range = 16f;
                    light.spotAngle = 110f;
                    light.intensity = 5f;
                }
                light.innerSpotAngle = light.spotAngle * 0.55f;
                glowBulb(go.transform, top - Vector3.up * 0.1f, flood ? 0.45f : 0.3f, glowMaterial(flood ? "Flood" : "Sodium", light.color * (flood ? 4f : 3f)));
                lit++;
            }

            // Aircraft-warning lights: the tower's top corners and the helipad's far corners, blinking together.
            var blink = new GameObject("Aircraft Warning Lights").AddComponent<VantageBlinkLights>();
            blink.transform.SetParent(root, false);
            var corners = new List<Vector3>();
            if (tower != null && VantageTower.WorldBounds(tower.transform, out var all))
            {
                var structure = VantageTower.Find(tower.transform, "LVL1_Structure");
                var body = structure != null && VantageTower.WorldBounds(structure, out var sb) ? sb : all;
                var y = all.max.y + 0.25f;
                foreach (var x in new[] { body.min.x + 0.4f, body.max.x - 0.4f })
                    foreach (var z in new[] { body.min.z + 0.4f, body.max.z - 0.4f })
                        corners.Add(new Vector3(x, y, z));
                var pad = VantageTower.Find(tower.transform, "FINAL_Helipad");
                if (pad != null && VantageTower.WorldBounds(pad, out var hb))
                {
                    var outward = hb.center - body.center; outward.y = 0;
                    outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;
                    var side = Vector3.Cross(Vector3.up, outward);
                    var edge = hb.center + outward * hb.extents.magnitude * 0.6f;
                    corners.Add(new Vector3(edge.x, hb.max.y + 0.2f, edge.z) + side * hb.extents.magnitude * 0.45f);
                    corners.Add(new Vector3(edge.x, hb.max.y + 0.2f, edge.z) - side * hb.extents.magnitude * 0.45f);
                }
            }
            for (int i = 0; i < corners.Count; i++)
            {
                var bulb = glowBulb(blink.transform, corners[i], 0.28f, glowMaterial("Warning", blink.Glow * blink.GlowIntensity));
                blink.Glows.Add(bulb);
                if (i % 2 != 0) continue;   // half of them carry a real light; the glow is what reads from afar
                var light = new GameObject("Warning Light").AddComponent<Light>();
                light.transform.SetParent(blink.transform, false);
                light.transform.position = corners[i] + Vector3.up * 0.3f;
                light.type = LightType.Point;
                light.color = blink.Glow;
                light.range = 7f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;
                blink.Lights.Add(light);
            }
            Debug.Log($"[Vantage] Dusk lights: {lit} pole lights ({poles.Count(p => p.flood)} floodlights within {PoleReach} m), {corners.Count} warning lights ({blink.Lights.Count} with a light).");
        }

        private static Renderer glowBulb(Transform parent, Vector3 at, float size, Material material)
        {
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.name = "Glow";
            bulb.transform.SetParent(parent, true);
            bulb.transform.position = at;
            bulb.transform.localScale = Vector3.one * size;
            var r = bulb.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>An emissive bulb material per kind (property blocks set in the editor aren't saved with the scene).</summary>
        private static Material glowMaterial(string kind, Color emission)
        {
            var path = OutFolder + $"/M_Glow_{kind}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
