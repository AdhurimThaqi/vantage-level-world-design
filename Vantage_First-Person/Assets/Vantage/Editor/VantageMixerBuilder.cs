using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Builds Assets/Audio/VantageMixer.mixer with the names the code uses (the contract in CLAUDE.md and the audio
    /// guide). Unity has no public API to create a mixer, so this drives the editor's own mixer classes by reflection
    /// (Unity 6000.0); it fails with a clear log line if they change.
    ///
    ///   Master                       exposed MasterVolume (the menu's Master slider)
    ///   ├ Music Volume → Music       MusicVolume on the bus; snapshots move Music
    ///   ├ Ambience Volume → Ambience AmbienceVolume on the bus (Effects slider); snapshots move Ambience
    ///   ├ SFX Volume → SFX           SfxVolume on the bus (Effects slider); SFX sends to Reverb (ReverbWet)
    ///   ├ Voice Volume → Voice       VoiceVolume on the bus (Effects slider); Voice sends to Reverb (fixed −10 dB)
    ///   ├ UI
    ///   └ Reverb                     Receive → SFX Reverb (ReverbRoom, ReverbDecay)
    ///
    /// The sliders sit on the buses and the snapshots (Explore, Combat, Cinematic) on the groups below them, so the two
    /// never fight over one fader (a parameter set from script stops following snapshots). An existing mixer is never
    /// overwritten: tune it in Window ▸ Audio ▸ Audio Mixer.
    /// </summary>
    public static class VantageMixerBuilder
    {
        public const string Path = VantageAudioSetup.MixerPath;
        private const string Ns = "UnityEditor.Audio.";
        private static readonly Assembly Editor = typeof(UnityEditor.Editor).Assembly;
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>Snapshot fader values (dB) per group; anything not listed is 0 dB.</summary>
        private static readonly Dictionary<string, Dictionary<string, float>> SnapshotDb = new Dictionary<string, Dictionary<string, float>>
        {
            { "Explore", new Dictionary<string, float> { { "Music", -2f } } },
            { "Combat", new Dictionary<string, float> { { "Music", -2f }, { "Ambience", -6f } } },
            { "Cinematic", new Dictionary<string, float> { { "Music", 0f }, { "SFX", -6f }, { "Ambience", -3f }, { "Voice", -3f } } },
        };

        [MenuItem("Vantage/Audio/Build Mixer (if missing)", priority = 61)]
        public static void BuildMenu()
        {
            var mixer = Build();
            if (mixer != null) { Selection.activeObject = mixer; EditorGUIUtility.PingObject(mixer); }
        }

        public static void BuildBatch() => Build();

        /// <summary>The mixer at Path, built first when missing (null when it could not be built).</summary>
        public static AudioMixer Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
            if (existing != null) { Debug.Log($"[Vantage] Mixer: {Path} exists, left as it is."); return existing; }
            try
            {
                var controllerType = type("AudioMixerController");
                var c = (AudioMixer)controllerType.GetMethod("CreateMixerControllerAtPath", Any).Invoke(null, new object[] { Path });
                var master = get(c, "masterGroup");

                object group(string name, object parent)
                {
                    var g = call(c, "CreateNewGroup", name, false);
                    call(c, "AddChildToParent", g, parent);
                    return g;
                }
                object effect(object owner, string name, int index)
                {
                    var e = Activator.CreateInstance(type("AudioMixerEffectController"), name);
                    call(c, "AddNewSubAsset", e, false);
                    call(e, "PreallocateGUIDs");
                    call(owner, "InsertEffect", e, index);
                    return e;
                }
                int count(object g) => ((Array)get(g, "effects")).Length;

                var groups = new Dictionary<string, object>();
                var buses = new Dictionary<string, object>();
                foreach (var name in new[] { "Music", "Ambience", "SFX", "Voice" })
                {
                    buses[name] = group(name + " Volume", master);
                    groups[name] = group(name, buses[name]);
                }
                groups["UI"] = group("UI", master);
                var reverb = groups["Reverb"] = group("Reverb", master);

                // Reverb: a Receive, then the reverb, before the group's own fader.
                var receive = effect(reverb, "Receive", 0);
                var room = effect(reverb, "SFX Reverb", 1);
                var sfxSend = effect(groups["SFX"], "Send", count(groups["SFX"]));
                set(sfxSend, "sendTarget", receive);
                var voiceSend = effect(groups["Voice"], "Send", count(groups["Voice"]));
                set(voiceSend, "sendTarget", receive);

                // Snapshots: the default one becomes Explore, then Combat and Cinematic.
                var snapshots = ((Array)get(c, "snapshots")).Cast<object>().ToList();
                ((UnityEngine.Object)snapshots[0]).name = "Explore";
                foreach (var name in new[] { "Combat", "Cinematic" })
                {
                    call(c, "CloneNewSnapshotFromTarget", false);
                    var all = ((Array)get(c, "snapshots")).Cast<object>().ToList();
                    ((UnityEngine.Object)all[all.Count - 1]).name = name;
                }
                snapshots = ((Array)get(c, "snapshots")).Cast<object>().ToList();
                foreach (var s in snapshots)
                {
                    var name = ((UnityEngine.Object)s).name;
                    foreach (var g in groups)
                        call(g.Value, "SetValueForVolume", c, s, SnapshotDb.TryGetValue(name, out var db) && db.TryGetValue(g.Key, out var v) ? v : 0f);
                    call(voiceSend, "SetValueForMixLevel", c, s, -10f);
                }
                set(c, "startSnapshot", snapshots[0]);
                set(c, "TargetSnapshot", snapshots[0]);

                // Exposed parameters, named as the code expects.
                var exposed = new List<(string name, object path)>
                {
                    ("MasterVolume", groupPath(master, call(master, "GetGUIDForVolume"))),
                    ("MusicVolume", groupPath(buses["Music"], call(buses["Music"], "GetGUIDForVolume"))),
                    ("AmbienceVolume", groupPath(buses["Ambience"], call(buses["Ambience"], "GetGUIDForVolume"))),
                    ("SfxVolume", groupPath(buses["SFX"], call(buses["SFX"], "GetGUIDForVolume"))),
                    ("VoiceVolume", groupPath(buses["Voice"], call(buses["Voice"], "GetGUIDForVolume"))),
                    ("ReverbRoom", effectPath(reverb, room, call(room, "GetGUIDForParameter", "Room"))),
                    ("ReverbDecay", effectPath(reverb, room, call(room, "GetGUIDForParameter", "Decay Time"))),
                    ("ReverbWet", effectPath(groups["SFX"], sfxSend, call(sfxSend, "GetGUIDForMixLevel"))),
                };
                foreach (var (_, path) in exposed) call(c, "AddExposedParameter", path);
                var parameters = (Array)get(c, "exposedParameters");
                var parameterType = type("ExposedAudioParameter");
                var guidField = parameterType.GetField("guid", Any);
                var nameField = parameterType.GetField("name", Any);
                foreach (var (name, path) in exposed)
                {
                    var guid = path.GetType().GetField("parameter", Any)?.GetValue(path) ?? guidOf(path);
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var p = parameters.GetValue(i);
                        if (!guidField.GetValue(p).Equals(guid)) continue;
                        nameField.SetValue(p, name);
                        parameters.SetValue(p, i);
                    }
                }
                set(c, "exposedParameters", parameters);

                call(c, "OnSubAssetChanged");
                EditorUtility.SetDirty(c);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
                var built = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
                Debug.Log($"[Vantage] Mixer built: {Path}, groups {string.Join(", ", built.FindMatchingGroups("Master").Select(g => g.name))}; " +
                          $"snapshots {string.Join(", ", new[] { "Explore", "Combat", "Cinematic" }.Where(n => built.FindSnapshot(n) != null))}; " +
                          $"exposed {string.Join(", ", ((Array)get(built, "exposedParameters")).Cast<object>().Select(p => (string)nameField.GetValue(p)))}.");
                return built;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Vantage] Mixer: could not build {Path} ({(e.InnerException ?? e).Message}). Build it by hand (Audio_Production/README.md, section 5).");
                return null;
            }
        }

        private static Type type(string name) => Editor.GetType(Ns + name) ?? throw new MissingMemberException(Ns + name);

        private static object call(object target, string method, params object[] args)
        {
            var t = target.GetType();
            var m = t.GetMethods(Any).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length)
                    ?? throw new MissingMethodException(t.Name, method);
            return m.Invoke(target, args);
        }

        private static object get(object target, string property) =>
            (target.GetType().GetProperty(property, Any) ?? throw new MissingMemberException(target.GetType().Name, property)).GetValue(target);

        private static void set(object target, string property, object value) =>
            (target.GetType().GetProperty(property, Any) ?? throw new MissingMemberException(target.GetType().Name, property)).SetValue(target, value);

        private static object groupPath(object group, object guid) => Activator.CreateInstance(type("AudioGroupParameterPath"), group, guid);

        private static object effectPath(object group, object effect, object guid) => Activator.CreateInstance(type("AudioEffectParameterPath"), group, effect, guid);

        /// <summary>The GUID an AudioParameterPath points at (its field name differs between versions).</summary>
        private static object guidOf(object path)
        {
            foreach (var f in path.GetType().GetFields(Any).Concat(path.GetType().BaseType.GetFields(Any)))
                if (f.FieldType.Name == "GUID") return f.GetValue(path);
            throw new MissingMemberException(path.GetType().Name, "GUID");
        }
    }
}
