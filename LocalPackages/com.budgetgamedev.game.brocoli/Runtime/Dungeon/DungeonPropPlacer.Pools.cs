using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        // Combined renderer footprint of DungeonWater.prefab, with a small visual gap.
        private const float PoolHalfWidth = 3.02f;
        private const float PoolHalfDepth = 2.38f;
        private const float PoolGap = 0.18f;
        private const int PoolPlacementAttempts = 48;

        private readonly struct PoolPlacement
        {
            public readonly Vector2 Center;
            public readonly Vector2 HalfExtents;
            public readonly Vector2 Right;
            public readonly Vector2 Forward;

            public PoolPlacement(Vector2 center, float scale, float yaw)
            {
                Center = center;
                HalfExtents = new Vector2(
                    PoolHalfWidth * scale + PoolGap,
                    PoolHalfDepth * scale + PoolGap
                );
                float radians = yaw * Mathf.Deg2Rad;
                Right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Forward = new Vector2(-Right.y, Right.x);
            }
        }

        private static bool TryPoolSpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<PoolPlacement> placed,
            Vector2 preferred,
            float scale,
            float yaw,
            out PoolPlacement result
        )
        {
            var footprint = new PoolPlacement(Vector2.zero, scale, yaw);
            float roomExtentX =
                Mathf.Abs(footprint.Right.x) * footprint.HalfExtents.x
                + Mathf.Abs(footprint.Forward.x) * footprint.HalfExtents.y;
            float roomExtentZ =
                Mathf.Abs(footprint.Right.y) * footprint.HalfExtents.x
                + Mathf.Abs(footprint.Forward.y) * footprint.HalfExtents.y;
            float minX = -archetype.HalfWidth + roomExtentX;
            float maxX = archetype.HalfWidth - roomExtentX;
            float minZ = -archetype.HalfDepth + roomExtentZ;
            float maxZ = archetype.HalfDepth - roomExtentZ;
            if (minX > maxX || minZ > maxZ)
            {
                result = default;
                return false;
            }

            for (int attempt = 0; attempt < PoolPlacementAttempts; attempt++)
            {
                Vector2 center =
                    attempt == 0
                        ? new Vector2(
                            Mathf.Clamp(preferred.x, minX, maxX),
                            Mathf.Clamp(preferred.y, minZ, maxZ)
                        )
                        : new Vector2(
                            Mathf.Lerp(minX, maxX, (float)random.NextDouble()),
                            Mathf.Lerp(minZ, maxZ, (float)random.NextDouble())
                        );
                if (IsOnDivider(center, archetype))
                    continue;

                var candidate = new PoolPlacement(center, scale, yaw);
                bool overlaps = false;
                foreach (PoolPlacement other in placed)
                {
                    if (!PoolFootprintsOverlap(candidate, other))
                        continue;
                    overlaps = true;
                    break;
                }
                if (overlaps)
                    continue;

                result = candidate;
                return true;
            }

            result = default;
            return false;
        }

        private static bool PoolFootprintsOverlap(PoolPlacement a, PoolPlacement b)
        {
            Vector2 delta = b.Center - a.Center;
            return OverlapsOnAxis(delta, a, b, a.Right)
                && OverlapsOnAxis(delta, a, b, a.Forward)
                && OverlapsOnAxis(delta, a, b, b.Right)
                && OverlapsOnAxis(delta, a, b, b.Forward);
        }

        private static bool OverlapsOnAxis(
            Vector2 delta,
            PoolPlacement a,
            PoolPlacement b,
            Vector2 axis
        )
        {
            float distance = Mathf.Abs(Vector2.Dot(delta, axis));
            float radiusA = ProjectionRadius(a, axis);
            float radiusB = ProjectionRadius(b, axis);
            return distance < radiusA + radiusB;
        }

        private static float ProjectionRadius(PoolPlacement pool, Vector2 axis)
        {
            return pool.HalfExtents.x * Mathf.Abs(Vector2.Dot(pool.Right, axis))
                + pool.HalfExtents.y * Mathf.Abs(Vector2.Dot(pool.Forward, axis));
        }
    }
}
