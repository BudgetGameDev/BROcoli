using System.Collections.Generic;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>One room's spawn, as the dungeon actually scaled it.</summary>
    internal readonly struct ScalingSample
    {
        internal readonly int Ring;
        internal readonly float PlayerPower;
        internal readonly float DepthScale;
        internal readonly float HealthPowerScale;
        internal readonly float DamageScale;
        internal readonly float CountScale;
        internal readonly float SpeedScale;
        internal readonly int Enemies;

        internal ScalingSample(
            int ring,
            float playerPower,
            float depthScale,
            float healthPowerScale,
            float damageScale,
            float countScale,
            float speedScale,
            int enemies
        )
        {
            Ring = ring;
            PlayerPower = playerPower;
            DepthScale = depthScale;
            HealthPowerScale = healthPowerScale;
            DamageScale = damageScale;
            CountScale = countScale;
            SpeedScale = speedScale;
            Enemies = enemies;
        }

        /// <summary>Everything the room did to enemy health: depth and power together.</summary>
        internal float HealthScale => DepthScale * HealthPowerScale;

        /// <summary>
        /// How dangerous one of the room's enemies is. Health alone measures how long
        /// a fight lasts; multiplying damage in is what makes it a measure of threat
        /// rather than of tedium.
        /// </summary>
        internal float Threat => HealthScale * DamageScale;

        /// <summary>
        /// The same, counting only what the player's own growth earned them. Depth is
        /// left out because a deeper ring is somewhere the player chose to walk, not
        /// the dungeon answering their build -- and it is graded on its own, as how
        /// far enemy health grew from the first room to the toughest.
        /// </summary>
        internal float PowerThreat => HealthPowerScale * DamageScale;

        /// <summary>Whether any of the power scales had run out of room to answer the player.</summary>
        internal bool Saturated =>
            EnemyScaling.AtCap(HealthPowerScale, EnemyScaling.MaxHealthPowerScale)
            || EnemyScaling.AtCap(DamageScale, EnemyScaling.MaxDamagePowerScale)
            || EnemyScaling.AtCap(CountScale, EnemyScaling.MaxCountPowerScale)
            || EnemyScaling.AtCap(SpeedScale, EnemyScaling.MaxSpeedScale);
    }

    /// <summary>
    /// Records what the dungeon did to each room's enemies as the run went on: the
    /// ring, the player's power score at that moment, and the depth, health, damage,
    /// and count multipliers the room was actually built with.
    ///
    /// The feature ledger says a system was reached; this says how hard it was set.
    /// Without it a run can only report that it fought enemies, which is true of a
    /// build whose scaling silently stopped applying.
    /// </summary>
    internal static class AutoplayScalingLog
    {
        private static readonly List<ScalingSample> Samples = new();

        internal static void Reset() => Samples.Clear();

        internal static int Count => Samples.Count;

        /// <summary>
        /// Records one room's spawn. Inert unless autoplay is driving, so the call
        /// sits in the dungeon's own spawn path without costing a normal session.
        /// </summary>
        internal static void Record(ScalingSample sample)
        {
            if (!AutoplayController.IsActive || sample.Enemies <= 0)
                return;

            Samples.Add(sample);
        }

        internal static ScalingSummary Summarize() => ScalingSummary.Of(Samples);
    }
}
