using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Looping 3D ambience synthesised at start-up, so the project needs no audio files for it.
    /// Placed at each way up, it gets louder as the player approaches: sound pulls the player forward.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VantageProceduralAudio : MonoBehaviour
    {
        public enum Kind { Wind, DroneHum, RadioStatic, Sea, Generator, Buzz }

        public Kind Sound = Kind.Wind;
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
            switch (kind)
            {
                case Kind.DroneHum: return hum(seed);
                case Kind.Sea: return sea(seed);
                case Kind.Generator: return generator(seed);
                case Kind.Buzz: return buzz(seed);
                case Kind.RadioStatic: return radio(seed);
                default: return wind(seed);
            }
        }

        private static AudioClip wind(int seed)
        {
            const float seconds = 8f;
            var count = (int)(Rate * seconds);
            var data = new float[count];
            var rng = new System.Random(seed);
            float brown = 0, low = 0;

            for (int i = 0; i < count; i++)
            {
                var white = (float)(rng.NextDouble() * 2 - 1);
                brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f);
                low += (white - low) * 0.05f;
                var t = i / (float)Rate;
                // Two slow gusts that fit the loop length exactly.
                var gust = 0.55f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f / seconds) + 0.15f * Mathf.Sin(t * Mathf.PI * 2f * 3f / seconds);
                data[i] = (brown * 0.8f + low * 0.6f) * gust;
            }

            return finish("Wind", data);
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

        private static AudioClip radio(int seed)
        {
            const float seconds = 6f;
            var count = (int)(Rate * seconds);
            var data = new float[count];
            var rng = new System.Random(seed);

            for (int i = 0; i < count; i++)
            {
                var t = i / (float)Rate;
                var hiss = (float)(rng.NextDouble() * 2 - 1) * 0.18f;
                var crackle = rng.NextDouble() < 0.002 ? (float)(rng.NextDouble() * 2 - 1) * 0.8f : 0f;
                // A short automated beep every 2 seconds: still transmitting, to nobody.
                var beep = (t % 2f) < 0.12f ? Mathf.Sin(t * Mathf.PI * 2f * 1000f) * 0.25f : 0f;
                data[i] = hiss + crackle + beep;
            }

            return finish("Radio Static", data);
        }

        private static AudioClip sea(int seed)
        {
            const float seconds = 12f;
            var count = (int)(Rate * seconds);
            var data = new float[count];
            var rng = new System.Random(seed);
            float low = 0, lower = 0;

            for (int i = 0; i < count; i++)
            {
                var t = i / (float)Rate;
                var white = (float)(rng.NextDouble() * 2 - 1);
                low += (white - low) * 0.08f;
                lower += (low - lower) * 0.05f;
                // Two waves per loop: a slow rise, a crash, a long hiss back out.
                var phase = (t % (seconds / 2)) / (seconds / 2);
                var swell = phase < 0.55f ? Mathf.SmoothStep(0.15f, 1f, phase / 0.55f) : Mathf.Lerp(1f, 0.15f, (phase - 0.55f) / 0.45f);
                data[i] = (lower * 1.6f + low * 0.5f * swell) * swell;
            }

            return finish("Sea", data);
        }

        private static AudioClip generator(int seed)
        {
            const float seconds = 2f;
            var count = (int)(Rate * seconds);
            var data = new float[count];
            var rng = new System.Random(seed);
            float noise = 0;

            for (int i = 0; i < count; i++)
            {
                var t = i / (float)Rate;
                noise += ((float)(rng.NextDouble() * 2 - 1) - noise) * 0.2f;
                // Diesel putter: 24 firing pulses per second over a 48 Hz body.
                var pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * 24f)), 6f);
                var body = Mathf.Sin(t * Mathf.PI * 2f * 48f) * 0.4f + Mathf.Sin(t * Mathf.PI * 2f * 96f) * 0.15f;
                data[i] = body * (0.5f + pulse * 0.5f) + noise * pulse * 0.6f;
            }

            return finish("Generator", data);
        }

        private static AudioClip buzz(int seed)
        {
            const float seconds = 1f;
            var count = (int)(Rate * seconds);
            var data = new float[count];
            var rng = new System.Random(seed);

            for (int i = 0; i < count; i++)
            {
                var t = i / (float)Rate;
                // Mains hum of an old floodlight ballast.
                data[i] = Mathf.Sin(t * Mathf.PI * 2f * 100f) * 0.5f
                        + Mathf.Sin(t * Mathf.PI * 2f * 200f) * 0.25f
                        + Mathf.Sin(t * Mathf.PI * 2f * 300f) * 0.12f
                        + (float)(rng.NextDouble() * 2 - 1) * 0.04f;
            }

            return finish("Buzz", data);
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
