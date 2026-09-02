using System;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    public static partial class AutoplayRunner
    {
        /// <summary>
        /// The pacing read-out. A run's verdict says whether the difficulty landed in
        /// band; these are the numbers it landed on, so a failing tier can be tuned
        /// without opening the summary file.
        /// </summary>
        internal static string DescribeProgression(RunSummary summary, IFormatProvider invariant)
        {
            ProgressionRecord progression = summary.progression;
            if (progression == null || progression.levels == 0)
                return "  pacing:     no level was reached";

            return $"  pacing:     level {progression.peakLevel} over "
                + $"{progression.levels} level-up(s), "
                + $"{Number(progression.secondsPerLevel, invariant)}s each "
                + $"({Number(progression.earlySecondsPerLevel, invariant)}s early, "
                + $"{Number(progression.lateSecondsPerLevel, invariant)}s late, "
                + $"{Number(progression.paceRatio, invariant)}x), "
                + $"{Number(progression.earlyKillsPerLevel, invariant)} to "
                + $"{Number(progression.lateKillsPerLevel, invariant)} kills per level"
                + Environment.NewLine
                + $"  pressure:   {Percent(progression.meanHealth, invariant)} mean health, "
                + $"{Percent(progression.lowestHealth, invariant)} at the worst, "
                + $"{Percent(progression.dangerShare, invariant)} of the run in danger, "
                + $"{progression.deaths} death(s) over {progression.lives} life/lives "
                + $"({Number(progression.deathsPerHour, invariant)}/hour)";
        }

        /// <summary>What the dungeon actually set each room to, as the run went deeper.</summary>
        internal static string DescribeScaling(RunSummary summary, IFormatProvider invariant)
        {
            ScalingRecord scaling = summary.scaling;
            if (scaling == null || scaling.rooms == 0)
                return "  scaling:    no room spawned enemies";

            return $"  scaling:    {scaling.rooms} room(s) out to ring {scaling.maxRing}, "
                + $"{scaling.enemies} enemies (up to {scaling.mostEnemiesInARoom} at once), "
                + $"player power {Number(scaling.peakPlayerPower, invariant)}x, "
                + $"enemy health {Number(scaling.firstHealthScale, invariant)}x to "
                + $"{Number(scaling.peakHealthScale, invariant)}x, damage up to "
                + $"{Number(scaling.peakDamageScale, invariant)}x";
        }

        /// <summary>The verdict itself: which bands the run left, and which way.</summary>
        internal static string DescribeBalance(RunSummary summary)
        {
            if (summary.balanceFindings == null || summary.balanceFindings.Length == 0)
                return "  balance:    in band";

            return "  balance:    "
                + string.Join(Environment.NewLine + "              ", summary.balanceFindings);
        }

        private static string Percent(float fraction, IFormatProvider invariant) =>
            (fraction * 100f).ToString("0", invariant) + "%";
    }
}
