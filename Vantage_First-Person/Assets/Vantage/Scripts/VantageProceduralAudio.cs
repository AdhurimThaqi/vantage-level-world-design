using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The last stand-in for two looping sounds when the user's file is missing: the drone hum (drone and turret
    /// prefabs, switched off as soon as DRONE_Hum plays) and the alarm siren (ALARM_Siren). Since 2026-11-16 nothing
    /// else is synthesised: an empty sound event is silent (the generated wind, radio static and guide pulse sounded
    /// like a broken speaker and a heart monitor and were removed).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VantageProceduralAudio : MonoBehaviour
    {
        public enum Kind { DroneHum = 1, Siren = 6 }   // values are serialized

        public Kind Sound = Kind.DroneHum;
        [Range(0, 1)] public float Volume = 0.6f;
        public float MinDistance = 2f;
        public float MaxDistance = 25f;
        [Tooltip("Pitch multiplier, e.g. to tell drones apart.")]
        public float Pitch = 1f;

        [Tooltip("Drone hum only: when this sound is not part of a drone, fade it out once no living drone on the same floor is within this distance (0 = never).")]
        public float SilenceWhenNoDroneWithin = 20f;
        [Tooltip("Drones more than this far above or below do not count (they are on another floor).")]
        public float SameFloorHeight = 3f;

        private const int Rate = 22050;

        private AudioSource _source;
        private bool _followsDrones;
        private float _nextCheck;
        private float _targetVolume;

        private void Awake()
        {
            var source = GetComponent<AudioSource>();
            _source = source;
            // A drone's own hum is stopped by the drone when it dies. A free-standing hum (the "drones upstairs"
            // guidance sound) must not keep playing after those drones are dead.
            _followsDrones = Sound == Kind.DroneHum && SilenceWhenNoDroneWithin > 0 && GetComponentInParent<VantageDrone>() == null;
            _targetVolume = Volume;
            source.clip = Create(Sound, GetInstanceID());
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.volume = Volume;
            source.pitch = Pitch;
            source.dopplerLevel = 0.3f;
            source.time = Random.Range(0f, source.clip.length * 0.9f);
            source.Play();
        }

        private void Update()
        {
            if (!_followsDrones)
                return;

            // Level 2 drones are generated at runtime, so look them up instead of wiring them in.
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 0.5f;
                _targetVolume = VantageDrone.AnyAliveNear(transform.position, SilenceWhenNoDroneWithin, SameFloorHeight) ? Volume : 0f;
            }

            _source.volume = Mathf.MoveTowards(_source.volume, _targetVolume, Volume * Time.deltaTime / 2f);
        }

        public static AudioClip Create(Kind kind, int seed)
        {
            return kind == Kind.Siren ? siren() : hum(seed);
        }

        private static AudioClip hum(int seed)
        {
            const float seconds = 2f;
            var count = (int)(Rate * seconds);
            var data = new float[count];
            var rng = new System.Random(seed);
            float noise = 0;

            for (int i = 0; i < count; i++)
            {
                var t = i / (float)Rate;
                // Whole-number cycles per loop keep it seamless: 110 Hz, 220 Hz, 331 Hz rotor whine.
                var tone = Mathf.Sin(t * Mathf.PI * 2f * 110f) * 0.5f
                         + Mathf.Sin(t * Mathf.PI * 2f * 220f) * 0.25f
                         + Mathf.Sin(t * Mathf.PI * 2f * 331f) * 0.12f;
                noise += ((float)(rng.NextDouble() * 2 - 1) - noise) * 0.3f;
                var wobble = 0.85f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f * 4f);
                data[i] = (tone + noise * 0.25f) * wobble * 0.6f;
            }

            return finish("Drone Hum", data);
        }

        private static AudioClip siren()
        {
            // Military wail: the pitch climbs and falls between 520 and 1050 Hz every 2 s (phase integrated, so
            // the sweep has no clicks), with a square-ish edge from the third harmonic.
            const float seconds = 2f;
            var count = (int)(Rate * seconds);
            var data = new float[count];
            var phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                var t = i / (float)Rate;
                var sweep = 0.5f - 0.5f * Mathf.Cos(t / seconds * Mathf.PI * 2f);
                var frequency = Mathf.Lerp(520f, 1050f, sweep);
                phase += frequency / Rate;
                var x = (float)(phase * Mathf.PI * 2.0);
                data[i] = Mathf.Sin(x) * 0.7f + Mathf.Sin(x * 3f) * 0.2f;
            }
            return finish("Siren", data);
        }

        private static AudioClip finish(string name, float[] data)
        {
            // Short crossfade so the loop point does not click.
            var fade = Rate / 10;
            for (int i = 0; i < fade; i++)
            {
                var k = i / (float)fade;
                data[i] = data[i] * k + data[data.Length - fade + i] * (1 - k);
            }

            var peak = 0.001f;
            foreach (var s in data)
                peak = Mathf.Max(peak, Mathf.Abs(s));
            for (int i = 0; i < data.Length; i++)
                data[i] = data[i] / peak * 0.8f;

            var clip = AudioClip.Create(name, data.Length - fade, 1, Rate, false);
            var trimmed = new float[data.Length - fade];
            System.Array.Copy(data, trimmed, trimmed.Length);
            clip.SetData(trimmed, 0);
            return clip;
        }
    }
}
