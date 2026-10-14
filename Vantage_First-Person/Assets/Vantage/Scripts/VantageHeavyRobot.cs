using CoverShooter;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage
{
    /// <summary>
    /// The heavy robot of level 3 and, with IsBoss, the helipad boss. Slow and armoured: only the Armory rifle hurts
    /// it (pistol hits bounce off with "You need a better gun"). It walks towards the player once it has seen them,
    /// stops to fire telegraphed bursts (the eye charges first, as with the drones), and the boss adds a charge:
    /// a visible wind-up, then a dash at the player that hits hard on contact.
    /// The look is a model under "Model" (a placeholder until the user's Blender model replaces it).
    /// </summary>
    [RequireComponent(typeof(CharacterHealth))]
    public class VantageHeavyRobot : MonoBehaviour, IVantageEnemy
    {
        [Header("Behaviour")]
        public bool IsBoss;
        public bool RifleOnly = true;
        public float MaxHealth = 600f;
        public float Speed = 1.6f;
        public float EngageRange = 25f;
        public float ThinkInterval = 0.2f;

        [Header("Weapon")]
        public float Damage = 9f;
        public int BurstShots = 3;
        public float BurstSpacing = 0.12f;
        [Tooltip("Seconds the eye charges before each burst.")]
        public float WindUp = 0.8f;
        public float FireInterval = 2.2f;
        [Range(0, 1)] public float Accuracy = 0.6f;
        public float Range = 35f;

        [Header("Boss charge")]
        public float ChargeInterval = 10f;
        public float ChargeWindUp = 1f;
        public float ChargeDistance = 8f;
        public float ChargeSpeed = 9f;
        public float ChargeDamage = 25f;

        [Header("Parts")]
        public Transform Model;
        public Transform Muzzle;
        public Renderer Eye;
        public Light EyeLight;
        public LineRenderer Tracer;
        public GameObject MuzzleFlash;
        public GameObject HitEffect;
        public GameObject DeathExplosion;
        public AudioClip FireSound;
        public AudioClip DeathSound;

        public bool IsDead { get; private set; }
        public float Health => _health != null ? _health.Health : 0f;

        private CharacterHealth _health;
        private NavMeshAgent _agent;
        private AudioSource _audio;
        private BaseActor _target;
        private bool _alerted, _canSee;
        private Vector3 _lastKnown;
        private float _nextThink, _nextBurst, _windUpStart = -1f, _nextShotInBurst, _tracerOff, _nextMessage;
        private int _shotsLeft;
        private float _nextCharge, _chargeWindUp = -1f, _chargeLeft;
        private Vector3 _chargeDir;
        private bool _chargeHit;
        private float _diedAt = -1f;
        private Quaternion _deathFrom;
        private Vector3 _deathPos;
        private Material _eyeMaterial;
        private float _shownCharge = -1f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _health = GetComponent<CharacterHealth>();
            _health.MaxHealth = MaxHealth;
            _health.Health = MaxHealth;
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null) { _agent.speed = Speed; _agent.stoppingDistance = 6f; }
            _audio = GetComponent<AudioSource>();
            if (Eye != null && Eye.material.HasProperty(EmissionColor)) _eyeMaterial = Eye.material;
            if (Tracer != null) Tracer.enabled = false;
            ForwardChildHits();
            _nextBurst = Time.time + 1.5f;
            _nextCharge = Time.time + ChargeInterval * 0.5f;
            setEye(0f);
        }

        /// <summary>Hits on any child collider (a replacement model's own colliders) reach OnHit on the robot.</summary>
        public void ForwardChildHits()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (c.gameObject != gameObject && c.GetComponent<VantageHitForwarder>() == null)
                    c.gameObject.AddComponent<VantageHitForwarder>().Target = gameObject;
        }

        private void Update()
        {
            if (IsDead) { collapse(); return; }
            if (Tracer != null && Tracer.enabled && Time.time > _tracerOff) Tracer.enabled = false;

            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + ThinkInterval;
                think();
            }
            if (_target == null) return;

            if (IsBoss && charge()) return;
            face(_canSee ? _target.transform.position : _lastKnown);
            shoot();
        }

        /// <summary>Looks for the player and decides where to walk (a few times a second).</summary>
        private void think()
        {
            var player = VantageEvents.ActivePlayer();
            _target = player;
            if (player == null) { _canSee = false; return; }
            var eye = Eye != null ? Eye.transform.position : transform.position + Vector3.up * 1.6f;
            var aim = VantagePhysics.AimPoint(player);
            var to = aim - eye;
            _canSee = to.sqrMagnitude < EngageRange * EngageRange
                      && (!Physics.Linecast(eye, aim, out var hit, VantagePhysics.Sight, QueryTriggerInteraction.Ignore)
                          || hit.transform.IsChildOf(player.transform));
            if (_canSee) { _alerted = true; _lastKnown = player.transform.position; }
            if (_agent == null || !_agent.isOnNavMesh || _chargeLeft > 0) return;
            // Walks up to firing range of where the player was last seen; stands still while it has them in sight.
            _agent.isStopped = !_alerted || (_canSee && to.magnitude < EngageRange * 0.6f);
            if (!_agent.isStopped) _agent.SetDestination(_lastKnown);
        }

        private void face(Vector3 point)
        {
            var flat = point - transform.position; flat.y = 0;
            if (flat.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 120f * Time.deltaTime);
        }

        /// <summary>Telegraphed burst: the eye charges for WindUp seconds, then BurstShots rounds.</summary>
        private void shoot()
        {
            if (_shotsLeft > 0)
            {
                if (Time.time >= _nextShotInBurst) { fire(); _shotsLeft--; _nextShotInBurst = Time.time + BurstSpacing; }
                if (_shotsLeft == 0) setEye(0f);
                return;
            }
            if (!_canSee || Time.time < _nextBurst) { _windUpStart = -1f; setEye(0f); return; }
            if (_windUpStart < 0) _windUpStart = Time.time;
            var charge = (Time.time - _windUpStart) / Mathf.Max(0.01f, WindUp);
            setEye(charge);
            if (charge < 1f) return;
            _windUpStart = -1f;
            _shotsLeft = BurstShots;
            _nextShotInBurst = Time.time;
            _nextBurst = Time.time + FireInterval * Random.Range(0.85f, 1.15f);
        }

        private void fire()
        {
            var origin = Muzzle != null ? Muzzle.position : transform.position + Vector3.up * 1.4f;
            var point = VantagePhysics.AimPoint(_target);
            var distance = Vector3.Distance(origin, point);
            var chance = Accuracy * Mathf.Clamp01(1.3f - distance / Range);
            var target = Random.value < chance ? point : point + Random.onUnitSphere * Random.Range(0.6f, 1.4f);
            var direction = (target - origin).normalized;
            var end = origin + direction * Range;
            if (Physics.Raycast(origin, direction, out var rayHit, Range, VantagePhysics.Sight, QueryTriggerInteraction.Ignore))
            {
                end = rayHit.point;
                if (!rayHit.collider.transform.IsChildOf(transform))
                {
                    var info = new Hit(rayHit.point, -direction, Damage, gameObject, rayHit.collider.gameObject, HitType.Rifle, 0);
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
                _audio.pitch = Random.Range(0.7f, 0.8f);
                _audio.PlayOneShot(FireSound, 0.9f);
            }
        }

        /// <summary>Boss only: wind-up (eye full red, standing still), then a dash at the player. True while charging.</summary>
        private bool charge()
        {
            if (_chargeLeft > 0)
            {
                var step = ChargeSpeed * Time.deltaTime;
                _chargeLeft -= step;
                var next = transform.position + _chargeDir * step;
                if (_agent != null && _agent.isOnNavMesh) _agent.Move(_chargeDir * step);
                else transform.position = next;
                var player = _target;
                if (!_chargeHit && player != null && Vector3.Distance(player.transform.position, transform.position) < 1.8f)
                {
                    _chargeHit = true;
                    var health = CharacterHealth.Get(player.gameObject);
                    if (health != null) health.Deal(ChargeDamage);
                }
                if (_chargeLeft <= 0) { setEye(0f); if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false; }
                return true;
            }
            if (_chargeWindUp >= 0)
            {
                setEye(1f);
                face(_target.transform.position);
                if (Time.time - _chargeWindUp < ChargeWindUp) return true;
                _chargeWindUp = -1f;
                _chargeDir = _target.transform.position - transform.position; _chargeDir.y = 0; _chargeDir.Normalize();
                _chargeLeft = ChargeDistance;
                _chargeHit = false;
                _nextCharge = Time.time + ChargeInterval;
                return true;
            }
            if (_canSee && _shotsLeft == 0 && Time.time >= _nextCharge)
            {
                _chargeWindUp = Time.time;
                _windUpStart = -1f;
                if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
                VantageEvents.RaiseMessage("It's charging!");
                return true;
            }
            return false;
        }

        private void setEye(float charge)
        {
            charge = Mathf.Clamp01(charge);
            if (charge == _shownCharge) return;
            _shownCharge = charge;
            var glow = Color.Lerp(new Color(1f, 0.25f, 0.1f), IsBoss ? new Color(1f, 0.05f, 0.02f) : new Color(1f, 0.95f, 0.6f), charge);
            if (_eyeMaterial != null) _eyeMaterial.SetColor(EmissionColor, glow * (2f + charge * 6f));
            if (EyeLight != null) { EyeLight.color = glow; EyeLight.intensity = 1f + charge * 4f; }
        }

        /// <summary>Called by every weapon through SendMessage. Only rifle rounds get through the armour.</summary>
        public void OnHit(Hit hit)
        {
            if (IsDead) return;
            var attacker = hit.Attacker != null ? hit.Attacker.GetComponentInParent<BaseActor>() : null;
            // Stray rounds from the soldiers fighting beside it don't hurt it.
            if (attacker != null && attacker.Side == VantageTowerLevels.EnemySide) return;
            if (attacker != null)
            {
                _alerted = true;
                _lastKnown = attacker.transform.position;
            }
            if (RifleOnly && hit.Type != HitType.Rifle && hit.Type != HitType.RifleMelee)
            {
                if (Time.time >= _nextMessage)
                {
                    _nextMessage = Time.time + 2f;
                    VantageEvents.RaiseMessage("You need a better gun");
                }
                return;
            }
            _health.Deal(hit.Damage);
            if (HitEffect != null) Destroy(Instantiate(HitEffect, hit.Position, Quaternion.LookRotation(hit.Normal)), 3f);
            if (_health.Health <= 0) die();
        }

        private void die()
        {
            IsDead = true;
            setEye(0f);
            if (_eyeMaterial != null) _eyeMaterial.SetColor(EmissionColor, Color.black);
            if (EyeLight != null) EyeLight.enabled = false;
            if (Tracer != null) Tracer.enabled = false;
            if (_agent != null) _agent.enabled = false;
            if (DeathExplosion != null) Destroy(Instantiate(DeathExplosion, transform.position + Vector3.up, Quaternion.identity), 5f);
            if (DeathSound != null) AudioSource.PlayClipAtPoint(DeathSound, transform.position, 1f);
            _diedAt = Time.time;
            _deathFrom = transform.rotation;
            _deathPos = transform.position;
            VantageEvents.RaiseEnemyKilled(IsBoss ? "Boss" : "Heavy robot", transform.position);
        }

        /// <summary>Tips over and sinks a little over a second; the wreck stays.</summary>
        private void collapse()
        {
            var t = Mathf.Clamp01((Time.time - _diedAt) / 1f);
            if (t >= 1f) enabled = false;
            transform.SetPositionAndRotation(_deathPos + Vector3.down * (0.35f * t), _deathFrom * Quaternion.Euler(70f * t * t, 0, 0));
        }
    }
}
