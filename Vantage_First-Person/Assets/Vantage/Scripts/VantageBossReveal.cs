using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The finale's boss reveal: when the mech appears on the helipad, an 8 s cutscene in the intro's style takes
    /// the camera (letterbox, subtitles in the bottom bar):
    ///   1. low in front of the machine, pushing in and tilting up at it ("It was waiting for you");
    ///   2. a slow arc around it at head height; it roars and its eye flares (the rifle-only rule, the way out);
    ///   3. back to the player's camera, and the fight starts with the STG_BossFight stinger.
    /// Sounds: BOSS_Reveal under the whole cutscene (faded out as the fight starts), BOSS_Roar at the machine.
    /// The boss stands still and the player takes no damage meanwhile (VantageCinematic); Space, Enter or Esc skip
    /// it after a second. Started by VantageTowerLevels.ReachHelipad; stopped at once if the boss dies (Stop),
    /// before the extraction takes the camera. Every position comes from the boss's own bounds and facing; the
    /// camera is kept in front of walls and railings by a sphere cast from the machine outwards.
    /// </summary>
    public class VantageBossReveal : MonoBehaviour
    {
        public const float ShotLow = 3.4f, ShotArc = 6.6f, Length = 8f, RoarAt = 3.8f;
        private const float SkipAfter = 1f, CameraRadius = 0.3f;

        public static VantageBossReveal Instance { get; private set; }
        /// <summary>Reveals started since Play mode began (the bot).</summary>
        public static int Count { get; private set; }
        public static bool Playing => Instance != null && Instance._playing;
        public float Elapsed => Time.time - _startTime;

        private VantageHeavyRobot _boss;
        private Camera _camera;
        private Vector3 _endPosition, _centre, _forward;
        private Quaternion _endRotation;
        private float _feet, _height, _radius, _startTime, _clear = -1f, _side = 1f;
        private int _shot = -1;
        private bool _playing, _roared;
        private Vector3 _arcEndPosition;
        private Quaternion _arcEndRotation;
        private VantageLetterbox _overlay;
        private AudioSource _sound;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void resetCount() => Count = 0;

        /// <summary>Starts the reveal for a boss that just spawned (no-op without a camera, a living player, or during another cutscene).</summary>
        public static void Play(VantageHeavyRobot boss)
        {
            if (boss == null || Instance != null || VantageCinematic.Active || Camera.main == null || VantageEvents.ActivePlayer() == null) return;
            new GameObject("VANTAGE Boss Reveal").AddComponent<VantageBossReveal>().begin(boss);
        }

        /// <summary>Ends a running reveal at once (the boss died): control back before anything else takes the camera.</summary>
        public static void Stop()
        {
            if (Instance != null) Instance.finish();
        }

        private void begin(VantageHeavyRobot boss)
        {
            Instance = this;
            Count++;
            _boss = boss;
            _camera = Camera.main;
            _endPosition = _camera.transform.position;
            _endRotation = _camera.transform.rotation;

            // The machine's size from its renderers (the model under it), its facing towards the player.
            var bounds = new Bounds(boss.transform.position, Vector3.one);
            var any = false;
            foreach (var r in boss.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer) continue;
                if (any) bounds.Encapsulate(r.bounds); else { bounds = r.bounds; any = true; }
            }
            _centre = new Vector3(bounds.center.x, 0f, bounds.center.z);
            _feet = boss.transform.position.y;
            _height = Mathf.Max(2f, bounds.max.y - _feet);
            _radius = Mathf.Max(1.5f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            _forward = boss.transform.forward; _forward.y = 0f;
            _forward = _forward.sqrMagnitude > 0.01f ? _forward.normalized : Vector3.forward;
            // The machine faces the player, so straight in front the player stands in the shot: the low shot looks
            // from 60° to one side, the side with more room (railings, the platform's edge).
            _side = room(Quaternion.AngleAxis(60f, Vector3.up) * _forward) >= room(Quaternion.AngleAxis(-60f, Vector3.up) * _forward) ? 1f : -1f;

            VantageCinematic.Begin();
            _overlay = new VantageLetterbox("VANTAGE Boss Reveal", 60);
            _sound = VantageAudio.PlayLoop(VantageSounds.BossReveal, null);
            if (_sound != null) _sound.loop = false;
            _startTime = Time.time;
            _playing = true;
            Debug.Log($"[Vantage] Boss reveal: {_height:0.0} m tall, {_radius:0.0} m wide.");
        }

        private void Update()
        {
            if (!_playing) return;
            var t = Elapsed;
            if (_boss == null || _boss.IsDead || VantageEvents.ActivePlayer() == null || t >= Length) { finish(); return; }
            if (t >= SkipAfter && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))) { finish(); return; }
            if (!_roared && t >= RoarAt)
            {
                _roared = true;
                VantageAudio.Play(VantageSounds.BossRoar, _boss.transform.position + Vector3.up * _height * 0.8f);
            }
            // The eye flares with the roar and dies down over two seconds.
            _boss.Glow(t < RoarAt ? 0.15f : Mathf.Clamp01(1f - (t - RoarAt - 0.6f) / 2f));
        }

        private void LateUpdate()
        {
            if (!_playing || _camera == null) return;
            var t = Elapsed;
            pose(t, out var position, out var rotation);
            _camera.transform.SetPositionAndRotation(position, rotation);
            overlay(t);
        }

        private void pose(float t, out Vector3 position, out Quaternion rotation)
        {
            var feet = _centre + Vector3.up * _feet;
            if (t < ShotLow)
            {
                // 1. Low at its front corner (off the player's line), pushing in and tilting up from its chest to its head.
                var u = Mathf.SmoothStep(0f, 1f, t / ShotLow);
                var at = feet + Vector3.up * Mathf.Lerp(_height * 0.55f, _height * 0.85f, u);
                var dirLow = Quaternion.AngleAxis(_side * Mathf.Lerp(65f, 50f, u), Vector3.up) * _forward;
                var want = feet + dirLow * Mathf.Lerp(_radius + 9f, _radius + 5f, u) + Vector3.up * Mathf.Lerp(0.6f, 1.4f, u);
                position = clear(at, want, 0, t);
                rotation = Quaternion.LookRotation(at - position);
                return;
            }
            if (t < ShotArc)
            {
                // 2. A slow arc around it at head height (it roars half way).
                var u = Mathf.SmoothStep(0f, 1f, (t - ShotLow) / (ShotArc - ShotLow));
                var at = feet + Vector3.up * _height * 0.6f;
                var dir = Quaternion.AngleAxis(_side * Mathf.Lerp(45f, -45f, u), Vector3.up) * _forward;
                var want = feet + dir * (_radius + 8f) + Vector3.up * (_height * 0.9f + 1.5f);
                position = clear(at, want, 1, t);
                rotation = Quaternion.LookRotation(at - position);
                _arcEndPosition = position;
                _arcEndRotation = rotation;
                return;
            }
            // 3. Back to the player's own camera.
            var k = Mathf.SmoothStep(0f, 1f, (t - ShotArc) / (Length - ShotArc));
            position = Vector3.Lerp(_arcEndPosition, _endPosition, k);
            rotation = Quaternion.Slerp(_arcEndRotation, _endRotation, k);
        }

        /// <summary>
        /// The wanted camera position pulled in front of anything solid between the machine and it (not the machine
        /// itself, not the player). The distance closes at once and opens again smoothly, so railings don't make it jump.
        /// </summary>
        private Vector3 clear(Vector3 from, Vector3 want, int shot, float t)
        {
            var dir = want - from;
            var distance = dir.magnitude;
            dir /= Mathf.Max(0.001f, distance);
            var free = distance;
            var player = VantageEvents.ActivePlayer();
            var n = Physics.SphereCastNonAlloc(from, CameraRadius, dir, _hits, distance, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i].collider.transform;
                if (_hits[i].distance <= 0f || c.IsChildOf(_boss.transform) || (player != null && c.IsChildOf(player.transform))) continue;
                free = Mathf.Min(free, _hits[i].distance - 0.1f);
            }
            free = Mathf.Max(free, _radius + 1f);
            if (shot != _shot || _clear < 0f || free < _clear) { _shot = shot; _clear = free; }
            else _clear = Mathf.MoveTowards(_clear, free, 6f * Time.deltaTime);
            return from + dir * _clear;
        }

        /// <summary>Free distance from the machine's chest along a direction (up to 15 m), for picking the low shot's side.</summary>
        private float room(Vector3 dir)
        {
            var from = _centre + Vector3.up * (_feet + _height * 0.5f);
            var free = 15f;
            var n = Physics.SphereCastNonAlloc(from, CameraRadius, dir, _hits, free, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (_hits[i].distance > 0f && !_hits[i].collider.transform.IsChildOf(_boss.transform)) free = Mathf.Min(free, _hits[i].distance);
            return free;
        }

        private void overlay(float t)
        {
            if (_overlay == null) return;
            _overlay.Bars = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((Length - t) / 0.6f);
            if (t >= 0.5f && t < ShotLow - 0.1f)
                _overlay.Subtitle(0, "Finale: Helipad", "It was waiting for you", VantageLetterbox.Fade(t, 0.5f, ShotLow - 0.1f));
            else if (t >= RoarAt + 0.2f && t < ShotArc + 0.6f)
                _overlay.Subtitle(1, _boss.RifleOnly ? "Only the rifle hurts it" : "The last machine",
                                  "Bring it down: the helicopter is your way out", VantageLetterbox.Fade(t, RoarAt + 0.2f, ShotArc + 0.6f));
            else _overlay.Subtitle(-1, "", "", 0f);
        }

        /// <summary>Camera back, controls back, the fight's stinger; the reveal's sound fades under it.</summary>
        private void finish()
        {
            if (!_playing) return;
            _playing = false;
            if (_camera != null) _camera.transform.SetPositionAndRotation(_endPosition, _endRotation);
            if (_boss != null && !_boss.IsDead) _boss.Glow(0f);
            VantageCinematic.End();
            VantageAudio.Stop(_sound, 2.5f);
            _sound = null;
            _overlay?.Destroy();
            _overlay = null;
            if (_boss != null && !_boss.IsDead)
            {
                var music = FindAnyObjectByType<VantageMusic>();
                if (music != null) music.Stinger(VantageSounds.StgBossFight);
                else VantageAudio.Play2D(VantageSounds.StgBossFight);
            }
            Debug.Log($"[Vantage] Boss reveal ended at {Elapsed:0.0} s.");
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _overlay?.Destroy();   // a scene reload mid-reveal: VantageCinematic resets itself on load
        }
    }
}
