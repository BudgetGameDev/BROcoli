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

        private static BotIntent currentIntent;

        private readonly Collider[] projectileBuffer = new Collider[64];
        private readonly Collider[] objectiveBuffer = new Collider[96];
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
        private Vector2Int explorationRoom;
        private Vector2Int occupiedRoom;
        private float nextPathRefresh;
        private float nextProgressCheck;
        private float nextObjectiveScan;
        private float stationaryTime;
        private float recoveryUntil;
        private float lastProgress;
        private float unwedgeUntil;
        private int recoveriesSinceProgress;
        private float lastExperience;
        private float lastHealth;
        private int frame;
        private int explorationDirection = -1;
        private int recoverySide;
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
            recoveriesSinceProgress = 0;
            lastExperience = -1f;
            lastHealth = float.MaxValue;
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
                Time.time - lastProgress > engagementStallDelay
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
                nextProgressCheck = Time.time + progressCheckInterval;
                hasPosition = true;
            }

            if (dungeon == null)
                dungeon = FindAnyObjectByType<DungeonManager>();
            return true;
        }
    }
}
