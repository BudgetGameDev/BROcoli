using System.Collections.Generic;
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
        internal readonly float Duration;
        internal readonly float SecondsPerLevel;
        internal readonly float EarlySecondsPerLevel;
        internal readonly float LateSecondsPerLevel;
        internal readonly float EarlyKillsPerLevel;
        internal readonly float LateKillsPerLevel;
        internal readonly float MeanHealthFraction;
        internal readonly float LowestHealthFraction;
        internal readonly float DangerShare;
        internal readonly float SafeShare;

        internal ProgressionSummary(
            int peakLevel,
            int levels,
            int lives,
            int deaths,
            float duration,
            float secondsPerLevel,
            float earlySecondsPerLevel,
            float lateSecondsPerLevel,
            float earlyKillsPerLevel,
            float lateKillsPerLevel,
            float meanHealthFraction,
            float lowestHealthFraction,
            float dangerShare,
            float safeShare
        )
        {
            PeakLevel = peakLevel;
            Levels = levels;
            Lives = lives;
            Deaths = deaths;
            Duration = duration;
            SecondsPerLevel = secondsPerLevel;
            EarlySecondsPerLevel = earlySecondsPerLevel;
            LateSecondsPerLevel = lateSecondsPerLevel;
            EarlyKillsPerLevel = earlyKillsPerLevel;
            LateKillsPerLevel = lateKillsPerLevel;
            MeanHealthFraction = meanHealthFraction;
            LowestHealthFraction = lowestHealthFraction;
            DangerShare = dangerShare;
            SafeShare = safeShare;
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

    /// <summary>
    /// Grades a run's progression and pressure against the band a session is meant
    /// to sit in -- challenging without being punishing -- and says which way it
    /// missed. The bands are the game's difficulty target written down: a run
    /// outside one of them is the harness reporting a tuning regression, in the same
    /// way a missing feature is it reporting a coverage regression.
    /// </summary>
    internal static class ProgressionBalance
    {
        /// <summary>Levels counted as the opening of a run, for pacing comparisons.</summary>
        internal const int EarlyLevels = 5;

        /// <summary>A verdict needs a run long enough for its averages to mean something.</summary>
        internal const float MinSecondsToJudge = 240f;

        /// <summary>And enough levels that early and late are both represented.</summary>
        internal const int MinLevelsToJudge = 6;

        // A level every twenty-five seconds is confetti; one every two and a half
        // minutes is a bar the player stops looking at.
        internal const float MinSecondsPerLevel = 25f;
        internal const float MaxSecondsPerLevel = 150f;

        // Late levels should cost more than early ones, or the curve is not a curve.
        // Past about four times, the run has hit a wall rather than a slope.
        internal const float MinPaceRatio = 0.9f;
        internal const float MaxPaceRatio = 4f;

        // Mean health over the run. Near full means nothing in the dungeon is a
        // threat; below half means the run is one mistake from over at all times.
        internal const float MinMeanHealth = 0.45f;
        internal const float MaxMeanHealth = 0.9f;

        // Time spent under the danger line. Never is a game without tension; a
        // third of the session is a game that is only tension.
        internal const float MinDangerShare = 0.02f;
        internal const float MaxDangerShare = 0.35f;

        // A roguelite is meant to kill you, occasionally.
        internal const float MinDeathsPerHour = 0.4f;
        internal const float MaxDeathsPerHour = 8f;

        /// <summary>Deepest ring a judged run is expected to have reached.</summary>
        internal const int MinRingToJudgeScaling = 2;

        /// <summary>
        /// Grades the run. The findings are the whole output: a bare pass or fail
        /// says a number moved without saying which one or in which direction, and
        /// the point of measuring difficulty is to be told what to change.
        /// </summary>
        internal static List<string> Evaluate(
            ProgressionSummary progression,
            ScalingSummary scaling
        )
        {
            var findings = new List<string>();
            if (TooShortToJudge(progression, findings))
                return findings;

            Band(
                findings,
                "level pace",
                progression.SecondsPerLevel,
                MinSecondsPerLevel,
                MaxSecondsPerLevel,
                "s per level",
                "levels arrive too fast to be worth choosing",
                "levelling has slowed to a grind"
            );
            Band(
                findings,
                "curve shape",
                progression.PaceRatio,
                MinPaceRatio,
                MaxPaceRatio,
                "x late vs early level",
                "late levels are no dearer than the first ones",
                "the experience curve walls"
            );
            Band(
                findings,
                "health pressure",
                progression.MeanHealthFraction,
                MinMeanHealth,
                MaxMeanHealth,
                "mean health",
                "the run is fought at the edge of death throughout",
                "nothing in the dungeon threatens the player"
            );
            Band(
                findings,
                "close calls",
                progression.DangerShare,
                MinDangerShare,
                MaxDangerShare,
                "share of the run under the danger line",
                "the run is never in trouble",
                "the run is in trouble more often than not"
            );
            Band(
                findings,
                "deaths",
                progression.DeathsPerHour,
                MinDeathsPerHour,
                MaxDeathsPerHour,
                "deaths per hour",
                "the run cannot be lost",
                "the run is lost faster than it can be learned"
            );
            EvaluateScaling(scaling, findings);
            return findings;
        }

        internal static bool Passed(ProgressionSummary progression, ScalingSummary scaling) =>
            Evaluate(progression, scaling).Count == 0;

        private static bool TooShortToJudge(ProgressionSummary progression, List<string> findings)
        {
            if (progression.Duration < MinSecondsToJudge)
                findings.Add(
                    $"too short to judge: {Round(progression.Duration)}s of play, "
                        + $"{Round(MinSecondsToJudge)}s needed"
                );
            else if (progression.Levels < MinLevelsToJudge)
                findings.Add(
                    $"too few levels to judge: {progression.Levels} reached, "
                        + $"{MinLevelsToJudge} needed"
                );
            return findings.Count > 0;
        }

        /// <summary>
        /// Scaling is graded separately from pacing because it fails silently. A
        /// build whose depth multiplier stopped applying still levels the player on
        /// schedule and still kills them occasionally; what it stops doing is making
        /// the tenth room different from the first.
        /// </summary>
        private static void EvaluateScaling(ScalingSummary scaling, List<string> findings)
        {
            if (scaling.Rooms == 0)
            {
                findings.Add("no room ever spawned enemies, so scaling went unmeasured");
                return;
            }

            if (scaling.MaxRing < MinRingToJudgeScaling)
            {
                findings.Add(
                    $"the run never left ring {scaling.MaxRing}, so depth scaling went unmeasured"
                );
                return;
            }

            if (scaling.PeakHealthScale <= scaling.FirstHealthScale)
                findings.Add(
                    $"enemy health never scaled: {Round(scaling.PeakHealthScale)}x at the "
                        + $"deepest, {Round(scaling.FirstHealthScale)}x at the first room"
                );
        }

        /// <summary>Reports one measurement that left its band, and which end it left.</summary>
        private static void Band(
            List<string> findings,
            string name,
            float measured,
            float low,
            float high,
            string unit,
            string belowMeans,
            string aboveMeans
        )
        {
            if (measured < low)
                findings.Add(
                    $"{name} too low: {Round(measured)} {unit} (want {Round(low)} to "
                        + $"{Round(high)}) -- {belowMeans}"
                );
            else if (measured > high)
                findings.Add(
                    $"{name} too high: {Round(measured)} {unit} (want {Round(low)} to "
                        + $"{Round(high)}) -- {aboveMeans}"
                );
        }

        private static string Round(float value) =>
            value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
