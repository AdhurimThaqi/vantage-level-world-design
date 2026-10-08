using CoverShooter;
using UnityEngine;
using UnityEngine.AI;

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

        /// <summary>
        /// Every drone and turret in the scene, so ambience can react to them being alive.
        /// </summary>
        public static readonly System.Collections.Generic.List<VantageDrone> All = new System.Collections.Generic.List<VantageDrone>();

        /// <summary>
        /// Is a living drone or turret within the given horizontal distance of a point, on roughly the same floor?
        /// </summary>
        public static bool AnyAliveNear(Vector3 point, float distance, float maxHeightDifference)
        {
            foreach (var drone in All)
            {
                if (drone == null || drone.IsDead)
                    continue;
                var offset = drone.transform.position - point;
                if (Mathf.Abs(offset.y) > maxHeightDifference)
                    continue;
                offset.y = 0;
                if (offset.sqrMagnitude <= distance * distance)
                    return true;
            }
            return false;
        }

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
        private Material _eyeMaterial;
        private float _shownCharge = -1f;
        private static readonly RaycastHit[] _hits = new RaycastHit[16];
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _health = GetComponent<CharacterHealth>();
            _audio = GetComponent<AudioSource>();
            _home = transform.position;
            _patrolTarget = _home;
            _nextShot = Time.time + Random.Range(0.5f, FireInterval);
            if (Tracer != null)
                Tracer.enabled = false;
            // One material instance per drone, made once (Renderer.material copies on first access).
            if (Eye != null && Eye.material.HasProperty(EmissionColor))
                _eyeMaterial = Eye.material;
        }

        /// <summary>Moves the drone and makes that spot its new post (used when it has left its level).</summary>
        public void Relocate(Vector3 position)
        {
            transform.position = position;
            _home = _patrolTarget = position;
            _alerted = false;
            _cornerCount = 0;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

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

            // The whole decision: in sight → attack and hold position; lost → follow to where it was last seen;
            // last enemies of the level → come to the player; otherwise guard the post.
            _target = VantageEvents.ActivePlayer();
            var canSee = _target != null && inSightRange(_target) && canSeeTarget(out _);
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

        private bool inSightRange(BaseActor target)
        {
            var range = _alerted ? ChaseRange : DetectRange;
            return (target.transform.position - transform.position).sqrMagnitude <= range * range;
        }

        private bool canSeeTarget(out Vector3 point)
        {
            point = aimPoint(_target);
            var origin = Muzzle != null ? Muzzle.position : transform.position;
            var direction = point - origin;
            var count = Physics.RaycastNonAlloc(origin, direction.normalized, _hits, direction.magnitude + 0.5f, VantagePhysics.Sight, QueryTriggerInteraction.Ignore);

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

        private static Vector3 aimPoint(BaseActor actor) => VantagePhysics.AimPoint(actor);

        #endregion

        #region Movement

        private void move(bool canSee)
        {
            var levels = VantageTowerLevels.Instance;
            Vector3 step;
            if (canSee)
                step = towards(combatPosition(), Speed);                          // attack: hold distance, strafe
            else if (_target != null && levels != null && levels.Hunting)
                step = alongPath(_target.transform.position, Speed);              // last enemies come to the player
            else if (_alerted)
                step = followLastKnown();                                         // lost sight: go where it was seen
            else
                step = guard();

            transform.position += avoid(step);
            // Gentle bob so it never looks parked.
            transform.position += Vector3.up * Mathf.Sin(Time.time * 2.1f + GetInstanceID()) * 0.002f;
        }

        /// <summary>In sight: keep the preferred distance, circle sideways, stay a little above the player's eyes.</summary>
        private Vector3 combatPosition()
        {
            var targetPos = aimPoint(_target);
            var away = transform.position - targetPos;
            away.y = 0;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            away.Normalize();

            if (Time.time > _nextStrafeFlip)
            {
                _strafeSign = Random.value < 0.5f ? -1f : 1f;
                _nextStrafeFlip = Time.time + Random.Range(1.5f, 3.5f);
            }
            var position = targetPos + away * PreferredDistance + Vector3.Cross(Vector3.up, away) * _strafeSign * 2.5f;
            position.y = targetPos.y + HoverHeight * 0.6f;
            return position;
        }

        private Vector3 followLastKnown()
        {
            var step = alongPath(_lastKnown, Speed);
            var flat = _lastKnown - transform.position;
            flat.y = 0;
            if (flat.sqrMagnitude < 2.25f)
                _alerted = false; // reached the spot and the player is gone: back to the post
            return step;
        }

        /// <summary>Idle: drift around the post; if chasing took it far away, fly back along the NavMesh.</summary>
        private Vector3 guard()
        {
            if ((transform.position - _home).sqrMagnitude > PatrolRadius * PatrolRadius * 4f)
                return alongPath(_home, Speed * 0.6f);
            if ((transform.position - _patrolTarget).sqrMagnitude < 0.25f)
                _patrolTarget = _home + new Vector3(Random.Range(-PatrolRadius, PatrolRadius), Random.Range(-0.4f, 0.4f), Random.Range(-PatrolRadius, PatrolRadius));
            return towards(_patrolTarget, Speed * 0.4f);
        }

        private Vector3 towards(Vector3 point, float speed) => Vector3.ClampMagnitude(point - transform.position, speed * Time.deltaTime);

        // ---- path following: the NavMesh path the soldiers walk, flown at PathHeight above its corners ----

        [Tooltip("Height above the walkable floor while following a path (low enough to pass under door frames).")]
        public float PathHeight = 1.5f;
        private const float RepathInterval = 0.5f;
        private NavMeshPath _path;
        private readonly Vector3[] _corners = new Vector3[24];
        private int _cornerCount, _corner;
        private float _nextRepath;
        private Vector3 _pathGoal;

        private Vector3 alongPath(Vector3 goal, float speed)
        {
            if (Time.time >= _nextRepath || (goal - _pathGoal).sqrMagnitude > 4f)
                repath(goal);
            if (_cornerCount == 0)
                return towards(goal + Vector3.up * PathHeight, speed); // no path (off the NavMesh): fly straight

            while (_corner < _cornerCount - 1 && (_corners[_corner] + Vector3.up * PathHeight - transform.position).sqrMagnitude < 0.36f)
                _corner++;
            return towards(_corners[_corner] + Vector3.up * PathHeight, speed);
        }

        private void repath(Vector3 goal)
        {
            _nextRepath = Time.time + RepathInterval;
            _pathGoal = goal;
            _cornerCount = 0;
            _path ??= new NavMeshPath();
            if (NavMesh.SamplePosition(transform.position + Vector3.down * PathHeight, out var from, 2.5f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(goal, out var to, 2.5f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path))
                _cornerCount = _path.GetCornersNonAlloc(_corners);
            _corner = Mathf.Min(1, Mathf.Max(0, _cornerCount - 1)); // corner 0 is where the drone already is
        }

        /// <summary>
        /// Slide along walls instead of flying through them.
        /// </summary>
        private Vector3 avoid(Vector3 step)
        {
            var distance = step.magnitude;
            if (distance < 0.0001f)
                return step;

            if (Physics.SphereCast(transform.position, 0.5f, step / distance, out var hit, distance + 0.3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
            {
                step = Vector3.ProjectOnPlane(step, hit.normal);
                if (Physics.SphereCast(transform.position, 0.5f, step.normalized, out _, step.magnitude + 0.3f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore))
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

            if (Physics.Raycast(origin, direction, out var rayHit, Range, VantagePhysics.Sight, QueryTriggerInteraction.Ignore))
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
            charge = Mathf.Clamp01(charge);
            if (charge == _shownCharge)
                return; // idle drones would otherwise rewrite the same colour every frame
            _shownCharge = charge;
            var glow = Color.Lerp(_eyeIdle, new Color(1f, 0.95f, 0.6f), charge);
            if (_eyeMaterial != null)
                _eyeMaterial.SetColor(EmissionColor, glow * (2f + charge * 6f));
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
                if (attacker != null && attacker.Side != VantageTowerLevels.EnemySide)
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
            if (_eyeMaterial != null)
                _eyeMaterial.SetColor(EmissionColor, Color.black);
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
