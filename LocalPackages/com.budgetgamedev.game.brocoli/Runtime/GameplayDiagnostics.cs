#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
using System;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Development-only injection boundary. No diagnostic type or call exists in a release.</summary>
    internal static class GameplayDiagnostics
    {
        internal static Action<string> Feature;
        internal static Action<int, float, float, float, float, float, float, int> RoomSpawned;
        internal static Func<Vector2?> Movement;
        internal static Func<bool> AllowCheckpoint;

        internal static void Record(string feature) => Feature?.Invoke(feature);

        internal static void RecordIf(bool condition, string feature)
        {
            if (condition)
                Record(feature);
        }
    }
}
#endif
