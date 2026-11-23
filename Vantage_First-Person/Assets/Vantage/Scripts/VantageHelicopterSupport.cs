using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The helicopter in the boss fight (lecturer's feedback, 2026-11-16). When the boss spawns, the extraction
    /// helicopter flies in (during the boss reveal) and sweeps back and forth over the valley side of the pad, never
    /// over the tower. Every pass it fires its cannon at the boss and draws its attention: the boss turns to it and
    /// fires at it for a few seconds, a window for the player to flank it. Its cannon gets through the armour but can
    /// take the boss only down to <see cref="BossFloor"/> of its health, so the kill stays the player's. When the boss
    /// dies, VantageExtraction lands this same helicopter (<see cref="HandOver"/>).
    /// On the Extraction object, next to VantageExtraction (its prefab, pad and approach side).
    /// </summary>
    [RequireComponent(typeof(VantageExtraction))]
    public class VantageHelicopterSupport : MonoBehaviour
    {
        [Header("Flight")]
        [Tooltip("Seconds from the boss spawning to the first pass (about the boss reveal's length).")]
        public float FlyInTime = 8f;
        [Tooltip("Distance from the pad it sweeps at, on the side away from the tower.")]
        public float OrbitRadius = 30f;
        [Tooltip("Height above the pad.")]
        public float OrbitHeight = 18f;
        [Tooltip("Half the arc it sweeps, in degrees either side of the approach direction (it never crosses the tower).")]
        public float SweepHalfAngle = 65f;
        [Tooltip("Seconds for one sweep from one end of the arc to the other and back.")]
        public float SweepPeriod = 24f;

        [Header("Cannon")]
        [Tooltip("Seconds between its attack passes.")]
        public float PassInterval = 9f;
        [Tooltip("Seconds after the reveal before the first pass.")]
        public float FirstPassDelay = 2f;
        public int BurstShots = 5;
        public float ShotSpacing = 0.15f;
        [Tooltip("Damage per round on the boss (through the armour).")]
        public float DamagePerShot = 10f;
        [Tooltip("Its cannon never takes the boss below this fraction of its health: the kill is the player's.")]
        [Range(0, 1)] public float BossFloor = 0.3f;
        [Tooltip("Seconds the boss turns to the helicopter on each pass.")]
        public float DistractSeconds = 4f;
        [Tooltip("Rounds land within this radius of the boss's centre.")]
        public float Spread = 0.6f;

        public static VantageHelicopterSupport Instance { get; private set; }
        /// <summary>The helicopter, once it is in the fight (handed to the extraction when the boss dies).</summary>
        public Transform Helicopter => _heli;
        /// <summary>Attack passes flown and rounds fired (for the bot).</summary>
        public int Passes { get; private set; }
        public int Rounds { get; private set; }
        /// <summary>It has reached the pad and sweeps.</summary>
        public bool Arrived { get; private set; }

        private VantageExtraction _extraction;
        private VantageHeavyRobot _boss;
        private Transform _heli, _mainRotor, _tailRotor, _muzzle;
        private LineRenderer _tracer;
        private Renderer[] _bossRenderers;
        private Vector3 _from, _away, _centre;
        private float _spawnedAt = -1f, _sweepAt, _nextPass = float.MaxValue, _nextShot, _tracerOff;
        private int _shotsLeft;
        private bool _handedOver;

        private void Awake()
        {
            Instance = this;
            _extraction = GetComponent<VantageExtraction>();
        }

        private void OnEnable() => VantageEvents.BossSpawned += onBossSpawned;
        private void OnDisable() => VantageEvents.BossSpawned -= onBossSpawned;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void onBossSpawned()
        {
            var levels = VantageTowerLevels.Instance;
            _boss = levels != null ? levels.Boss : null;
            if (_heli != null || _boss == null || _extraction == null || _extraction.HelicopterPrefab == null || _extraction.Pad == null) return;
            _away = _extraction.ApproachFrom; _away.y = 0;
            _away = _away.sqrMagnitude > 0.01f ? _away.normalized : Vector3.right;
            _centre = _extraction.Pad.position;
            _from = _centre + _away * _extraction.StartDistance + Vector3.up * _extraction.StartHeight;
            _heli = Instantiate(_extraction.HelicopterPrefab, _from, Quaternion.LookRotation(-_away)).transform;
            _heli.name = "Helicopter (support)";
            foreach (var c in _heli.GetComponentsInChildren<Collider>()) c.enabled = false;   // the player's and the boss's rounds pass
            _mainRotor = find("Main Rotor");
            _tailRotor = find("Tail Rotor");
            _muzzle = new GameObject("Cannon").transform;
            _muzzle.SetParent(_heli, false);
            _muzzle.localPosition = new Vector3(0f, -0.6f, 3f);
            _tracer = buildTracer();
            _bossRenderers = _boss.GetComponentsInChildren<Renderer>();
            VantageAudio.PlayLoop(VantageSounds.HeliRotor, _heli);
            _spawnedAt = Time.time;
            Debug.Log("[Vantage] Helicopter support inbound.");
        }

        private Transform find(string name)
        {
            foreach (var t in _heli.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private LineRenderer buildTracer()
        {
            var line = _muzzle.gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = 0.08f;
            line.endWidth = 0.04f;
            line.useWorldSpace = true;
            // The boss's tracer material when there is one (same look as every other shot in the level).
            var bossTracer = _boss != null ? _boss.Tracer : null;
            line.sharedMaterial = bossTracer != null ? bossTracer.sharedMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(1f, 0.85f, 0.4f);
            line.endColor = new Color(1f, 0.6f, 0.2f, 0.4f);
            line.enabled = false;
            return line;
        }

        /// <summary>The boss is dead: the extraction takes this helicopter over (it stops fighting and flying here).</summary>
        public Transform HandOver()
        {
            if (_heli == null) return null;
            _handedOver = true;
            if (_tracer != null) _tracer.enabled = false;
            return _heli;
        }

        private void Update()
        {
            if (_heli == null || _handedOver) return;
            if (_tracer != null && _tracer.enabled && Time.time > _tracerOff) _tracer.enabled = false;
            spinRotors(1300f);
            fly();
            if (!Arrived || _boss == null || _boss.IsDead || VantageCinematic.Active) return;
            if (_nextPass == float.MaxValue) _nextPass = Time.time + FirstPassDelay;
            fight();
        }

        private void spinRotors(float speed)
        {
            var spin = speed * Time.deltaTime;
            if (_mainRotor != null) _mainRotor.Rotate(0, spin, 0, Space.Self);
            if (_tailRotor != null) _tailRotor.Rotate(spin, 0, 0, Space.Self);
        }

        /// <summary>Flies in along a curve, then sweeps the arc on the valley side, nose on the boss.</summary>
        private void fly()
        {
            var t = Mathf.Clamp01((Time.time - _spawnedAt) / Mathf.Max(0.1f, FlyInTime));
            if (!Arrived && t >= 1f) { Arrived = true; _sweepAt = 0f; }
            Vector3 position;
            if (!Arrived)
            {
                var eased = 1f - (1f - t) * (1f - t);
                var entry = sweepPoint(0f);
                var control = Vector3.Lerp(_from, entry, 0.5f) + Vector3.up * 10f;
                position = Vector3.Lerp(Vector3.Lerp(_from, control, eased), Vector3.Lerp(control, entry, eased), eased);
            }
            else
            {
                _sweepAt += Time.deltaTime;
                position = sweepPoint(_sweepAt);
            }
            var velocity = position - _heli.position;
            _heli.position = position;
            var lookAt = _boss != null && !_boss.IsDead ? _boss.transform.position : _centre;
            var flat = lookAt - position; flat.y = 0;
            if (flat.sqrMagnitude < 0.01f) return;
            // Nose on the boss, banked a little into the sweep.
            var side = Vector3.Dot(velocity / Mathf.Max(Time.deltaTime, 1e-4f), Vector3.Cross(Vector3.up, flat.normalized));
            var target = Quaternion.LookRotation(flat) * Quaternion.Euler(6f, 0f, -Mathf.Clamp(side * 2f, -15f, 15f));
            _heli.rotation = Quaternion.Slerp(_heli.rotation, target, Time.deltaTime * 2f);
        }

        private Vector3 sweepPoint(float time)
        {
            var angle = SweepHalfAngle * Mathf.Sin(time * 2f * Mathf.PI / Mathf.Max(1f, SweepPeriod));
            var dir = Quaternion.Euler(0f, angle, 0f) * _away;
            return _centre + dir * OrbitRadius + Vector3.up * OrbitHeight;
        }

        /// <summary>A pass: the boss turns to it, then a burst from the cannon.</summary>
        private void fight()
        {
            if (_shotsLeft > 0)
            {
                if (Time.time >= _nextShot) { shoot(); _shotsLeft--; _nextShot = Time.time + ShotSpacing; }
                return;
            }
            if (Time.time < _nextPass) return;
            _nextPass = Time.time + PassInterval;
            Passes++;
            _shotsLeft = Mathf.Max(1, BurstShots);
            _nextShot = Time.time + 0.4f;
            _boss.Distract(_heli, DistractSeconds);
            if (Passes == 1) VantageEvents.RaiseMessage("The helicopter draws its fire");
        }

        private void shoot()
        {
            Rounds++;
            var origin = _muzzle.position;
            var bounds = bossBounds();
            var point = bounds.center + Random.insideUnitSphere * Spread;
            _boss.SupportDamage(DamagePerShot, BossFloor, point);
            if (_tracer != null)
            {
                _tracer.enabled = true;
                _tracer.SetPosition(0, origin);
                _tracer.SetPosition(1, point);
                _tracerOff = Time.time + 0.05f;
            }
            // HELI_Gun; while it has no recording, the user's robot shot (ROBOT_Fire) pitched down like a heavy cannon.
            // HELI_Gun is the user's whole burst (2026-11-23, cut to about 0.9 s for the 5 rounds): once per pass, on its first round.
            if (VantageAudio.Has(VantageSounds.HeliGun)) { if (_shotsLeft >= Mathf.Max(1, BurstShots)) VantageAudio.Play(VantageSounds.HeliGun, origin); return; }
            var laser = VantageAudio.Clips(VantageSounds.RobotFire);
            var source = VantageAudio.Play(VantageSounds.HeliGun, origin, laser != null && laser.Length > 0 ? laser[0] : null);
            var pitch = VantageAudio.Tuning.HeliGunPitch;
            if (source != null) source.pitch = Random.Range(pitch.x, pitch.y);
        }

        private Bounds bossBounds()
        {
            var renderers = _bossRenderers;
            if (renderers == null || renderers.Length == 0) return new Bounds(_boss.transform.position + Vector3.up * 1.5f, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
