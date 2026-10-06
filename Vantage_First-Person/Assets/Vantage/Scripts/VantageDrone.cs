using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The tower's automated defence. One prefab, two behaviours (concept doc):
    /// Mobile = hovering drone that strafes around the player, otherwise a fixed turret that only turns.
    /// Uses Cover Shooter's Hit / CharacterHealth so every weapon in the project can damage it,
    /// and damages whichever player view (first or third person) is active.
    /// </summary>
    [RequireComponent(typeof(CharacterHealth))]
    public class VantageDrone : MonoBehaviour
    {
        [Header("Behaviour")]
        public bool Mobile = true;
        public float DetectRange = 16f;
        [Tooltip("Once alerted it keeps hunting up to this range.")]
        public float ChaseRange = 30f;
        public float PreferredDistance = 9f;
        public float Speed = 3.5f;
        public float HoverHeight = 2.2f;
        [Tooltip("How far it wanders from its post while idle.")]
        public float PatrolRadius = 3f;

        [Header("Weapon")]
        public float Damage = 8f;
        public float FireInterval = 1.1f;
        [Tooltip("Seconds the eye glows before each shot, so the player can react.")]
        public float WindUp = 0.35f;
        [Range(0, 1)] public float Accuracy = 0.55f;
        public float Range = 35f;

        [Header("Parts")]
        public Transform Head;
        public Transform Muzzle;
        public Transform[] Rotors;
        public Renderer Eye;
        public Light EyeLight;
        public LineRenderer Tracer;
        public GameObject MuzzleFlash;
        public GameObject HitEffect;
        public GameObject DeathExplosion;
        public AudioClip FireSound;
        public AudioClip DeathSound;

        public bool IsDead { get; private set; }

        private CharacterHealth _health;
        private AudioSource _audio;
        private Vector3 _home;
        private Vector3 _patrolTarget;
        private BaseActor _target;
        private Vector3 _lastKnown;
        private bool _alerted;
        private float _nextShot;
        private float _windUpStart = -1f;
        private float _strafeSign = 1f;
        private float _nextStrafeFlip;
        private float _tracerOff;
        private Color _eyeIdle = new Color(1f, 0.25f, 0.1f);
        private static readonly RaycastHit[] _hits = new RaycastHit[16];

        private const int ObstacleMask = ~((1 << 2) | (1 << 10) | (1 << 11));
        private const int SightMask = ~((1 << 2) | (1 << 11));

        private void Awake()
        {
            _health = GetComponent<CharacterHealth>();
            _audio = GetComponent<AudioSource>();
            _home = transform.position;
            _patrolTarget = _home;
            _nextShot = Time.time + Random.Range(0.5f, FireInterval);
            if (Tracer != null)
                Tracer.enabled = false;
        }

        private void Update()
        {
            if (IsDead)
                return;

            if (_health.Health <= 0)
            {
                die();
                return;
            }

            spinRotors();
            if (Tracer != null && Tracer.enabled && Time.time > _tracerOff)
                Tracer.enabled = false;

            acquireTarget();

            var canSee = _target != null && canSeeTarget(out _);
            if (canSee)
            {
                _alerted = true;
                _lastKnown = aimPoint(_target);
            }

            if (Mobile)
                move(canSee);

            aim(canSee);
            shoot(canSee);
        }

        #region Senses

        private void acquireTarget()
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null)
            {
                _target = null;
                return;
            }

            var range = _alerted ? ChaseRange : DetectRange;
            _target = Vector3.Distance(player.transform.position, transform.position) <= range ? player : null;

            if (_target == null)
                _alerted = false;
        }

        private bool canSeeTarget(out Vector3 point)
        {
            point = aimPoint(_target);
            var origin = Muzzle != null ? Muzzle.position : transform.position;
            var direction = point - origin;
            var count = Physics.RaycastNonAlloc(origin, direction.normalized, _hits, direction.magnitude + 0.5f, SightMask, QueryTriggerInteraction.Ignore);

            var closest = float.MaxValue;
            Transform first = null;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform) || _hits[i].distance >= closest)
                    continue;
                closest = _hits[i].distance;
                first = _hits[i].collider.transform;
            }

            return first != null && first.IsChildOf(_target.transform);
        }

        private static Vector3 aimPoint(BaseActor actor)
        {
            var collider = actor.Collider;
            return collider != null ? collider.bounds.center + Vector3.up * 0.35f : actor.transform.position + Vector3.up * 1.4f;
        }

        #endregion

        #region Movement

        private void move(bool canSee)
        {
            Vector3 desired;

            if (_alerted && _target != null)
            {
                var targetPos = canSee ? aimPoint(_target) : _lastKnown;
                var flat = transform.position - targetPos;
                flat.y = 0;
                if (flat.sqrMagnitude < 0.01f)
                    flat = transform.forward;

                if (Time.time > _nextStrafeFlip)
                {
                    _strafeSign = Random.value < 0.5f ? -1f : 1f;
                    _nextStrafeFlip = Time.time + Random.Range(1.5f, 3.5f);
                }

                // Hold the preferred distance, circle sideways, stay a bit above the player's eye line.
                var keep = targetPos + flat.normalized * (canSee ? PreferredDistance : 1.5f);
                var side = Vector3.Cross(Vector3.up, flat.normalized) * _strafeSign * (canSee ? 2.5f : 0f);
                desired = keep + side;
                desired.y = targetPos.y + HoverHeight * 0.6f;
            }
            else
            {
                if (Vector3.Distance(transform.position, _patrolTarget) < 0.5f)
                    _patrolTarget = _home + new Vector3(Random.Range(-PatrolRadius, PatrolRadius), Random.Range(-0.4f, 0.4f), Random.Range(-PatrolRadius, PatrolRadius));
                desired = _patrolTarget;
            }

            var speed = _alerted ? Speed : Speed * 0.4f;
            var step = Vector3.ClampMagnitude(desired - transform.position, speed * Time.deltaTime);
            step = avoid(step);
            transform.position += step;

            // Gentle bob so it never looks parked.
            transform.position += Vector3.up * Mathf.Sin(Time.time * 2.1f + GetInstanceID()) * 0.002f;
        }

        /// <summary>
        /// Slide along walls instead of flying through them.
        /// </summary>
        private Vector3 avoid(Vector3 step)
        {
            var distance = step.magnitude;
            if (distance < 0.0001f)
                return step;

            if (Physics.SphereCast(transform.position, 0.5f, step / distance, out var hit, distance + 0.3f, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                step = Vector3.ProjectOnPlane(step, hit.normal);
                if (Physics.SphereCast(transform.position, 0.5f, step.normalized, out _, step.magnitude + 0.3f, ObstacleMask, QueryTriggerInteraction.Ignore))
                    return Vector3.zero;
            }

            return step;
        }

        private void spinRotors()
        {
            if (Rotors == null)
                return;
            foreach (var rotor in Rotors)
                if (rotor != null)
                    rotor.Rotate(0, 1800f * Time.deltaTime, 0, Space.Self);
        }

        #endregion

        #region Combat

        private void aim(bool canSee)
        {
            var head = Head != null ? Head : transform;
            Vector3 look;

            if (_alerted && _target != null)
                look = (canSee ? aimPoint(_target) : _lastKnown) - head.position;
            else
                look = Mobile ? transform.forward : Quaternion.Euler(0, Time.time * 20f, 0) * Vector3.forward;

            if (look.sqrMagnitude < 0.001f)
                return;

            var turn = Mobile ? 6f : 4f;
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(look), Time.deltaTime * turn);

            if (Mobile && head != transform)
            {
                var flat = new Vector3(look.x, 0, look.z);
                if (flat.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 4f);
            }
        }

        private void shoot(bool canSee)
        {
            if (!canSee || Vector3.Distance(transform.position, _target.transform.position) > Range)
            {
                _windUpStart = -1f;
                setEye(0f);
                return;
            }

            if (Time.time < _nextShot)
                return;

            if (_windUpStart < 0)
                _windUpStart = Time.time;

            var charge = (Time.time - _windUpStart) / Mathf.Max(0.01f, WindUp);
            setEye(charge);
            if (charge < 1f)
                return;

            _windUpStart = -1f;
            _nextShot = Time.time + FireInterval * Random.Range(0.85f, 1.15f);
            fire();
        }

        private void fire()
        {
            var origin = Muzzle != null ? Muzzle.position : transform.position;
            var point = aimPoint(_target);
            var distance = Vector3.Distance(origin, point);

            // Accuracy drops with distance; a miss goes past the player's head.
            var chance = Accuracy * Mathf.Clamp01(1.3f - distance / Range);
            var hit = Random.value < chance;
            var target = hit ? point : point + Random.onUnitSphere * Random.Range(0.6f, 1.4f);
            var direction = (target - origin).normalized;
            var end = origin + direction * Range;

            if (Physics.Raycast(origin, direction, out var rayHit, Range, SightMask, QueryTriggerInteraction.Ignore))
            {
                end = rayHit.point;
                if (!rayHit.collider.transform.IsChildOf(transform))
                {
                    var info = new Hit(rayHit.point, -direction, Damage, gameObject, rayHit.collider.gameObject, HitType.Pistol, 0);
                    rayHit.collider.SendMessage("OnHit", info, SendMessageOptions.DontRequireReceiver);

                    if (HitEffect != null && CharacterHealth.Get(rayHit.collider.gameObject) == null)
                        Destroy(Instantiate(HitEffect, rayHit.point, Quaternion.LookRotation(rayHit.normal)), 3f);
                }
            }

            if (Tracer != null)
            {
                Tracer.enabled = true;
                Tracer.SetPosition(0, origin);
                Tracer.SetPosition(1, end);
                _tracerOff = Time.time + 0.06f;
            }

            if (MuzzleFlash != null && Muzzle != null)
                Destroy(Instantiate(MuzzleFlash, Muzzle.position, Muzzle.rotation, Muzzle), 1.5f);

            if (_audio != null && FireSound != null)
            {
                _audio.pitch = Random.Range(1.3f, 1.5f);
                _audio.PlayOneShot(FireSound, 0.7f);
            }
        }

        private void setEye(float charge)
        {
            var glow = Color.Lerp(_eyeIdle, new Color(1f, 0.95f, 0.6f), Mathf.Clamp01(charge));
            if (Eye != null && Eye.material.HasProperty("_EmissionColor"))
                Eye.material.SetColor("_EmissionColor", glow * (2f + charge * 6f));
            if (EyeLight != null)
            {
                EyeLight.color = glow;
                EyeLight.intensity = 1f + charge * 4f;
            }
        }

        /// <summary>
        /// Called by every Cover Shooter and Vantage weapon through SendMessage.
        /// </summary>
        public void OnHit(Hit hit)
        {
            if (IsDead)
                return;

            _health.Deal(hit.Damage);

            if (HitEffect != null)
                Destroy(Instantiate(HitEffect, hit.Position, Quaternion.LookRotation(hit.Normal)), 3f);

            // Getting shot always gives the attacker away.
            if (hit.Attacker != null)
            {
                var attacker = hit.Attacker.GetComponentInParent<BaseActor>();
                if (attacker != null && attacker.Side != 0)
                {
                    _alerted = true;
                    _lastKnown = aimPoint(attacker);
                }
            }

            if (_health.Health <= 0)
                die();
        }

        private void die()
        {
            if (IsDead)
                return;

            IsDead = true;
            setEye(0f);
            if (Eye != null)
                Eye.material.SetColor("_EmissionColor", Color.black);
            if (EyeLight != null)
                EyeLight.enabled = false;
            if (Tracer != null)
                Tracer.enabled = false;

            var hum = GetComponentInChildren<VantageProceduralAudio>();
            if (hum != null)
                hum.GetComponent<AudioSource>().Stop();

            if (DeathExplosion != null)
                Destroy(Instantiate(DeathExplosion, transform.position, Quaternion.identity), 5f);
            if (DeathSound != null)
                AudioSource.PlayClipAtPoint(DeathSound, transform.position, 0.8f);

            VantageEvents.RaiseEnemyKilled(Mobile ? "Drone" : "Turret", transform.position);

            if (Mobile)
            {
                // Falls out of the air and tumbles.
                var body = GetComponent<Rigidbody>();
                if (body == null)
                    body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = false;
                body.useGravity = true;
                body.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
                Destroy(gameObject, 8f);
            }
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, DetectRange);
        }
    }
}
