using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private static readonly Vector3 LowBarrierScale = new(1.65f, 0.65f, 1.65f);
        private static readonly Vector3 BoundaryRockScale = new(1.1f, 0.9f, 1.1f);

        /// <summary>
        /// Adds the dressing a broad environment theme puts on the platform
        /// boundary. The structural facade below it is built by
        /// DungeonRoomBuilder on every boundary edge; rock-line themes stand
        /// their profile's boundary props along it, and the other themes are
        /// intentionally no-ops until their own assets are assigned in
        /// <see cref="DungeonEnvironmentProfile"/>.
        /// </summary>
        public void BuildBoundaryDressing(
            Transform parent,
            Vector2Int room,
            int direction,
            DungeonLayout.EnvironmentTheme environment,
            System.Random random
        )
        {
            DungeonEnvironmentProfile profile = DungeonEnvironmentProfile.Of(environment);
            if (profile.BoundaryStyle != DungeonBoundaryStyle.RockLine)
                return;

            int count = profile.BoundaryPropsPerEdge;
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = FindProp(
                    profile.BoundaryTokens[i % profile.BoundaryTokens.Length]
                );
                if (prefab == null)
                    prefab = FindProp(profile.BoundaryTokens[0]);
                if (prefab == null)
                    return;

                float radius = Measure(prefab).Radius * BoundaryRockScale.x;
                float halfRun = direction is DungeonLayout.North or DungeonLayout.South
                    ? HalfRoomWidth
                    : HalfRoomDepth;
                float available = Mathf.Max(0f, halfRun - radius - 0.3f);
                float along = Mathf.Lerp(-available, available, (i + 0.5f) / count);
                along += Mathf.Lerp(-0.3f, 0.3f, (float)random.NextDouble());
                SpawnScaledProp(
                    parent,
                    prefab,
                    BoundaryDressingSpot(room, direction, along, radius),
                    GroundPlane.YawRotation(random.Next(0, 360)),
                    BoundaryRockScale
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
            // The deeper inset keeps a prop's full base visibly on the floor
            // slab even at the camera-facing cliff, where anything flush with
            // the lip reads as hanging over the void.
            float safeRadius = Mathf.Max(0f, radius);
            float safeAlong = Mathf.Clamp(along, -halfRun + safeRadius, halfRun - safeRadius);
            return DungeonLayout.RoomCenter(room)
                + outward * (halfDepth - safeRadius - 0.6f)
                + tangent * safeAlong;
        }

        /// <summary>
        /// Rock-themed environments scatter broken rubble on the outer
        /// shoulders of a diagonal gallery's railing lane, so the masonry
        /// diagonal reads grown-through rather than freshly built. Spots that
        /// would land on the railings themselves are skipped.
        /// </summary>
        private void BuildVisibilityFriendlyBarriers(
            Transform parent,
            Vector2 center,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied
        )
        {
            DungeonEnvironmentProfile profile = DungeonEnvironmentProfile.Of(archetype.Environment);
            if (
                !profile.UsesRubbleBarriers
                || archetype.Shape != DungeonLayout.RoomShape.DiagonalGallery
            )
                return;

            bool mirrored = (archetype.Variant & 1) != 0;
            foreach (Vector2 spot in new[] { new Vector2(-7f, 3.2f), new Vector2(7f, -3.2f) })
            {
                Vector2 local = mirrored ? new Vector2(-spot.x, spot.y) : spot;
                PlaceLowBarrier(parent, center, local, archetype, random, occupied, profile);
            }
        }

        private void PlaceLowBarrier(
            Transform parent,
            Vector2 center,
            Vector2 local,
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied,
            DungeonEnvironmentProfile profile
        )
        {
            string[] tokens = profile.RubbleTokens;
            GameObject prefab = FindProp(
                random.NextDouble() < 0.72 ? tokens[0] : tokens[Mathf.Min(1, tokens.Length - 1)]
            );
            if (prefab == null)
                return;

            DungeonPropMeasurement measurement = Measure(prefab);
            if (OverlapsInteriorWall(local, measurement.Radius * LowBarrierScale.x, archetype))
                return;

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
