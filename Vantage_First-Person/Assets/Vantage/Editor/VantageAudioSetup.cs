using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Vantage → Audio → Refresh Sound Bank: makes Assets/Audio and its folders, the bank (every VantageSounds event
    /// with the spec's defaults; settings already edited are kept), fills each event's clips from the WAVs named
    /// EVENT.wav or EVENT_NN.wav anywhere under Assets/Audio, picks up Assets/Audio/VantageMixer.mixer when it exists,
    /// and sets up the scene object "VANTAGE Audio". Run it after every render.
    /// </summary>
    public static class VantageAudioSetup
    {
        public const string AudioFolder = "Assets/Audio";
        public const string BankPath = AudioFolder + "/VantageSoundBank.asset";
        public const string MixerPath = AudioFolder + "/VantageMixer.mixer";
        public const string ObjectName = "VANTAGE Audio";
        private static readonly string[] Folders = { "Ambience", "SFX", "Voice", "Music", "UI" };

        /// <summary>The spec's defaults: group, 2D (0) or 3D (1), loop, min and max distance.</summary>
        private static readonly Dictionary<string, (string group, float spatial, bool loop, float min, float max)> Defaults =
            new Dictionary<string, (string, float, bool, float, float)>
            {
                { VantageSounds.AmbExterior, ("Ambience", 0, true, 1, 50) },
                { VantageSounds.AmbL1, ("Ambience", 0, true, 1, 50) },
                { VantageSounds.AmbL2, ("Ambience", 0, true, 1, 50) },
                { VantageSounds.AmbL3, ("Ambience", 0, true, 1, 50) },
                { VantageSounds.AmbHelipad, ("Ambience", 0, true, 1, 50) },
                { VantageSounds.AmbDebris, ("Ambience", 1, false, 3, 30) },
                { VantageSounds.FsConcrete, ("SFX", 1, false, 1, 20) },
                { VantageSounds.FsMetal, ("SFX", 1, false, 1, 20) },
                { VantageSounds.FsDirt, ("SFX", 1, false, 1, 20) },
                { VantageSounds.AlarmSiren, ("SFX", 1, true, 6, 70) },
                { VantageSounds.ShutterSlam, ("SFX", 1, false, 4, 45) },
                { VantageSounds.ShutterRoll, ("SFX", 1, false, 3, 35) },
                { VantageSounds.KeyPickup, ("SFX", 1, false, 1, 15) },
                { VantageSounds.KeySlotTurn, ("SFX", 1, false, 1, 15) },
                { VantageSounds.SentryGlint, ("SFX", 0, false, 1, 50) },
                { VantageSounds.SentryCrack, ("SFX", 1, false, 20, 300) },
                { VantageSounds.SentryWarningCall, ("Voice", 1, false, 15, 200) },
                { VantageSounds.VoContact, ("Voice", 1, false, 3, 40) },
                { VantageSounds.RobotDeflect, ("SFX", 1, false, 3, 40) },
                { VantageSounds.RobotServo, ("SFX", 1, true, 3, 35) },
                { VantageSounds.RobotTelegraph, ("SFX", 1, false, 4, 45) },
                { VantageSounds.DroneHum, ("SFX", 1, true, 2, 25) },
                { VantageSounds.HeliRotor, ("SFX", 1, true, 15, 250) },
                { VantageSounds.UiMove, ("UI", 0, false, 1, 50) },
                { VantageSounds.UiSelect, ("UI", 0, false, 1, 50) },
                { VantageSounds.CoinCollect, ("SFX", 1, false, 1, 15) },
                { VantageSounds.PickupWeapon, ("SFX", 1, false, 1, 15) },
                { VantageSounds.MusExplore, ("Music", 0, true, 1, 50) },
                { VantageSounds.MusCombat, ("Music", 0, true, 1, 50) },
                { VantageSounds.StgLockdown, ("Music", 0, false, 1, 50) },
                { VantageSounds.StgCleared, ("Music", 0, false, 1, 50) },
                { VantageSounds.StgExtraction, ("Music", 0, false, 1, 50) },
                { VantageSounds.MusMenu, ("Music", 0, true, 1, 50) },
                { VantageSounds.GuidePa, ("SFX", 1, true, 2, 30) },
                { VantageSounds.GuideStairs, ("SFX", 1, true, 2, 30) },
                { VantageSounds.GuideRadio, ("SFX", 1, true, 2, 30) },
                { VantageSounds.GuidePhone, ("SFX", 1, true, 2, 30) },
                { VantageSounds.KeySlotBeep, ("SFX", 1, true, 2, 30) },
                { VantageSounds.GuideWind, ("Ambience", 1, true, 2, 30) },
                { VantageSounds.GuideHeliRadio, ("SFX", 1, true, 2, 30) },
            };

        private static bool isGuide(string name) => name.StartsWith("GUIDE_") || name == VantageSounds.KeySlotBeep;

        [MenuItem("Vantage/Audio/Refresh Sound Bank", priority = 60)]
        public static void Refresh()
        {
            var bank = RefreshBank();
            AddFootsteps();
            SetupScene(bank);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Batch: -executeMethod Vantage.EditorTools.VantageAudioSetup.RefreshBatch (saves the scene).</summary>
        public static void RefreshBatch()
        {
            var exitCode = 0;
            try
            {
                var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
                Refresh();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>The bank with every event and its clips from the WAVs under Assets/Audio.</summary>
        public static VantageSoundBank RefreshBank()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder)) AssetDatabase.CreateFolder("Assets", "Audio");
            foreach (var f in Folders)
                if (!AssetDatabase.IsValidFolder($"{AudioFolder}/{f}")) AssetDatabase.CreateFolder(AudioFolder, f);
            var production = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Audio_Production"));
            Directory.CreateDirectory(production);

            var bank = AssetDatabase.LoadAssetAtPath<VantageSoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<VantageSoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            foreach (var name in VantageSounds.All)
            {
                if (bank.Events.Any(e => e.Name == name)) continue;
                var d = Defaults.TryGetValue(name, out var v) ? v : ("SFX", 1f, false, 2f, 40f);
                bank.Events.Add(new SoundEvent { Name = name, Group = d.Item1, Spatial = d.Item2, Loop = d.Item3, MinDistance = d.Item4, MaxDistance = d.Item5,
                    Pitch = d.Item1 == "Music" || d.Item1 == "Ambience" || isGuide(name) ? Vector2.one : new Vector2(0.95f, 1.05f),
                    Volume = isGuide(name) ? 0.8f : 1f });
            }

            // EVENT.wav or EVENT_NN.wav, anywhere under Assets/Audio.
            var byEvent = new Dictionary<string, List<AudioClip>>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var stem = Regex.Replace(Path.GetFileNameWithoutExtension(path), @"_\d+$", "");
                if (!byEvent.TryGetValue(stem, out var list)) byEvent[stem] = list = new List<AudioClip>();
                list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            var filled = 0;
            var unknown = byEvent.Keys.Where(k => !VantageSounds.All.Contains(k)).ToList();
            foreach (var e in bank.Events)
            {
                e.Clips = byEvent.TryGetValue(e.Name, out var clips) ? clips.OrderBy(c => c.name).ToList() : new List<AudioClip>();
                if (e.Filled) filled++;
            }
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Vantage] Sound bank refreshed: {filled} / {bank.Events.Count} events filled." +
                      (unknown.Count > 0 ? $" Files with no event (check the name): {string.Join(", ", unknown)}." : ""));
            return bank;
        }

        /// <summary>Surface footsteps on the player rig and the soldier (prefab components: the motor collects listeners in Awake).</summary>
        public static void AddFootsteps()
        {
            foreach (var path in new[] { VantageEditorUtil.PlayerRigPath, "Assets/Vantage/Prefabs/Soldier.prefab" })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.Log($"[Vantage] Footsteps: FAIL no prefab at {path}."); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // The character is the object with the motor (the rig's player is a child of the rig).
                    var motor = root.GetComponentInChildren<CoverShooter.CharacterMotor>(true);
                    if (motor == null) { Debug.Log($"[Vantage] Footsteps: FAIL no CharacterMotor in {path}."); continue; }
                    if (motor.GetComponent<VantageFootsteps>() == null) motor.gameObject.AddComponent<VantageFootsteps>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[Vantage] Footsteps on {motor.name} in {path}.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        /// <summary>The scene object that plays everything.</summary>
        public static GameObject SetupScene(VantageSoundBank bank)
        {
            var go = GameObject.Find(ObjectName);
            if (go == null) go = new GameObject(ObjectName);
            // Not ??: Unity's fake-null objects slip past it in the editor.
            var audio = go.GetComponent<VantageAudio>();
            if (audio == null) audio = go.AddComponent<VantageAudio>();
            audio.Bank = bank;
            audio.Mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            EditorUtility.SetDirty(audio);
            if (go.GetComponent<VantageAudioZones>() == null) go.AddComponent<VantageAudioZones>();   // its presets are the user's once added
            if (go.GetComponent<VantageMusic>() == null) go.AddComponent<VantageMusic>();
            Debug.Log($"[Vantage] Audio object: bank {(bank != null)}, mixer {(audio.Mixer != null ? "found" : "not built yet (everything plays to the output)")}.");
            return go;
        }
    }
}
