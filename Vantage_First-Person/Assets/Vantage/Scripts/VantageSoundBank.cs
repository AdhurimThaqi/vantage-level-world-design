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
    /// Every sound event of the game (Assets/Audio/VantageSoundBank.asset). Built and filled from the WAVs in
    /// Assets/Audio by Vantage → Audio → Refresh Sound Bank; the settings per event are edited here.
    /// </summary>
    [CreateAssetMenu(menuName = "Vantage/Sound Bank")]
    public class VantageSoundBank : ScriptableObject
    {
        public List<SoundEvent> Events = new List<SoundEvent>();

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
