using System.Collections.Generic;
using CoverShooter;
using StarterAssets;
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

        private const string SoldierPrefab = Prefabs + "Soldier.prefab";
        private const string PistolPrefab = Prefabs + "Pistol.prefab";
        private const string RiflePrefab = Prefabs + "Rifle.prefab";

        #region Player

        [MenuItem("Vantage/1. Set Up Player For Combat", priority = 1)]
        public static void SetUpPlayer()
        {
            VantageLayers.Ensure();

            var controller = Object.FindFirstObjectByType<FirstPersonController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Vantage", "No FirstPersonController in the open scene. Drag in Starter Assets > FirstPersonController > Prefabs > PlayerCapsule first.", "OK");
                return;
            }

            var player = controller.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(player, "Set Up Vantage Player");

            // The AI only finds targets on the Character layer.
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = VantageLayers.Character;

            getOrAdd<VantagePlayer>(player);
            getOrAdd<VantageHUD>(player);

            var cameraRoot = controller.CinemachineCameraTarget != null ? controller.CinemachineCameraTarget : player;
            var weapons = getOrAdd<VantageWeapons>(cameraRoot);

            if (weapons.Weapons.Count == 0)
            {
                var holder = new GameObject("Viewmodels");
                Undo.RegisterCreatedObjectUndo(holder, "Create Viewmodels");
                holder.transform.SetParent(cameraRoot.transform, false);

                weapons.Weapons.Add(createPistol(holder.transform));
                weapons.Weapons.Add(createRifle(holder.transform));
                weapons.ImpactEffect = load<GameObject>(Prefabs + "HitAnythingParticles.prefab");
            }
            else
                Debug.Log("[Vantage] VantageWeapons already has weapons, left them unchanged.");

            EditorUtility.SetDirty(weapons);
            EditorSceneManager.MarkSceneDirty(player.scene);
            Selection.activeGameObject = player;

            Debug.Log("[Vantage] Player set up: BaseActor (AI target, side 1), CharacterHealth, VantagePlayer, VantageHUD, VantageWeapons (pistol unlocked, rifle locked).");
        }

        private static WeaponSlot createPistol(Transform holder)
        {
            var model = createViewmodel(PistolPrefab, "Pistol", holder, new Vector3(0.18f, -0.2f, 0.4f));

            return new WeaponSlot
            {
                Name = "Pistol",
                Unlocked = true,
                Automatic = false,
                Damage = 25,
                FireRate = 4,
                MagazineSize = 12,
                ReserveAmmo = 36,
                ReloadTime = 1.2f,
                Spread = 0.6f,
                HitType = HitType.Pistol,
                Model = model,
                Muzzle = model != null ? createMuzzle(model) : null,
                MuzzleFlash = load<GameObject>(Prefabs + "PistolShootParticles.prefab"),
                FireSounds = loadAll<AudioClip>(Sounds + "FIREARM_Handgun_B_FS92_9mm_Fire_Short_Reverb_Tail_RR1_stereo.wav",
                                                Sounds + "FIREARM_Handgun_B_FS92_9mm_Fire_Short_Reverb_Tail_RR2_stereo.wav"),
                ReloadSound = load<AudioClip>(Sounds + "RELOAD_Rechamber_Leaver_Action_stereo.wav"),
            };
        }

        private static WeaponSlot createRifle(Transform holder)
        {
            var model = createViewmodel(RiflePrefab, "Rifle", holder, new Vector3(0.16f, -0.22f, 0.3f));

            return new WeaponSlot
            {
                Name = "Rifle",
                Unlocked = false,
                Automatic = true,
                Damage = 34,
                FireRate = 9,
                MagazineSize = 30,
                ReserveAmmo = 90,
                ReloadTime = 2f,
                Spread = 1.2f,
                HitType = HitType.Rifle,
                Model = model,
                Muzzle = model != null ? createMuzzle(model) : null,
                MuzzleFlash = load<GameObject>(Prefabs + "RifleShootParticles.prefab"),
                FireSounds = loadAll<AudioClip>(Sounds + "FIREARM_Assault_Rifle_Model_01_Fire_Single_RR1_stereo.wav",
                                                Sounds + "FIREARM_Assault_Rifle_Model_01_Fire_Single_RR2_stereo.wav",
                                                Sounds + "FIREARM_Assault_Rifle_Model_01_Fire_Single_RR3_stereo.wav"),
                ReloadSound = load<AudioClip>(Sounds + "RELOAD_Eject_Leaver_Action_stereo.wav"),
            };
        }

        #endregion

        #region Level content

        [MenuItem("Vantage/2. Add Enemy Soldier At Scene View", priority = 2)]
        public static void AddSoldier()
        {
            var prefab = load<GameObject>(SoldierPrefab);
            if (prefab == null)
                return;

            var soldier = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            soldier.transform.position = dropPoint();
            Undo.RegisterCreatedObjectUndo(soldier, "Add Enemy Soldier");
            Selection.activeGameObject = soldier;
        }

        [MenuItem("Vantage/3. Bake NavMesh For Enemies", priority = 3)]
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
        /// so it can be used as a first-person model or a pickup visual.
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

        /// <summary>
        /// Muzzle point at the front of the model's renderers.
        /// </summary>
        private static Transform createMuzzle(GameObject model)
        {
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model.transform, false);

            var hasBounds = false;
            var bounds = new Bounds();

            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = model.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));

                    if (!hasBounds)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                        bounds.Encapsulate(local);
                }
            }

            muzzle.localPosition = hasBounds ? new Vector3(bounds.center.x, bounds.center.y + bounds.extents.y * 0.4f, bounds.max.z) : new Vector3(0, 0, 0.3f);
            return muzzle;
        }

        private static Vector3 dropPoint()
        {
            var view = SceneView.lastActiveSceneView;
            var point = view != null ? view.pivot : Vector3.zero;

            if (Physics.Raycast(point + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            return point;
        }

        private static T getOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static T load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogWarning("[Vantage] Missing asset: " + path);
            return asset;
        }

        private static T[] loadAll<T>(params string[] paths) where T : Object
        {
            var list = new List<T>();
            foreach (var path in paths)
            {
                var asset = load<T>(path);
                if (asset != null)
                    list.Add(asset);
            }
            return list.ToArray();
        }

        #endregion
    }
}
