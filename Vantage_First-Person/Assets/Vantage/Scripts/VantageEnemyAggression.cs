using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Makes template soldiers stand and fight instead of running off. Out of the box the fighter brain backs away
    /// whenever the player is within 4 m (avoidAndFight), retreats to cover at 25 health, and runs to any cover
    /// up to 30 m away, which in an open base reads as running away. This component:
    /// - turns those down (avoid 1.2 m, no retreat, cover only within CoverRange),
    /// - and every half second, if the soldier can see the player on its own level and is not already fighting
    ///   them, locks on and attacks (FighterBrain.ToAttack), so being shot at or seen always gets a response.
    /// </summary>
    [RequireComponent(typeof(FighterBrain))]
    public class VantageEnemyAggression : MonoBehaviour
    {
        [Tooltip("The soldier engages the player inside this range when it has line of sight.")]
        public float EngageRange = 32f;
        [Tooltip("Only cover this close is used, so soldiers hold their floor instead of running across the map.")]
        public float CoverRange = 9f;
        [Tooltip("Players more than this far above or below are on another floor and ignored.")]
        public float FloorTolerance = 2.6f;

        private FighterBrain _brain;
        private BaseActor _actor;
        private float _next;
        private float _lastOrder = -10f;

        private const int SightMask = ~((1 << 2) | (1 << 8) | (1 << 11));

        private void Awake()
        {
            _brain = GetComponent<FighterBrain>();
            _actor = GetComponent<BaseActor>();

            // The template soldier starts in "searchAround" and wanders off; guard the spawn room instead.
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
            if (Time.time < _next || _actor == null || !_actor.IsAlive)
                return;
            _next = Time.time + 0.5f;

            var player = VantageEvents.ActivePlayer();
            if (player == null)
                return;

            var to = player.transform.position - transform.position;
            if (Mathf.Abs(to.y) > FloorTolerance || to.magnitude > EngageRange)
                return;

            var eye = transform.position + Vector3.up * 1.6f;
            var target = player.transform.position + Vector3.up * 1.3f;
            if (Physics.Linecast(eye, target, out var hit, SightMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(player.transform))
                return;

            // Re-issue at most every 3 s, or straight away if the brain lost or never had the player.
            var engaged = _brain.Threat == player && _brain.CanSeeTheThreat;
            if (engaged && Time.time - _lastOrder < 3f)
                return;
            _lastOrder = Time.time;
            _brain.ToAttack(player);
            _brain.ToOpenFire();
        }
    }
}
