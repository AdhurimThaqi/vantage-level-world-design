using System.Collections.Generic;
using System.IO;
using CoverShooter;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Second pass on the level: drones and turret, guard hut approach, procedural level 2, roof waves,
    /// per-space volumes and sound, occlusion areas, playtest logger and performance capture.
    /// Rebuilds the tower in place and replaces the enemies; the rest of the scene is left alone.
    /// </summary>
    public static class VantageEnhance
    {
        public const string DronePrefabPath = "Assets/Vantage/Prefabs/Drone.prefab";
        public const string TurretPrefabPath = "Assets/Vantage/Prefabs/Turret.prefab";
        private const string Prefabs = "Assets/ThirdPersonCoverShooter/Assets/Prefabs/";
        private const string Sounds = "Assets/ThirdPersonCoverShooter/Sounds/";
        private const string EnemiesName = "VANTAGE Enemies";
        private const string PlaytestName = "VANTAGE Playtest Tools";

        #region Entry points

        [MenuItem("Vantage/Enhance Scene (drones, waves, procedural L2, mood, logging)", priority = 1)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(VantageAutoSetup.ScenePath, OpenSceneMode.Single);
            Apply();
            EditorSceneManager.SaveScene(scene);
        }

        public static void RunBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(args, "-vantageShots");
            var shots = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            var exitCode = 0;

            try
            {
                VantageLayers.Ensure();
                var scene = EditorSceneManager.OpenScene(VantageAutoSetup.ScenePath, OpenSceneMode.Single);
                var tower = Apply();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[Vantage] Enhanced scene saved.");

                BakeOcclusion();
                EditorSceneManager.SaveScene(scene);

                if (!string.IsNullOrEmpty(shots))
                    screenshots(shots, tower);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Applies everything to the open scene. Returns the rebuilt tower.
        /// </summary>
        public static VantageTowerBuilder.Result Apply()
        {
            VantageTowerBuilder.DronePrefab = buildDronePrefab();
            var turretPrefab = buildTurretPrefab();

            // Reference point for a first build; a rebuild keeps the tower where it is.
            var character = findCharacter();
            var fps = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            var near = character != null ? character.transform.position : fps != null ? fps.transform.position : Vector3.zero;

            var tower = VantageTowerBuilder.Build(near);
            var scene = tower.Root.scene;

            setUpThirdPersonOnly(tower);
            placeEnemies(tower, turretPrefab);
            addPlaytestTools(tower);
            goldenHourSun(tower.Root.transform);
            setUpCameras();
            VantageSetup.BakeNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Vantage] Enhancements applied.");
            return tower;
        }

        private static CharacterMotor findCharacter()
        {
            foreach (var input in Object.FindObjectsByType<ThirdPersonInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                return input.GetComponent<CharacterMotor>();
            return null;
        }

        /// <summary>
        /// The template's third-person character becomes the only player: the first-person rig, its cameras
        /// and the V switch are removed, and the character starts unarmed at the tower approach.
        /// </summary>
        private static void setUpThirdPersonOnly(VantageTowerBuilder.Result tower)
        {
            var scene = tower.Root.scene;

            foreach (var viewSwitch in Object.FindObjectsByType<VantageViewSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (viewSwitch.FirstPersonCameras != null)
                    foreach (var cam in viewSwitch.FirstPersonCameras)
                        if (cam != null)
                            Object.DestroyImmediate(cam);
                Object.DestroyImmediate(viewSwitch.gameObject);
            }

            foreach (var fps in Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(fps.gameObject);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "MainCamera" || root.name.StartsWith("PlayerFollowCamera"))
                    Object.DestroyImmediate(root);

            var character = findCharacter();
            if (character == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VantageThirdPerson.RigPrefabPath);
                if (prefab == null)
                    throw new System.Exception("No third-person character in the scene and no rig prefab at " + VantageThirdPerson.RigPrefabPath);
                PrefabUtility.InstantiatePrefab(prefab);
                character = findCharacter();
            }

            var rig = character.transform.root.gameObject;
            rig.SetActive(true);
            character.gameObject.SetActive(true);
            character.transform.SetPositionAndRotation(tower.PlayerStart + Vector3.up * 0.05f, tower.PlayerFacing);

            if (character.GetComponent<VantageArsenal>() == null)
                character.gameObject.AddComponent<VantageArsenal>();
            if (character.GetComponent<VantageThirdPersonPlayer>() == null)
                character.gameObject.AddComponent<VantageThirdPersonPlayer>();
            var actor = character.GetComponent<BaseActor>();
            if (actor != null)
                actor.Side = 1;

            var tpsCamera = rig.GetComponentInChildren<ThirdPersonCamera>(true);
            if (tpsCamera != null)
            {
                tpsCamera.gameObject.tag = "MainCamera";
                tpsCamera.transform.SetPositionAndRotation(tower.PlayerStart - tower.PlayerFacing * Vector3.forward * 4f + Vector3.up * 2.2f, tower.PlayerFacing);
                if (tpsCamera.GetComponent<VantageCameraFader>() == null)
                    tpsCamera.gameObject.AddComponent<VantageCameraFader>();
            }

            Debug.Log("[Vantage] Third person only: " + character.name + " at the tower approach, unarmed.");
        }

        [MenuItem("Vantage/Level 2/Preview Layout (new seed)", priority = 30)]
        public static void PreviewLevel2()
        {
            var generator = Object.FindFirstObjectByType<VantageLevel2Generator>();
            if (generator == null)
                return;
            var seed = Random.Range(0, 100000);
            generator.Generate(seed, false);
            Debug.Log("[Vantage] Previewing level 2 seed " + seed + " (preview objects are not saved).");
        }

        [MenuItem("Vantage/Level 2/Clear Preview", priority = 31)]
        public static void ClearLevel2Preview()
        {
            var generator = Object.FindFirstObjectByType<VantageLevel2Generator>();
            if (generator != null)
                generator.Clear();
        }

        [MenuItem("Vantage/Performance/Bake Occlusion Culling", priority = 40)]
        public static void BakeOcclusion()
        {
            StaticOcclusionCulling.smallestOccluder = 2f;
            StaticOcclusionCulling.smallestHole = 0.3f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var ok = StaticOcclusionCulling.Compute();
            Debug.Log($"[Vantage] Occlusion culling bake {(ok ? "done" : "FAILED")} in {timer.Elapsed.TotalSeconds:F0}s.");
        }

        #endregion

        #region Enemies

        private static void placeEnemies(VantageTowerBuilder.Result tower, GameObject turretPrefab)
        {
            var old = GameObject.Find(EnemiesName);
            if (old != null)
                Object.DestroyImmediate(old);

            var group = new GameObject(EnemiesName).transform;
            var center = tower.Root.transform.position;
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Soldier.prefab");
            var counts = new Dictionary<VantageTowerBuilder.EnemyKind, int>();

            foreach (var spot in tower.EnemySpots)
            {
                GameObject prefab;
                switch (spot.Kind)
                {
                    case VantageTowerBuilder.EnemyKind.Soldier: prefab = soldier; break;
                    case VantageTowerBuilder.EnemyKind.Turret: prefab = turretPrefab; break;
                    default: prefab = VantageTowerBuilder.DronePrefab; break;
                }

                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                enemy.name = "Enemy - " + spot.Label;
                enemy.transform.position = spot.Position;
                var look = new Vector3(center.x, spot.Position.y, center.z) - spot.Position;
                if (spot.Kind == VantageTowerBuilder.EnemyKind.ApproachDrone || spot.Kind == VantageTowerBuilder.EnemyKind.Turret)
                    look = -tower.Root.transform.forward;
                if (look.sqrMagnitude > 0.01f)
                    enemy.transform.rotation = Quaternion.LookRotation(look);

                var drone = enemy.GetComponent<VantageDrone>();
                switch (spot.Kind)
                {
                    case VantageTowerBuilder.EnemyKind.Soldier:
                        enemy.AddComponent<VantageKillReporter>().EnemyName = "Soldier";
                        var actor = enemy.GetComponent<BaseActor>();
                        if (actor != null)
                            actor.Side = 0;
                        // The template's 35 damage is tuned for third person; drones now do most of the work.
                        foreach (var gun in enemy.GetComponentsInChildren<BaseGun>(true))
                            gun.Damage = 18f;
                        break;

                    case VantageTowerBuilder.EnemyKind.SlowDrone:
                        drone.Speed = 1.6f;
                        drone.DetectRange = 13f;
                        drone.FireInterval = 1.7f;
                        drone.Accuracy = 0.45f;
                        break;

                    case VantageTowerBuilder.EnemyKind.ApproachDrone:
                        drone.DetectRange = 9.5f;
                        drone.ChaseRange = 22f;
                        drone.PatrolRadius = 2f;
                        drone.Accuracy = 0.45f;
                        break;
                }

                counts[spot.Kind] = counts.TryGetValue(spot.Kind, out var n) ? n + 1 : 1;
            }

            var summary = new List<string>();
            foreach (var c in counts)
                summary.Add($"{c.Value} {c.Key}");
            Debug.Log("[Vantage] Enemies placed: " + string.Join(", ", summary) + " (+3 level 2 drones and 9 roof wave drones at runtime).");
        }

        #endregion

        #region Drone prefabs

        private static GameObject buildDronePrefab()
        {
            var root = new GameObject("Drone") { layer = VantageLayers.Character };
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.45f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            setHealth(root.AddComponent<CharacterHealth>(), 60f);
            root.AddComponent<AudioSource>().spatialBlend = 1f;

            var drone = root.AddComponent<VantageDrone>();
            drone.Mobile = true;

            var head = child(root.transform, "Head", Vector3.zero);
            part(head, PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(0.75f, 0.32f, 0.75f), Quaternion.identity, "Metal");
            part(head, PrimitiveType.Cube, "Sensor Housing", new Vector3(0, -0.05f, 0.22f), new Vector3(0.32f, 0.18f, 0.25f), Quaternion.identity, "Concrete Dark");
            var eye = part(head, PrimitiveType.Sphere, "Eye", new Vector3(0, -0.04f, 0.36f), Vector3.one * 0.15f, Quaternion.identity, "Glow Red");
            part(head, PrimitiveType.Cylinder, "Barrel", new Vector3(0, -0.17f, 0.3f), new Vector3(0.06f, 0.18f, 0.06f), Quaternion.Euler(90, 0, 0), "Metal");
            var muzzle = child(head, "Muzzle", new Vector3(0, -0.17f, 0.5f));

            var rotors = new List<Transform>();
            foreach (var corner in new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) })
            {
                var tip = corner * 0.38f;
                part(root.transform, PrimitiveType.Cube, "Arm", tip * 0.5f + Vector3.up * 0.05f, new Vector3(0.05f, 0.04f, 0.55f), Quaternion.LookRotation(corner), "Metal");
                var rotor = part(root.transform, PrimitiveType.Cylinder, "Rotor", tip + Vector3.up * 0.1f, new Vector3(0.36f, 0.006f, 0.36f), Quaternion.identity, "Concrete Dark");
                rotors.Add(rotor.transform);
            }

            wireDrone(drone, head, muzzle, eye, rotors.ToArray());

            var hum = child(root.transform, "Hum", Vector3.zero);
            hum.gameObject.AddComponent<AudioSource>();
            var humAudio = hum.gameObject.AddComponent<VantageProceduralAudio>();
            humAudio.Sound = VantageProceduralAudio.Kind.DroneHum;
            humAudio.Volume = 0.45f;
            humAudio.MinDistance = 1.5f;
            humAudio.MaxDistance = 16f;

            return save(root, DronePrefabPath);
        }

        private static GameObject buildTurretPrefab()
        {
            var root = new GameObject("Turret") { layer = VantageLayers.Character };
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.55f, 0);
            collider.size = new Vector3(0.9f, 1.1f, 0.9f);
            setHealth(root.AddComponent<CharacterHealth>(), 200f);
            root.AddComponent<AudioSource>().spatialBlend = 1f;

            var drone = root.AddComponent<VantageDrone>();
            drone.Mobile = false;
            drone.Damage = 6f;
            drone.FireInterval = 0.3f;
            drone.WindUp = 0.2f;
            drone.Accuracy = 0.35f;
            drone.Range = 42f;
            drone.DetectRange = 32f;
            drone.ChaseRange = 42f;

            part(root.transform, PrimitiveType.Cylinder, "Base", new Vector3(0, 0.2f, 0), new Vector3(0.8f, 0.2f, 0.8f), Quaternion.identity, "Metal");
            var head = child(root.transform, "Head", new Vector3(0, 0.75f, 0));
            part(head, PrimitiveType.Cube, "Head Box", Vector3.zero, new Vector3(0.6f, 0.4f, 0.7f), Quaternion.identity, "Mast Red");
            var eye = part(head, PrimitiveType.Sphere, "Eye", new Vector3(0, 0.08f, 0.36f), Vector3.one * 0.14f, Quaternion.identity, "Glow Red");
            part(head, PrimitiveType.Cylinder, "Barrel L", new Vector3(-0.13f, -0.06f, 0.55f), new Vector3(0.07f, 0.25f, 0.07f), Quaternion.Euler(90, 0, 0), "Metal");
            part(head, PrimitiveType.Cylinder, "Barrel R", new Vector3(0.13f, -0.06f, 0.55f), new Vector3(0.07f, 0.25f, 0.07f), Quaternion.Euler(90, 0, 0), "Metal");
            var muzzle = child(head, "Muzzle", new Vector3(0, -0.06f, 0.82f));

            wireDrone(drone, head, muzzle, eye, new Transform[0]);

            var hum = child(root.transform, "Hum", Vector3.zero);
            hum.gameObject.AddComponent<AudioSource>();
            var humAudio = hum.gameObject.AddComponent<VantageProceduralAudio>();
            humAudio.Sound = VantageProceduralAudio.Kind.DroneHum;
            humAudio.Volume = 0.35f;
            humAudio.Pitch = 0.6f;
            humAudio.MaxDistance = 20f;

            return save(root, TurretPrefabPath);
        }

        private static void wireDrone(VantageDrone drone, Transform head, Transform muzzle, GameObject eye, Transform[] rotors)
        {
            drone.Head = head;
            drone.Muzzle = muzzle;
            drone.Rotors = rotors;
            drone.Eye = eye.GetComponent<Renderer>();

            var lightObject = child(eye.transform.parent, "Eye Light", eye.transform.localPosition + Vector3.forward * 0.1f);
            var light = lightObject.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.25f, 0.1f);
            light.range = 4f;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            drone.EyeLight = light;

            var tracer = muzzle.gameObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.widthMultiplier = 0.035f;
            tracer.sharedMaterial = material("Glow Red");
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.enabled = false;
            drone.Tracer = tracer;

            drone.MuzzleFlash = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "PistolShootParticles.prefab");
            drone.HitEffect = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "HitAnythingParticles.prefab");
            drone.DeathExplosion = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "GranadeBigExplosion.prefab");
            drone.FireSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + "FIREARM_Handgun_B_FS92_9mm_Fire_Short_Reverb_Tail_RR1_stereo.wav");
            drone.DeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + "EXPLOSION_Medium_Bright_Kickback_stereo.wav");
        }

        private static void setHealth(CharacterHealth health, float value)
        {
            health.MaxHealth = value;
            health.Health = value;
        }

        private static Transform child(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static GameObject part(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Quaternion rotation, string materialName)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = parent.gameObject.layer;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material(materialName);
            return go;
        }

        private static Material material(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/Vantage/Materials/" + name + ".mat");
        }

        private static GameObject save(GameObject root, string path)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Vantage/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Vantage", "Prefabs");

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        #endregion

        #region Scene setup

        private static void addPlaytestTools(VantageTowerBuilder.Result tower)
        {
            var old = GameObject.Find(PlaytestName);
            if (old != null)
                Object.DestroyImmediate(old);

            var go = new GameObject(PlaytestName);
            go.AddComponent<VantagePlaytestLogger>().Tower = tower.Root.transform;
            go.AddComponent<VantagePerfCapture>();
        }

        /// <summary>
        /// Concept doc: warm low sun through broken glass, golden hour on the roof.
        /// The sun comes in low from the south-west, through the lobby's front and west windows.
        /// </summary>
        private static void goldenHourSun(Transform tower)
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional && (sun == null || light.intensity > sun.intensity))
                        sun = light;

            if (sun == null)
                return;

            Undo.RecordObject(sun, "Golden hour");
            Undo.RecordObject(sun.transform, "Golden hour");
            sun.transform.rotation = Quaternion.LookRotation(tower.rotation * Quaternion.Euler(14f, 40f, 0) * Vector3.forward);
            sun.color = new Color(1f, 0.76f, 0.52f);
            sun.intensity = Mathf.Max(sun.intensity, 1.4f);
            sun.shadows = LightShadows.Soft;
            Debug.Log("[Vantage] Sun set to golden hour: " + sun.name);
        }

        /// <summary>
        /// Post-processing on, and the Ignore Raycast layer (where the per-space volumes live) in the volume mask.
        /// </summary>
        private static void setUpCameras()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null)
                    continue;
                Undo.RecordObject(data, "Camera volumes");
                data.renderPostProcessing = true;
                data.volumeLayerMask |= 1 << VantageAtmosphere.VolumeLayer;
                cam.useOcclusionCulling = true;
                EditorUtility.SetDirty(data);
            }
        }

        #endregion

        #region Screenshots

        private static void screenshots(string folder, VantageTowerBuilder.Result tower)
        {
            Directory.CreateDirectory(folder);
            var root = tower.Root.transform;
            var docs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Documentation");
            Directory.CreateDirectory(docs);

            shot(folder, "1_approach", tower.PlayerStart - tower.PlayerFacing * Vector3.forward * 4f + Vector3.up * 2.2f, tower.PlayerStart + Vector3.up * 1.6f + tower.PlayerFacing * Vector3.forward * 6f, false, 0);
            shot(folder, "2_hut", root.TransformPoint(new Vector3(-3f, 1.8f, -19f)), root.TransformPoint(new Vector3(-7f, 1f, -20f)), false, 0);

            if (tower.Level2 != null)
            {
                foreach (var seed in new[] { 1234, 52071 })
                {
                    tower.Level2.Generate(seed, true);
                    shot(folder, $"3_level2_seed_{seed}", root.TransformPoint(new Vector3(7.2f, 8.9f, 6.5f)), root.TransformPoint(new Vector3(-5f, 8.6f, 2f)), false, 0);
                }
                tower.Level2.Clear();
                level2Plans(docs, root, tower.Level2);
            }

            shot(folder, "4_roof_turret", root.TransformPoint(new Vector3(4f, 17.4f, -6.5f)), root.TransformPoint(new Vector3(-4.5f, 20.5f, 4.5f)), false, 0);
            shot(folder, "5_aerial", root.TransformPoint(new Vector3(-30f, 30f, -38f)), root.TransformPoint(new Vector3(0, 6f, -6f)), false, 0);
            Debug.Log("[Vantage] Screenshots written; level 2 plans saved to " + docs);
        }

        /// <summary>
        /// Re-renders only the level 2 plan images into Documentation/. Does not modify the scene.
        /// </summary>
        public static void DocumentationBatch()
        {
            var exitCode = 0;
            try
            {
                EditorSceneManager.OpenScene(VantageAutoSetup.ScenePath, OpenSceneMode.Single);
                var tower = GameObject.Find(VantageTowerBuilder.TowerName);
                var generator = Object.FindFirstObjectByType<VantageLevel2Generator>();
                var docs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Documentation");
                Directory.CreateDirectory(docs);
                level2Plans(docs, tower.transform, generator);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Top-down plans of level 2 for two seeds: the layout varies, entry, exit and enemy count do not.
        /// Everything above level 2 is hidden for the render and shown again afterwards.
        /// </summary>
        private static void level2Plans(string folder, Transform root, VantageLevel2Generator generator)
        {
            var hidden = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n == "Floor L3" || n == "Roof Deck" || n == "Level 3 - Collapsed" || n == "Roof" || n == "Roof Waves" || n == "Fire Escape")
                    hidden.Add(t.gameObject);
            }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.transform.position.y > root.position.y + 11f && !r.transform.IsChildOf(root) && Vector3.Distance(Flat(r.transform.position), Flat(root.position)) < 14f)
                    hidden.Add(r.gameObject);

            foreach (var go in hidden)
                go.SetActive(false);

            var fog = RenderSettings.fog;
            RenderSettings.fog = false;
            // Unlit black so walls read as a clean plan, no sky reflections on their top faces.
            var wallColour = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            wallColour.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.1f));

            try
            {
                foreach (var seed in new[] { 1234, 52071 })
                {
                    generator.Generate(seed, true);

                    // Plan readability only: draw the generated walls dark against the floor.
                    foreach (var r in generator.GetComponentsInChildren<Renderer>(true))
                        if (r.name == "Lintel")
                            r.enabled = false; // show doorways as gaps
                        else if (r.name == "Wall")
                            r.sharedMaterial = wallColour;

                    shot(folder, $"level2_plan_seed_{seed}", root.TransformPoint(new Vector3(-1.5f, 40f, 0)), root.TransformPoint(new Vector3(-1.5f, 0f, 0.001f)), true, 10f);
                }
            }
            finally
            {
                RenderSettings.fog = fog;
                generator.Clear();
                foreach (var go in hidden)
                    if (go != null)
                        go.SetActive(true);
            }

            Debug.Log("[Vantage] Level 2 plans saved to " + folder);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        private static void shot(string folder, string name, Vector3 position, Vector3 target, bool orthographic, float size)
        {
            const int width = 1280, height = 720;
            // Compile shaders before rendering, otherwise batch screenshots show the async-compile placeholder colour.
            ShaderUtil.allowAsyncCompilation = false;
            var go = new GameObject("Screenshot Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = position;
                cam.transform.LookAt(target);
                cam.fieldOfView = 65f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 3000f;
                cam.orthographic = orthographic;
                cam.orthographicSize = size;
                // Baked occlusion would hide things behind floors that are only switched off for the render.
                cam.useOcclusionCulling = false;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = !orthographic;
                data.volumeLayerMask |= 1 << VantageAtmosphere.VolumeLayer;

                var rt = new RenderTexture(width, height, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(texture);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Vantage] Screenshot " + name + " failed: " + e.Message);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        #endregion
    }
}
