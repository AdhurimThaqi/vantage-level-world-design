using System.Collections.Generic;
using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The soldier's decision, twice a second:
    ///   player visible  → attack (the template's FighterBrain then fights, follows and searches on its own);
    ///   alerted or level hunting → run to the player along the NavMesh (alarm, patrol alert, the last enemies);
    ///   patrol route    → walk it back and forth (the L3 outside patrols);
    ///   otherwise       → keep guarding.
    /// It also turns down the template's urge to retreat, so soldiers hold their floor instead of running off.
    /// </summary>
    [RequireComponent(typeof(FighterBrain))]
    public class VantageEnemyAggression : MonoBehaviour
    {
        [Tooltip("The soldier engages the player inside this range when it has line of sight.")]
        public float EngageRange = 32f;
        [Tooltip("Cover this close is used. 0 (default): soldiers don't take cover. In the tower they picked cover they "
                 + "couldn't reach (a desk or corner in the way), stood in 'take cover' without firing, pressed into walls.")]
        public float CoverRange = 0f;
        [Tooltip("Players more than this far above or below are on another floor and can't be seen.")]
        public float FloorTolerance = 2.6f;
        [Tooltip("Seconds between decisions.")]
        public float ThinkInterval = 0.5f;

        [Tooltip("Set by the level brain: this soldier comes for the player (alarm, alert trigger).")]
        public bool Alerted;
        [Tooltip("Walked back and forth while nothing is happening (world points).")]
        public List<Vector3> PatrolPoints;

        private const float ReorderInterval = 3f;
        private const float PatrolReach = 1.2f;
        private int _patrolIndex, _patrolStep = 1;

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
            else if (VantageTowerLevels.Instance != null && VantageTowerLevels.Instance.PlayerOnCurrentLevel
                     && (Alerted || VantageTowerLevels.Instance.Hunting))
                hunt(player);
            else if (PatrolPoints != null && PatrolPoints.Count > 1)
                patrol();
        }

        /// <summary>Walks the route back and forth; the next point is ordered on arrival or every few seconds.</summary>
        private void patrol()
        {
            var to = PatrolPoints[_patrolIndex] - transform.position;
            to.y = 0;
            var arrived = to.sqrMagnitude < PatrolReach * PatrolReach;
            if (arrived)
            {
                if (_patrolIndex + _patrolStep < 0 || _patrolIndex + _patrolStep >= PatrolPoints.Count) _patrolStep = -_patrolStep;
                _patrolIndex += _patrolStep;
            }
            if (!arrived && Time.time - _lastOrder < ReorderInterval)
                return;
            _lastOrder = Time.time;
            _brain.ToWalkTo(PatrolPoints[_patrolIndex]);
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
