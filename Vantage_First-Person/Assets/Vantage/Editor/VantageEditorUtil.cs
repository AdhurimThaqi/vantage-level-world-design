using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Vantage.EditorTools
{
    /// <summary>Shared editor helpers: the level scene, prefab paths, screenshots and the occlusion bake.</summary>
    public static class VantageEditorUtil
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        public const string DronePrefabPath = "Assets/Vantage/Prefabs/Drone.prefab";
        public const string TurretPrefabPath = "Assets/Vantage/Prefabs/Turret.prefab";
        public const string PlayerRigPath = "Assets/Vantage/Prefabs/Third Person Rig.prefab";

        /// <summary>Value after a batch-mode argument (e.g. -vantageShots folder), or null.</summary>
        public static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
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

        /// <summary>Renders one 1280x720 view into folder/name.png (works in batch mode).</summary>
        public static void Shot(string folder, string name, Vector3 position, Vector3 target, bool orthographic, float size)
        {
            const int width = 1280, height = 720;
            // Compile shaders first, otherwise batch screenshots show the async-compile placeholder colour.
            ShaderUtil.allowAsyncCompilation = false;
            var go = new GameObject("Screenshot Camera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = position;
                cam.transform.LookAt(target);
                cam.fieldOfView = 65f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 3000f;
                cam.orthographic = orthographic;
                cam.orthographicSize = size;
                cam.useOcclusionCulling = false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = !orthographic;

                var rt = new RenderTexture(width, height, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(folder);
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
