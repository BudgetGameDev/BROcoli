using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private static readonly Vector3 LowBarrierScale = new(1.65f, 0.65f, 1.65f);

        /// <summary>
        /// Breaks up the south cliff with low boulder clusters. Their broad
        /// colliders keep the player away from the lip, while their deliberately
        /// low profile stays below automatic occlusion-fade height.
        /// </summary>
        public void BuildSouthCliffDressing(
            Transform parent,
            DungeonEdge edge,
            System.Random random
        )
        {
            GameObject rocks = FindProp(DungeonPropTokens.Rocks);
            GameObject stones = FindProp(DungeonPropTokens.Stones);
            Vector2 edgeCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
            edgeCenter.y += DungeonLayout.RoomDepth * 0.5f + 1.05f;

            for (int i = -2; i <= 2; i++)
            {
                // Keep one clear lookout in the middle: the parapet itself still
                // blocks the drop, while the side clusters discourage walking the
                // whole lip and preserve a readable silhouette around the player.
                if (i == 0)
                    continue;
                GameObject prefab = (i & 1) == 0 ? rocks : stones;
                if (prefab == null)
                    prefab = rocks != null ? rocks : stones;
                float jitter = Mathf.Lerp(-0.35f, 0.35f, (float)random.NextDouble());
                SpawnScaledProp(
                    parent,
                    prefab,
                    edgeCenter + new Vector2(i * 5.1f + jitter, 0f),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    LowBarrierScale
                );
            }
        }

        /// <summary>
        /// Replaces camera-facing interior wall runs with low, broad rubble. The
        /// room keeps its authored shape and collision boundary, but a character
        /// remains visible over it and the visibility system has nothing to lower.
        /// Diagonal galleries use the same language to carve a non-rectangular
        /// playable outline without introducing a diagonal full-height wall.
        /// </summary>
        private void BuildVisibilityFriendlyBarriers(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            float southRun = archetype.Shape switch
            {
                DungeonLayout.RoomShape.Tiny => -4f,
                DungeonLayout.RoomShape.Compact => -6f,
                DungeonLayout.RoomShape.NarrowHorizontal => -4f,
                DungeonLayout.RoomShape.LongHorizontal => -6f,
                _ => float.NaN,
            };

            if (!float.IsNaN(southRun))
            {
                foreach (float x in new[] { -8f, -4f, 4f, 8f })
                    PlaceLowBarrier(parent, center, new Vector2(x, southRun), random, occupied);
            }

            if (archetype.Shape != DungeonLayout.RoomShape.DiagonalGallery)
                return;

            foreach (float x in new[] { -8f, -4f, 4f, 8f })
            {
                float z = -4.5f + x * 0.25f;
                PlaceLowBarrier(parent, center, new Vector2(x, z), random, occupied);
            }
        }

        private void PlaceLowBarrier(
            Transform parent,
            Vector2 center,
            Vector2 local,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            GameObject prefab = FindProp(
                random.NextDouble() < 0.72 ? DungeonPropTokens.Rocks : DungeonPropTokens.Stones
            );
            if (prefab == null)
                return;

            DungeonPropMeasurement measurement = Measure(prefab);
            SpawnScaledProp(
                parent,
                prefab,
                center + local,
                GroundPlane.YawRotation(random.Next(0, 360)),
                LowBarrierScale
            );
            occupied.Add(new OccupiedSpot(local, measurement.Radius * LowBarrierScale.x, true));
        }
    }
}
