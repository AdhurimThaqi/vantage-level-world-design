using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>One sound event: its clip variations and how they play.</summary>
    [Serializable]
    public class SoundEvent
    {
        public string Name;
        public List<AudioClip> Clips = new List<AudioClip>();
        [Range(0f, 1f)] public float Volume = 1f;
        [Tooltip("Random pitch between x and y each time it plays.")]
        public Vector2 Pitch = new Vector2(0.95f, 1.05f);
        [Tooltip("0 = 2D (beds, music, UI), 1 = 3D in the world.")]
        [Range(0f, 1f)] public float Spatial = 1f;
        public float MinDistance = 2f;
        public float MaxDistance = 40f;
        public bool Loop;
        [Tooltip("Mixer group: Music, Ambience, SFX, Voice or UI.")]
        public string Group = "SFX";

        public bool Filled => Clips.Count > 0;

        public AudioClip Pick() => Clips.Count == 0 ? null : Clips[UnityEngine.Random.Range(0, Clips.Count)];
    }

    /// <summary>
    /// The audio numbers that belong to no single event (2026-11-16: they were written into the scripts). Read through
    /// VantageAudio.Tuning; edited in the Sound Board (Vantage → Audio → Sound Board) or on the bank asset.
    /// </summary>
    [Serializable]
    public class SoundTuning
    {
        [Header("Playback")]
        [Tooltip("One-shot sources shared by every sound; when all are busy the oldest 3D one is taken over.")]
        [Min(4)] public int PoolSize = 24;
        [Tooltip("Min and max distance of the template's own sounds (gunshots, hits) played through the pool.")]
        public Vector2 TemplateSoundDistance = new Vector2(1f, 500f);

        [Header("Machines")]
        [Tooltip("Pitch range of a drone's shot while DRONE_Fire is empty (it then borrows ROBOT_Fire).")]
        public Vector2 DroneFirePitch = new Vector2(1.35f, 1.5f);
        [Tooltip("Pitch range of a turret's shot while TURRET_Fire is empty (it then borrows ROBOT_Fire).")]
        public Vector2 TurretFirePitch = new Vector2(0.72f, 0.8f);
        [Tooltip("Pitch range of the helicopter's cannon while HELI_Gun is empty (it then borrows ROBOT_Fire).")]
        public Vector2 HeliGunPitch = new Vector2(0.55f, 0.62f);
        [Tooltip("The boss's shot and servo against the robot's (same events, pitched down).")]
        [Range(0.3f, 1.5f)] public float BossShotPitch = 0.75f;
        [Range(0.3f, 1.5f)] public float BossServoPitch = 0.7f;
        [Tooltip("Seconds the robot's servo loop fades when it stops walking or dies.")]
        public float ServoFade = 0.3f;
        [Tooltip("At most one ROBOT_Hit clank this often (the rifle fires fast).")]
        public float RobotHitGap = 0.25f;

        [Header("People")]
        [Tooltip("BODY_Fall plays this long after a death (the body reaching the ground).")]
        public float BodyFallDelay = 0.5f;
        [Tooltip("At most one VO_Contact bark this often across all soldiers.")]
        public float BarkGap = 3f;
        [Tooltip("The sentries' own voice source (the warning call, the shot crack without a SENTRY_Crack): min / max distance.")]
        public Vector2 SentryVoiceDistance = new Vector2(8f, 120f);
        [Range(0f, 1f)] public float WarningCallVolume = 0.9f;
        [Tooltip("A sentry's radio loop (its RadioLoop clip, if set): min / max distance and volume.")]
        public Vector2 SentryRadioDistance = new Vector2(2f, 25f);
        [Range(0f, 1f)] public float SentryRadioVolume = 0.25f;

        [Header("Cutscenes")]
        [Tooltip("Seconds into each CAM_Swoosh clip where its peak sits: a swoosh starts this long before its camera cut.")]
        public float SwooshPeak = 0.6f;
        [Tooltip("Volume of the swooshes on the intro's climb moves (the hard cuts play at full volume).")]
        [Range(0f, 1f)] public float SwooshSoftVolume = 0.55f;
        [Tooltip("In a cutscene, footsteps and a body falling carry this many times further (the intro's deserter is heard from the camera).")]
        [Min(1f)] public float CinematicReach = 5f;

        [Header("Fades (seconds)")]
        public float SirenFade = 1.5f;
        [Tooltip("BOSS_Reveal fading out as the fight starts.")]
        public float RevealFade = 2.5f;
        [Tooltip("The title screen helicopter's rotor after Play.")]
        public float MenuRotorFade = 1f;
    }

    /// <summary>
    /// Every sound event of the game (Assets/Audio/VantageSoundBank.asset). Built and filled from the WAVs in
    /// Assets/Audio by Vantage → Audio → Refresh Sound Bank; the settings per event and the Tuning are edited here or
    /// in the Sound Board (Vantage → Audio → Sound Board).
    /// </summary>
    [CreateAssetMenu(menuName = "Vantage/Sound Bank")]
    public class VantageSoundBank : ScriptableObject
    {
        public List<SoundEvent> Events = new List<SoundEvent>();
        public SoundTuning Tuning = new SoundTuning();

        private Dictionary<string, SoundEvent> _byName;

        public SoundEvent Find(string name)
        {
            if (_byName == null || _byName.Count != Events.Count)
            {
                _byName = new Dictionary<string, SoundEvent>(Events.Count);
                foreach (var e in Events)
                    if (e != null && !string.IsNullOrEmpty(e.Name)) _byName[e.Name] = e;
            }
            return name != null && _byName.TryGetValue(name, out var found) ? found : null;
        }

        private void OnValidate() => _byName = null;
    }
}
