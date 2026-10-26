using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// A sentry on the outside of the tower's top (spawned by VantageSentries). Decides four times a second:
    ///   dead                                  → remembered for the run (VantageSentries won't spawn it again);
    ///   player in the ring's warning/kill zone, in sight → stop, face the player, fire ring shots: warning shots
    ///                                           (misses that kick up dust near the feet) or, in the kill zone, hits;
    ///   player on the sentry's level, in sight within EngageRange → the template's own combat;
    ///   too far from the route                → walk back to it;
    ///   otherwise                             → patrol the route back and forth, scanning outward at each end.
    /// Ring shots are scripted (the template's guns don't reach 40–80 m downhill and can't miss on purpose): a red
    /// glint at the eye, then a tracer and a crack. The template's own sight and hearing are only on while the
    /// player is on the sentry's level, so the template brain never starts a fight with the player down below.
    /// Never hunts and never takes stairs.
    /// </summary>
    [RequireComponent(typeof(FighterBrain))]
    public class VantageSentry : MonoBehaviour
    {
        [Tooltip("Degrees the sentry turns either way from 'outward' while scanning at the end of its route.")]
        public float ScanAngle = 60f;
        [Tooltip("Seconds it stands and scans at each end of its route.")]
        public float EndPause = 3f;
        [Tooltip("Farther than this from its route it walks back.")]
        public float Leash = 6f;
        [Tooltip("The template's combat starts when the player is on the sentry's level and in sight within this range.")]
        public float EngageRange = 32f;
        public float ThinkInterval = 0.25f;
        [Header("Ring shots")]
        public float ShotInterval = 1.2f;
        public float ShotDamage = 25f;
        [Tooltip("Seconds the eye glints before a shot, so the player sees where it comes from.")]
        public float GlintTime = 0.35f;
        [Tooltip("Warning shots land this far from the player (min, max metres).")]
        public Vector2 MissRadius = new Vector2(1f, 3f);
        [Header("Sounds (empty: synthesized)")]
        public AudioClip WarningCall;
        public AudioClip RadioLoop;
        public AudioClip ShotCrack;

        public bool IsDead => _motor != null && !_motor.IsAlive;
        /// <summary>Warning shots fired so far (for the autoplay bot).</summary>
        public int WarningShots { get; private set; }
        /// <summary>Ring shots that hit the player so far.</summary>
        public int Hits { get; private set; }

        private const float PatrolReach = 1.2f;
        private const float ReorderInterval = 3f;
        private static bool _warnedThisScene;
        private static AudioClip _crack;

        private VantageSentries.Route _route;
        private CharacterMotor _motor;
        private FighterBrain _brain;
        private AISight _sight;
        private AIListener _listener;
        private Material _tracerMaterial;
        private LineRenderer _tracer;
        private Light _glint;
        private ParticleSystem _dust;
        private AudioSource _voice, _radio;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private int _index, _step = 1;
        private float _nextThink, _lastOrder = -10f, _pauseUntil = -1f, _nextShot, _tracerUntil, _scriptedUntil = -1f;
        private Vector3 _scriptedAim;
        private bool _remembered, _inCombat, _firing;

        /// <summary>Distance on the ground to the route's polyline.</summary>
        public float DistanceFromRoute => _route == null ? 0f : distanceToRoute(transform.position, out _);

        public void Init(VantageSentries.Route route, Material tracerMaterial = null)
        {
            _route = route;
            _tracerMaterial = tracerMaterial;
            _motor = GetComponent<CharacterMotor>();
            _brain = GetComponent<FighterBrain>();
            _sight = GetComponent<AISight>();
            _listener = GetComponent<AIListener>();

            // Same tuning as the level soldiers (VantageEnemyAggression): guard, never retreat, no cover.
            var start = _brain.Start;
            start.Mode = AIStartMode.idle;
            _brain.Start = start;
            _brain.AvoidDistance = 1.2f;
            _brain.ImmediateThreatReaction = true;
            _brain.AttackAggressors = true;
            var retreat = _brain.Retreat;
            retreat.Health = 0f;
            _brain.Retreat = retreat;
            var cover = GetComponent<AICover>();
            if (cover != null) cover.MaxCoverDistance = 0f;
            setTemplateSenses(false);
            buildEffects();
        }

        private void Update()
        {
            if (_tracer != null && _tracer.enabled && Time.time > _tracerUntil) _tracer.enabled = false;
            if (_route == null || Time.time < _nextThink) return;
            _nextThink = Time.time + ThinkInterval;

            if (IsDead) { die(); return; }

            // The intro has it: stand and aim where it says.
            if (Time.time < _scriptedUntil)
            {
                stopFiring();
                _brain.Message("ToStopMoving");
                _brain.Message("ToTurnAt", _scriptedAim);
                _brain.Message("ToAimAt", _scriptedAim);
                return;
            }

            var player = VantageEvents.ActivePlayer();
            var ring = VantageBoundary.Instance;
            var levels = VantageTowerLevels.Instance;
            var sees = player != null && player.IsAlive && canSee(player);

            // The player on the sentry's level: the template's combat (its own eyes and ears on).
            var onLevel = player != null && levels != null && levels.LevelAt(player.transform.position) == _route.Level;
            var fight = onLevel && sees && (player.transform.position - transform.position).sqrMagnitude <= EngageRange * EngageRange;
            if (onLevel != _inCombat) { _inCombat = onLevel; setTemplateSenses(onLevel); }

            var zone = ring != null && ring.Enforcing ? ring.PlayerZone : VantageBoundary.Zone.Free;
            if (!onLevel && sees && (zone == VantageBoundary.Zone.Warning || zone == VantageBoundary.Zone.Kill))
                ringFire(player, zone == VantageBoundary.Zone.Kill);
            else
            {
                stopFiring();
                if (fight) attack(player);
                else if (DistanceFromRoute > Leash) backToRoute();
                else patrol();
            }
        }

        #region Ring shots

        private void ringFire(BaseActor player, bool kill)
        {
            if (!_firing)
            {
                _firing = true;
                _brain.Message("ToStopMoving");
                _nextShot = Time.time + Mathf.Max(GlintTime, ShotInterval * 0.5f);
                if (!_warnedThisScene)
                {
                    _warnedThisScene = true;
                    play(_voice, WarningCall != null ? WarningCall : VantageProceduralAudio.Create(VantageProceduralAudio.Kind.RadioStatic, 7), 0.9f);
                }
            }
            _brain.Message("ToTurnAt", player.transform.position);
            _brain.Message("ToAimAt", VantagePhysics.AimPoint(player));

            var glint = Mathf.Clamp01(1f - (_nextShot - Time.time) / GlintTime);
            if (_glint != null) { _glint.enabled = glint > 0f; _glint.intensity = 6f * glint; }
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + ShotInterval;
            if (_glint != null) _glint.enabled = false;

            var eye = VantagePhysics.Eye(_motor);
            if (kill)
            {
                var target = VantagePhysics.AimPoint(player);
                if (firstHit(eye, target, out var hit) && hit.transform.IsChildOf(player.transform))
                {
                    var info = new Hit(hit.point, (target - eye).normalized * -1f, ShotDamage, gameObject, hit.collider.gameObject, HitType.Rifle, 0);
                    hit.collider.SendMessage("OnHit", info, SendMessageOptions.DontRequireReceiver);
                    Hits++;
                    shotEffects(eye, hit.point, false);
                }
            }
            else
            {
                // A miss on the ground between the sentry and the player, a little to one side.
                var toSentry = transform.position - player.transform.position; toSentry.y = 0;
                var side = Vector3.Cross(Vector3.up, toSentry.normalized);
                var miss = player.transform.position + toSentry.normalized * Random.Range(MissRadius.x, MissRadius.y)
                           + side * Random.Range(-1f, 1f);
                if (Physics.Raycast(miss + Vector3.up * 3f, Vector3.down, out var ground, 8f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
                    miss = ground.point;
                WarningShots++;
                shotEffects(eye, miss, true);
            }
        }

        #region Scripted (the intro)

        /// <summary>Stands and aims at a point for a while instead of patrolling (the intro's deserter).</summary>
        public void HoldAim(Vector3 at, float seconds)
        {
            _scriptedAim = at;
            _scriptedUntil = Time.time + seconds;
            _nextThink = 0f;
        }

        /// <summary>The red glint at the eye, 0..1 (it warns of a shot).</summary>
        public void Glint(float amount)
        {
            if (_glint == null) return;
            _glint.enabled = amount > 0f;
            _glint.intensity = 6f * amount;
        }

        /// <summary>A ring shot's look and sound (tracer, crack, dust when it misses), no damage.</summary>
        public void ScriptedShot(Vector3 to, bool miss)
        {
            Glint(0f);
            shotEffects(VantagePhysics.Eye(_motor), to, miss);
        }

        #endregion

        private void stopFiring()
        {
            if (!_firing) return;
            _firing = false;
            if (_glint != null) _glint.enabled = false;
            _lastOrder = -10f;   // resume the route at once
        }

        private void shotEffects(Vector3 from, Vector3 to, bool dust)
        {
            if (_tracer != null)
            {
                _tracer.SetPosition(0, from);
                _tracer.SetPosition(1, to);
                _tracer.enabled = true;
                _tracerUntil = Time.time + 0.08f;
            }
            if (dust && _dust != null)
            {
                _dust.transform.position = to;
                _dust.Emit(14);
            }
            play(_voice, ShotCrack != null ? ShotCrack : crack(), 1f);
        }

        #endregion

        #region Movement and fighting

        private void attack(BaseActor player)
        {
            if (_brain.Threat == player && _brain.CanSeeTheThreat && Time.time - _lastOrder < ReorderInterval) return;
            _lastOrder = Time.time;
            _brain.ToAttack(player);
            _brain.ToOpenFire();
        }

        private void backToRoute()
        {
            if (Time.time - _lastOrder < ReorderInterval) return;
            _lastOrder = Time.time;
            distanceToRoute(transform.position, out var nearest);
            _index = nearest;
            _brain.Message("ToWalkTo", _route.Points[nearest]);
            _brain.Message("ToFaceWalkDirection");
        }

        /// <summary>Back and forth along the route; at either end it stands and scans away from the tower.</summary>
        private void patrol()
        {
            if (Time.time < _pauseUntil)
            {
                // Scan: sweep ±ScanAngle around "outward" (away from the tower) over the pause.
                var outward = transform.position - towerCentre(); outward.y = 0;
                if (outward.sqrMagnitude < 0.01f) outward = transform.forward;
                var phase = 1f - (_pauseUntil - Time.time) / EndPause;
                var look = Quaternion.Euler(0, Mathf.Sin(phase * Mathf.PI * 2f) * ScanAngle, 0) * outward.normalized;
                _brain.Message("ToSlowlyTurnTo", look);
                return;
            }
            var to = _route.Points[_index] - transform.position; to.y = 0;
            var arrived = to.sqrMagnitude < PatrolReach * PatrolReach;
            if (arrived)
            {
                var end = _index + _step < 0 || _index + _step >= _route.Points.Count;
                if (end)
                {
                    _step = -_step;
                    _pauseUntil = Time.time + EndPause;
                    _brain.Message("ToStopMoving");
                    _lastOrder = -10f;
                    return;
                }
                _index += _step;
            }
            if (!arrived && Time.time - _lastOrder < ReorderInterval) return;
            _lastOrder = Time.time;
            _brain.Message("ToWalkTo", _route.Points[_index]);
            _brain.Message("ToFaceWalkDirection");
        }

        private void die()
        {
            if (_remembered) return;
            _remembered = true;
            VantageRun.DeadSentries.Add(name);
            if (_radio != null) _radio.Stop();
            if (_glint != null) _glint.enabled = false;
            enabled = false;
        }

        #endregion

        #region Senses and geometry

        /// <summary>Line of sight from the eye to the player's chest (the sentry's own colliders ignored).</summary>
        private bool canSee(BaseActor player)
        {
            var eye = VantagePhysics.Eye(_motor);
            return firstHit(eye, VantagePhysics.AimPoint(player), out var hit) && hit.transform.IsChildOf(player.transform);
        }

        private bool firstHit(Vector3 from, Vector3 to, out RaycastHit first)
        {
            first = default;
            var dir = to - from;
            var count = Physics.RaycastNonAlloc(from, dir.normalized, _hits, dir.magnitude + 0.5f, VantagePhysics.Sight, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform) || _hits[i].distance >= best) continue;
                best = _hits[i].distance;
                first = _hits[i];
            }
            return best < float.MaxValue;
        }

        /// <summary>The template's own sight and hearing: on only while the player is on the sentry's level.</summary>
        private void setTemplateSenses(bool on)
        {
            if (_sight != null) _sight.enabled = on;
            if (_listener != null) _listener.enabled = on;
        }

        private float distanceToRoute(Vector3 position, out int nearestPoint)
        {
            var best = float.MaxValue;
            nearestPoint = 0;
            var p = position; p.y = 0;
            for (int i = 1; i < _route.Points.Count; i++)
            {
                var a = _route.Points[i - 1]; a.y = 0;
                var b = _route.Points[i]; b.y = 0;
                var ab = b - a;
                var t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                var d = Vector3.Distance(p, a + ab * t);
                if (d < best) { best = d; nearestPoint = t < 0.5f ? i - 1 : i; }
            }
            return best;
        }

        private Vector3 towerCentre()
        {
            var levels = VantageTowerLevels.Instance;
            return levels != null ? levels.TowerArea.center : transform.position - transform.forward;
        }

        #endregion

        #region Effects and sound

        private void buildEffects()
        {
            var eye = VantagePhysics.Eye(_motor);
            var glint = new GameObject("Glint");
            glint.transform.SetParent(transform, false);
            glint.transform.position = eye;
            _glint = glint.AddComponent<Light>();
            _glint.type = LightType.Point;
            _glint.color = new Color(1f, 0.15f, 0.1f);
            _glint.range = 4f;
            _glint.shadows = LightShadows.None;
            _glint.enabled = false;

            if (_tracerMaterial != null)
            {
                var tracer = new GameObject("Tracer");
                tracer.transform.SetParent(transform, false);
                _tracer = tracer.AddComponent<LineRenderer>();
                _tracer.sharedMaterial = _tracerMaterial;
                _tracer.useWorldSpace = true;
                _tracer.positionCount = 2;
                _tracer.widthMultiplier = 0.05f;
                _tracer.startColor = _tracer.endColor = new Color(1f, 0.85f, 0.5f);
                _tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _tracer.enabled = false;

                var dust = new GameObject("Dust");
                dust.transform.SetParent(transform, false);
                _dust = dust.AddComponent<ParticleSystem>();
                _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _dust.main;
                main.playOnAwake = false;
                main.loop = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 0.6f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                main.startColor = new Color(0.55f, 0.45f, 0.32f, 0.8f);
                main.gravityModifier = 0.6f;
                var emission = _dust.emission;
                emission.rateOverTime = 0f;
                var shape = _dust.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.15f;
                dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = _tracerMaterial;
            }

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.spatialBlend = 1f;
            _voice.minDistance = 8f;
            _voice.maxDistance = 120f;
            _voice.playOnAwake = false;

            _radio = gameObject.AddComponent<AudioSource>();
            _radio.clip = RadioLoop != null ? RadioLoop : VantageProceduralAudio.Create(VantageProceduralAudio.Kind.RadioStatic, name.GetHashCode());
            _radio.loop = true;
            _radio.spatialBlend = 1f;
            _radio.minDistance = 2f;
            _radio.maxDistance = 25f;
            _radio.volume = 0.25f;
            _radio.Play();
        }

        private static void play(AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null) source.PlayOneShot(clip, volume);
        }

        /// <summary>A rifle crack: a short burst of noise with a fast decay (made once).</summary>
        private static AudioClip crack()
        {
            if (_crack != null) return _crack;
            const int rate = 44100;
            var samples = new float[rate / 6];
            var random = new System.Random(11);
            for (int i = 0; i < samples.Length; i++)
            {
                var t = (float)i / rate;
                samples[i] = (float)(random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 38f) * 0.9f;
            }
            _crack = AudioClip.Create("Sentry Crack", samples.Length, 1, rate, false);
            _crack.SetData(samples, 0);
            return _crack;
        }

        // A new play session or scene: the first warning call plays again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void clearOnPlay() => _warnedThisScene = false;

        #endregion
    }
}
