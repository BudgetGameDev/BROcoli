using System.Collections.Generic;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// The scaling half of the verdict. Scaling is graded separately from pacing
    /// because it fails silently: a build whose depth multiplier stopped applying
    /// still levels the player on schedule and still kills them occasionally, and what
    /// it stops doing is making the tenth room different from the first. Pacing and
    /// pressure both read the same on that build as on a working one.
    /// </summary>
    internal static partial class ProgressionBalance
    {
        /// <summary>Deepest ring a judged run is expected to have reached.</summary>
        internal const int MinRingToJudgeScaling = 2;

        // How much tougher the deepest room's enemies are than the first's. Below the
        // floor the dungeon is flat; above the ceiling it answers a build faster than
        // any build can be assembled.
        internal const float MinHealthGrowth = 1.5f;
        internal const float MaxHealthGrowth = 6f;

        // And how much harder they hit. Damage is the multiplier that makes a deep
        // room dangerous rather than merely long, so it is graded on its own.
        internal const float MinDamageGrowth = 1.2f;
        internal const float MaxDamageGrowth = 3f;

        /// <summary>
        /// Player power growth below which the tracking exponent is noise rather than
        /// a measurement: dividing one log by another needs both to be a real number.
        /// </summary>
        internal const float MinPowerGrowthToJudgeTracking = 1.5f;

        // How much of the player's own growth the dungeon answers, as an exponent:
        // enemy threat grows as player power to this power. One is a treadmill --
        // every upgrade answered in full, so nothing the player earns is ever felt.
        // Well under one is a run the player walks out of, still fighting the first
        // room's enemies with a build many times what beat them.
        internal const float MinTrackingRatio = 0.6f;
        internal const float MaxTrackingRatio = 1.15f;

        /// <summary>
        /// Share of rooms built against a scaling ceiling. A run that spends most of
        /// its rooms pinned against one has stopped scaling for the rest of the
        /// session, whatever the player does next.
        /// </summary>
        internal const float MaxSaturatedShare = 0.35f;

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

            Band(
                findings,
                "enemy health",
                scaling.HealthScaleGrowth,
                MinHealthGrowth,
                MaxHealthGrowth,
                "x from the first room to the toughest",
                "the deepest room is the first room with different furniture",
                "enemy health outgrows anything a build can be assembled to cut through"
            );
            Band(
                findings,
                "enemy damage",
                scaling.PeakDamageScale,
                MinDamageGrowth,
                MaxDamageGrowth,
                "x at the hardest",
                "depth makes fights longer without making any of them dangerous",
                "a deep room takes the player out faster than they can read it"
            );
            EvaluateTracking(scaling, findings);

            if (float.IsNaN(scaling.SaturatedShare) || float.IsInfinity(scaling.SaturatedShare))
                findings.Add("scaling headroom invalid: a finite measurement is required");
            else if (scaling.SaturatedShare > MaxSaturatedShare)
                findings.Add(
                    $"scaling headroom too low: {Round(scaling.SaturatedShare * 100f)}% of rooms "
                        + $"built against a ceiling (want under {Round(MaxSaturatedShare * 100f)}%)"
                        + " -- the dungeon has stopped answering the player at all"
                );
        }

        /// <summary>
        /// Whether the dungeon kept up with the player. This is the measurement the
        /// rest of scaling exists to support: health and damage can both grow on
        /// schedule and still leave a run trivial, if the build they are answering
        /// grew faster than either of them.
        /// </summary>
        private static void EvaluateTracking(ScalingSummary scaling, List<string> findings)
        {
            if (scaling.PowerGrowth < MinPowerGrowthToJudgeTracking)
            {
                findings.Add(
                    $"the player only ever got {Round(scaling.PowerGrowth)}x stronger, so "
                        + "there was nothing for scaling to answer"
                );
                return;
            }

            Band(
                findings,
                "difficulty tracking",
                scaling.TrackingRatio,
                MinTrackingRatio,
                MaxTrackingRatio,
                $"of the player's {Round(scaling.PowerGrowth)}x growth answered",
                "the player outgrows the dungeon and walks the rest of the run",
                "every upgrade is answered in full, so none of them is ever felt"
            );
        }
    }
}
