using UnityEngine;

namespace BudgetGameDev.Autoplay
{
    /// <summary>How a run progressed and how hard it was pressed while doing it.</summary>
    public readonly struct ProgressionSummary
    {
        public readonly int PeakLevel;
        public readonly int Levels;
        public readonly int Lives;
        public readonly int Deaths;
        public readonly int DeepestRing;
        public readonly int Rings;
        public readonly float Duration;
        public readonly float SecondsPerLevel;
        public readonly float EarlySecondsPerLevel;
        public readonly float LateSecondsPerLevel;
        public readonly float EarlyKillsPerLevel;
        public readonly float LateKillsPerLevel;
        public readonly float SecondsPerRing;
        public readonly float MeanHealthFraction;
        public readonly float LowestHealthFraction;
        public readonly float DangerShare;
        public readonly float SafeShare;

        public ProgressionSummary(
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

        public float DeathsPerHour => Deaths / Mathf.Max(1f / 3600f, Duration / 3600f);

        /// <summary>
        /// How much slower a late level is than an early one. This is the number that
        /// says whether a curve walls: one means the run never slows down, and a
        /// large one means the bar stopped moving while the rooms kept deepening.
        /// </summary>
        public float PaceRatio =>
            LateSecondsPerLevel <= 0f || EarlySecondsPerLevel <= 0f
                ? 1f
                : LateSecondsPerLevel / EarlySecondsPerLevel;
    }

    /// <summary>What the run's levels cost, split into the opening and everything after.</summary>
    public readonly struct LevelPacing
    {
        public readonly int PeakLevel;
        public readonly int Count;
        public readonly float Seconds;
        public readonly float EarlySeconds;
        public readonly float LateSeconds;
        public readonly float EarlyKills;
        public readonly float LateKills;

        public LevelPacing(
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
    public readonly struct DepthPacing
    {
        public readonly int DeepestRing;
        public readonly int Count;
        public readonly float Seconds;

        public DepthPacing(int deepestRing, int count, float seconds)
        {
            DeepestRing = deepestRing;
            Count = count;
            Seconds = seconds;
        }
    }

    /// <summary>How much of the run was spent hurt.</summary>
    public readonly struct HealthPressure
    {
        public readonly float Mean;
        public readonly float Lowest;
        public readonly float DangerShare;
        public readonly float SafeShare;

        public HealthPressure(float mean, float lowest, float dangerShare, float safeShare)
        {
            Mean = mean;
            Lowest = lowest;
            DangerShare = dangerShare;
            SafeShare = safeShare;
        }
    }
}
