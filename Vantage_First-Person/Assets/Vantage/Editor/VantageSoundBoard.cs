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
    /// Vantage → Audio → Sound Board: every sound of the game in one place (2026-11-16).
    ///   Events: each event of the sound bank by category, where it plays in the game, its WAV files (click to find
    ///           them), volume, pitch, 2D/3D, distances, loop, mixer group, a play button; empty events marked.
    ///   Mix:    the mixer (open it in the Audio Mixer window), the zones' reverb rows, the music states, the guide
    ///           steps: the VANTAGE Audio components' own settings, drawn here.
    ///   Tuning: the bank's audio numbers that belong to no single event (fades, pitches, distances).
    /// Changes are saved to the bank asset / the scene like any Inspector edit (with undo).
    /// </summary>
    public class VantageSoundBoard : EditorWindow
    {
        private enum Tab { Events, Mix, Tuning }

        /// <summary>Where each event plays (the player-facing moment), by event name.</summary>
        private static readonly Dictionary<string, string> Where = new Dictionary<string, string>
        {
            { VantageSounds.MusMenu, "Title screen, until Play (fades out)" },
            { VantageSounds.MusIntro, "The intro flyover after Play, rings out into the first minute" },
            { VantageSounds.MusExplore, "Level music while nothing fights (layer 1)" },
            { VantageSounds.MusCombat, "Level music in a fight (layer 2, fades in over the explore layer)" },
            { VantageSounds.MusBossFight, "The boss fight, from the end of the boss reveal until it dies" },
            { VantageSounds.MusEnd, "From the boss's death through the extraction and end screen" },
            { VantageSounds.StgLockdown, "Stinger: the L1 alarm springs" },
            { VantageSounds.StgCleared, "Stinger: a level is cleared" },
            { VantageSounds.StgBossFight, "Stinger: the boss fight starts (end of the reveal)" },
            { VantageSounds.StgExtraction, "Stinger: the boss dies, the helicopter comes" },
            { VantageSounds.UiMove, "Menus: moving the selection (hover, W/S)" },
            { VantageSounds.UiSelect, "Menus: choosing a row (click, Enter)" },
            { VantageSounds.AmbExterior, "Ambience bed outside the tower" },
            { VantageSounds.AmbL1, "Ambience bed on the ground floor" },
            { VantageSounds.AmbL2, "Ambience bed in the armory (level 2)" },
            { VantageSounds.AmbL3, "Ambience bed on the destroyed floor (level 3)" },
            { VantageSounds.AmbHelipad, "Ambience bed on the helipad" },
            { VantageSounds.AmbDebris, "Level 3: crumbling debris every 6–14 s" },
            { VantageSounds.FsConcrete, "Footsteps on concrete (inside, stairs): player and soldiers" },
            { VantageSounds.FsMetal, "Footsteps on metal (grates, rails); concrete plays while empty" },
            { VantageSounds.FsDirt, "Footsteps on the terrain outside" },
            { VantageSounds.GunPistol, "The player's pistol shot" },
            { VantageSounds.GunEmpty, "Firing an empty gun (player)" },
            { VantageSounds.PlayerJump, "The player's jump" },
            { VantageSounds.BodyFall, "A body hitting the ground after a death" },
            { VantageSounds.VoContact, "A soldier spotting the player (bark)" },
            { VantageSounds.AlarmSiren, "L1 alarm siren loop" },
            { VantageSounds.ShutterSlam, "The maintenance shutter slamming down (alarm)" },
            { VantageSounds.ShutterRoll, "A shutter or stair gate rolling up" },
            { VantageSounds.KeyPickup, "Picking up the key (L2)" },
            { VantageSounds.KeySlotTurn, "The key turning in the slot (L2)" },
            { VantageSounds.SentryGlint, "A sentry's scope glinting before a shot" },
            { VantageSounds.SentryCrack, "A sentry's rifle shot (warning ring, intro)" },
            { VantageSounds.SentryWarningCall, "A sentry warning a player in the warning band" },
            { VantageSounds.DroneHum, "Drone hover loop (drones only)" },
            { VantageSounds.DroneFire, "A drone's shot (empty: ROBOT_Fire pitched up)" },
            { VantageSounds.TurretFire, "A turret's shot (empty: ROBOT_Fire pitched down)" },
            { VantageSounds.HeliGun, "The helicopter's cannon firing at the boss (empty: ROBOT_Fire pitched down)" },
            { VantageSounds.RobotFire, "The L3 robot's and the boss's shot" },
            { VantageSounds.RobotHit, "A rifle round hitting the robot or boss" },
            { VantageSounds.RobotDeflect, "A pistol round bouncing off the robot" },
            { VantageSounds.RobotServo, "The robot walking (loop)" },
            { VantageSounds.RobotTelegraph, "The robot winding up a burst" },
            { VantageSounds.BossReveal, "Under the boss reveal cutscene" },
            { VantageSounds.BossRoar, "The boss roaring in its reveal" },
            { VantageSounds.HeliRotor, "Helicopter rotor loop (title screen, extraction)" },
            { VantageSounds.CoinCollect, "Collecting a coin" },
            { VantageSounds.PickupWeapon, "Picking up a gun" },
            { VantageSounds.GuidePa, "Guide: PA at the turret hallway during the L1 lockdown" },
            { VantageSounds.GuideStairs, "Guide: from the stairs up after a level is cleared" },
            { VantageSounds.GuideRadio, "Guide: radio by the rifle (L2); also stands in for empty guide steps" },
            { VantageSounds.GuidePhone, "Guide: phone by the key (L2)" },
            { VantageSounds.KeySlotBeep, "Guide: the key slot beeping once you have the key (L2)" },
            { VantageSounds.GuideWind, "Guide: wind at the breach to the open area (L3)" },
            { VantageSounds.GuideHeliRadio, "Guide: helicopter radio over the pad (finale)" },
        };

        private static readonly (string title, Func<string, bool> match)[] Categories =
        {
            ("Music", n => n.StartsWith("MUS_") || n.StartsWith("STG_")),
            ("Interface", n => n.StartsWith("UI_")),
            ("Ambience", n => n.StartsWith("AMB_")),
            ("Guide (wayfinding)", n => n.StartsWith("GUIDE_") || n == VantageSounds.KeySlotBeep),
            ("Player and soldiers", n => n.StartsWith("FS_") || n.StartsWith("GUN_") || n.StartsWith("PLAYER_") || n.StartsWith("BODY_") || n.StartsWith("VO_")),
            ("Machines and boss", n => n.StartsWith("ROBOT_") || n.StartsWith("BOSS_") || n.StartsWith("DRONE_") || n.StartsWith("TURRET_") || n.StartsWith("HELI_")),
            ("Level", n => true),
        };

        private Tab _tab;
        private string _filter = "";
        private bool _emptyOnly;
        private Vector2 _scroll;
        private readonly HashSet<string> _open = new HashSet<string>();
        private VantageSoundBank _bank;
        private SerializedObject _bankObject;
        private readonly Dictionary<UnityEngine.Object, Editor> _editors = new Dictionary<UnityEngine.Object, Editor>();

        [MenuItem("Vantage/Audio/Sound Board", priority = 59)]
        public static void Open() => GetWindow<VantageSoundBoard>("Sound Board").minSize = new Vector2(520, 400);

        private void OnDisable()
        {
            stopPreview();
            foreach (var e in _editors.Values) if (e != null) DestroyImmediate(e);
            _editors.Clear();
        }

        private void OnGUI()
        {
            if (_bank == null) _bank = AssetDatabase.LoadAssetAtPath<VantageSoundBank>(VantageAudioSetup.BankPath);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Events", "Mix", "Tuning" }, EditorStyles.toolbarButton, GUILayout.Width(240));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Audio folder", EditorStyles.toolbarButton)) ping(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(VantageAudioSetup.AudioFolder));
                if (GUILayout.Button("Refresh Sound Bank", EditorStyles.toolbarButton)) { VantageAudioSetup.Refresh(); _bank = null; GUIUtility.ExitGUI(); }
            }
            if (_bank == null)
            {
                EditorGUILayout.HelpBox("No sound bank yet: press Refresh Sound Bank.", MessageType.Info);
                return;
            }
            if (_bankObject == null || _bankObject.targetObject != _bank) _bankObject = new SerializedObject(_bank);
            _bankObject.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Events: events(); break;
                case Tab.Mix: mix(); break;
                case Tab.Tuning: EditorGUILayout.PropertyField(_bankObject.FindProperty("Tuning"), true); break;
            }
            EditorGUILayout.EndScrollView();
            _bankObject.ApplyModifiedProperties();
        }

        #region Events

        private void events()
        {
            var filled = _bank.Events.Count(e => e.Filled);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{filled} / {_bank.Events.Count} events have sound. Files: Assets/Audio/<folder>/EVENT.wav or EVENT_01.wav …, then Refresh Sound Bank.", EditorStyles.miniLabel);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
                _emptyOnly = GUILayout.Toggle(_emptyOnly, "Empty only", GUILayout.Width(90));
                if (GUILayout.Button("Stop", GUILayout.Width(50))) stopPreview();
            }
            var events = _bankObject.FindProperty("Events");
            var shown = new HashSet<int>();
            foreach (var (title, match) in Categories)
            {
                var rows = new List<int>();
                for (int i = 0; i < _bank.Events.Count; i++)
                {
                    var e = _bank.Events[i];
                    if (shown.Contains(i) || !match(e.Name)) continue;
                    shown.Add(i);
                    if (_emptyOnly && e.Filled) continue;
                    if (_filter.Length > 0 && e.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0
                        && describe(e.Name).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    rows.Add(i);
                }
                if (rows.Count == 0) continue;
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                foreach (var i in rows) row(_bank.Events[i], events.GetArrayElementAtIndex(i));
            }
        }

        private void row(SoundEvent e, SerializedProperty p)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var open = _open.Contains(e.Name);
                var color = GUI.color;
                if (!e.Filled) GUI.color = new Color(1f, 0.6f, 0.5f);
                if (GUILayout.Button(open ? "▼" : "▶", EditorStyles.label, GUILayout.Width(14))) { if (open) _open.Remove(e.Name); else _open.Add(e.Name); }
                EditorGUILayout.LabelField(new GUIContent(e.Name, describe(e.Name)), EditorStyles.boldLabel, GUILayout.Width(170));
                GUI.color = color;
                EditorGUILayout.LabelField(e.Filled ? describe(e.Name) : "EMPTY (silent) · " + describe(e.Name), EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(!e.Filled))
                    if (GUILayout.Button("▶ Play", GUILayout.Width(56))) preview(e.Pick(), e.Loop);
            }
            if (!_open.Contains(e.Name)) return;
            EditorGUI.indentLevel++;
            for (int i = 0; i < e.Clips.Count; i++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField($"File {i + 1}", e.Clips[i], typeof(AudioClip), false);
                    if (GUILayout.Button("Show", GUILayout.Width(50))) ping(e.Clips[i]);
                    if (GUILayout.Button("▶", GUILayout.Width(26))) preview(e.Clips[i], e.Loop);
                }
            if (e.Clips.Count == 0)
                EditorGUILayout.LabelField($"No file. Add Assets/Audio/…/{e.Name}.wav (or {e.Name}_01.wav, _02 … for variations), then Refresh Sound Bank.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Volume"));
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Pitch"), new GUIContent("Pitch (random min–max)"));
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Spatial"), new GUIContent("2D (0) … 3D (1)"));
            if (e.Spatial > 0f)
            {
                EditorGUILayout.PropertyField(p.FindPropertyRelative("MinDistance"));
                EditorGUILayout.PropertyField(p.FindPropertyRelative("MaxDistance"));
            }
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Loop"));
            var group = p.FindPropertyRelative("Group");
            var groups = new[] { "Music", "Ambience", "SFX", "Voice", "UI" };
            var index = Mathf.Max(0, Array.IndexOf(groups, group.stringValue));
            group.stringValue = groups[EditorGUILayout.Popup("Mixer group", index, groups)];
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private static string describe(string name) => Where.TryGetValue(name, out var d) ? d : "";

        #endregion

        #region Mix

        private void mix()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(VantageMixerBuilder.Path);
            EditorGUILayout.LabelField("Mixer", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(mixer, typeof(AudioMixer), false);
                if (mixer == null && GUILayout.Button("Build mixer", GUILayout.Width(110))) VantageMixerBuilder.BuildMenu();
                if (mixer != null && GUILayout.Button("Open Audio Mixer", GUILayout.Width(130)))
                {
                    Selection.activeObject = mixer;
                    EditorApplication.ExecuteMenuItem("Window/Audio/Audio Mixer");
                }
            }
            EditorGUILayout.LabelField("Groups: Music, Ambience, SFX, Voice under their \"… Volume\" buses (the menu sliders), UI, Reverb. " +
                                       "Snapshots Explore / Combat / Cinematic move the group faders; the sliders move the buses.", EditorStyles.wordWrappedMiniLabel);

            var audio = FindAnyObjectByType<VantageAudio>();
            if (audio == null)
            {
                EditorGUILayout.HelpBox("Open SampleScene to edit the zones, music and guide (they live on its VANTAGE Audio object).", MessageType.Info);
                return;
            }
            foreach (var component in new MonoBehaviour[] { audio, audio.GetComponent<VantageAudioZones>(), audio.GetComponent<VantageMusic>(), audio.GetComponent<VantageGoalSound>() })
            {
                if (component == null) continue;
                EditorGUILayout.Space(8);
                EditorGUILayout.InspectorTitlebar(true, component);
                if (!_editors.TryGetValue(component, out var editor) || editor == null) _editors[component] = editor = Editor.CreateEditor(component);
                editor.OnInspectorGUI();
            }
        }

        #endregion

        #region Preview (the editor's own clip preview, by reflection)

        private static readonly Type AudioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");

        private static void preview(AudioClip clip, bool loop)
        {
            if (clip == null || AudioUtil == null) return;
            stopPreview();
            var play = AudioUtil.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            play?.Invoke(null, new object[] { clip, 0, loop });
        }

        private static void stopPreview() =>
            AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null);

        private static void ping(UnityEngine.Object o)
        {
            if (o == null) return;
            Selection.activeObject = o;
            EditorGUIUtility.PingObject(o);
        }

        #endregion
    }
}
