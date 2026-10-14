using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The Room A security camera: it sweeps the room with a visible light cone. If it sees the player for a moment
    /// it sets off the alarm early (the same trap as the trigger). Shooting it switches it off, so the player can
    /// choose to deal with it before walking into its view. It stops watching once the alarm has gone off.
    /// </summary>
    public class VantageSecurityCamera : MonoBehaviour
    {
        [Tooltip("The part that turns (head with the lens and the cone light).")]
        public Transform Head;
        public Light Cone;
        public Renderer Lens;
        public float SweepAngle = 50f;
        public float SweepSpeed = 20f;
        public float Range = 12f;
        [Tooltip("Full angle of the view cone.")]
        public float ViewAngle = 60f;
        [Tooltip("Seconds the player must stay in view before the alarm goes off.")]
        public float SpotTime = 0.6f;
        public float CheckInterval = 0.1f;
        public Color Watching = new Color(1f, 0.95f, 0.8f);
        public Color Alarmed = new Color(1f, 0.1f, 0.05f);

        public bool Disabled { get; private set; }

        private Quaternion _rest;
        private float _sweep, _direction = 1f, _seen, _nextCheck;
        private bool _alarmed;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            if (Head == null) Head = transform;
            _rest = Head.localRotation;
            tint(Watching);
        }

        private void OnEnable() => VantageEvents.AlarmRaised += onAlarm;
        private void OnDisable() => VantageEvents.AlarmRaised -= onAlarm;

        private void onAlarm(string source)
        {
            _alarmed = true;
            if (!Disabled) tint(Alarmed);
        }

        private void Update()
        {
            if (Disabled) return;
            var player = VantageEvents.ActivePlayer();
            if (_alarmed)
            {
                // Follows the player once the alarm is on (no more checks).
                if (player != null) aimAt(VantagePhysics.AimPoint(player), 90f);
                return;
            }

            _sweep += _direction * SweepSpeed * Time.deltaTime;
            if (Mathf.Abs(_sweep) > SweepAngle) { _sweep = Mathf.Clamp(_sweep, -SweepAngle, SweepAngle); _direction = -_direction; }
            Head.localRotation = _rest * Quaternion.Euler(0, _sweep, 0);

            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;
            if (player != null && sees(player)) _seen += CheckInterval;
            else _seen = 0;
            if (_seen >= SpotTime)
            {
                _seen = 0;
                VantageEvents.RaiseAlarm("camera");
            }
        }

        private bool sees(BaseActor player)
        {
            var eye = Head.position;
            var target = VantagePhysics.AimPoint(player);
            var to = target - eye;
            if (to.sqrMagnitude > Range * Range || Vector3.Angle(Head.forward, to) > ViewAngle * 0.5f) return false;
            return !Physics.Linecast(eye + to.normalized * 0.3f, target, out var hit, VantagePhysics.Sight, QueryTriggerInteraction.Ignore)
                   || hit.transform.IsChildOf(player.transform);
        }

        private void aimAt(Vector3 point, float speed)
        {
            var want = Quaternion.LookRotation(point - Head.position);
            Head.rotation = Quaternion.RotateTowards(Head.rotation, want, speed * Time.deltaTime);
        }

        /// <summary>Bullets: the camera is knocked out (light off, lens dark).</summary>
        public void OnHit(Hit hit)
        {
            if (Disabled) return;
            Disabled = true;
            if (Cone != null) Cone.enabled = false;
            tint(Color.black);
            Head.localRotation = _rest * Quaternion.Euler(25f, _sweep, 0);
            VantageEvents.RaiseMessage("Camera down");
        }

        private void tint(Color c)
        {
            if (Cone != null) Cone.color = c;
            if (Lens == null) return;
            _block ??= new MaterialPropertyBlock();
            Lens.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", c * 0.3f);
            _block.SetColor("_EmissionColor", c * 2f);
            Lens.SetPropertyBlock(_block);
        }
    }
}
