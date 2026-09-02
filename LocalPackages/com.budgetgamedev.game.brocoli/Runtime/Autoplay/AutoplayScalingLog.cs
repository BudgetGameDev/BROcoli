using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>One room's spawn, as the dungeon actually scaled it.</summary>
    internal readonly struct ScalingSample
    {
        internal readonly int Ring;
        internal readonly float PlayerPower;
        internal readonly float HealthScale;
        internal readonly float DamageScale;
        internal readonly int Enemies;

        internal ScalingSample(
            int ring,
            float playerPower,
            float healthScale,
            float damageScale,
            int enemies
        )
        {
            Ring = ring;
            PlayerPower = playerPower;
            HealthScale = healthScale;
            DamageScale = damageScale;
            Enemies = enemies;
        }
    }

    /// <summary>What a run's rooms added up to, in the shape a verdict reads.</summary>
    internal readonly struct ScalingSummary
    {
        internal readonly int Rooms;
        internal readonly int MaxRing;
        internal readonly int Enemies;
        internal readonly int MostEnemiesInARoom;
        internal readonly float PeakPlayerPower;
        internal readonly float FirstHealthScale;
        internal readonly float PeakHealthScale;
        internal readonly float PeakDamageScale;

        internal ScalingSummary(
            int rooms,
            int maxRing,
            int enemies,
            int mostEnemiesInARoom,
            float peakPlayerPower,
            float firstHealthScale,
            float peakHealthScale,
            float peakDamageScale
        )
        {
            Rooms = rooms;
            MaxRing = maxRing;
            Enemies = enemies;
            MostEnemiesInARoom = mostEnemiesInARoom;
            PeakPlayerPower = peakPlayerPower;
            FirstHealthScale = firstHealthScale;
            PeakHealthScale = peakHealthScale;
            PeakDamageScale = peakDamageScale;
        }

        /// <summary>How much tougher the last room's enemies were than the first's.</summary>
        internal float HealthScaleGrowth => PeakHealthScale / Mathf.Max(0.0001f, FirstHealthScale);
    }

    /// <summary>
    /// Records what the dungeon did to each room's enemies as the run went on: the
    /// ring, the player's power score at that moment, and the health, damage, and
    /// count multipliers the room was actually built with.
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
        internal static void Record(
            int ring,
            float playerPower,
            float healthScale,
            float damageScale,
            int enemies
        )
        {
            if (!AutoplayController.IsActive || enemies <= 0)
                return;

            Samples.Add(new ScalingSample(ring, playerPower, healthScale, damageScale, enemies));
        }

        internal static ScalingSummary Summarize()
        {
            if (Samples.Count == 0)
                return new ScalingSummary(0, 0, 0, 0, 0f, 1f, 1f, 1f);

            int maxRing = 0;
            int enemies = 0;
            int mostInARoom = 0;
            float peakPower = 0f;
            float peakHealth = 0f;
            float peakDamage = 0f;
            foreach (ScalingSample sample in Samples)
            {
                maxRing = Mathf.Max(maxRing, sample.Ring);
                enemies += sample.Enemies;
                mostInARoom = Mathf.Max(mostInARoom, sample.Enemies);
                peakPower = Mathf.Max(peakPower, sample.PlayerPower);
                peakHealth = Mathf.Max(peakHealth, sample.HealthScale);
                peakDamage = Mathf.Max(peakDamage, sample.DamageScale);
            }

            return new ScalingSummary(
                Samples.Count,
                maxRing,
                enemies,
                mostInARoom,
                peakPower,
                Samples[0].HealthScale,
                peakHealth,
                peakDamage
            );
        }
    }
}
