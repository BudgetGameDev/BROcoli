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
            if (doorwayCommitment.TryGoal(position, Time.time, out Vector2 committedGoal))
                return committedGoal;
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

            if (hasExplorationRoom)
                doorwayCommitment.Begin(position, currentRoom, explorationRoom, Time.time);
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
                    out _,
                    out Vector2Int firstStep,
                    rejectedExits
                )
            )
            {
                // The frontier may lie beyond the streamed NavMesh. Follow its
                // connected first doorway instead of steering straight at an unloaded room.
                explorationRoom = firstStep;
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
                explorationDirection,
                rejectedExits
            );
            ApplyExplorationDirection(currentRoom, explorationDirection);
        }

        internal void ApplyExplorationDirection(Vector2Int currentRoom, int direction)
        {
            explorationDirection = direction;
            if (explorationDirection < 0)
            {
                hasExplorationRoom = false;
                rejectedExits.Clear();
                unwedgeUntil = Time.time + unwedgeSeconds;
                return;
            }

            explorationRoom = currentRoom + DungeonLayout.DirectionOffsets[explorationDirection];
            hasExplorationRoom = true;
            nextPathRefresh = 0f;
        }
    }
}
