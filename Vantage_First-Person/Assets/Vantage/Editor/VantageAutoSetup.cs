using System.Collections.Generic;
using System.IO;
using CoverShooter;
using StarterAssets;
using UnityEditor;
using UnityEditor.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Runs every setup step on the level scene in one go: layers, URP materials, the template's third-person rig,
    /// the VANTAGE tower, first-person player at the entrance, enemies per level, V-key view switch, NavMesh, save.
    /// Safe to run again: the tower, enemies and rig are rebuilt, everything else in the scene is left alone.
    /// Command line: Unity.exe -batchmode -projectPath ... -executeMethod Vantage.EditorTools.VantageAutoSetup.RunBatch
    /// </summary>
    public static class VantageAutoSetup
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string EnemiesName = "VANTAGE Enemies";

        [MenuItem("Vantage/Set Up My Scene (Everything)", priority = 0)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Run(null);
        }

        public static void RunBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            var shots = System.Array.IndexOf(args, "-vantageShots");
            var exitCode = 0;

            try
            {
                Run(shots >= 0 && shots + 1 < args.Length ? args[shots + 1] : null);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Rewrites the scene and the rig prefab in the project's serialization format (Force Text).
        /// </summary>
        public static void ReserializeBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VantageSetup.BakeNavMesh();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { ScenePath, VantageThirdPerson.RigPrefabPath }, ForceReserializeAssetsOptions.ReserializeAssets);
            Debug.Log("[Vantage] Reserialized " + ScenePath);
            EditorApplication.Exit(0);
        }

        private static void Run(string screenshotFolder)
        {
            Debug.Log("[Vantage] Auto setup started.");

            VantageLayers.Ensure();

            Debug.Log("[Vantage] Converting Built-in materials to URP...");
            Converters.RunInBatchMode(ConverterContainerId.BuiltInToURP, new List<ConverterId> { ConverterId.Material }, ConverterFilter.Inclusive);
            VantageSetup.FixParticleMaterials();
            AssetDatabase.SaveAssets();

            VantageThirdPerson.EnsureRigPrefab();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VantageThirdPerson.SourceScenePath) != null)
                AssetDatabase.DeleteAsset(Path.GetDirectoryName(VantageThirdPerson.SourceScenePath).Replace('\\', '/'));

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var controller = Object.FindFirstObjectByType<FirstPersonController>();
            if (controller == null)
                throw new System.Exception("No FirstPersonController (PlayerCapsule) in " + ScenePath);

            // The Starter Assets cameras, collected before the third-person rig adds its own.
            var firstPersonCameras = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.Contains("PlayerFollowCamera") || root.GetComponent<Camera>() != null && root.CompareTag("MainCamera"))
                    firstPersonCameras.Add(root);

            VantageSetup.SetUpPlayer();

            var weapons = controller.GetComponentInChildren<VantageWeapons>(true);
            if (weapons != null && weapons.Weapons.Count > 0)
            {
                weapons.Weapons[0].Unlocked = false; // concept doc: start unarmed, pistol is behind the reception desk
                EditorUtility.SetDirty(weapons);
            }

            VantageThirdPerson.AddToScene(controller.GetComponent<VantagePlayer>(), firstPersonCameras.ToArray());

            // Tower, approach, enemies, level 2 generator, waves, mood, logging, NavMesh.
            // Also switches the level to third person only (removes the first-person rig set up above).
            var tower = VantageEnhance.Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Vantage] Auto setup finished. Scene saved: " + ScenePath);

            if (!string.IsNullOrEmpty(screenshotFolder))
                takeScreenshots(screenshotFolder, tower);
        }

        private static void takeScreenshots(string folder, VantageTowerBuilder.Result tower)
        {
            Directory.CreateDirectory(folder);
            var root = tower.Root.transform;

            shot(folder, "1_approach", tower.PlayerStart + Vector3.up * 1.7f, root.TransformPoint(new Vector3(0, 10f, 0)));
            shot(folder, "2_aerial", root.TransformPoint(new Vector3(-38f, 34f, -34f)), root.TransformPoint(new Vector3(0, 8f, 0)));
            shot(folder, "3_lobby", root.TransformPoint(new Vector3(0, 1.7f, -7.5f)), root.TransformPoint(new Vector3(4f, 3f, 4f)));
            shot(folder, "4_level2", root.TransformPoint(new Vector3(6.8f, 8.9f, 7.4f)), root.TransformPoint(new Vector3(-6f, 8.5f, 7f)));
            shot(folder, "5_level3", root.TransformPoint(new Vector3(-8f, 12.9f, 2.4f)), root.TransformPoint(new Vector3(3.5f, 13f, 0f)));
            shot(folder, "6_roof", root.TransformPoint(new Vector3(3.5f, 17.4f, -6f)), root.TransformPoint(new Vector3(-4.5f, 22f, 4.5f)));
        }

        private static void shot(string folder, string name, Vector3 position, Vector3 target)
        {
            const int width = 1280, height = 720;
            var go = new GameObject("Screenshot Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = position;
                cam.transform.LookAt(target);
                cam.fieldOfView = 65f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 3000f;

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
    }
}
