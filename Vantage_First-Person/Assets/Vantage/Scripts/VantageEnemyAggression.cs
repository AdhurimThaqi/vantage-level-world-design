using System.Collections.Generic;
using CoverShooter;
using UnityEngine;
using UnityEngine.AI;

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
        // Walking fixes (2026-11-23, the user: soldiers slid, walked into walls, stopped and started). A soldier ordered
        // somewhere that stands still this long is ordered again at once (not after ReorderInterval); one that moves
        // but gets less than StuckDistance further in StuckTime is stuck (a wall, a desk, a friend) and goes elsewhere.
        private const float RestartAfter = 0.6f, StuckTime = 2f, StuckDistance = 0.4f, NavSearch = 2.5f;
        private float _stillSince = -1f, _progressAt;
        private Vector3 _progressFrom;
        private bool _ordered;
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
        private bool _loggedHighAim;
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
            // A soldier waiting for its turn holds its aim (the pose firing soldiers have), standing still and never
            // through a reload (2026-11-25: aiming through the reload it was told to do bent the body back, and the
            // pistol idle pose without aiming tilted the Low Poly body up: L2's waiting pistol soldiers stared at the
            // ceiling either way).
            if (Waiting && _motor != null && !_motor.IsMoving && !_motor.IsReloading) _motor.InputAim();
            if (Time.time < _nextThink)
                return;
            _nextThink = Time.time + ThinkInterval;

            var player = VantageEvents.ActivePlayer();
            if (player == null)
                return;

            // Diagnostic (2026-11-25): a soldier aiming far above its head while the player isn't up there, once each.
            if (!_loggedHighAim && _motor != null)
            {
                var eye = VantagePhysics.Eye(_actor);
                if (_motor.AimTarget.y > eye.y + 2.5f && VantagePhysics.AimPoint(player).y < eye.y + 1f)
                {
                    _loggedHighAim = true;
                    Debug.Log($"[Vantage] {name} aims high: target {_motor.AimTarget:F1}, eye {eye:F1}, threat {(_brain.Threat != null ? _brain.Threat.name : "none")} at {_brain.LastKnownThreatPosition:F1}, waiting {Waiting}, aiming {_motor.IsAiming}, moving {_motor.IsMoving}.");
                }
            }

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
            if (!arrived && stuck())
            {
                // Can't get there (a wall, a desk, a friend in the way): on to the next point instead of pushing.
                if (_patrolIndex + _patrolStep < 0 || _patrolIndex + _patrolStep >= PatrolPoints.Count) _patrolStep = -_patrolStep;
                _patrolIndex += _patrolStep;
                arrived = true;
            }
            if (!arrived && !standingStill() && Time.time - _lastOrder < ReorderInterval)
                return;
            // Broadcast like the template's brains do: AIMovement walks; FighterBrain.ToWalkTo alone only records it.
            order("ToWalkTo", onNavMesh(PatrolPoints[_patrolIndex]));
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
        /// switched off (it fires by itself whenever the brain says "open fire"), the soldier holds its aim on the player.
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
            _ordered = false;   // the template's fight states move it now
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
            var target = onNavMesh(player.transform.position);
            var isStuck = stuck();
            if (isStuck)
            {
                // Blocked: try a reachable point a few metres around the player instead of the same wall.
                var side = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * 3f;
                target = onNavMesh(player.transform.position + side);
            }
            if (!isStuck && !standingStill() && Time.time - _lastOrder < ReorderInterval)
                return;
            var state = _brain.State;
            if (state != FighterState.idleButAlerted && state != FighterState.process)
                _brain.SetIdleAlertedState();
            order("ToRunTo", target);
        }

        /// <summary>Orders a walk or run (broadcast: AIMovement acts on it) and starts watching its progress.</summary>
        private void order(string move, Vector3 to)
        {
            _lastOrder = Time.time;
            _brain.Message(move, to);
            _brain.Message("ToFaceWalkDirection");
            _ordered = true;
            _stillSince = -1f;
            _progressAt = Time.time;
            _progressFrom = transform.position;
        }

        /// <summary>
        /// The point on the NavMesh nearest to 'p' at this soldier's floor (the player on a crate, a stair or a desk
        /// isn't on it: a path there ended short, against a wall, and AIMovement gave up a target 1 m above or below).
        /// </summary>
        private Vector3 onNavMesh(Vector3 p)
        {
            var at = new Vector3(p.x, Mathf.Lerp(p.y, transform.position.y, 0.5f), p.z);
            if (NavMesh.SamplePosition(at, out var hit, NavSearch, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - transform.position.y) < FloorTolerance)
                return hit.position;
            return NavMesh.SamplePosition(p, out hit, NavSearch, NavMesh.AllAreas) ? hit.position : p;
        }

        /// <summary>Ordered to move but has stood still for RestartAfter: AIMovement dropped the order, give a new one.</summary>
        private bool standingStill()
        {
            if (!_ordered || _motor == null) return false;
            if (_motor.IsMoving) { _stillSince = -1f; return false; }
            if (_stillSince < 0f) _stillSince = Time.time;
            return Time.time - _stillSince >= RestartAfter;
        }

        /// <summary>Moving, but less than StuckDistance further in StuckTime: walking into something.</summary>
        private bool stuck()
        {
            if (!_ordered || _motor == null || !_motor.IsMoving) return false;
            if (Time.time - _progressAt < StuckTime) return false;
            var moved = transform.position - _progressFrom; moved.y = 0;
            _progressAt = Time.time;
            _progressFrom = transform.position;
            return moved.sqrMagnitude < StuckDistance * StuckDistance;
        }
    }
}
