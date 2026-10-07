using System.Collections.Generic;
using CoverShooter;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Makes sure the layers Cover Shooter hard-codes (8-11, see CoverShooter.Layers) exist in this project.
    /// </summary>
    [InitializeOnLoad]
    public static class VantageLayers
    {
        public const int Character = 10;

        private static readonly (int index, string name)[] Required =
        {
            (8, "Cover"), (9, "Scope"), (10, "Character"), (11, "Zones")
        };

        static VantageLayers()
        {
            EditorApplication.delayCall += Ensure;
        }

        public static void Ensure()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return;

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            var changed = false;

            foreach (var (index, name) in Required)
            {
                var layer = layers.GetArrayElementAtIndex(index);

                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = name;
                    changed = true;
                }
                else if (layer.stringValue != name)
                    Debug.LogWarning($"[Vantage] Layer {index} is '{layer.stringValue}', but Cover Shooter uses it as '{name}'. Objects on it will be treated as {name}.");
            }

            if (changed)
            {
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("[Vantage] Added Cover Shooter layers: 8 Cover, 9 Scope, 10 Character, 11 Zones.");
            }
        }
    }

    public static class VantageSetup
    {
        private const string Root = "Assets/ThirdPersonCoverShooter/";
        private const string Prefabs = Root + "Assets/Prefabs/";
        private const string Sounds = Root + "Sounds/";

        private const string PistolPrefab = Prefabs + "Pistol.prefab";
        private const string RiflePrefab = Prefabs + "Rifle.prefab";

        #region Level content

        [MenuItem("Vantage/Bake NavMesh For Enemies", priority = 3)]
        public static void BakeNavMesh()
        {
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();

            if (surface == null)
            {
                var go = new GameObject("NavMesh");
                Undo.RegisterCreatedObjectUndo(go, "Create NavMesh");
                surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;

                // Characters and trigger zones should not shape the walkable area.
                surface.layerMask = ~((1 << VantageLayers.Character) | (1 << 11) | (1 << 2) | (1 << 5));
            }

            surface.BuildNavMesh();
            saveNavMeshAsset(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            Debug.Log("[Vantage] NavMesh baked. Re-run this after changing the level layout.");
        }

        /// <summary>
        /// Stores the baked NavMesh as an asset in a folder named after the scene, like the Bake button does.
        /// Left inside the scene, the binary-only NavMesh data forces the whole scene file to binary.
        /// </summary>
        private static void saveNavMeshAsset(NavMeshSurface surface)
        {
            var data = surface.navMeshData;
            var scenePath = surface.gameObject.scene.path;
            if (data == null || AssetDatabase.Contains(data) || string.IsNullOrEmpty(scenePath))
                return;

            var parent = System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/');
            var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            var folder = parent + "/" + sceneName;
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, sceneName);

            var assetPath = folder + "/NavMesh-" + surface.name + ".asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);
            EditorUtility.SetDirty(surface);
        }

        [MenuItem("Vantage/Add Pistol Pickup At Scene View", priority = 20)]
        public static void AddPistolPickup() => AddPickupAt("Pistol", dropPoint() + Vector3.up);

        [MenuItem("Vantage/Add Rifle Pickup At Scene View", priority = 21)]
        public static void AddRiflePickup() => AddPickupAt("Rifle", dropPoint() + Vector3.up);

        public static GameObject AddPickupAt(string weaponName, Vector3 position, Transform parent = null, float spinSpeed = 45f)
        {
            var prefabPath = weaponName == "Pistol" ? PistolPrefab : RiflePrefab;
            var ammo = weaponName == "Pistol" ? 24 : 60;

            var pickup = new GameObject(weaponName + " Pickup");
            Undo.RegisterCreatedObjectUndo(pickup, "Add " + weaponName + " Pickup");
            if (parent != null)
                pickup.transform.SetParent(parent, false);
            pickup.transform.position = position;

            var component = pickup.AddComponent<WeaponPickup>();
            component.WeaponName = weaponName;
            component.ExtraAmmo = ammo;
            component.SpinSpeed = spinSpeed;
            component.PickupSound = load<AudioClip>(Sounds + "RELOAD_Rechamber_Leaver_Action_stereo.wav");

            var model = createViewmodel(prefabPath, weaponName + " Model", pickup.transform, Vector3.zero);
            if (model != null)
                model.SetActive(true);

            Selection.activeGameObject = pickup;
            return pickup;
        }

        #endregion

        #region URP

        [MenuItem("Vantage/Fix Cover Shooter Particle Materials For URP", priority = 40)]
        public static void FixParticleMaterials()
        {
            var urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticles == null)
            {
                Debug.LogError("[Vantage] URP particle shader not found.");
                return;
            }

            var converted = 0;
            var skipped = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ThirdPersonCoverShooter", "Assets/PistolAnimsetPro" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                    continue;

                var shaderName = material.shader.name;
                var isLegacyParticle = shaderName.Contains("Particles/") && !shaderName.StartsWith("Universal Render Pipeline") && !shaderName.Contains("Standard");

                if (!isLegacyParticle)
                {
                    if (shaderName.StartsWith("Hidden/InternalErrorShader"))
                        skipped.Add(path);
                    continue;
                }

                convertParticle(material, urpParticles, shaderName);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Vantage] Converted {converted} legacy particle materials to URP Particles/Unlit.");

            if (skipped.Count > 0)
                Debug.LogWarning("[Vantage] These materials use a shader that no longer exists and need a manual fix:\n" + string.Join("\n", skipped));
        }

        private static void convertParticle(Material material, Shader shader, string legacyName)
        {
            Undo.RecordObject(material, "Convert Particle Material");

            var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            var scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
            var offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;

            // Legacy particle shaders treat a tint of 0.5 grey as neutral.
            var color = Color.white;
            if (material.HasProperty("_TintColor"))
            {
                color = material.GetColor("_TintColor") * 2f;
                color.a = Mathf.Clamp01(color.a);
            }
            else if (material.HasProperty("_Color"))
                color = material.GetColor("_Color");

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetColor("_BaseColor", color);

            // URP particle blend modes: 0 alpha, 2 additive, 3 multiply.
            var blend = 0;
            if (legacyName.Contains("Additive"))
                blend = 2;
            else if (legacyName.Contains("Multiply"))
                blend = 3;

            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", blend);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", 0);

            switch (blend)
            {
                case 2:
                    setBlend(material, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One);
                    break;
                case 3:
                    setBlend(material, UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.Zero);
                    material.EnableKeyword("_ALPHAMODULATE_ON");
                    break;
                default:
                    setBlend(material, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
        }

        private static void setBlend(Material material, UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst)
        {
            material.SetFloat("_SrcBlend", (float)src);
            material.SetFloat("_DstBlend", (float)dst);
            material.SetFloat("_SrcBlendAlpha", (float)src);
            material.SetFloat("_DstBlendAlpha", (float)dst);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Instantiates a Cover Shooter gun prefab and strips everything but its meshes,
        /// so it can be used as a pickup visual.
        /// </summary>
        private static GameObject createViewmodel(string prefabPath, string name, Transform parent, Vector3 localPosition)
        {
            var prefab = load<GameObject>(prefabPath);
            if (prefab == null)
                return null;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = name;

            foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true))
                if (particles != null && particles.gameObject != model)
                    Object.DestroyImmediate(particles.gameObject);

            // Several passes because some components depend on others.
            for (int pass = 0; pass < 4; pass++)
                foreach (var component in model.GetComponentsInChildren<Component>(true))
                    if (component != null && !(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer) && !(component is SkinnedMeshRenderer))
                        Object.DestroyImmediate(component);

            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 0;

            model.transform.SetParent(parent, false);
            model.transform.localPosition = localPosition;
            model.transform.localRotation = Quaternion.identity;
            model.SetActive(false);

            Undo.RegisterCreatedObjectUndo(model, "Create " + name);
            return model;
        }

        private static Vector3 dropPoint()
        {
            var view = SceneView.lastActiveSceneView;
            var point = view != null ? view.pivot : Vector3.zero;

            if (Physics.Raycast(point + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            return point;
        }

        private static T load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogWarning("[Vantage] Missing asset: " + path);
            return asset;
        }

        #endregion
    }
}
