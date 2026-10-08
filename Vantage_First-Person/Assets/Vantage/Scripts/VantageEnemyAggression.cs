using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The soldier's decision, twice a second:
    ///   player visible  → attack (the template's FighterBrain then fights, follows and searches on its own);
    ///   level hunting   → run to the player along the NavMesh (the last enemies come to the player);
    ///   otherwise       → keep guarding.
    /// It also turns down the template's urge to retreat, so soldiers hold their floor instead of running off.
    /// </summary>
    [RequireComponent(typeof(FighterBrain))]
    public class VantageEnemyAggression : MonoBehaviour
    {
        [Tooltip("The soldier engages the player inside this range when it has line of sight.")]
        public float EngageRange = 32f;
        [Tooltip("Only cover this close is used, so soldiers hold their floor instead of running across the map.")]
        public float CoverRange = 9f;
        [Tooltip("Players more than this far above or below are on another floor and can't be seen.")]
        public float FloorTolerance = 2.6f;
        [Tooltip("Seconds between decisions.")]
        public float ThinkInterval = 0.5f;

        private const float ReorderInterval = 3f;

        private FighterBrain _brain;
        private BaseActor _actor;
        private float _nextThink;
        private float _lastOrder = -10f;

        private void Awake()
        {
            _brain = GetComponent<FighterBrain>();
            _actor = GetComponent<BaseActor>();

            // Guard the spawn room instead of wandering ("searchAround" is the template's default).
            var start = _brain.Start;
            start.Mode = AIStartMode.idle;
            _brain.Start = start;

            _brain.AvoidDistance = 1.2f;
            _brain.DistanceToGoToCoverFromStandOrCircle = 3f;
            _brain.ImmediateThreatReaction = true;
            _brain.AttackAggressors = true;
            var retreat = _brain.Retreat;
            retreat.Health = 0f;
            _brain.Retreat = retreat;

            var cover = GetComponent<AICover>();
            if (cover != null)
            {
                cover.MaxCoverDistance = CoverRange;
                cover.AvoidDistance = 1.5f;
            }
        }

        private void Update()
        {
            if (Time.time < _nextThink || _actor == null || !_actor.IsAlive)
                return;
            _nextThink = Time.time + ThinkInterval;

            var player = VantageEvents.ActivePlayer();
            if (player == null)
                return;

            if (canSee(player))
                attack(player);
            else if (VantageTowerLevels.Instance != null && VantageTowerLevels.Instance.Hunting)
                hunt(player);
        }

        private bool canSee(BaseActor player)
        {
            var to = player.transform.position - transform.position;
            if (Mathf.Abs(to.y) > FloorTolerance || to.sqrMagnitude > EngageRange * EngageRange)
                return false;
            var eye = VantagePhysics.Eye(_actor);
            var target = VantagePhysics.AimPoint(player);
            return !Physics.Linecast(eye, target, out var hit, VantagePhysics.Sight, QueryTriggerInteraction.Ignore)
                   || hit.transform.IsChildOf(player.transform);
        }

        /// <summary>Lock on and fire. Re-issued only when the brain lost the player or every few seconds.</summary>
        private void attack(BaseActor player)
        {
            var engaged = _brain.Threat == player && _brain.CanSeeTheThreat;
            if (engaged && Time.time - _lastOrder < ReorderInterval)
                return;
            _lastOrder = Time.time;
            _brain.ToAttack(player);
            _brain.ToOpenFire();
        }

        /// <summary>Path to the player's current position; refreshed every few seconds as the player moves.</summary>
        private void hunt(BaseActor player)
        {
            if (Time.time - _lastOrder < ReorderInterval)
                return;
            _lastOrder = Time.time;
            _brain.ToRunTo(player.transform.position);
        }
    }
}
