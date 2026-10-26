using CoverShooter;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage
{
    /// <summary>
    /// The intro's deserter: a soldier who walks out of the base, ignores a warning shot and is shot by the slab
    /// sentry, so the warning ring is shown before it is ever explained. Made from the level soldier with its AI,
    /// navigation and kill report off; VantageIntro tells it when to walk, run and die. The body stays where it fell
    /// (it is not a level enemy: no KILL list, count or HUD pip knows it).
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class VantageDeserter : MonoBehaviour
    {
        private CharacterMotor _motor;
        private CharacterHealth _health;
        private BaseActor _actor;
        private Vector3 _heading;
        private float _speed;

        public bool IsDead => _motor == null || !_motor.IsAlive;
        /// <summary>Where a sentry aims (upper chest).</summary>
        public Vector3 AimPoint => _actor != null ? VantagePhysics.AimPoint(_actor) : transform.position;

        public static VantageDeserter Spawn(GameObject soldier, Vector3 at, Vector3 heading)
        {
            var go = Instantiate(soldier, at, Quaternion.LookRotation(heading));
            go.name = "Deserter";
            foreach (var b in go.GetComponentsInChildren<Behaviour>(true))
                if (b is AIBase || b is AIListener || b is VantageEnemyAggression || b is VantageKillReporter || b is NavMeshAgent)
                    b.enabled = false;
            var deserter = go.AddComponent<VantageDeserter>();
            deserter._heading = heading;
            return deserter;
        }

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _health = GetComponent<CharacterHealth>();
            _actor = GetComponent<BaseActor>();
            // Unarmed: he is leaving, not fighting.
            _motor.IsEquipped = false;
        }

        /// <summary>0 stands, 0.5 walks, 1 runs (CharacterMovement magnitude).</summary>
        public void Move(float speed) => _speed = speed;

        private void Update()
        {
            if (IsDead || _speed <= 0f) return;
            var ahead = transform.position + _heading * 10f;
            _motor.SetBodyTarget(ahead);
            _motor.SetAimTarget(ahead + Vector3.up * (AimPoint.y - transform.position.y));
            _motor.InputMovement(new CharacterMovement(_heading, _speed));
        }

        /// <summary>Shot dead by `shooter` (a rifle hit: the template's hit reaction and death).</summary>
        public void Kill(GameObject shooter)
        {
            if (IsDead) return;
            _speed = 0f;
            var at = AimPoint;
            var from = shooter != null ? shooter.transform.position : at + _heading;
            var amount = _health != null ? _health.Health + 100f : 1000f;
            SendMessage("OnHit", new Hit(at, (from - at).normalized, amount, shooter, gameObject, HitType.Rifle, 0), SendMessageOptions.DontRequireReceiver);
            if (_motor.IsAlive) _motor.Die();
        }
    }
}
