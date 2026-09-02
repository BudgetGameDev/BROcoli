using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        private static readonly float[] AvoidanceAngles =
        {
            0f,
            35f,
            -35f,
            70f,
            -70f,
            110f,
            -110f,
            180f,
        };

        private Vector2 GetExplorationTarget(Vector2 position)
        {
            Vector2Int currentRoom = DungeonLayout.RoomAt(position);
            if (Time.time - lastProgress > explorationStallDelay)
            {
                AbandonCurrentTarget();
                lastProgress = Time.time; // the next choice gets its own fair go
            }

            if (Time.time < unwedgeUntil)
                return DungeonLayout.RoomCenter(currentRoom);

            if (
                dungeon == null
                || dungeon.Layout == null
                || !hasExplorationRoom
                || currentRoom == explorationRoom
            )
            {
                PickExplorationRoom(currentRoom);
            }

            return hasExplorationRoom ? DungeonLayout.RoomCenter(explorationRoom) : position;
        }

        private void PickExplorationRoom(Vector2Int currentRoom)
        {
            float healthFraction =
                stats != null && stats.CurrentMaxHealth > 0f
                    ? stats.CurrentHealth / stats.CurrentMaxHealth
                    : 1f;
            explorationDirection = BotExplorationPolicy.ChooseDirection(
                dungeon != null ? dungeon.Layout : null,
                currentRoom,
                visitedRooms,
                healthFraction,
                explorationDirection
            );
            if (explorationDirection < 0)
            {
                hasExplorationRoom = false;
                return;
            }

            explorationRoom = currentRoom + DungeonLayout.DirectionOffsets[explorationDirection];
            hasExplorationRoom = true;
            nextPathRefresh = 0f;
        }

        private Vector2 NavigateTo(Vector2 position, Vector2 target)
        {
            bool targetChanged = (target - cachedPathTarget).sqrMagnitude > 0.5f;
            if (Time.time < nextPathRefresh && !targetChanged)
                return NavigateLocal(position, cachedPathDirection);

            nextPathRefresh = Time.time + pathRefreshInterval;
            cachedPathTarget = target;
            ReplanCount++;

            Vector3 fromWorld = position.ToWorld(player.position.y);
            Vector3 targetWorld = target.ToWorld(player.position.y);
            if (
                !NavMesh.SamplePosition(fromWorld, out NavMeshHit fromHit, 2.5f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(
                    targetWorld,
                    out NavMeshHit targetHit,
                    4f,
                    NavMesh.AllAreas
                )
                || !NavMesh.CalculatePath(
                    fromHit.position,
                    targetHit.position,
                    NavMesh.AllAreas,
                    path
                )
            )
            {
                cachedPathDirection = target - position;
                return NavigateLocal(position, cachedPathDirection);
            }

            int cornerCount = path.GetCornersNonAlloc(pathCorners);
            cachedPathDirection = SelectPathDirection(pathCorners, cornerCount, position, target);
            return NavigateLocal(position, cachedPathDirection);
        }

        internal static Vector2 SelectPathDirection(
            Vector3[] corners,
            int cornerCount,
            Vector2 position,
            Vector2 target
        )
        {
            int corner = 1;
            while (
                corner < cornerCount - 1
                && (corners[corner].ToGround() - position).sqrMagnitude < 0.5f
            )
                corner++;
            return cornerCount > 1 ? corners[corner].ToGround() - position : target - position;
        }

        private Vector2 NavigateLocal(Vector2 position, Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Vector2 normalized = desired.normalized;
            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < AvoidanceAngles.Length; i++)
            {
                float signedAngle = AvoidanceAngles[i] * recoverySide;
                Vector2 candidate = Quaternion.Euler(0f, 0f, signedAngle) * normalized;
                if (!TryMeasureClearance(position, candidate, out float clearance))
                    continue;

                float alignment = Vector2.Dot(candidate, normalized);
                float score = alignment * 2f + clearance - i * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore > float.NegativeInfinity ? best : -normalized;
        }

        private bool TryMeasureClearance(Vector2 position, Vector2 direction, out float clearance)
        {
            const float probeDistance = 1.8f;
            clearance = probeDistance;
            Vector3 origin = position.ToWorld(0.75f);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                0.42f,
                direction.ToWorld(),
                obstacleHits,
                probeDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = obstacleHits[i];
                if (!IsNavigationObstacle(hit.collider))
                    continue;
                clearance = Mathf.Min(clearance, hit.distance);
            }

            if (clearance < 0.2f)
                return false;

            if (NavMesh.SamplePosition(origin, out NavMeshHit fromHit, 1.5f, NavMesh.AllAreas))
            {
                Vector3 requested = (position + direction * navigationLookAhead).ToWorld();
                if (
                    !NavMesh.SamplePosition(requested, out NavMeshHit toHit, 0.8f, NavMesh.AllAreas)
                    || NavMesh.Raycast(fromHit.position, toHit.position, out _, NavMesh.AllAreas)
                )
                    return false;
            }

            clearance /= probeDistance;
            return true;
        }

        private bool IsNavigationObstacle(Collider candidate)
        {
            if (candidate == null || candidate.transform.IsChildOf(player))
                return false;
            if (candidate.GetComponentInParent<EnemyBase>() != null)
                return false;
            if (candidate.GetComponentInParent<EnemyProjectile>() != null)
                return false;
            return true;
        }
    }
}
