using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Autonomous playtest agent. It observes combat and dungeon state, chooses an
    /// intent, plans paths on the runtime NavMesh, and feeds ordinary player input.
    /// Normal play is unaffected unless autoplay creates and enables this component.
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

        [Header("Navigation")]
        [SerializeField]
        private float navigationLookAhead = 3f;

        [SerializeField]
        private float pathRefreshInterval = 0.15f;

        [SerializeField]
        private float progressCheckInterval = 0.5f;

        [SerializeField]
        private float stuckRecoveryDelay = 1f;

        private static BotIntent currentIntent;

        private readonly Collider[] projectileBuffer = new Collider[64];
        private readonly RaycastHit[] obstacleHits = new RaycastHit[24];
        private readonly Vector3[] pathCorners = new Vector3[32];
        private readonly HashSet<Vector2Int> visitedRooms = new();

        private Transform player;
        private PlayerStats stats;
        private DungeonManager dungeon;
        private NavMeshPath path;
        private Vector2 lastDodge;
        private Vector2 lastPosition;
        private Vector2 lastProgressPosition;
        private Vector2 recoveryDirection;
        private Vector2 cachedPathDirection;
        private Vector2 cachedPathTarget;
        private Vector2Int explorationRoom;
        private float nextPathRefresh;
        private float nextProgressCheck;
        private float stationaryTime;
        private float recoveryUntil;
        private int frame;
        private int explorationDirection = -1;
        private int recoverySide;
        private bool hasPosition;
        private bool hasExplorationRoom;

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
            hasExplorationRoom = false;
            explorationDirection = -1;
            stationaryTime = 0f;
            recoveryUntil = 0f;
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
            EnemyObservation enemies = ObserveEnemies(position);
            NearbyEnemyCount = enemies.Count;

            if (++frame % 3 == 0)
                lastDodge = ComputeProjectileDodge(position);

            float hpFraction =
                stats != null && stats.CurrentMaxHealth > 0f
                    ? stats.CurrentHealth / stats.CurrentMaxHealth
                    : 1f;
            var situation = new BotSituation(
                enemies.Count > 0,
                enemies.NearestDistance,
                enemies.CloseCount,
                hpFraction,
                lastDodge.sqrMagnitude > 0.0001f,
                Time.time < recoveryUntil
            );
            currentIntent = BotDecisionPolicy.ChooseIntent(
                situation,
                dangerRadius,
                crowdCount,
                lowHpFraction
            );

            Vector2 desired = NavigateIntent(currentIntent, position, enemies);

            Move = Vector2.ClampMagnitude(desired, 1f);
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

        private void TrackProgress(Vector2 position)
        {
            if (!hasPosition)
            {
                lastPosition = position;
                lastProgressPosition = position;
                nextProgressCheck = Time.time + progressCheckInterval;
                hasPosition = true;
                return;
            }

            float travelled = Vector2.Distance(position, lastPosition);
            DistanceTravelled += travelled;
            lastPosition = position;
            if (Time.time < nextProgressCheck)
                return;

            float elapsed = Mathf.Max(progressCheckInterval, Time.time - nextProgressCheck);
            nextProgressCheck = Time.time + progressCheckInterval;
            float progress = Vector2.Distance(position, lastProgressPosition);
            if (Move.sqrMagnitude > 0.2f && progress < 0.12f)
                stationaryTime += elapsed;
            else
                stationaryTime = 0f;

            if (stationaryTime >= stuckRecoveryDelay)
                BeginStuckRecovery();
            lastProgressPosition = position;
        }

        private void BeginStuckRecovery()
        {
            Vector2 basis = Move.sqrMagnitude > 0.01f ? Move.normalized : Vector2.up;
            recoveryDirection = Vector2.Perpendicular(basis) * recoverySide - basis * 0.25f;
            recoveryDirection.Normalize();
            recoverySide = -recoverySide;
            recoveryUntil = Time.time + 0.8f;
            stationaryTime = 0f;
            nextPathRefresh = 0f;
            StuckRecoveryCount++;
        }
    }
}
