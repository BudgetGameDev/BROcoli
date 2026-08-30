using System;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Works out which wall pieces have to fade as one unit.
    ///
    /// A freestanding interior structure is read as a single object: where an
    /// interior run crosses or meets another, lowering one arm and leaving the
    /// other standing reads as a bug, so touching interior runs are fused into one
    /// visibility group. Room-boundary runs are deliberately left alone - a room's
    /// south wall dropping must not take the east and west walls with it.
    /// </summary>
    public static class DungeonWallGrouping
    {
        /// <summary>How near two slabs must come to count as one structure.</summary>
        public const float ContactTolerance = 0.05f;

        /// <summary>
        /// Maps every interior section name in <paramref name="walls"/> to the
        /// group it fades with. The representative is the lowest section name in
        /// the group, so the result does not depend on iteration order.
        /// </summary>
        public static Dictionary<string, string> ResolveInteriorGroups(
            IReadOnlyList<DungeonWallPiece> walls
        )
        {
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DungeonWallPiece piece in walls)
            {
                if (piece.Kind == DungeonWallKind.Interior)
                    parent.TryAdd(piece.Section, piece.Section);
            }

            for (int i = 0; i < walls.Count; i++)
            for (int j = i + 1; j < walls.Count; j++)
            {
                DungeonWallPiece first = walls[i];
                DungeonWallPiece second = walls[j];
                if (
                    first.Kind != DungeonWallKind.Interior
                    || second.Kind != DungeonWallKind.Interior
                    || string.Equals(first.Section, second.Section, StringComparison.Ordinal)
                    || !AreInContact(first, second)
                )
                    continue;

                Union(parent, first.Section, second.Section);
            }

            var groups = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string section in parent.Keys)
                groups[section] = Find(parent, section);
            return groups;
        }

        /// <summary>True when two slabs overlap or meet within the tolerance.</summary>
        public static bool AreInContact(DungeonWallPiece first, DungeonWallPiece second)
        {
            Rect a = first.Footprint;
            Rect b = second.Footprint;
            return Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > -ContactTolerance
                && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > -ContactTolerance;
        }

        private static string Find(Dictionary<string, string> parent, string section)
        {
            string root = section;
            while (!string.Equals(parent[root], root, StringComparison.Ordinal))
                root = parent[root];
            while (!string.Equals(parent[section], root, StringComparison.Ordinal))
            {
                string next = parent[section];
                parent[section] = root;
                section = next;
            }
            return root;
        }

        private static void Union(Dictionary<string, string> parent, string first, string second)
        {
            string firstRoot = Find(parent, first);
            string secondRoot = Find(parent, second);
            if (string.Equals(firstRoot, secondRoot, StringComparison.Ordinal))
                return;

            if (string.CompareOrdinal(firstRoot, secondRoot) <= 0)
                parent[secondRoot] = firstRoot;
            else
                parent[firstRoot] = secondRoot;
        }
    }
}
