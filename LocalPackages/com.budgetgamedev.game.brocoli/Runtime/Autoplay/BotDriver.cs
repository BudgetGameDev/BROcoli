using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Autonomous playtest agent. It observes combat, loot, and dungeon state, scores
    /// its competing goals, plans paths on the runtime NavMesh, and feeds ordinary
    /// player input. Normal play is unaffected unless autoplay creates and enables
    /// this component.
    /// </summary>
    public partial class BotDriver : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static Vector2 Move { get; private set; }
        public static string IntentName => currentIntent.ToString().ToLowerInvariant();
        public static int NearbyEnemyCount { get; private set; }
        public static int ReplanCount { get; private set; }
        public static int StuckRecoveryCount { get; private set; }
        public static float DistanceTravelled { get; private set; }

        [Header("Combat reasoning")]
        [SerializeField]
        private float senseRadius = 14f;

        [SerializeField]
        private float engageRadius = 4.2f;

        [SerializeField]
        private float dangerRadius = 2.5f;

        [SerializeField]
        private int crowdCount = 5;

        [SerializeField]
        private float lowHpFraction = 0.4f;

        [SerializeField]
        private float strafeWeight = 0.55f;

        [Tooltip("Game-seconds the agent circles one way before reversing at weapon range.")]
        [SerializeField]
        private float strafeHoldSeconds = 2.5f;

        [Tooltip("How far out enemies count toward being boxed in.")]
        [SerializeField]
        private float encirclementRadius = 7f;

        /// <summary>
        /// Share of the surrounding space that has to have something in it before the
        /// agent stops backing off and starts breaking out. Half is deliberately early:
        /// the point of the manoeuvre is to leave while there is still a way out, and
        /// waiting for a closed ring is waiting until there is not one.
        /// </summary>
        private const float EncirclementBreakout = 0.5f;

        [Header("Projectile dodging")]
        [SerializeField]
        private float projectileSenseRadius = 8f;

        [SerializeField]
        private float dodgeRadius = 1.4f;

        [Header("Loot and pickups")]
        [SerializeField]
        private float objectiveRadius = 16f;

        [SerializeField]
        private float objectiveScanInterval = 0.4f;

        [Header("Navigation")]
        [SerializeField]
        private float navigationLookAhead = 3f;

        [SerializeField]
        private float pathRefreshInterval = 0.15f;

        [SerializeField]
        private float progressCheckInterval = 0.5f;

        [SerializeField]
        private float stuckRecoveryDelay = 1f;

        [Tooltip("Game-seconds of travelling judged together for whether it went anywhere.")]
        [SerializeField]
        private float loiterWindow = 4f;

        [Tooltip("Share of a window's walking that has to end up as net displacement.")]
        [SerializeField]
        private float loiterEfficiency = 0.3f;

        [Header("Giving up")]
        [Tooltip("Game-seconds of fruitless fighting before the current threat is written off.")]
        [SerializeField]
        private float engagementStallDelay = 12f;

        [Tooltip("Game-seconds of reaching no new room before the destination is written off.")]
        [SerializeField]
        private float explorationStallDelay = 30f;

        [Tooltip("Unsticking manoeuvres in one room before the destination is written off.")]
        [SerializeField]
        private int recoveriesBeforeAbandoning = 4;

        [Tooltip("Game-seconds spent heading for the room centre after getting wedged.")]
        [SerializeField]
        private float unwedgeSeconds = 5f;

        [Tooltip("Game-seconds to ignore loot after giving up on reaching it.")]
        [SerializeField]
        private float abandonedObjectiveDelay = 6f;

        /// <summary>How near the middle of a room counts as having got there.</summary>
        private const float UnwedgeArrival = 1.5f;

        /// <summary>Ticks with nowhere walkable in the fan before heading for open floor.</summary>
        private const int BlockedTicksBeforeUnwedging = 6;

        /// <summary>What open ground is worth when there is something to be cornered by.</summary>
        private const float OpenGroundWeight = 1.35f;

        /// <summary>How near the middle of a fresh room counts as having taken it.</summary>
        private const float StagingArrival = 3.5f;

        /// <summary>Game-seconds spent trying to reach a fresh room's middle before giving up.</summary>
        private const float StagingSeconds = 6f;

        /// <summary>
        /// Ground a window has to cover before its efficiency means anything. Under
        /// this the agent has barely moved, which is the stationary check's
        /// question rather than this one's.
        /// </summary>
        private const float MinimumJudgeableTravel = 2f;

        /// <summary>
        /// How much continuing the way it was already going is worth when the agent
        /// picks its way around an obstacle. Two roughly equal ways past a wall used
        /// to be settled by a tie-break that flipped every time the agent unstuck
        /// itself, so it chose left, then right, then left, and paced the wall
        /// instead of getting round it. Small enough to stay a preference: a
        /// genuinely clearer way still wins on the same tick it opens up.
        ///
        /// Raised once already, because the same flip-flop is what a prop does to the
        /// agent at close quarters: it clips one, two ways round score alike, and it
        /// picks at the corner for a second or two before carrying on. Committing
        /// harder is what turns that into going round.
        /// </summary>
        private const float HeadingCommitment = 1.15f;

        private static BotIntent currentIntent;

        private readonly System.Collections.Generic.List<Vector2> encirclementBuffer = new();

        /// <summary>Rooms whose open middle the agent has already walked out to.</summary>
        private readonly System.Collections.Generic.HashSet<Vector2Int> stagedRooms = new();

        private Vector2Int stagingRoom;
        private float stagingDeadline;

        /// <summary>The way out of the crowd, as of the last look at it.</summary>
        private Vector2 lastEscape;

        private readonly Collider[] projectileBuffer = new Collider[64];

        // A cleared room can hold an orb per enemy that died in it alongside every
        // prop collider in sweep range, and an overflowing buffer drops whichever
        // colliders the query happened to reach last -- which is how a run walks past
        // the experience it is standing next to.
        private readonly Collider[] objectiveBuffer = new Collider[256];
        private readonly RaycastHit[] obstacleHits = new RaycastHit[24];
        private readonly Vector3[] pathCorners = new Vector3[32];
        private readonly HashSet<Vector2Int> visitedRooms = new();

        private Transform player;
        private PlayerStats stats;
        private DungeonManager dungeon;
        private NavMeshPath path;
        private ObjectiveObservation objectives;
        private Vector2 lastDodge;
        private Vector2 lastPosition;
        private Vector2 lastProgressPosition;
        private Vector2 recoveryDirection;
        private Vector2 cachedPathDirection;
        private Vector2 cachedPathTarget;
        private Vector2 committedHeading;
        private Vector2 loiterOrigin;
        private Vector2Int explorationRoom;
        private Vector2Int occupiedRoom;
        private float nextPathRefresh;
        private float nextProgressCheck;
        private float nextObjectiveScan;
        private float stationaryTime;
        private float recoveryUntil;
        private float lastProgress;
        private float unwedgeUntil;
        private float loiterTravelled;
        private float nextLoiterCheck;
        private int recoveriesSinceProgress;
        private int lastKills;
        private int frame;
        private int explorationDirection = -1;
        private int blockedTicks;

        private int recoverySide;

        /// <summary>
        /// Which way the agent circles while holding its weapon's range, reversed on
        /// a timer. Circling is how a ranged fight is meant to look, but always
        /// circling the same way is an orbit rather than a fight: with a crowd
        /// following it round, the agent walks a closed ring at engage range,
        /// covering two hundred metres a minute and ending each lap where it began.
        /// Reversing keeps the sidestep and loses the ring.
        /// </summary>
        private int StrafeSide =>
            Mathf.FloorToInt(Time.time / Mathf.Max(0.1f, strafeHoldSeconds)) % 2 == 0 ? 1 : -1;
        private bool hasPosition;
        private bool hasExplorationRoom;
        private bool hasOccupiedRoom;

        private BotTuning Tuning =>
            new(dangerRadius, crowdCount, lowHpFraction, senseRadius, objectiveRadius);

        private void Awake()
        {
            path = new NavMeshPath();
            recoverySide = 1;
        }

        private void OnEnable()
        {
            Active = true;
            Move = Vector2.zero;
            currentIntent = BotIntent.Waiting;
            NearbyEnemyCount = 0;
            ReplanCount = 0;
            StuckRecoveryCount = 0;
            DistanceTravelled = 0f;
            visitedRooms.Clear();
            objectives = ObjectiveObservation.None;
            hasExplorationRoom = false;
            hasOccupiedRoom = false;
            explorationDirection = -1;
            stationaryTime = 0f;
            recoveryUntil = 0f;
            nextObjectiveScan = 0f;
            lastProgress = 0f;
            unwedgeUntil = 0f;
            committedHeading = Vector2.zero;
            loiterOrigin = Vector2.zero;
            loiterTravelled = 0f;
            nextLoiterCheck = 0f;
            recoveriesSinceProgress = 0;
            blockedTicks = 0;
            stagedRooms.Clear();
            stagingDeadline = 0f;
            lastEscape = Vector2.zero;
            lastKills = 0;
            hasPosition = false;
        }

        private void OnDisable()
        {
            Active = false;
            Move = Vector2.zero;
            currentIntent = BotIntent.Waiting;
        }

        private void FixedUpdate()
        {
            if (!ResolveWorld())
            {
                currentIntent = BotIntent.Waiting;
                Move = Vector2.zero;
                return;
            }

            Vector2 position = player.position.ToGround();
            TrackProgress(position);
            TrackRoom(position);
            EnemyObservation enemies = ObserveEnemies(position);
            NearbyEnemyCount = enemies.Count;
            TrackCombatProgress();

            if (++frame % 3 == 0)
                lastDodge = ComputeProjectileDodge(position);
            if (Time.time >= nextObjectiveScan)
            {
                nextObjectiveScan = Time.time + objectiveScanInterval;
                objectives = ObserveObjectives(position);
            }

            currentIntent = BotDecisionPolicy.ChooseIntent(
                Observe(position, enemies),
                Tuning,
                currentIntent
            );
            if (currentIntent == BotIntent.Dodge)
                AutoplayFeatureLog.Record(AutoplayFeatures.ProjectileDodged);

            Move = Vector2.ClampMagnitude(NavigateIntent(currentIntent, position, enemies), 1f);
        }

        private BotSituation Observe(Vector2 position, EnemyObservation enemies)
        {
            float hpFraction =
                stats != null && stats.CurrentMaxHealth > 0f
                    ? stats.CurrentHealth / stats.CurrentMaxHealth
                    : 1f;
            return new BotSituation(
                enemies.Count > 0,
                enemies.NearestDistance,
                enemies.CloseCount,
                hpFraction,
                lastDodge.sqrMagnitude > 0.0001f,
                Time.time < recoveryUntil,
                objectives.ChestDistance(position),
                objectives.PickupDistance(position),
                Time.time - lastProgress > engagementStallDelay,
                enemies.Coverage
            );
        }

        private Vector2 NavigateIntent(
            BotIntent intent,
            Vector2 position,
            EnemyObservation enemies
        ) =>
            intent switch
            {
                BotIntent.Explore => NavigateTo(position, GetExplorationTarget(position)),
                BotIntent.Engage => NavigateCombat(position, enemies, false),
                BotIntent.Retreat => NavigateCombat(position, enemies, true),
                BotIntent.Loot => NavigateTo(position, objectives.Chest),
                BotIntent.Collect => NavigateTo(position, objectives.Pickup),
                BotIntent.Dodge => NavigateLocal(position, lastDodge * 3f + enemies.Repulsion),
                BotIntent.Recover => NavigateLocal(position, recoveryDirection),
                _ => Vector2.zero,
            };

        private bool ResolveWorld()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject == null)
                    return false;
                player = playerObject.transform;
                stats = playerObject.GetComponent<PlayerStats>();
                lastPosition = player.position.ToGround();
                lastProgressPosition = lastPosition;
                loiterOrigin = lastPosition;
                nextProgressCheck = Time.time + progressCheckInterval;
                nextLoiterCheck = Time.time + loiterWindow;
                hasPosition = true;
            }

            if (dungeon == null)
                dungeon = FindAnyObjectByType<DungeonManager>();
            return true;
        }
    }
}
