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
    /// The intro has its own track (MUS_Intro): it starts with the flyover (crossfading with the title's MUS_Menu)
    /// and plays to its own end after the hand-back, so it rings out into the level's ambience; MUS_Explore waits
    /// under it and fades in once it is over. A fight cuts it short (IntroFightFade) and the layers take over; a
    /// long track is faded IntroTail seconds after the hand-back.
    /// The boss has its own track (MUS_BossFight, looped) from the end of its reveal until it dies; the layers are
    /// silent under it. Its death fades it and starts MUS_End, which plays through the extraction and the end screen.
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
        [Tooltip("Fade-out of the intro track (MUS_Intro) when a fight starts before it has ended, in seconds.")]
        public float IntroFightFade = 3f;
        [Tooltip("Fade-in of MUS_Explore after the intro track, in seconds.")]
        public float ExploreFadeIn = 4f;
        [Tooltip("Seconds of the intro track kept after the player has control, then it fades over IntroTailFade.")]
        public float IntroTail = 15f;
        public float IntroTailFade = 8f;
        [Tooltip("Fade-out of the boss track (MUS_BossFight) when the boss dies, in seconds.")]
        public float BossMusicFade = 2f;

        public bool Combat => Time.time - _lastFight < CombatHold && !Ended;
        public bool Ended { get; private set; }
        /// <summary>The intro track is playing (or paused with the game).</summary>
        public bool IntroMusicPlaying => _intro != null;
        /// <summary>The boss track is playing.</summary>
        public bool BossMusicPlaying => _boss != null;

        private AudioSource _explore, _combat, _intro, _boss;
        private bool _introStarted, _bossStarted;
        private float _introHandback = -1f;
        private float _exploreIn = 1f;
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
            introMusic();
            bossMusic();
            _exploreIn = Mathf.MoveTowards(_exploreIn, _intro != null || _boss != null ? 0f : 1f, Time.deltaTime / Mathf.Max(0.01f, ExploreFadeIn));
            var target = Combat && !_cinematic && _boss == null ? 1f : 0f;
            _combatMix = Mathf.MoveTowards(_combatMix, target, Time.deltaTime / Mathf.Max(0.01f, CombatFade));
            var dip = Time.time < _dipUntil ? Mathf.Pow(10f, StingerDip / 20f) : 1f;
            if (_combat != null) _combat.volume = _combatVolume * _combatMix * dip;
            if (_explore != null)
            {
                var ending = Ended ? Mathf.Clamp01(1f - (Time.time - _endedAt) / Mathf.Max(0.01f, EndingFade)) : 1f;
                _explore.volume = _exploreVolume * dip * ending * _exploreIn;
            }
        }

        /// <summary>
        /// Starts MUS_Intro when the intro starts (once per load), and lets it go when it has played out (not while
        /// the game is paused: a paused source isn't playing either) or fades it when a fight starts.
        /// </summary>
        private void introMusic()
        {
            var intro = VantageIntro.Instance;
            if (!_introStarted && intro != null && intro.Playing)
            {
                _introStarted = true;
                _intro = VantageAudio.PlayLoop(VantageSounds.MusIntro, null);
                if (_intro != null) { _intro.loop = false; _exploreIn = 0f; }
            }
            if (_intro == null) return;
            if (_introHandback < 0f && intro != null && !intro.Playing) _introHandback = Time.time;
            if (Combat || Ended) { VantageAudio.Stop(_intro, IntroFightFade); _intro = null; }
            else if (_introHandback >= 0f && Time.time - _introHandback >= IntroTail) { VantageAudio.Stop(_intro, IntroTailFade); _intro = null; }
            else if (!_intro.isPlaying && !AudioListener.pause) { VantageAudio.Stop(_intro); _intro = null; }
        }

        /// <summary>MUS_BossFight once the boss is there and no cutscene has the camera (after its reveal).</summary>
        private void bossMusic()
        {
            if (_bossStarted || _cinematic || Ended) return;
            var levels = VantageTowerLevels.Instance;
            if (levels == null || !levels.BossSpawned || levels.BossDead) return;
            _bossStarted = true;
            if (_intro != null) { VantageAudio.Stop(_intro, IntroFightFade); _intro = null; }
            _boss = VantageAudio.PlayLoop(VantageSounds.MusBossFight, null);
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

        /// <summary>A stinger from outside (the boss reveal's fight start): plays it and dips the layers under it.</summary>
        public void Stinger(string name) => stinger(name);

        private void stinger(string name)
        {
            var source = VantageAudio.Play2D(name);
            if (source != null && source.clip != null) _dipUntil = Time.time + source.clip.length / Mathf.Max(0.01f, source.pitch);
        }

        private void onAlarm(string source) => stinger(VantageSounds.StgLockdown);
        private void onCleared(int level) => stinger(VantageSounds.StgCleared);

        private void onBoss()
        {
            VantageAudio.Stop(_boss, BossMusicFade);
            _boss = null;
            var end = VantageAudio.PlayLoop(VantageSounds.MusEnd, null);
            if (end != null) end.loop = false;
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
