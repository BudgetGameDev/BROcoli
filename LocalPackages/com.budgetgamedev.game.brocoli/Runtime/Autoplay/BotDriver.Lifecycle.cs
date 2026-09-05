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
