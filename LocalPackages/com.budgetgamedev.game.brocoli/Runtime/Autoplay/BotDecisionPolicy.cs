using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal enum BotIntent
    {
        Waiting,
        Explore,
        Engage,
        Retreat,
        Dodge,
        Recover,
    }

    internal readonly struct BotSituation
    {
        internal readonly bool HasEnemies;
        internal readonly float NearestEnemyDistance;
        internal readonly int CloseEnemyCount;
        internal readonly float HealthFraction;
        internal readonly bool IncomingProjectile;
        internal readonly bool Recovering;

        internal BotSituation(
            bool hasEnemies,
            float nearestEnemyDistance,
            int closeEnemyCount,
            float healthFraction,
            bool incomingProjectile,
            bool recovering
        )
        {
            HasEnemies = hasEnemies;
            NearestEnemyDistance = nearestEnemyDistance;
            CloseEnemyCount = closeEnemyCount;
            HealthFraction = healthFraction;
            IncomingProjectile = incomingProjectile;
            Recovering = recovering;
        }
    }

    /// <summary>Pure utility decisions shared by the live agent and EditMode tests.</summary>
    internal static class BotDecisionPolicy
    {
        internal static BotIntent ChooseIntent(
            BotSituation situation,
            float dangerRadius,
            int crowdCount,
            float lowHealthFraction
        )
        {
            if (situation.Recovering)
                return BotIntent.Recover;
            if (situation.IncomingProjectile)
                return BotIntent.Dodge;
            if (!situation.HasEnemies)
                return BotIntent.Explore;
            if (
                situation.NearestEnemyDistance < dangerRadius
                || situation.CloseEnemyCount >= crowdCount
                || situation.HealthFraction < lowHealthFraction
            )
                return BotIntent.Retreat;
            return BotIntent.Engage;
        }

        internal static int ChooseExplorationDirection(
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
                if (!layout.IsDoorOpen(room, direction))
                    continue;

                Vector2Int candidate = room + DungeonLayout.DirectionOffsets[direction];
                DungeonLayout.RoomPopulation population = layout.Population(candidate);
                float score = visited.Contains(candidate) ? -20f : 20f;
                score += DungeonLayout.Ring(candidate) * 0.2f;

                float danger = population.Count + (population.Elite ? 5f : 0f);
                score += healthFraction < 0.5f ? -danger : Mathf.Min(danger, 6f) * 0.25f;
                if (previousDirection >= 0 && direction == (previousDirection + 2) % 4)
                    score -= 2f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = direction;
                }
            }

            return bestDirection;
        }
    }
}
