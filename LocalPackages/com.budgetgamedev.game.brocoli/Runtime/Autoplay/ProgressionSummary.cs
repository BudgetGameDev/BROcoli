using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>How a run progressed and how hard it was pressed while doing it.</summary>
    internal readonly struct ProgressionSummary
    {
        internal readonly int PeakLevel;
        internal readonly int Levels;
        internal readonly int Lives;
        internal readonly int Deaths;
        internal readonly int DeepestRing;
        internal readonly int Rings;
        internal readonly float Duration;
        internal readonly float SecondsPerLevel;
        internal readonly float EarlySecondsPerLevel;
        internal readonly float LateSecondsPerLevel;
        internal readonly float EarlyKillsPerLevel;
        internal readonly float LateKillsPerLevel;
        internal readonly float SecondsPerRing;
        internal readonly float MeanHealthFraction;
        internal readonly float LowestHealthFraction;
        internal readonly float DangerShare;
        internal readonly float SafeShare;

        internal ProgressionSummary(
            LevelPacing levels,
            DepthPacing depth,
            HealthPressure health,
            int lives,
            int deaths,
            float duration
        )
        {
            PeakLevel = levels.PeakLevel;
            Levels = levels.Count;
            Lives = lives;
            Deaths = deaths;
            DeepestRing = depth.DeepestRing;
            Rings = depth.Count;
            Duration = duration;
            SecondsPerLevel = levels.Seconds;
            EarlySecondsPerLevel = levels.EarlySeconds;
            LateSecondsPerLevel = levels.LateSeconds;
            EarlyKillsPerLevel = levels.EarlyKills;
            LateKillsPerLevel = levels.LateKills;
            SecondsPerRing = depth.Seconds;
            MeanHealthFraction = health.Mean;
            LowestHealthFraction = health.Lowest;
            DangerShare = health.DangerShare;
            SafeShare = health.SafeShare;
        }

        internal float DeathsPerHour => Deaths / Mathf.Max(1f / 3600f, Duration / 3600f);

        /// <summary>
        /// How much slower a late level is than an early one. This is the number that
        /// says whether a curve walls: one means the run never slows down, and a
        /// large one means the bar stopped moving while the rooms kept deepening.
        /// </summary>
        internal float PaceRatio =>
            LateSecondsPerLevel <= 0f || EarlySecondsPerLevel <= 0f
                ? 1f
                : LateSecondsPerLevel / EarlySecondsPerLevel;
    }

    /// <summary>What the run's levels cost, split into the opening and everything after.</summary>
    internal readonly struct LevelPacing
    {
        internal readonly int PeakLevel;
        internal readonly int Count;
        internal readonly float Seconds;
        internal readonly float EarlySeconds;
        internal readonly float LateSeconds;
        internal readonly float EarlyKills;
        internal readonly float LateKills;

        internal LevelPacing(
            int peakLevel,
            int count,
            float seconds,
            float earlySeconds,
            float lateSeconds,
            float earlyKills,
            float lateKills
        )
        {
            PeakLevel = peakLevel;
            Count = count;
            Seconds = seconds;
            EarlySeconds = earlySeconds;
            LateSeconds = lateSeconds;
            EarlyKills = earlyKills;
            LateKills = lateKills;
        }
    }

    /// <summary>How far out of the dungeon the run pushed, and how long each ring took.</summary>
    internal readonly struct DepthPacing
    {
        internal readonly int DeepestRing;
        internal readonly int Count;
        internal readonly float Seconds;

        internal DepthPacing(int deepestRing, int count, float seconds)
        {
            DeepestRing = deepestRing;
            Count = count;
            Seconds = seconds;
        }
    }

    /// <summary>How much of the run was spent hurt.</summary>
    internal readonly struct HealthPressure
    {
        internal readonly float Mean;
        internal readonly float Lowest;
        internal readonly float DangerShare;
        internal readonly float SafeShare;

        internal HealthPressure(float mean, float lowest, float dangerShare, float safeShare)
        {
            Mean = mean;
            Lowest = lowest;
            DangerShare = dangerShare;
            SafeShare = safeShare;
        }
    }
}
