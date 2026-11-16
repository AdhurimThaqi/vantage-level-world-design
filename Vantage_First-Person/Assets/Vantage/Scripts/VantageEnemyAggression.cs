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
    /// Attack turns (lecturer's feedback, 2026-11-16): at most <see cref="VantageTowerLevels.AttackSlots"/> soldiers fire
    /// at once. The others keep aiming and reload but hold fire until a turn comes free; a turn is handed on after
    /// <see cref="VantageTowerLevels.AttackTurn"/> seconds when someone waits, or as soon as its holder loses sight.
    /// Until it has fought, a soldier doesn't notice a player outside the tower or anyone during a cutscene (its own
    /// and the template's senses off): the tower is quiet on the approach, and nobody walks out of the open entrance
    /// to shoot a player who has just been handed control after the intro. A hit wakes it at once.
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
        private AISight _sight;
        private AIListener _listener;
        private bool _engaged, _sensesOn = true;
        private float _nextThink;
        private float _lastOrder = -10f;

        // Attack turns, shared by every level soldier in the scene.
        private static readonly List<VantageEnemyAggression> Turns = new List<VantageEnemyAggression>();
        private static int _waitingCount;
        /// <summary>Soldiers holding an attack turn now (for the bot).</summary>
        public static int AttackersNow => Turns.Count;
        /// <summary>Soldiers that see the player but wait for a turn (for the bot).</summary>
        public static int WaitingNow => _waitingCount;
        /// <summary>This soldier may fire (holds a turn, or turns are off).</summary>
        public bool HasTurn => Turns.Contains(this);
        /// <summary>Sees the player but holds fire until a turn comes free.</summary>
        public bool Waiting { get; private set; }
        private const float LostSightRelease = 1.5f;
        private float _turnSince, _turnAgainAt, _lastSawPlayer = -10f;
        private AIFire _fire;
        private CharacterMotor _motor;

        private void Awake()
        {
            _brain = GetComponent<FighterBrain>();
            _actor = GetComponent<BaseActor>();
            _sight = GetComponent<AISight>();
            _listener = GetComponent<AIListener>();
            _fire = GetComponent<AIFire>();
            _motor = GetComponent<CharacterMotor>();
            setSenses(false);   // until the first decision: the template's sight would lock on in the first frame

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

        private void OnDisable()
        {
            releaseTurn();
            setWaiting(false);
        }

        private void Update()
        {
            if (_actor == null) return;
            if (!_actor.IsAlive) { releaseTurn(); return; }
            if (Waiting && _motor != null) _motor.InputAim();   // keeps the gun up on the player while holding fire
            if (Time.time < _nextThink)
                return;
            _nextThink = Time.time + ThinkInterval;

            var player = VantageEvents.ActivePlayer();
            if (player == null)
                return;

            var levels = VantageTowerLevels.Instance;
            if (!_engaged && _brain.Threat != null) wake();   // the template found a fight on its own (a friend was hit)
            var quiet = !_engaged && (VantageCinematic.Active || (levels != null && !levels.PlayerOnCurrentLevel && !Alerted));
            setSenses(!quiet);
            if (quiet)
            {
                releaseTurn();
                setWaiting(false);
                return;
            }

            var sees = canSee(player);
            if (sees) _lastSawPlayer = Time.time;
            else if (Time.time - _lastSawPlayer > LostSightRelease) { releaseTurn(); setWaiting(false); }
            if (sees)
                attack(player);
            else if (levels != null && levels.PlayerOnCurrentLevel && (Alerted || levels.Hunting))
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
            // Broadcast like the template's brains do: AIMovement walks; FighterBrain.ToWalkTo alone only records it.
            _brain.Message("ToWalkTo", PatrolPoints[_patrolIndex]);
            _brain.Message("ToFaceWalkDirection");
        }

        /// <summary>Shot: awake for good (the template's brain turns to the attacker itself).</summary>
        private void OnHit(Hit hit)
        {
            if (!enabled) return;   // SendMessage reaches disabled scripts too (the intro's deserter)
            if (!_engaged) wake();
            setSenses(true);
        }

        private static float _nextBark;

        /// <summary>Awake for good, with a bark ("Contact!") unless another soldier just barked.</summary>
        private void wake()
        {
            _engaged = true;
            if (Time.time < _nextBark) return;
            _nextBark = Time.time + VantageAudio.Tuning.BarkGap;
            VantageAudio.Play(VantageSounds.VoContact, VantagePhysics.Eye(_actor));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void clearOnPlay()
        {
            _nextBark = 0f;
            Turns.Clear();
            _waitingCount = 0;
        }

        /// <summary>
        /// Takes or keeps an attack turn and lets the gun fire only with one. Without a turn the template's AIFire is
        /// switched off (it fires by itself whenever the brain says "open fire"), the soldier reloads and keeps aiming.
        /// </summary>
        private void updateTurn()
        {
            var levels = VantageTowerLevels.Instance;
            var slots = levels != null ? levels.AttackSlots : 0;
            var turn = levels != null ? levels.AttackTurn : 5f;
            if (slots <= 0) { setWaiting(false); return; }
            Turns.RemoveAll(t => t == null || t._actor == null || !t._actor.IsAlive);
            if (HasTurn && _waitingCount > 0 && Time.time - _turnSince > turn)
            {
                releaseTurn();
                _turnAgainAt = Time.time + turn * 0.5f;   // let the waiting ones go first
            }
            if (!HasTurn && Turns.Count < slots && Time.time >= _turnAgainAt)
            {
                Turns.Add(this);
                _turnSince = Time.time;
            }
            setWaiting(!HasTurn);
        }

        private void releaseTurn()
        {
            Turns.Remove(this);
            if (!_actor || !_actor.IsAlive) setWaiting(false);
        }

        private void setWaiting(bool waiting)
        {
            if (_fire != null && _fire.enabled == waiting)
            {
                _fire.enabled = !waiting;
                if (waiting && _motor != null && _motor.IsAlive) _motor.InputReload();   // use the pause: reload
            }
            if (waiting == Waiting) return;
            Waiting = waiting;
            _waitingCount += waiting ? 1 : -1;
        }

        /// <summary>The template's own sight and hearing, which lock on without asking this component.</summary>
        private void setSenses(bool on)
        {
            if (on == _sensesOn) return;
            _sensesOn = on;
            if (_sight != null) _sight.enabled = on;
            if (_listener != null) _listener.enabled = on;
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
            updateTurn();
            var engaged = _brain.Threat == player && _brain.CanSeeTheThreat;
            if (engaged && Time.time - _lastOrder < ReorderInterval)
                return;
            _lastOrder = Time.time;
            if (!_engaged) wake();
            _brain.ToAttack(player);
            _brain.ToOpenFire();
        }

        /// <summary>
        /// Path to the player's current position; refreshed every few seconds as the player moves. The brain is set
        /// to alerted first: its own fight states (circling, cover) would keep it near the last place it saw the player.
        /// </summary>
        private void hunt(BaseActor player)
        {
            if (Time.time - _lastOrder < ReorderInterval)
                return;
            _lastOrder = Time.time;
            var state = _brain.State;
            if (state != FighterState.idleButAlerted && state != FighterState.process)
                _brain.SetIdleAlertedState();
            _brain.Message("ToRunTo", player.transform.position);
            _brain.Message("ToFaceWalkDirection");
        }
    }
}
