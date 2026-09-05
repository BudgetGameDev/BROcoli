using System.Collections.Generic;

namespace BudgetGameDev.Autoplay
{
    /// <summary>A game adapter scores its own actions using its current observations.</summary>
    public interface IUtilityPolicy<TAction>
    {
        float Score(TAction action);
    }

    /// <summary>Stable action selection with hysteresis, shared by game-specific drivers.</summary>
    public static class UtilitySelection
    {
        public static TAction Choose<TAction, TPolicy>(
            IReadOnlyList<TAction> actions,
            TPolicy policy,
            TAction previous,
            float hysteresis,
            TAction fallback
        )
            where TPolicy : struct, IUtilityPolicy<TAction>
        {
            TAction best = fallback;
            float bestScore = float.NegativeInfinity;
            foreach (TAction action in actions)
            {
                float score = policy.Score(action);
                if (float.IsNaN(score))
                    continue;
                if (EqualityComparer<TAction>.Default.Equals(action, previous))
                    score += hysteresis;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = action;
                }
            }
            return best;
        }
    }
}
