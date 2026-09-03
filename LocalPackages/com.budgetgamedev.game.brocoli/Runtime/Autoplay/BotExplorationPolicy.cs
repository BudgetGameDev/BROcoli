using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Chooses which doorway to take next. Kept separate from goal scoring because
    /// it answers a different question: not "what should I want" but "which way is
    /// the unseen part of the dungeon".
    /// </summary>
    internal static partial class BotExplorationPolicy
    {
        internal static int ChooseDirection(
            DungeonLayout layout,
            Vector2Int room,
            HashSet<Vector2Int> visited,
            float healthFraction,
            int previousDirection
        )
        {
            if (layout == null)
                return -1;

            int bestDirection = -1;
            float bestScore = float.NegativeInfinity;
            for (int direction = 0; direction < DungeonLayout.DirectionOffsets.Length; direction++)
            {
                if (!layout.IsPlayableDoorOpen(room, direction))
                    continue;

                float score = ScoreCandidate(
                    layout,
                    room + DungeonLayout.DirectionOffsets[direction],
                    visited,
                    healthFraction
                );
                if (previousDirection >= 0 && direction == (previousDirection + 2) % 4)
                    score -= 2f; // doubling back is how an agent paces one corridor forever

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = direction;
                }
            }

            return bestDirection;
        }

        internal static float ScoreCandidate(
            DungeonLayout layout,
            Vector2Int candidate,
            HashSet<Vector2Int> visited,
            float healthFraction
        )
        {
            DungeonLayout.RoomPopulation population = layout.Population(candidate);
            float score = visited.Contains(candidate) ? -20f : 20f;
            score += DungeonLayout.Ring(candidate) * 0.2f;
            score += UnvisitedNeighbours(layout, candidate, visited) * 3f;

            float danger = population.Count + (population.Elite ? 5f : 0f);
            return score + (healthFraction < 0.5f ? -danger : Mathf.Min(danger, 6f) * 0.25f);
        }

        /// <summary>
        /// Rewards rooms that open onto more unseen rooms. Without it the agent
        /// finishes a dead-end branch and walks the whole way back; with it, it
        /// prefers the junction that keeps the frontier growing.
        /// </summary>
        private static int UnvisitedNeighbours(
            DungeonLayout layout,
            Vector2Int room,
            HashSet<Vector2Int> visited
        )
        {
            int count = 0;
            for (int direction = 0; direction < DungeonLayout.DirectionOffsets.Length; direction++)
            {
                if (!layout.IsPlayableDoorOpen(room, direction))
                    continue;
                if (!visited.Contains(room + DungeonLayout.DirectionOffsets[direction]))
                    count++;
            }

            return count;
        }
    }
}
