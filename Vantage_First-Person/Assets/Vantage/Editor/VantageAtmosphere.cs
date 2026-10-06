using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Mood, sound and culling per space, following the concept doc's lighting table:
    ///   Lobby        warm low sun, long shadows      inviting, readable
    ///   Corridors    emergency lighting only         threat, disorientation
    ///   Fire escape  open sky, full exposure         vulnerability, first real view
    ///   Collapsed    shafts of daylight              dramatic, reveals the verticality
    ///   Roof         full golden hour                release
    /// Volumes sit on the Ignore Raycast layer so their trigger boxes never block bullets or AI sight;
    /// VantageEnhance adds that layer to the cameras' volume mask.
    /// </summary>
    public static class VantageAtmosphere
    {
        public const int VolumeLayer = 2;
        private const string Folder = "Assets/Vantage/Volumes";
        private const float F2 = 7.2f, F3 = 11.2f, FR = 15.7f;

        public static void Build(Transform parent)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Vantage", "Volumes");

            var volumes = new GameObject("Volumes (one per space)").transform;
            volumes.SetParent(parent, false);

            volume(volumes, "Lobby - inviting", new Vector3(0, F2 / 2, 0), new Vector3(17.2f, F2 - 0.4f, 17.2f), profile("Lobby", p =>
            {
                whiteBalance(p, 18f, 0f);
                colorAdjust(p, 0.15f, 5f, 10f, Color.white);
                bloom(p, 1.1f, 0.4f);
                vignette(p, 0.2f);
            }));

            volume(volumes, "Corridors - threat", new Vector3(0, (F2 + F3) / 2, 0), new Vector3(17.2f, F3 - F2 - 0.4f, 17.2f), profile("Corridors", p =>
            {
                colorAdjust(p, -0.35f, 20f, -35f, new Color(1f, 0.86f, 0.86f));
                vignette(p, 0.45f);
                grain(p, 0.35f);
                chromatic(p, 0.15f);
            }));

            volume(volumes, "Fire Escape - exposed", new Vector3(-10.3f, (F2 + FR) / 2, -2f), new Vector3(2.8f, FR - F2, 12.6f), profile("FireEscape", p =>
            {
                whiteBalance(p, 6f, 0f);
                colorAdjust(p, 0.3f, 8f, 5f, Color.white);
                vignette(p, 0.08f);
            }));

            volume(volumes, "Collapsed - dramatic", new Vector3(0, (F3 + FR) / 2, 0), new Vector3(17.2f, FR - F3 - 0.4f, 17.2f), profile("Collapsed", p =>
            {
                whiteBalance(p, -8f, 0f);
                colorAdjust(p, -0.1f, 25f, -10f, Color.white);
                bloom(p, 0.9f, 1f);
                vignette(p, 0.3f);
            }));

            volume(volumes, "Roof - golden hour", new Vector3(0, FR + 7f, 0), new Vector3(26f, 14f, 26f), profile("Roof", p =>
            {
                whiteBalance(p, 30f, 5f);
                colorAdjust(p, 0.15f, 10f, 15f, Color.white);
                bloom(p, 0.9f, 0.8f);
                vignette(p, 0.15f);
            }));

            buildSound(parent);
            buildOcclusionAreas(parent);
            AssetDatabase.SaveAssets();
        }

        #region Sound

        private static void buildSound(Transform parent)
        {
            var sound = new GameObject("Sound Guidance").transform;
            sound.SetParent(parent, false);

            // Each way up carries wind (and a hum from upstairs) that grows louder as the player approaches.
            ambience(sound, "Wind - top of stairwell", new Vector3(7.3f, F2 + 1.5f, 6f), VantageProceduralAudio.Kind.Wind, 0.55f, 3f, 22f, 0.9f);
            ambience(sound, "Hum - drones upstairs", new Vector3(5f, F2 + 2f, 2f), VantageProceduralAudio.Kind.DroneHum, 0.25f, 2f, 18f, 0.8f);
            ambience(sound, "Wind - fire escape door", new Vector3(-10.3f, F2 + 1.5f, -6.4f), VantageProceduralAudio.Kind.Wind, 0.8f, 3f, 20f, 1.1f);
            ambience(sound, "Wind - ramp to the roof", new Vector3(3.5f, FR + 1f, 4f), VantageProceduralAudio.Kind.Wind, 0.75f, 3f, 18f, 1.2f);
            ambience(sound, "Story 5 - radio static", new Vector3(6.1f, FR + 0.6f, 5.9f), VantageProceduralAudio.Kind.RadioStatic, 0.35f, 1f, 9f, 1f);
        }

        private static void ambience(Transform parent, string name, Vector3 local, VantageProceduralAudio.Kind kind, float volume, float min, float max, float pitch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<AudioSource>();
            var audio = go.AddComponent<VantageProceduralAudio>();
            audio.Sound = kind;
            audio.Volume = volume;
            audio.MinDistance = min;
            audio.MaxDistance = max;
            audio.Pitch = pitch;
        }

        #endregion

        #region Occlusion

        /// <summary>
        /// Each level is its own occlusion area (concept doc: only one level is ever visible from inside).
        /// Bake with Vantage > Performance > Bake Occlusion Culling.
        /// </summary>
        private static void buildOcclusionAreas(Transform parent)
        {
            var areas = new GameObject("Occlusion Areas").transform;
            areas.SetParent(parent, false);
            occlusion(areas, "Occlusion - Approach", new Vector3(0, 3f, -20f), new Vector3(30f, 6f, 24f));
            occlusion(areas, "Occlusion - L1 Lobby", new Vector3(0, F2 / 2, 0), new Vector3(18f, F2, 18f));
            occlusion(areas, "Occlusion - L2 Corridors", new Vector3(-1f, (F2 + F3) / 2, 0), new Vector3(23f, F3 - F2, 18f));
            occlusion(areas, "Occlusion - L3 Collapsed", new Vector3(-1f, (F3 + FR) / 2, 0), new Vector3(23f, FR - F3, 18f));
            occlusion(areas, "Occlusion - Roof", new Vector3(0, FR + 3f, 0), new Vector3(20f, 6f, 20f));
        }

        private static void occlusion(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var area = go.AddComponent<OcclusionArea>();
            area.center = Vector3.zero;
            area.size = size;
        }

        #endregion

        #region Volumes

        private static void volume(Transform parent, string name, Vector3 center, Vector3 size, VolumeProfile profile)
        {
            var go = new GameObject(name) { layer = VolumeLayer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            var v = go.AddComponent<Volume>();
            v.isGlobal = false;
            v.priority = 10;
            v.blendDistance = 1.5f;
            v.sharedProfile = profile;
        }

        private static VolumeProfile profile(string name, System.Action<VolumeProfile> setup)
        {
            var path = $"{Folder}/Vantage {name}.asset";
            AssetDatabase.DeleteAsset(path);

            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            setup(p);

            foreach (var component in p.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, p);
            }

            EditorUtility.SetDirty(p);
            return p;
        }

        private static void whiteBalance(VolumeProfile p, float temperature, float tint)
        {
            var c = p.Add<WhiteBalance>(true);
            c.temperature.Override(temperature);
            c.tint.Override(tint);
        }

        private static void colorAdjust(VolumeProfile p, float exposure, float contrast, float saturation, Color filter)
        {
            var c = p.Add<ColorAdjustments>(true);
            c.postExposure.Override(exposure);
            c.contrast.Override(contrast);
            c.saturation.Override(saturation);
            c.colorFilter.Override(filter);
        }

        private static void bloom(VolumeProfile p, float threshold, float intensity)
        {
            var c = p.Add<Bloom>(true);
            c.threshold.Override(threshold);
            c.intensity.Override(intensity);
        }

        private static void vignette(VolumeProfile p, float intensity)
        {
            var c = p.Add<Vignette>(true);
            c.intensity.Override(intensity);
            c.smoothness.Override(0.4f);
        }

        private static void grain(VolumeProfile p, float intensity)
        {
            var c = p.Add<FilmGrain>(true);
            c.intensity.Override(intensity);
        }

        private static void chromatic(VolumeProfile p, float intensity)
        {
            var c = p.Add<ChromaticAberration>(true);
            c.intensity.Override(intensity);
        }

        #endregion
    }
}
