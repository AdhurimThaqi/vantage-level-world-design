using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The sound of the space the player is in: outside, level 1, level 2, level 3 or the helipad (the level brain's
    /// own areas, so a re-export keeps working). A new zone counts once it has held for Settle seconds (no flapping at
    /// the top of a stair flight); then its ambience bed fades in over the old one and the mixer's reverb eases to the
    /// zone's preset (exposed parameters ReverbRoom, ReverbDecay, ReverbWet). On level 3 the debris one-shots crumble
    /// around the player now and then. The presets are the user's to tune.
    /// </summary>
    public class VantageAudioZones : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public string Name;
            public string Bed;
            [Tooltip("SFX Reverb room level (mB, -10000 to 0).")]
            public float Room;
            [Tooltip("SFX Reverb decay time (s).")]
            public float Decay;
            [Tooltip("Send level from SFX/Voice to the Reverb group (dB).")]
            public float Wet;
        }

        public List<Zone> Zones = new List<Zone>
        {
            new Zone { Name = "Outside", Bed = VantageSounds.AmbExterior, Room = -3000f, Decay = 0.6f, Wet = -20f },
            new Zone { Name = "Level 1", Bed = VantageSounds.AmbL1, Room = -1200f, Decay = 0.8f, Wet = -10f },
            new Zone { Name = "Level 2", Bed = VantageSounds.AmbL2, Room = -800f, Decay = 1.8f, Wet = -6f },
            new Zone { Name = "Level 3", Bed = VantageSounds.AmbL3, Room = -1500f, Decay = 1.1f, Wet = -12f },
            new Zone { Name = "Helipad", Bed = VantageSounds.AmbHelipad, Room = -6000f, Decay = 0.3f, Wet = -30f },
        };
        [Tooltip("Seconds for the bed crossfade and the reverb change.")]
        public float Fade = 2f;
        public float Poll = 0.25f;
        [Tooltip("A new zone must hold this long before it counts.")]
        public float Settle = 0.75f;
        [Tooltip("Seconds between debris crumbles on level 3 (random in this range).")]
        public Vector2 DebrisEvery = new Vector2(6f, 14f);

        public int Current { get; private set; } = -1;
        public string ActiveBed => Current >= 0 && Current < Zones.Count ? Zones[Current].Bed : null;
        /// <summary>How many zone changes since the scene loaded.</summary>
        public int Changes { get; private set; }

        private const int Level3 = 3;
        private AudioSource _bed, _fading;
        private float _nextPoll, _candidateSince, _fadeStart = -100f, _nextDebris, _bedTarget, _fadingFrom;
        private bool _blending;
        private int _candidate = -1;
        private Zone _from, _to;

        private void Update()
        {
            if (Time.time >= _nextPoll)
            {
                _nextPoll = Time.time + Poll;
                var zone = zoneNow();
                if (zone != _candidate) { _candidate = zone; _candidateSince = Time.time; }
                if (zone >= 0 && zone != Current && Time.time - _candidateSince >= (Current < 0 ? 0f : Settle)) enter(zone);
            }
            blend();
            debris();
        }

        private int zoneNow()
        {
            var player = VantageEvents.ActivePlayer();
            var levels = VantageTowerLevels.Instance;
            if (player == null || levels == null) return -1;
            var zone = levels.LevelAt(player.transform.position) + 1;   // -1 outside → 0
            return Mathf.Clamp(zone, 0, Zones.Count - 1);
        }

        private void enter(int zone)
        {
            _from = Current >= 0 ? Zones[Current] : Zones[zone];
            _to = Zones[zone];
            Current = zone;
            Changes++;
            if (_fading != null) VantageAudio.Stop(_fading);
            _fading = _bed;
            _fadingFrom = _fading != null ? _fading.volume : 0f;
            var fallback = zone == 0 ? VantageProceduralAudio.Create(VantageProceduralAudio.Kind.Wind, 3) : null;
            _bed = VantageAudio.PlayLoop(_to.Bed, null, fallback);
            _bedTarget = VantageAudio.Has(_to.Bed) ? VantageAudio.Instance.Bank.Find(_to.Bed).Volume : 0.5f;
            if (_bed != null) _bed.volume = 0f;
            _fadeStart = Time.time;
            _blending = true;
        }

        /// <summary>The bed crossfade and the reverb ease, while a change is under way.</summary>
        private void blend()
        {
            if (!_blending) return;
            var k = Mathf.Clamp01((Time.time - _fadeStart) / Mathf.Max(0.01f, Fade));
            if (_bed != null) _bed.volume = _bedTarget * k;
            if (_fading != null) _fading.volume = _fadingFrom * (1f - k);
            var s = Mathf.SmoothStep(0f, 1f, k);
            VantageAudio.SetParameter("ReverbRoom", Mathf.Lerp(_from.Room, _to.Room, s));
            VantageAudio.SetParameter("ReverbDecay", Mathf.Lerp(_from.Decay, _to.Decay, s));
            VantageAudio.SetParameter("ReverbWet", Mathf.Lerp(_from.Wet, _to.Wet, s));
            if (k < 1f) return;
            if (_fading != null) { VantageAudio.Stop(_fading); _fading = null; }
            _blending = false;
        }

        private void debris()
        {
            if (Current != Level3 || Time.time < _nextDebris) return;
            _nextDebris = Time.time + UnityEngine.Random.Range(DebrisEvery.x, DebrisEvery.y);
            if (!VantageAudio.Has(VantageSounds.AmbDebris)) return;
            var player = VantageEvents.ActivePlayer();
            if (player == null) return;
            var dir = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0) * Vector3.forward;
            VantageAudio.Play(VantageSounds.AmbDebris, player.transform.position + dir * UnityEngine.Random.Range(6f, 15f) + Vector3.up * 2f);
        }
    }
}
