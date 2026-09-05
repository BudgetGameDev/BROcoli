using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
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
            FailedPathCount = 0;
            BlockedStepCount = 0;
            StuckRecoveryCount = 0;
            DistanceTravelled = 0f;
            ResetReactionCounters();
            ResetLifeNavigation();
        }

        // A respawn owns fresh rooms and targets; cumulative run counters remain intact.
        internal void ResetLifeNavigation()
        {
            visitedRooms.Clear();
            rejectedExits.Clear();
            LastPathStatus = "none";
            objectives = ObjectiveObservation.None;
            hasExplorationRoom = false;
            hasOccupiedRoom = false;
            explorationDirection = -1;
            stationaryTime = 0f;
            recoveryUntil = 0f;
            nextObjectiveScan = 0f;
            nextPathRefresh = 0f;
            cachedPathDirection = Vector2.zero;
            hasCachedRoute = false;
            ActiveRoute = false;
            AcceptedStep = Vector2.zero;
            MovementBlocked = false;
            StepStatus = "none";
            objectiveProgress.Clear();
            doorwayCommitment.Clear();
            ResetReactionLife();
            lastDodge = Vector2.zero;
            frame = 0;
            lastProgress = Time.time;
            lastCombatProgress = Time.time;
            unwedgeUntil = 0f;
            committedHeading = Vector2.zero;
            loiterOrigin = Vector2.zero;
            loiterTravelled = 0f;
            nextLoiterCheck = 0f;
            recoveriesSinceProgress = 0;
            blockedTicks = 0;
            stagedRooms.Clear();
            stagingDeadline = 0f;
            stagingRoom = new Vector2Int(int.MinValue, int.MinValue);
            lastEscape = Vector2.zero;
            lastKills = AutoplayFeatureLog.Count(AutoplayFeatures.EnemyKilled);
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
            TrackCombatProgress();
            if (objectiveProgress.Observe(position, Time.time))
            {
                objectives = ObjectiveObservation.None;
                nextObjectiveScan = 0f;
            }

            UpdateTactics(position);
            EnemyObservation enemies = activeTactics.Enemies;
            NearbyEnemyCount = enemies.Count;
            currentIntent =
                Time.time < recoveryUntil
                    ? BotIntent.Recover
                    : doorwayCommitment.Resolve(
                        tacticalIntent,
                        activeTactics.Situation,
                        Tuning,
                        position,
                        Time.time
                    );
            if (currentIntent == BotIntent.Dodge)
                AutoplayFeatureLog.Record(AutoplayFeatures.ProjectileDodged);

            if (currentIntent == BotIntent.Collect && objectives.HasPickup)
                objectiveProgress.Pursue(objectives.Pickup, position, Time.time);
            else if (currentIntent == BotIntent.Loot && objectives.HasChest)
                objectiveProgress.Pursue(objectives.Chest, position, Time.time);

            ActiveRoute = false;
            LastPathStatus = "local-" + IntentName;
            AcceptedStep = Vector2.zero;
            MovementBlocked = false;
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
                Time.time - lastCombatProgress > engagementStallDelay,
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
                ResetLifeNavigation();
                player = playerObject.transform;
                movement = playerObject.GetComponent<PlayerMovement>();
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
