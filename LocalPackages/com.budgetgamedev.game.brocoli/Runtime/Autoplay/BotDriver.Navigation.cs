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

        /// <summary>
        /// Turns a direction the agent wants to go into one it can actually walk,
        /// by probing a fan of alternatives and taking the best compromise between
        /// pointing the right way and having room to move.
        ///
        /// The score carries a bias toward whichever way it went last tick. Without
        /// one, two comparable ways around a wall are settled by a tie-break, and
        /// the agent alternates between them on the spot -- pacing the wall rather
        /// than getting round it, which is the shape most of a wasted run takes.
        /// </summary>
        private Vector2 NavigateLocal(Vector2 position, Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Vector2 normalized = desired.normalized;
            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < AvoidanceAngles.Length; i++)
            {
                Vector2 candidate = Quaternion.Euler(0f, 0f, AvoidanceAngles[i]) * normalized;
                if (!TryMeasureClearance(position, candidate, out float clearance))
                    continue;

                float score =
                    ScoreHeading(candidate, normalized, committedHeading, clearance) - i * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            // Nothing in the fan is walkable, so back out the way it came in and let
            // the stuck manoeuvre take over rather than pressing on into geometry.
            Vector2 chosen = bestScore > float.NegativeInfinity ? best : -normalized;
            committedHeading = chosen;
            return chosen;
        }

        /// <summary>
        /// What one candidate direction is worth: mostly how close it is to where
        /// the agent wants to go and how much room it has, plus a nudge for
        /// continuing the way it was already going.
        /// </summary>
        internal static float ScoreHeading(
            Vector2 candidate,
            Vector2 desired,
            Vector2 committed,
            float clearance
        )
        {
            float score = Vector2.Dot(candidate, desired) * 2f + clearance;
            if (committed.sqrMagnitude < 0.0001f)
                return score;
            return score + Vector2.Dot(candidate, committed) * HeadingCommitment;
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
