using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private static readonly Vector3 LowBarrierScale = new(1.65f, 0.65f, 1.65f);
        private static readonly Vector3 CaveBoundaryScale = new(1.45f, 1.15f, 1.45f);

        /// <summary>
        /// Adds the visual boundary owned by a broad environment theme. Dungeon
        /// railings are structural and are built by DungeonRoomBuilder; cave
        /// boundaries use the available rocks. The remaining categories are
        /// intentionally no-ops until their own assets are assigned here.
        /// </summary>
        public void BuildBoundaryDressing(
            Transform parent,
            Vector2Int room,
            int direction,
            DungeonLayout.EnvironmentTheme environment,
            System.Random random
        )
        {
            if (environment != DungeonLayout.EnvironmentTheme.Cave)
                return;

            GameObject rocks = FindProp(DungeonPropTokens.Rocks);
            GameObject stones = FindProp(DungeonPropTokens.Stones);
            for (int i = 0; i < 4; i++)
            {
                GameObject prefab = (i & 1) == 0 ? rocks : stones;
                if (prefab == null)
                    prefab = rocks != null ? rocks : stones;
                if (prefab == null)
                    return;

                float radius = Measure(prefab).Radius * CaveBoundaryScale.x;
                float halfRun = direction is DungeonLayout.North or DungeonLayout.South
                    ? HalfRoomWidth
                    : HalfRoomDepth;
                float available = Mathf.Max(0f, halfRun - radius - 0.3f);
                float along = Mathf.Lerp(-available, available, (i + 0.5f) / 4f);
                along += Mathf.Lerp(-0.3f, 0.3f, (float)random.NextDouble());
                SpawnScaledProp(
                    parent,
                    prefab,
                    BoundaryDressingSpot(room, direction, along, radius),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    CaveBoundaryScale
                );
            }
        }

        internal static Vector2 BoundaryDressingSpot(
            Vector2Int room,
            int direction,
            float along,
            float radius
        )
        {
            Vector2Int gridOutward = DungeonLayout.DirectionOffsets[direction];
            var outward = new Vector2(gridOutward.x, gridOutward.y);
            Vector2 tangent = direction is DungeonLayout.North or DungeonLayout.South
                ? Vector2.right
                : Vector2.up;
            float halfDepth = direction is DungeonLayout.North or DungeonLayout.South
                ? HalfRoomDepth
                : HalfRoomWidth;
            float halfRun = direction is DungeonLayout.North or DungeonLayout.South
                ? HalfRoomWidth
                : HalfRoomDepth;
            float safeRadius = Mathf.Max(0f, radius);
            float safeAlong = Mathf.Clamp(along, -halfRun + safeRadius, halfRun - safeRadius);
            return DungeonLayout.RoomCenter(room)
                + outward * (halfDepth - safeRadius - 0.25f)
                + tangent * safeAlong;
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
            if (
                archetype.Environment != DungeonLayout.EnvironmentTheme.Cave
                || archetype.Shape != DungeonLayout.RoomShape.DiagonalGallery
            )
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
