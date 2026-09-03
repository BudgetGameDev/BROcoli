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
            {
                // Walking at a point it is already standing on leaves the avoidance
                // fan with no direction worth preferring, and the agent turns on the
                // spot in the middle of the room for the rest of the manoeuvre.
                Vector2 centre = DungeonLayout.RoomCenter(currentRoom);
                if ((centre - position).sqrMagnitude > UnwedgeArrival * UnwedgeArrival)
                    return centre;
                unwedgeUntil = 0f;
            }

            if (TryStageInRoom(currentRoom, position, out Vector2 staging))
                return staging;

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

        /// <summary>
        /// Walking into a room nobody has cleared, head for the middle of it before
        /// heading anywhere else.
        ///
        /// The shortest line from one doorway to the next runs along a wall, and taking
        /// it with a group waking up behind is how a fight starts in a corner: the
        /// agent has a quarter of the room to back into and the crowd has the rest.
        /// The middle of a room is the one part of it the dungeon keeps clear of
        /// spawns and props, so it is both the safest place to meet a room and the
        /// easiest place to leave from once it is met. Each room is staged once --
        /// crossing it again later costs nothing.
        /// </summary>
        private bool TryStageInRoom(Vector2Int currentRoom, Vector2 position, out Vector2 staging)
        {
            staging = position;
            if (stagedRooms.Contains(currentRoom))
                return false;

            if (currentRoom != stagingRoom)
            {
                stagingRoom = currentRoom;
                stagingDeadline = Time.time + StagingSeconds;
            }

            Vector2 centre = DungeonLayout.RoomCenter(currentRoom);
            bool arrived = (centre - position).sqrMagnitude <= StagingArrival * StagingArrival;

            // Not every room has a middle the agent can stand in: a divided room, or
            // one whose centre a prop sits on, will never be arrived at. Staging is
            // worth a few seconds and never worth a run -- a bounded attempt is the
            // difference between a manoeuvre and a wedge, and one seed spent its
            // whole session walking at a point it could not reach.
            if (arrived || Time.time > stagingDeadline)
            {
                stagedRooms.Add(currentRoom);
                return false;
            }

            staging = centre;
            return true;
        }

        /// <summary>
        /// Chooses somewhere unseen to walk to. The nearest room the run has not been
        /// in wins, however many known rooms lie between here and it -- the route is
        /// the navmesh's problem, not this one's. Only when nothing unseen is
        /// reachable does the agent fall back to ranking the four rooms next door,
        /// which is all it can do in a pocket it has already cleared.
        /// </summary>
        private void PickExplorationRoom(Vector2Int currentRoom)
        {
            DungeonLayout layout = dungeon != null ? dungeon.Layout : null;
            float healthFraction =
                stats != null && stats.CurrentMaxHealth > 0f
                    ? stats.CurrentHealth / stats.CurrentMaxHealth
                    : 1f;

            if (
                BotExplorationPolicy.TryFindFrontier(
                    layout,
                    currentRoom,
                    visitedRooms,
                    healthFraction,
                    out Vector2Int frontier
                )
            )
            {
                explorationRoom = frontier;
                explorationDirection = -1;
                hasExplorationRoom = true;
                nextPathRefresh = 0f;
                return;
            }

            explorationDirection = BotExplorationPolicy.ChooseDirection(
                layout,
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
            float openness = OpennessWeight();
            for (int i = 0; i < AvoidanceAngles.Length; i++)
            {
                Vector2 candidate = Quaternion.Euler(0f, 0f, AvoidanceAngles[i]) * normalized;
                if (!TryMeasureClearance(position, candidate, out float clearance))
                    continue;

                float score =
                    ScoreHeading(candidate, normalized, committedHeading, clearance, openness)
                    - i * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (bestScore > float.NegativeInfinity)
            {
                blockedTicks = 0;
                committedHeading = best;
                return best;
            }

            // Nothing in the fan is walkable. Backing out the way it came in is the
            // right first answer, but on its own it is only half a manoeuvre: the
            // route is recomputed a sixth of a second later and points back into the
            // same geometry, so the agent shuffles in and out of the corner it is
            // wedged in without the stationary check ever quite firing. After a few
            // ticks of finding nowhere to go, head for the middle of the room
            // instead -- the one point in a room that is always on the navmesh.
            if (++blockedTicks >= BlockedTicksBeforeUnwedging)
            {
                blockedTicks = 0;
                unwedgeUntil = Time.time + unwedgeSeconds;
                nextPathRefresh = 0f;
            }

            committedHeading = -normalized;
            return committedHeading;
        }

        /// <summary>
        /// What one candidate direction is worth: mostly how close it is to where
        /// the agent wants to go and how much room it has, plus a nudge for
        /// continuing the way it was already going.
        /// </summary>
        /// <summary>
        /// How much room to move is worth against pointing the right way. Enough to
        /// bow the agent off a wall it would otherwise scrape along, and not enough to
        /// out-vote the route: a doorway is a gap one body wide, and an agent that
        /// preferred open ground over its destination stopped using them at all --
        /// measured as a run that reached ten rooms and then spent six game-minutes
        /// wandering the ones it had already seen.
        /// </summary>
        private float OpennessWeight() => NearbyEnemyCount > 0 ? OpenGroundWeight : 1f;

        internal static float ScoreHeading(
            Vector2 candidate,
            Vector2 desired,
            Vector2 committed,
            float clearance,
            float clearanceWeight = 1f
        )
        {
            float score = Vector2.Dot(candidate, desired) * 2f + clearance * clearanceWeight;
            if (committed.sqrMagnitude < 0.0001f)
                return score;
            return score + Vector2.Dot(candidate, committed) * HeadingCommitment;
        }

        private bool TryMeasureClearance(Vector2 position, Vector2 direction, out float clearance)
        {
            // Far enough to tell open floor from a wall the agent is about to be
            // pinned against. At the old distance a wall a stride and a half away
            // measured as clear as the middle of the room.
            const float probeDistance = 3.2f;

            // The body's own width, and no wider. A fatter sweep reads a doorway as
            // impassable -- the arch is a tile across and the sides are inside the
            // sweep from the moment the agent lines up with it -- and an agent that
            // cannot use doorways stops exploring: measured as a run pinned in five
            // rooms with fifty unsticking manoeuvres to show for it.
            const float probeRadius = 0.42f;
            clearance = probeDistance;
            Vector3 origin = position.ToWorld(0.75f);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
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
