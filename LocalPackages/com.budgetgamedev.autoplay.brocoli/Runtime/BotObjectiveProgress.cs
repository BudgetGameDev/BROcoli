using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed class BotObjectiveProgress
    {
        private readonly Dictionary<Vector2Int, float> retired = new();
        private bool pursuing;
        private Vector2 target;
        private float bestDistance;
        private float lastProgress;
        private const float ProgressBudget = 20f;
        private const float RetryDelay = 60f;

        internal bool IsRetired(Vector2 position, float time) =>
            retired.TryGetValue(Key(position), out float until) && time < until;

        internal void Pursue(Vector2 position, Vector2 player, float time)
        {
            if (pursuing && Key(position) == Key(target))
                return;
            pursuing = true;
            target = position;
            bestDistance = Vector2.Distance(player, target);
            lastProgress = time;
        }

        internal bool Observe(Vector2 player, float time)
        {
            if (!pursuing)
                return false;
            float distance = Vector2.Distance(player, target);
            if (distance < bestDistance - 0.5f)
            {
                bestDistance = distance;
                lastProgress = time;
            }
            if (time - lastProgress < ProgressBudget)
                return false;
            retired[Key(target)] = time + RetryDelay;
            pursuing = false;
            return true;
        }

        internal void Clear()
        {
            retired.Clear();
            pursuing = false;
        }

        private static Vector2Int Key(Vector2 position) => Vector2Int.RoundToInt(position * 2f);
    }
}
