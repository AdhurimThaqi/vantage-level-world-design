using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Reports a Cover Shooter character's death (e.g. a soldier) to VantageEvents for the playtest log.
    /// </summary>
    [RequireComponent(typeof(CharacterHealth))]
    public class VantageKillReporter : MonoBehaviour
    {
        public string EnemyName = "Soldier";
        /// <summary>Seconds from the death to the body hitting the ground (BODY_Fall).</summary>
        public const float BodyFallDelay = 0.5f;

        private CharacterHealth _health;
        private bool _reported;

        private void Awake()
        {
            _health = GetComponent<CharacterHealth>();
        }

        private void Update()
        {
            if (_reported || _health.Health > 0)
                return;

            _reported = true;
            VantageEvents.RaiseEnemyKilled(EnemyName, transform.position);
            VantageAudio.PlayDelayed(VantageSounds.BodyFall, transform.position, BodyFallDelay);
        }
    }
}
