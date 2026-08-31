using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private static readonly Vector3 LowBarrierScale = new(1.65f, 0.65f, 1.65f);
        private static readonly Vector3 OutcropScale = new(2.1f, 1.7f, 2.1f);

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
            Vector2 boundary = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
            boundary.y += DungeonLayout.RoomDepth * 0.5f;
            Vector2 lip = boundary + Vector2.up * 1.05f;

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
                    lip + new Vector2(i * 5.1f + jitter, 0f),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    LowBarrierScale
                );
            }

            // Rock shoulders jut from the cliff face beyond the parapet, mostly
            // sunk below floor level. They break the masonry's straight silhouette
            // where it meets the void, selling the drop as natural terrain the
            // platform was carved from - and, standing outside the playable floor,
            // they can never stand between the camera and a character.
            for (int i = -2; i <= 2; i++)
            {
                // An uneven line: some positions stay bare, and every shoulder
                // rolls its own bulk and depth, so the lip reads as broken
                // terrain rather than a second, rockier parapet.
                bool bare = random.NextDouble() < 0.25;
                GameObject prefab = (i & 1) == 0 ? stones : rocks;
                if (prefab == null)
                    prefab = rocks != null ? rocks : stones;
                float jitter = Mathf.Lerp(-0.6f, 0.6f, (float)random.NextDouble());
                float reach = Mathf.Lerp(-2.3f, -1.4f, (float)random.NextDouble());
                float sink = Mathf.Lerp(0.7f, 1.4f, (float)random.NextDouble());
                float bulk = Mathf.Lerp(0.75f, 1.25f, (float)random.NextDouble());
                if (!bare)
                    SpawnScaledProp(
                        parent,
                        prefab,
                        boundary + new Vector2(i * 4.6f + jitter, reach),
                        GroundPlane.YawRotation(random.Next(0, 360)),
                        OutcropScale * bulk,
                        -sink
                    );
            }
        }

        /// <summary>
        /// Gives diagonal galleries a broken, low rubble line instead of the
        /// axial masonry railings used by the other shaped rooms. Its angle keeps
        /// this archetype's route loose and visibly distinct.
        /// </summary>
        private void BuildVisibilityFriendlyBarriers(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
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
