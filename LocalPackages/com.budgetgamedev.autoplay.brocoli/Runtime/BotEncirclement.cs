using System;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Reads how boxed in the agent is, and which way is out.
    ///
    /// Backing away from the middle of a crowd works while the crowd is on one side
    /// of the agent, and stops meaning anything the moment it is on every side: the
    /// centre of a ring is where the agent is standing, so the direction away from it
    /// is noise, and an agent steering by it drifts on the spot until the ring closes.
    /// Getting out of a ring is a different question -- which way is thinnest -- and
    /// it has to be asked before the ring is a ring.
    /// </summary>
    internal static class BotEncirclement
    {
        /// <summary>Directions the surrounding space is divided into.</summary>
        internal const int Sectors = 12;

        /// <summary>
        /// How much of the surrounding space has something in it, from nothing to
        /// completely enclosed, and the middle of the widest clear arc.
        /// </summary>
        internal static void Measure(
            Vector2 position,
            IReadOnlyList<Vector2> threats,
            float radius,
            out float coverage,
            out Vector2 escape
        )
        {
            coverage = 0f;
            escape = Vector2.zero;
            if (threats == null || threats.Count == 0 || radius <= 0f)
                return;

            Span<bool> blocked = stackalloc bool[Sectors];
            int blockedCount = 0;
            for (int index = 0; index < threats.Count; index++)
            {
                Vector2 offset = threats[index] - position;
                float distance = offset.magnitude;
                if (distance < 0.001f || distance > radius)
                    continue;

                int sector = SectorOf(offset);
                // A body near the agent blocks the way past it as well as the way
                // through it, so it shades its neighbours too.
                int spread = distance < radius * 0.5f ? 1 : 0;
                for (int step = -spread; step <= spread; step++)
                {
                    int at = ((sector + step) % Sectors + Sectors) % Sectors;
                    if (blocked[at])
                        continue;
                    blocked[at] = true;
                    blockedCount++;
                }
            }

            if (blockedCount == 0)
                return;

            coverage = blockedCount / (float)Sectors;
            escape = WidestGap(blocked, blockedCount);
        }

        internal static int SectorOf(Vector2 offset)
        {
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            int sector = Mathf.FloorToInt((angle + 360f) % 360f / (360f / Sectors));
            return Mathf.Clamp(sector, 0, Sectors - 1);
        }

        /// <summary>The middle of the longest unblocked run of sectors.</summary>
        private static Vector2 WidestGap(Span<bool> blocked, int blockedCount)
        {
            if (blockedCount >= Sectors)
                return Vector2.zero;

            int bestStart = -1;
            int bestLength = 0;
            int start = -1;
            int length = 0;
            // Twice round, so a gap that straddles the seam is measured whole.
            for (int step = 0; step < Sectors * 2; step++)
            {
                int at = step % Sectors;
                if (blocked[at])
                {
                    start = -1;
                    length = 0;
                    continue;
                }

                if (start < 0)
                    start = at;
                if (++length <= bestLength)
                    continue;

                bestLength = length;
                bestStart = start;
            }

            if (bestStart < 0)
                return Vector2.zero;

            float middle = (bestStart + (bestLength - 1) * 0.5f) % Sectors;
            float radians = (middle + 0.5f) * (360f / Sectors) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
