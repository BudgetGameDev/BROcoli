using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        private Vector2 NavigateTo(Vector2 position, Vector2 target)
        {
            bool targetChanged = (target - cachedPathTarget).sqrMagnitude > 0.5f;
            if (Time.time < nextPathRefresh && !targetChanged && hasCachedRoute)
                return FollowRoute(position);

            nextPathRefresh = Time.time + pathRefreshInterval;
            cachedPathTarget = target;
            NavigationTarget = target;
            ReplanCount++;

            Vector3 fromWorld = position.ToWorld(player.position.y);
            Vector3 targetWorld = target.ToWorld(player.position.y);
            if (!TryCompletePath(fromWorld, targetWorld, out NavMeshHit targetHit))
            {
                hasCachedRoute = false;
                FailedPathCount++;
                if (RetireUnreachableCenter(position, target))
                {
                    cachedPathDirection = Vector2.zero;
                    return Vector2.zero;
                }
                AbandonCurrentTarget();
                // A failed route is not permission to walk directly through a wall.
                // Return toward clear ground and pick a different connected exit.
                cachedPathDirection =
                    DungeonLayout.RoomCenter(DungeonLayout.RoomAt(position)) - position;
                return NavigateLocal(position, cachedPathDirection);
            }

            cachedCornerCount = path.GetCornersNonAlloc(pathCorners);
            cachedCornerIndex = 1;
            cachedPathEndpoint = targetHit.position.ToGround();
            hasCachedRoute = true;
            return FollowRoute(position);
        }

        private Vector2 FollowRoute(Vector2 position)
        {
            ActiveRoute = true;
            LastPathStatus = "complete";
            while (
                cachedCornerIndex < cachedCornerCount - 1
                && (pathCorners[cachedCornerIndex].ToGround() - position).sqrMagnitude < 0.04f
            )
                cachedCornerIndex++;
            Vector2 waypoint =
                cachedCornerCount > 1
                    ? pathCorners[cachedCornerIndex].ToGround()
                    : cachedPathEndpoint;
            cachedPathDirection = waypoint - position;
            if (movement != null)
                return NavigatePhysical(position, cachedPathDirection, true);
            return NavigateLocal(position, cachedPathDirection);
        }

        private bool TryCompletePath(Vector3 from, Vector3 target, out NavMeshHit targetHit)
        {
            targetHit = default;
            LastPathStatus = "source-off-mesh";
            if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, 2.5f, NavMesh.AllAreas))
                return false;
            LastPathStatus = "target-off-mesh";
            if (!NavMesh.SamplePosition(target, out targetHit, 4f, NavMesh.AllAreas))
                return false;
            LastPathStatus = "target-projected-outside-room";
            if (
                DungeonLayout.RoomAt(targetHit.position.ToGround())
                != DungeonLayout.RoomAt(target.ToGround())
            )
                return false;
            LastPathStatus = "invalid";
            if (
                !NavMesh.CalculatePath(fromHit.position, targetHit.position, NavMesh.AllAreas, path)
            )
                return false;
            LastPathStatus = path.status.ToString();
            return path.status == NavMeshPathStatus.PathComplete;
        }

        internal bool RetireUnreachableCenter(Vector2 position, Vector2 target)
        {
            Vector2Int room = DungeonLayout.RoomAt(position);
            if ((DungeonLayout.RoomCenter(room) - target).sqrMagnitude > 0.01f)
                return false;

            // A divider can put the nearest center projection on another NavMesh
            // island. Retrying that recovery point must not extend its own deadline.
            stagedRooms.Add(room);
            unwedgeUntil = 0f;
            hasExplorationRoom = false;
            nextPathRefresh = 0f;
            return true;
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
                && (corners[corner].ToGround() - position).sqrMagnitude < 0.04f
            )
                corner++;
            return cornerCount > 1 ? corners[corner].ToGround() - position : target - position;
        }
    }
}
