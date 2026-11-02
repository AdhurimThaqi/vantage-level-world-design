using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Vantage
{
    /// <summary>
    /// Plays sound events by name (VantageSounds) from the bank: a random clip, pitch and volume from the event's
    /// settings, 2D or 3D, routed to the event's mixer group by name. One-shots share a pool of sources; loops get
    /// their own. An empty event plays the caller's fallback (the old synthesised sound) or nothing; without a mixer
    /// everything goes straight to the output. Every request is counted (Plays), filled or not, for the bot.
    /// One object in the scene ("VANTAGE Audio", made by Vantage → Audio → Refresh Sound Bank).
    /// </summary>
    public class VantageAudio : MonoBehaviour
    {
        public VantageSoundBank Bank;
        [Tooltip("Assets/Audio/VantageMixer.mixer (built by hand). Empty: no routing, no snapshots.")]
        public AudioMixer Mixer;
        /// <summary>One-shot sources (the template's gunshots and hits share them).</summary>
        public const int PoolSize = 24;

        /// <summary>PlayerPrefs keys of the volume settings (0–1).</summary>
        public const string MasterKey = "vantage.master", MusicKey = "vantage.music", EffectsKey = "vantage.effects";
        private const string OldVolumeKey = "vantage.volume";

        public static VantageAudio Instance { get; private set; }

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly List<(AudioSource source, Transform follow, bool follows)> _loops = new List<(AudioSource, Transform, bool)>();
        private readonly Dictionary<string, AudioMixerGroup> _groups = new Dictionary<string, AudioMixerGroup>();
        private static readonly Dictionary<string, int> _plays = new Dictionary<string, int>();
        private int _next;

        private void Awake()
        {
            Instance = this;
            _plays.Clear();
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("One-shot " + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _pool.Add(s);
            }
        }

        private void Start()
        {
            ApplyVolumes();   // a mixer takes SetFloat from Start on, not in Awake
            if (Bank == null) { Debug.Log("[Vantage] Sound bank: none assigned."); return; }
            var missing = new List<string>();
            foreach (var name in VantageSounds.All)
            {
                var e = Bank.Find(name);
                if (e == null || !e.Filled) missing.Add(name);
            }
            Debug.Log($"[Vantage] Sound bank: {VantageSounds.All.Length - missing.Count} / {VantageSounds.All.Length} events filled" +
                      (missing.Count > 0 ? $" (missing: {string.Join(", ", missing)})." : "."));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            for (int i = _loops.Count - 1; i >= 0; i--)
            {
                var (source, follow, follows) = _loops[i];
                if (source == null) { _loops.RemoveAt(i); continue; }
                // A loop on something that was destroyed ends with it.
                if (follows && follow == null) { Destroy(source.gameObject); _loops.RemoveAt(i); continue; }
                if (follow != null) source.transform.position = follow.position;
            }
        }

        #region Static API

        /// <summary>A one-shot at a point in the world (2D events ignore the point). Null when nothing plays.</summary>
        public static AudioSource Play(string name, Vector3 position, AudioClip fallback = null)
        {
            count(name);
            return Instance != null ? Instance.oneShot(name, position, fallback) : null;
        }

        /// <summary>A one-shot with no position (UI, stingers).</summary>
        public static AudioSource Play2D(string name, AudioClip fallback = null)
        {
            count(name);
            return Instance != null ? Instance.oneShot(name, Instance.transform.position, fallback, true) : null;
        }

        /// <summary>A looping source of its own, following 'follow' when given (and ending when it is destroyed). Stop it with Stop. Null when nothing plays.</summary>
        public static AudioSource PlayLoop(string name, Transform follow, AudioClip fallback = null)
        {
            count(name);
            return Instance != null ? Instance.loop(name, follow, fallback) : null;
        }

        /// <summary>Stops (and removes) a loop from PlayLoop, optionally fading out.</summary>
        public static void Stop(AudioSource loop, float fade = 0f)
        {
            if (loop == null) return;
            if (fade <= 0f || Instance == null) { Destroy(loop.gameObject); return; }
            Instance.StartCoroutine(fadeOut(loop, fade));
        }

        public static void SetSnapshot(string name, float seconds)
        {
            if (Instance == null || Instance.Mixer == null) return;
            var snapshot = Instance.Mixer.FindSnapshot(name);
            if (snapshot != null) snapshot.TransitionTo(seconds);
        }

        /// <summary>Sets an exposed mixer parameter (no-op without a mixer or when it isn't exposed).</summary>
        public static void SetParameter(string name, float value)
        {
            if (Instance != null && Instance.Mixer != null) Instance.Mixer.SetFloat(name, value);
        }

        public static AudioMixerGroup Group(string name) => Instance != null ? Instance.group(name) : null;

        /// <summary>A volume setting (0–1); Master starts at the old single volume setting if there was one.</summary>
        public static float Setting(string key) =>
            PlayerPrefs.GetFloat(key, key == MasterKey ? PlayerPrefs.GetFloat(OldVolumeKey, 1f) : 1f);

        /// <summary>
        /// The volume settings into the mixer's exposed parameters (dB): MasterVolume, MusicVolume, and Effects into
        /// SfxVolume, VoiceVolume and AmbienceVolume. Without a mixer only Master works, as AudioListener.volume.
        /// </summary>
        public static void ApplyVolumes()
        {
            var mixer = Instance != null ? Instance.Mixer : null;
            AudioListener.volume = mixer != null ? 1f : Setting(MasterKey);
            if (mixer == null) return;
            mixer.SetFloat("MasterVolume", decibels(Setting(MasterKey)));
            mixer.SetFloat("MusicVolume", decibels(Setting(MusicKey)));
            var effects = decibels(Setting(EffectsKey));
            mixer.SetFloat("SfxVolume", effects);
            mixer.SetFloat("VoiceVolume", effects);
            mixer.SetFloat("AmbienceVolume", effects);
        }

        private static float decibels(float volume) => 20f * Mathf.Log10(Mathf.Max(volume, 0.0001f));

        /// <summary>
        /// A template sound (gunshot, hit, grunt) through the pool and a mixer group, so the volume settings reach it;
        /// it keeps PlayClipAtPoint's 3D distances (1–500 m). Without the audio object it is PlayClipAtPoint.
        /// </summary>
        public static void PlayClip(AudioClip clip, Vector3 position, string group)
        {
            if (clip == null) return;
            if (Instance == null) { AudioSource.PlayClipAtPoint(clip, position); return; }
            var source = Instance.free();
            Instance.setup(source, null, false);
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.outputAudioMixerGroup = Instance.group(group);
            source.transform.position = position;
            source.clip = clip;
            source.loop = false;
            source.Play();
        }

        /// <summary>The event exists and has clips.</summary>
        public static bool Has(string name)
        {
            var e = Instance != null && Instance.Bank != null ? Instance.Bank.Find(name) : null;
            return e != null && e.Filled;
        }

        /// <summary>How often the event was requested since the scene loaded (filled or not).</summary>
        public static int Plays(string name) => name != null && _plays.TryGetValue(name, out var n) ? n : 0;

        private static void count(string name)
        {
            if (name == null) return;
            _plays.TryGetValue(name, out var n);
            _plays[name] = n + 1;
        }

        #endregion

        private AudioSource oneShot(string name, Vector3 position, AudioClip fallback, bool flat = false)
        {
            var e = Bank != null ? Bank.Find(name) : null;
            var clip = e != null && e.Filled ? e.Pick() : fallback;
            if (clip == null) return null;
            var source = free();
            setup(source, e, flat);
            source.transform.position = position;
            source.clip = clip;
            source.loop = false;
            source.Play();
            return source;
        }

        /// <summary>
        /// The next free pool source; if all are busy, the oldest 3D one is taken over (gunfire), so 2D sounds
        /// (stingers, UI) always play out.
        /// </summary>
        private AudioSource free()
        {
            AudioSource source = null;
            for (int i = 0; i < _pool.Count && source == null; i++)
            {
                var s = _pool[(_next + i) % _pool.Count];
                if (!s.isPlaying) source = s;
            }
            for (int i = 0; i < _pool.Count && source == null; i++)
            {
                var s = _pool[(_next + i) % _pool.Count];
                if (s.spatialBlend > 0f) source = s;
            }
            if (source == null) source = _pool[_next % _pool.Count];
            _next = (_pool.IndexOf(source) + 1) % _pool.Count;
            return source;
        }

        private AudioSource loop(string name, Transform follow, AudioClip fallback)
        {
            var e = Bank != null ? Bank.Find(name) : null;
            var clip = e != null && e.Filled ? e.Pick() : fallback;
            if (clip == null) return null;
            var go = new GameObject("Loop " + name);
            go.transform.SetParent(transform, false);
            if (follow != null) go.transform.position = follow.position;
            var source = go.AddComponent<AudioSource>();
            setup(source, e, false);
            source.clip = clip;
            source.loop = true;
            source.Play();
            _loops.Add((source, follow, follow != null));
            return source;
        }

        private void setup(AudioSource source, SoundEvent e, bool flat)
        {
            source.playOnAwake = false;
            source.volume = e != null ? e.Volume : 1f;
            source.pitch = e != null ? Random.Range(e.Pitch.x, e.Pitch.y) : 1f;
            source.spatialBlend = flat ? 0f : e != null ? e.Spatial : 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = e != null ? e.MinDistance : 2f;
            source.maxDistance = e != null ? e.MaxDistance : 40f;
            source.dopplerLevel = 0.5f;
            source.outputAudioMixerGroup = group(e != null ? e.Group : "SFX");
        }

        private AudioMixerGroup group(string name)
        {
            if (Mixer == null || string.IsNullOrEmpty(name)) return null;
            if (_groups.TryGetValue(name, out var g)) return g;
            g = null;
            foreach (var match in Mixer.FindMatchingGroups(name))
                if (match.name == name) { g = match; break; }
            _groups[name] = g;
            return g;
        }

        private static IEnumerator fadeOut(AudioSource source, float seconds)
        {
            var from = source.volume;
            for (float t = 0; t < seconds && source != null; t += Time.deltaTime)
            {
                source.volume = from * (1f - t / seconds);
                yield return null;
            }
            if (source != null) Destroy(source.gameObject);
        }
    }
}
