using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vantage
{
    /// <summary>
    /// Writes one report per play session for the documented playtests (concept doc: testing and feedback loops).
    /// Logs the enemy placement seed, time per space, deaths, kills, pickups, waves, back-tracking
    /// ("nothing is sealed behind the player" is a testable claim) and a position trail for heatmaps.
    /// F8 = mark a moment (e.g. when the tester says something worth noting).
    /// Files: [project]/Playtests/ in the editor, persistentDataPath/Playtests in a build.
    /// </summary>
    public class VantagePlaytestLogger : MonoBehaviour
    {
        [Tooltip("Command tower root (the levels come from VantageTowerLevels).")]
        public Transform Tower;
        public float TrailInterval = 1f;
        public bool ShowOverlay = true;

        private static readonly string[] Spaces = { "Military Base", "Level 1 (ground, F1)", "Level 2 (F2, F3)", "Level 3 (F4, F5)", "Level 4 (roof)" };
        private static readonly float[] Progress = { 0f, 1f, 2f, 3f, 4f };

        /// <summary>Name of the space the player is in, or null before the first update (shown by the minimap).</summary>
        public string CurrentSpace => _space >= 0 ? Spaces[_space] : null;

        private readonly StringBuilder _events = new StringBuilder();
        private readonly StringBuilder _trail = new StringBuilder("time,x,y,z,space\n");
        private readonly float[] _timeIn = new float[Spaces.Length];
        private readonly int[] _deathsIn = new int[Spaces.Length];
        private readonly Dictionary<string, int> _kills = new Dictionary<string, int>();

        private int _space = -1;
        private float _bestProgress;
        private int _backtracks;
        private int _seed = -1;
        private bool _completed;
        private float _nextTrail;
        private string _folder;
        private string _stamp;
        private bool _written;

        private void OnEnable()
        {
            VantageEvents.PlayerDied += onDied;
            VantageEvents.EnemyKilled += onKill;
            VantageEvents.WeaponPickedUp += onPickup;
            VantageEvents.LayoutGenerated += onSeed;
            VantageEvents.LevelCompleted += onComplete;
        }

        private void OnDisable()
        {
            VantageEvents.PlayerDied -= onDied;
            VantageEvents.EnemyKilled -= onKill;
            VantageEvents.WeaponPickedUp -= onPickup;
            VantageEvents.LayoutGenerated -= onSeed;
            VantageEvents.LevelCompleted -= onComplete;
            write();
        }

        private void Start()
        {
            _stamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _folder = Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Playtests")
                : Path.Combine(Application.persistentDataPath, "Playtests");
            log("Session started");
        }

        private void Update()
        {
            var player = VantageEvents.ActivePlayer();
            if (player != null)
            {
                var position = player.transform.position;
                var space = spaceOf(position);

                if (space != _space)
                {
                    if (_space >= 0)
                        log($"{Spaces[_space]} -> {Spaces[space]}");

                    if (Progress[space] + 0.01f < _bestProgress)
                    {
                        _backtracks++;
                        log($"BACKTRACK to {Spaces[space]} (furthest so far: {furthest()})");
                    }

                    _bestProgress = Mathf.Max(_bestProgress, Progress[space]);
                    _space = space;
                }

                _timeIn[space] += Time.deltaTime;

                if (Time.time >= _nextTrail)
                {
                    _nextTrail = Time.time + TrailInterval;
                    _trail.Append($"{Time.time:F1},{position.x:F2},{position.y:F2},{position.z:F2},{Spaces[space]}\n");
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                log($"MARK at {(player != null ? player.transform.position.ToString("F1") : "?")} in {(_space >= 0 ? Spaces[_space] : "?")}");
        }

        /// <summary>0 outside the tower, 1–4 for the tower's levels.</summary>
        private int spaceOf(Vector3 world)
        {
            var levels = VantageTowerLevels.Instance;
            var level = levels != null ? levels.LevelAt(world) : -1;
            return level >= 0 ? Mathf.Min(level + 1, Spaces.Length - 1) : 0;
        }

        private string furthest()
        {
            for (int i = Progress.Length - 1; i >= 0; i--)
                if (Progress[i] <= _bestProgress)
                    return Spaces[i];
            return Spaces[0];
        }

        private void onDied(string cause, Vector3 position)
        {
            var space = spaceOf(position);
            _deathsIn[space]++;
            log($"DEATH in {Spaces[space]} at {position:F1} ({cause})");
            write();
        }

        private void onKill(string enemy, Vector3 position)
        {
            _kills[enemy] = _kills.TryGetValue(enemy, out var n) ? n + 1 : 1;
            log($"Killed {enemy} in {Spaces[spaceOf(position)]}");
        }

        private void onPickup(string weapon) => log("Picked up " + weapon);
        private void onSeed(int seed) { _seed = seed; log("Enemy placement seed " + seed); }

        private void onComplete()
        {
            _completed = true;
            log("LEVEL COMPLETE");
            write();
        }

        private void log(string message)
        {
            _events.Append($"[{Time.time,7:F1}s] {message}\n");
        }

        private void write()
        {
            if (string.IsNullOrEmpty(_folder) || (_written && !_completed && _events.Length == 0))
                return;

            try
            {
                Directory.CreateDirectory(_folder);
                var report = new StringBuilder();
                report.Append("VANTAGE playtest  ").Append(_stamp).Append('\n');
                report.Append("Enemy placement seed: ").Append(_seed >= 0 ? _seed.ToString() : "n/a").Append('\n');
                report.Append($"Session length: {Time.time:F0}s   Completed: {(_completed ? "yes" : "no")}   Furthest: {furthest()}   Backtracks: {_backtracks}\n\n");

                report.Append("Time per space\n");
                for (int i = 0; i < Spaces.Length; i++)
                    report.Append($"  {Spaces[i],-14} {_timeIn[i],6:F0}s   deaths {_deathsIn[i]}\n");

                report.Append("\nKills\n");
                foreach (var kill in _kills)
                    report.Append($"  {kill.Key,-14} {kill.Value}\n");

                report.Append("\nTester notes (fill in after the session)\n  Tester: \n  What they said: \n  What changes as a result: \n");
                report.Append("\nEvents\n").Append(_events);

                File.WriteAllText(Path.Combine(_folder, $"playtest_{_stamp}.txt"), report.ToString());
                File.WriteAllText(Path.Combine(_folder, $"playtest_{_stamp}_trail.csv"), _trail.ToString());
                _written = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Vantage] Could not write playtest log: " + e.Message);
            }
        }

        private void OnGUI()
        {
            if (!ShowOverlay)
                return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            var space = _space >= 0 ? Spaces[_space] : "-";
            GUI.Label(new Rect(10, 6, 700, 20), $"PLAYTEST  seed {(_seed >= 0 ? _seed.ToString() : "-")}  |  {space}  |  F8 mark   F9 perf capture   F10 occlusion", style);
        }
    }
}
