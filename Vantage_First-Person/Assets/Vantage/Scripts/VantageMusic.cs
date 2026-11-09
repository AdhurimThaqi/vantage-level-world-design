using System.Collections;
using System.Collections.Generic;
using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The adaptive music. MUS_Explore (the drone) and MUS_Combat (percussion) start on the same DSP time and loop
    /// together forever (so they need the same tempo, key and length); the combat layer fades in while anything
    /// fights the player (a soldier with the player as its threat, a drone, turret or robot engaging, the alarm, the
    /// boss alive) and out CombatHold seconds after the last of it. Stingers mark the pacing curve: STG_Lockdown when
    /// the alarm springs, STG_Cleared on a level clear, STG_Extraction when the boss dies; the layers dip under them.
    /// After the extraction stinger the drone fades out for good: the end screen is silent. Cutscenes (title screen,
    /// intro, extraction) use the Cinematic snapshot with the combat layer down.
    /// </summary>
    public class VantageMusic : MonoBehaviour
    {
        public float CombatFade = 2.5f;
        [Tooltip("Seconds without any fighting before the combat layer goes.")]
        public float CombatHold = 6f;
        [Tooltip("dB the layers dip under a stinger.")]
        public float StingerDip = -8f;
        public float SnapshotBlend = 1.5f;
        public float EndingFade = 4f;

        public bool Combat => Time.time - _lastFight < CombatHold && !Ended;
        public bool Ended { get; private set; }

        private AudioSource _explore, _combat;
        private float _exploreVolume = 1f, _combatVolume = 1f, _combatMix, _dipUntil, _lastFight = -100f, _nextCheck, _endedAt;
        private bool _cinematic;
        private readonly Dictionary<GameObject, (CharacterMotor motor, FighterBrain brain, IVantageEnemy machine)> _parts =
            new Dictionary<GameObject, (CharacterMotor, FighterBrain, IVantageEnemy)>();

        private void OnEnable()
        {
            VantageEvents.AlarmRaised += onAlarm;
            VantageEvents.CinematicStarted += onCinematicStarted;
            VantageEvents.CinematicEnded += onCinematicEnded;
        }

        private void OnDisable()
        {
            VantageEvents.AlarmRaised -= onAlarm;
            VantageEvents.CinematicStarted -= onCinematicStarted;
            VantageEvents.CinematicEnded -= onCinematicEnded;
            var levels = VantageTowerLevels.Instance;
            if (levels != null) { levels.LevelCleared -= onCleared; levels.BossDefeated -= onBoss; }
        }

        private IEnumerator Start()
        {
            var levels = VantageTowerLevels.Instance;
            if (levels != null) { levels.LevelCleared += onCleared; levels.BossDefeated += onBoss; }
            // The title screen has its own music (MUS_Menu): the layers start after Play. The menu shows a frame after
            // the load; if it hasn't within 30 frames, don't wait for it (frames: the first one after a load is long).
            var menu = VantageMainMenu.Instance;
            for (int frame = 0; menu != null && (menu.Showing || (menu.Holding && frame < 30)); frame++) yield return null;
            _explore = VantageAudio.PlayLoop(VantageSounds.MusExplore, null);
            _combat = VantageAudio.PlayLoop(VantageSounds.MusCombat, null);
            // Restart both on one DSP time, so the layers stay sample-locked for the whole game.
            var at = AudioSettings.dspTime + 0.2;
            if (_explore != null) { _exploreVolume = _explore.volume; _explore.Stop(); _explore.PlayScheduled(at); }
            if (_combat != null) { _combatVolume = _combat.volume; _combat.Stop(); _combat.PlayScheduled(at); _combat.volume = 0f; }
            _cinematic = VantageCinematic.Active;
            VantageAudio.SetSnapshot(_cinematic ? "Cinematic" : "Explore", 0f);
        }

        private void Update()
        {
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 0.25f;
                if (!_cinematic && !Ended && fighting()) _lastFight = Time.time;
            }
            var target = Combat && !_cinematic ? 1f : 0f;
            _combatMix = Mathf.MoveTowards(_combatMix, target, Time.deltaTime / Mathf.Max(0.01f, CombatFade));
            var dip = Time.time < _dipUntil ? Mathf.Pow(10f, StingerDip / 20f) : 1f;
            if (_combat != null) _combat.volume = _combatVolume * _combatMix * dip;
            if (_explore != null)
            {
                var ending = Ended ? Mathf.Clamp01(1f - (Time.time - _endedAt) / Mathf.Max(0.01f, EndingFade)) : 1f;
                _explore.volume = _exploreVolume * dip * ending;
            }
        }

        /// <summary>Anything fighting the player right now (cached components; four times a second).</summary>
        private bool fighting()
        {
            var levels = VantageTowerLevels.Instance;
            if (levels == null) return false;
            if (VantageAlarm.Instance != null && VantageAlarm.Instance.Active) return true;
            if (levels.BossSpawned && !levels.BossDead) return true;
            var player = VantageEvents.ActivePlayer();
            if (player == null) return false;
            var enemies = levels.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                if (!_parts.TryGetValue(e, out var p))
                    _parts[e] = p = (e.GetComponent<CharacterMotor>(), e.GetComponent<FighterBrain>(), e.GetComponent<IVantageEnemy>());
                if (p.brain != null && p.motor != null && p.motor.IsAlive && p.brain.Threat == player) return true;
                if (p.machine != null && p.machine.Engaging) return true;
            }
            return false;
        }

        private void stinger(string name)
        {
            var source = VantageAudio.Play2D(name);
            if (source != null && source.clip != null) _dipUntil = Time.time + source.clip.length / Mathf.Max(0.01f, source.pitch);
        }

        private void onAlarm(string source) => stinger(VantageSounds.StgLockdown);
        private void onCleared(int level) => stinger(VantageSounds.StgCleared);

        private void onBoss()
        {
            stinger(VantageSounds.StgExtraction);
            Ended = true;
            _endedAt = Time.time;
        }

        private void onCinematicStarted()
        {
            _cinematic = true;
            VantageAudio.SetSnapshot("Cinematic", SnapshotBlend);
        }

        private void onCinematicEnded()
        {
            _cinematic = false;
            VantageAudio.SetSnapshot(Combat ? "Combat" : "Explore", SnapshotBlend);
        }

        private void LateUpdate()
        {
            // The Combat snapshot follows the combat state (ambience ducked while fighting).
            var combatSnapshot = Combat && !_cinematic;
            if (combatSnapshot == _combatSnapshot) return;
            _combatSnapshot = combatSnapshot;
            if (!_cinematic) VantageAudio.SetSnapshot(combatSnapshot ? "Combat" : "Explore", SnapshotBlend);
        }

        private bool _combatSnapshot;
    }
}
