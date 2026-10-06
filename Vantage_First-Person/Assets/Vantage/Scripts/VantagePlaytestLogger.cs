using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vantage
{
    /// <summary>
    /// Writes one report per play session for the documented playtests (concept doc: testing and feedback loops).
    /// Logs the level 2 seed, time per space, deaths, kills, pickups, waves, view switches, back-tracking
    /// ("nothing is sealed behind the player" is a testable claim) and a position trail for heatmaps.
    /// F8 = mark a moment (e.g. when the tester says something worth noting).
    /// Files: [project]/Playtests/ in the editor, persistentDataPath/Playtests in a build.
    /// </summary>
    public class VantagePlaytestLogger : MonoBehaviour
    {
        [Tooltip("Tower root, used to work out which space the player is in.")]
        public Transform Tower;
        public float TrailInterval = 1f;
        public bool ShowOverlay = true;

        private const float F2 = 7.2f, F3 = 11.2f, FR = 15.7f;
        private static readonly string[] Spaces = { "Yard", "L1 Lobby", "L2 Corridors", "Fire Escape", "L3 Collapsed", "Roof" };
        private static readonly float[] Progress = { 0f, 1f, 2f, 2.5f, 3f, 4f };

        private readonly StringBuilder _events = new StringBuilder();
        private readonly StringBuilder _trail = new StringBuilder("time,x,y,z,space,view\n");
        private readonly float[] _timeIn = new float[Spaces.Length];
        private readonly int[] _deathsIn = new int[Spaces.Length];
        private readonly Dictionary<string, int> _kills = new Dictionary<string, int>();

        private int _space = -1;
        private float _bestProgress;
        private int _backtracks;
        private int _seed = -1;
        private bool _thirdPerson;
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
            VantageEvents.ViewSwitched += onView;
            VantageEvents.LayoutGenerated += onSeed;
            VantageEvents.WaveStarted += onWave;
            VantageEvents.LevelCompleted += onComplete;
        }

        private void OnDisable()
        {
            VantageEvents.PlayerDied -= onDied;
            VantageEvents.EnemyKilled -= onKill;
            VantageEvents.WeaponPickedUp -= onPickup;
            VantageEvents.ViewSwitched -= onView;
            VantageEvents.LayoutGenerated -= onSeed;
            VantageEvents.WaveStarted -= onWave;
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

                    if (Progress[space] + 0.01f < _bestProgress && space != 3)
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
                    _trail.Append($"{Time.time:F1},{position.x:F2},{position.y:F2},{position.z:F2},{Spaces[space]},{(_thirdPerson ? "3P" : "1P")}\n");
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                log($"MARK at {(player != null ? player.transform.position.ToString("F1") : "?")} in {(_space >= 0 ? Spaces[_space] : "?")}");
        }

        private int spaceOf(Vector3 world)
        {
            if (Tower == null)
                return 0;

            var p = Tower.InverseTransformPoint(world);
            var inside = Mathf.Abs(p.x) < 9f && Mathf.Abs(p.z) < 9f;
            var onFireEscape = p.x < -8.9f && p.x > -12f && p.z > -8.5f && p.z < 4.2f && p.y > F2 - 1f;

            if (onFireEscape)
                return 3;
            if (!inside)
                return p.y > FR - 0.5f ? 5 : 0;
            if (p.y > FR - 0.5f)
                return 5;
            if (p.y > F3 - 0.5f)
                return 4;
            if (p.y > F2 - 0.5f)
                return 2;
            return 1;
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
        private void onView(bool thirdPerson) { _thirdPerson = thirdPerson; log("View: " + (thirdPerson ? "third person" : "first person")); }
        private void onSeed(int seed) { _seed = seed; log("Level 2 layout seed " + seed); }
        private void onWave(int wave, int count) => log($"Roof wave {wave} started ({count} drones)");

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
                report.Append("Level 2 seed: ").Append(_seed >= 0 ? _seed.ToString() : "n/a").Append('\n');
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
