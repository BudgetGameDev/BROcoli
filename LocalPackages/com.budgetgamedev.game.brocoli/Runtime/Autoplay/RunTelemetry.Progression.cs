using System.Collections.Generic;
using System.Text;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class RunTelemetry
    {
        /// <summary>How the run progressed, as of now. Named so a test can read it.</summary>
        internal ProgressionSummary Progression => _progression.Summarize(_elapsed);

        /// <summary>What the dungeon scaled its rooms to over the run.</summary>
        internal static ScalingSummary Scaling => AutoplayScalingLog.Summarize();

        /// <summary>
        /// Everything a difficulty verdict was drawn from, written out beside the
        /// verdict itself. A run that failed on pacing is only actionable if the
        /// numbers behind the finding are in the file next to it.
        /// </summary>
        private void AppendProgression(StringBuilder sb, List<string> findings)
        {
            ProgressionSummary progression = Progression;
            ScalingSummary scaling = Scaling;

            sb.Append("\"progression\":{");
            sb.Append("\"peakLevel\":").Append(progression.PeakLevel).Append(',');
            sb.Append("\"levels\":").Append(progression.Levels).Append(',');
            sb.Append("\"lives\":").Append(progression.Lives).Append(',');
            sb.Append("\"deaths\":").Append(progression.Deaths).Append(',');
            Num(sb, "deathsPerHour", progression.DeathsPerHour);
            sb.Append(',');
            Num(sb, "secondsPerLevel", progression.SecondsPerLevel);
            sb.Append(',');
            Num(sb, "earlySecondsPerLevel", progression.EarlySecondsPerLevel);
            sb.Append(',');
            Num(sb, "lateSecondsPerLevel", progression.LateSecondsPerLevel);
            sb.Append(',');
            Num(sb, "paceRatio", progression.PaceRatio);
            sb.Append(',');
            Num(sb, "earlyKillsPerLevel", progression.EarlyKillsPerLevel);
            sb.Append(',');
            Num(sb, "lateKillsPerLevel", progression.LateKillsPerLevel);
            sb.Append(',');
            Num(sb, "meanHealth", progression.MeanHealthFraction);
            sb.Append(',');
            Num(sb, "lowestHealth", progression.LowestHealthFraction);
            sb.Append(',');
            Num(sb, "dangerShare", progression.DangerShare);
            sb.Append(',');
            Num(sb, "safeShare", progression.SafeShare);
            sb.Append("},");

            sb.Append("\"scaling\":{");
            sb.Append("\"rooms\":").Append(scaling.Rooms).Append(',');
            sb.Append("\"maxRing\":").Append(scaling.MaxRing).Append(',');
            sb.Append("\"enemies\":").Append(scaling.Enemies).Append(',');
            sb.Append("\"mostEnemiesInARoom\":").Append(scaling.MostEnemiesInARoom).Append(',');
            Num(sb, "peakPlayerPower", scaling.PeakPlayerPower);
            sb.Append(',');
            Num(sb, "firstHealthScale", scaling.FirstHealthScale);
            sb.Append(',');
            Num(sb, "peakHealthScale", scaling.PeakHealthScale);
            sb.Append(',');
            Num(sb, "peakDamageScale", scaling.PeakDamageScale);
            sb.Append(',');
            Num(sb, "healthScaleGrowth", scaling.HealthScaleGrowth);
            sb.Append("},");

            AppendStrings(sb, "balanceFindings", findings);
        }
    }
}
