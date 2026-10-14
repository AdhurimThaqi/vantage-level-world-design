using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Placeholder models for the heavy robot, the boss and the extraction helicopter, until the user's Blender
    /// models replace them. Each is a few boxes merged into one mesh with one material (plus the parts that move or
    /// glow: eye, rotors), under a child named "Model": drop the real model in its place and nothing else changes
    /// (the robots' Eye and Muzzle sit beside "Model"; the model's own colliders are fine, hits are forwarded).
    /// Existing prefabs are left alone (they may already hold the real model); the menu item rebuilds them.
    /// </summary>
    public static class VantagePlaceholders
    {
        public const string Folder = "Assets/Vantage/World/Placeholders";
        public const string HelicopterPrefabPath = "Assets/Vantage/Prefabs/Helicopter.prefab";

        [MenuItem("Vantage/Enemies/Rebuild Placeholder Models (robot, boss, helicopter)", priority = 90)]
        public static void RebuildFromMenu() => Build(true);

        /// <summary>Builds the prefabs that don't exist yet (or all of them with force).</summary>
        public static void Build(bool force)
        {
            var built = new List<string>();
            if (force || !exists(VantageTower.HeavyRobotPrefabPath)) { robot(VantageTower.HeavyRobotPrefabPath, false); built.Add("heavy robot"); }
            if (force || !exists(VantageTower.BossPrefabPath)) { robot(VantageTower.BossPrefabPath, true); built.Add("boss"); }
            if (force || !exists(HelicopterPrefabPath)) { helicopter(); built.Add("helicopter"); }
            if (built.Count > 0) Debug.Log("[Vantage] Placeholders built: " + string.Join(", ", built) + ".");
        }

        private static bool exists(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

        private static void robot(string path, bool boss)
        {
            var s = boss ? 1.6f : 1f;
            var root = new GameObject(boss ? "Boss" : "HeavyRobot");
            try
            {
                var body = root.AddComponent<CapsuleCollider>();
                body.height = 2.4f * s;
                body.radius = 0.6f * s;
                body.center = new Vector3(0, 1.2f * s, 0);
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.6f * s;
                agent.height = 2.4f * s;
                agent.angularSpeed = 120f;
                agent.acceleration = 6f;
                root.AddComponent<CoverShooter.CharacterHealth>();
                var audio = root.AddComponent<AudioSource>();
                audio.spatialBlend = 1f;
                audio.maxDistance = 45f;
                audio.playOnAwake = false;

                // Legs, hips, torso, shoulders, head, gun arm.
                var model = merged("Model", root.transform, metal(boss), s, new[]
                {
                    box(-0.3f, 0.5f, 0, 0.35f, 1.0f, 0.4f), box(0.3f, 0.5f, 0, 0.35f, 1.0f, 0.4f),
                    box(0, 1.05f, 0, 0.9f, 0.25f, 0.5f), box(0, 1.55f, 0, 1.1f, 0.85f, 0.7f),
                    box(-0.7f, 1.85f, 0, 0.35f, 0.35f, 0.6f), box(0.7f, 1.85f, 0, 0.35f, 0.35f, 0.6f),
                    box(0, 2.15f, 0, 0.5f, 0.35f, 0.45f),
                    box(0.75f, 1.45f, 0.35f, 0.25f, 0.25f, 1.0f), box(-0.75f, 1.45f, 0.1f, 0.22f, 0.5f, 0.3f),
                }, path);

                var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
                eye.name = "Eye";
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                // Eye and muzzle sit beside the model, not in it, so replacing "Model" keeps them.
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(0, 2.17f, 0.23f) * s;
                eye.transform.localScale = new Vector3(0.32f, 0.07f, 0.04f) * s;
                eye.GetComponent<Renderer>().sharedMaterial = eyeMaterial();
                var eyeLight = new GameObject("Eye Light").AddComponent<Light>();
                eyeLight.transform.SetParent(eye.transform, false);
                eyeLight.type = LightType.Point;
                eyeLight.range = 3f * s;
                eyeLight.shadows = LightShadows.None;
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0.75f, 1.45f, 0.9f) * s;

                var r = root.AddComponent<VantageHeavyRobot>();
                r.IsBoss = boss;
                r.MaxHealth = boss ? 1500f : 600f;
                r.Speed = boss ? 2.2f : 1.6f;
                r.Damage = boss ? 11f : 9f;
                r.Model = model;
                r.Muzzle = muzzle;
                r.Eye = eye.GetComponent<Renderer>();
                r.EyeLight = eyeLight;
                // Effects and sounds from the drone, so all machines sound and look alike.
                var drone = AssetDatabase.LoadAssetAtPath<GameObject>(VantageEditorUtil.DronePrefabPath)?.GetComponent<VantageDrone>();
                if (drone != null)
                {
                    r.MuzzleFlash = drone.MuzzleFlash;
                    r.HitEffect = drone.HitEffect;
                    r.DeathExplosion = drone.DeathExplosion;
                    r.FireSound = drone.FireSound;
                    r.DeathSound = drone.DeathSound;
                    if (drone.Tracer != null)
                    {
                        var tracer = new GameObject("Tracer").AddComponent<LineRenderer>();
                        tracer.transform.SetParent(root.transform, false);
                        tracer.useWorldSpace = true;
                        tracer.positionCount = 2;
                        tracer.widthMultiplier = drone.Tracer.widthMultiplier * 1.5f;
                        tracer.sharedMaterial = drone.Tracer.sharedMaterial;
                        tracer.colorGradient = drone.Tracer.colorGradient;
                        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.Tracer = tracer;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void helicopter()
        {
            var root = new GameObject("Helicopter");
            try
            {
                var model = merged("Model", root.transform, material("Helicopter", new Color(0.18f, 0.22f, 0.14f), 0.35f), 1f, new[]
                {
                    box(0, 1.6f, 0.5f, 2.4f, 2.0f, 5.0f), box(0, 1.9f, 3.4f, 2.0f, 1.4f, 1.2f),
                    box(0, 2.0f, -3.6f, 0.6f, 0.6f, 4.4f), box(0, 2.7f, -5.6f, 0.15f, 1.4f, 0.9f),
                    box(-1.1f, 0.15f, 0.5f, 0.12f, 0.12f, 5.2f), box(1.1f, 0.15f, 0.5f, 0.12f, 0.12f, 5.2f),
                    box(-0.9f, 0.4f, 1.6f, 0.08f, 0.5f, 0.08f), box(0.9f, 0.4f, 1.6f, 0.08f, 0.5f, 0.08f),
                    box(-0.9f, 0.4f, -0.6f, 0.08f, 0.5f, 0.08f), box(0.9f, 0.4f, -0.6f, 0.08f, 0.5f, 0.08f),
                    box(0, 2.75f, 0.5f, 0.4f, 0.3f, 0.4f),
                }, HelicopterPrefabPath);
                var main = merged("Main Rotor", model, material("Rotor", new Color(0.08f, 0.08f, 0.08f), 0.2f), 1f, new[]
                {
                    box(0, 0, 0, 0.3f, 0.05f, 11f), box(0, 0, 0, 11f, 0.05f, 0.3f),
                }, HelicopterPrefabPath);
                main.localPosition = new Vector3(0, 2.95f, 0.5f);
                var tail = merged("Tail Rotor", model, material("Rotor", new Color(0.08f, 0.08f, 0.08f), 0.2f), 1f, new[]
                {
                    box(0, 0, 0, 0.05f, 1.6f, 0.15f), box(0, 0, 0, 0.05f, 0.15f, 1.6f),
                }, HelicopterPrefabPath);
                tail.localPosition = new Vector3(0.15f, 2.7f, -5.8f);
                PrefabUtility.SaveAsPrefabAsset(root, HelicopterPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static (Vector3 c, Vector3 size) box(float x, float y, float z, float sx, float sy, float sz) => (new Vector3(x, y, z), new Vector3(sx, sy, sz));

        /// <summary>Boxes merged into one mesh (saved as an asset next to the prefab's name), as a child with one material.</summary>
        private static Transform merged(string name, Transform parent, Material material, float scale, (Vector3 c, Vector3 size)[] boxes, string prefabPath)
        {
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var combine = new CombineInstance[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
                combine[i] = new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(boxes[i].c * scale, Quaternion.identity, boxes[i].size * scale) };
            var mesh = new Mesh { name = $"{System.IO.Path.GetFileNameWithoutExtension(prefabPath)} {name}" };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            ensureFolder();
            var meshPath = $"{Folder}/{mesh.name}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        private static Material metal(bool boss) => boss
            ? material("Boss Metal", new Color(0.22f, 0.07f, 0.06f), 0.45f, 0.7f)
            : material("Robot Metal", new Color(0.16f, 0.17f, 0.18f), 0.45f, 0.7f);

        private static Material eyeMaterial()
        {
            var m = material("Robot Eye", new Color(0.3f, 0.05f, 0.02f), 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.1f) * 2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material material(string name, Color colour, float smoothness, float metallic = 0f)
        {
            ensureFolder();
            var path = $"{Folder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void ensureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Vantage/World")) AssetDatabase.CreateFolder("Assets/Vantage", "World");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Vantage/World", "Placeholders");
        }
    }
}
