using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal static partial class BotExplorationPolicy
    {
        /// <summary>
        /// Rooms the search will walk through before giving up. A dungeon is
        /// generated on demand and has no edge, so the frontier has to be bounded by
        /// something; this is far enough to cross the whole of what a run has opened
        /// up and cheap enough to run on every doorway.
        /// </summary>
        internal const int SearchLimit = 400;

        /// <summary>
        /// The nearest room the run has not been in, measured in doorways rather than
        /// in metres, with ties settled by which one is the better place to go next.
        ///
        /// Picking among the four rooms next door cannot do this. Once a run has
        /// cleared the rooms around it, every neighbour scores as visited and the
        /// agent hill-climbs between rooms it has already seen -- which reads, from
        /// outside, as an agent that explores a corner of the dungeon and then paces
        /// it. Crossing three known rooms to reach an unknown one is the whole of
        /// exploring, and it needs a target further away than the next door.
        /// </summary>
        internal static bool TryFindFrontier(
            DungeonLayout layout,
            Vector2Int from,
            HashSet<Vector2Int> visited,
            float healthFraction,
            out Vector2Int frontier
        ) => TryFindFrontier(layout, from, visited, healthFraction, out frontier, out _);

        /// <summary>The adjacent first room on a connected route to the selected frontier.</summary>
        internal static bool TryFindFrontier(
            DungeonLayout layout,
            Vector2Int from,
            HashSet<Vector2Int> visited,
            float healthFraction,
            out Vector2Int frontier,
            out Vector2Int firstStep,
            HashSet<Vector2Int> rejectedFirstSteps = null
        )
        {
            frontier = from;
            firstStep = from;
            if (layout == null)
                return false;

            var seen = new HashSet<Vector2Int> { from };
            var firstSteps = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);

            int depth = 0;
            int remainingAtDepth = 1;
            int nextDepthCount = 0;
            bool found = false;
            float bestScore = float.NegativeInfinity;
            int foundDepth = int.MaxValue;

            while (queue.Count > 0 && seen.Count < SearchLimit)
            {
                Vector2Int room = queue.Dequeue();
                for (
                    int direction = 0;
                    direction < DungeonLayout.DirectionOffsets.Length;
                    direction++
                )
                {
                    if (!layout.IsPlayableDoorOpen(room, direction))
                        continue;

                    Vector2Int next = room + DungeonLayout.DirectionOffsets[direction];
                    if (
                        room == from
                        && rejectedFirstSteps != null
                        && rejectedFirstSteps.Contains(next)
                    )
                        continue;
                    if (!seen.Add(next))
                        continue;

                    firstSteps[next] = room == from ? next : firstSteps[room];

                    if (visited.Contains(next))
                    {
                        queue.Enqueue(next);
                        nextDepthCount++;
                        continue;
                    }

                    // The first unvisited ring of rooms wins outright; within it, the
                    // same scoring that ranks the rooms next door decides which.
                    float score = ScoreCandidate(layout, next, visited, healthFraction);
                    if (depth + 1 < foundDepth || (depth + 1 == foundDepth && score > bestScore))
                    {
                        foundDepth = depth + 1;
                        bestScore = score;
                        frontier = next;
                        firstStep = firstSteps[next];
                        found = true;
                    }
                }

                if (--remainingAtDepth > 0)
                    continue;

                // A whole ring of doorways is searched before the next, so the first
                // ring that held anything unseen is the nearest one.
                if (found)
                    return true;
                depth++;
                remainingAtDepth = nextDepthCount;
                nextDepthCount = 0;
            }

            return found;
        }
    }
}
