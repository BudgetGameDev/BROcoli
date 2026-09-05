using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// What a run's rooms added up to, in the shape a verdict reads. Scaling is worth
    /// summarising separately from pacing because it fails quietly: a build whose
    /// depth multiplier stopped applying still levels the player on schedule and still
    /// kills them occasionally, and what it stops doing is making the tenth room
    /// different from the first.
    /// </summary>
    internal readonly struct ScalingSummary
    {
        internal readonly int Rooms;
        internal readonly int MaxRing;
        internal readonly int Enemies;
        internal readonly int MostEnemiesInARoom;
        internal readonly float FirstPlayerPower;
        internal readonly float PeakPlayerPower;
        internal readonly float FirstHealthScale;
        internal readonly float PeakHealthScale;
        internal readonly float PeakDamageScale;
        internal readonly float PeakCountScale;
        internal readonly float PeakSpeedScale;
        internal readonly float FirstThreat;
        internal readonly float PeakThreat;
        internal readonly float FirstPowerThreat;
        internal readonly float PeakPowerThreat;
        internal readonly float SaturatedShare;
        internal readonly float SpeedCappedShare;

        private ScalingSummary(
            int rooms,
            int maxRing,
            int enemies,
            int mostEnemiesInARoom,
            ScalingSample first,
            ScalingSample peak,
            float peakPlayerPower,
            float peakHealthScale,
            float peakDamageScale,
            float peakCountScale,
            float peakSpeedScale,
            float peakPowerThreat,
            float saturatedShare,
            float speedCappedShare
        )
        {
            Rooms = rooms;
            MaxRing = maxRing;
            Enemies = enemies;
            MostEnemiesInARoom = mostEnemiesInARoom;
            FirstPlayerPower = first.PlayerPower;
            PeakPlayerPower = peakPlayerPower;
            FirstHealthScale = first.HealthScale;
            PeakHealthScale = peakHealthScale;
            PeakDamageScale = peakDamageScale;
            PeakCountScale = peakCountScale;
            PeakSpeedScale = peakSpeedScale;
            FirstThreat = first.Threat;
            PeakThreat = peak.Threat;
            FirstPowerThreat = first.PowerThreat;
            PeakPowerThreat = peakPowerThreat;
            SaturatedShare = saturatedShare;
            SpeedCappedShare = speedCappedShare;
        }

        /// <summary>How much tougher the deepest room's enemies were than the first's.</summary>
        internal float HealthScaleGrowth => PeakHealthScale / Mathf.Max(0.0001f, FirstHealthScale);

        /// <summary>And how much more dangerous, counting the damage they deal.</summary>
        internal float ThreatGrowth => PeakThreat / Mathf.Max(0.0001f, FirstThreat);

        /// <summary>How much stronger the player got over the same stretch.</summary>
        internal float PowerGrowth => PeakPlayerPower / Mathf.Max(0.0001f, FirstPlayerPower);

        /// <summary>How much of that the player's own growth alone accounts for.</summary>
        internal float PowerThreatGrowth => PeakPowerThreat / Mathf.Max(0.0001f, FirstPowerThreat);

        /// <summary>
        /// Whether the dungeon answered the player, and by how much: the exponent
        /// relating enemy threat to player power, so a run reports the feedback
        /// strength the game was actually built with rather than a ratio that means
        /// something different at every power level.
        ///
        /// One is a treadmill -- every upgrade answered in full, so nothing the
        /// player earns is ever felt. Near zero is a feedback path that has stopped
        /// applying, or one whose ceilings the run has already run into. The band
        /// between them is the whole design.
        /// </summary>
        internal float TrackingRatio =>
            PowerGrowth <= 1.0001f ? 1f : Mathf.Log(PowerThreatGrowth) / Mathf.Log(PowerGrowth);

        /// <summary>
        /// A room that was never built. A run that spawned nothing reads as unscaled
        /// rather than as collapsed to zero, so an empty ledger reports that scaling
        /// went unmeasured instead of reporting that it failed.
        /// </summary>
        private static readonly ScalingSample Unscaled = new(0, 1f, 1f, 1f, 1f, 1f, 1f, 0);

        /// <summary>Folds a run's rooms into the numbers a verdict is drawn from.</summary>
        internal static ScalingSummary Of(IReadOnlyList<ScalingSample> samples)
        {
            if (samples == null || samples.Count == 0)
                return new ScalingSummary(
                    0,
                    0,
                    0,
                    0,
                    Unscaled,
                    Unscaled,
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    1f,
                    0f,
                    0f
                );

            ScalingSample first = samples[0];
            ScalingSample peak = first;
            int maxRing = 0;
            int enemies = 0;
            int mostInARoom = 0;
            int saturated = 0;
            int speedCapped = 0;
            float peakPower = 0f;
            float peakHealth = 0f;
            float peakDamage = 0f;
            float peakCount = 0f;
            float peakSpeed = 0f;
            float peakPowerThreat = 0f;
            foreach (ScalingSample sample in samples)
            {
                maxRing = Mathf.Max(maxRing, sample.Ring);
                enemies += sample.Enemies;
                mostInARoom = Mathf.Max(mostInARoom, sample.Enemies);
                peakPower = Mathf.Max(peakPower, sample.PlayerPower);
                peakHealth = Mathf.Max(peakHealth, sample.HealthScale);
                peakDamage = Mathf.Max(peakDamage, sample.DamageScale);
                peakCount = Mathf.Max(peakCount, sample.CountScale);
                peakSpeed = Mathf.Max(peakSpeed, sample.SpeedScale);
                peakPowerThreat = Mathf.Max(peakPowerThreat, sample.PowerThreat);
                if (sample.Saturated)
                    saturated++;
                if (sample.SpeedCapped)
                    speedCapped++;
                if (sample.Threat > peak.Threat)
                    peak = sample;
            }

            return new ScalingSummary(
                samples.Count,
                maxRing,
                enemies,
                mostInARoom,
                first,
                peak,
                peakPower,
                peakHealth,
                peakDamage,
                peakCount,
                peakSpeed,
                peakPowerThreat,
                saturated / (float)samples.Count,
                speedCapped / (float)samples.Count
            );
        }
    }
}
