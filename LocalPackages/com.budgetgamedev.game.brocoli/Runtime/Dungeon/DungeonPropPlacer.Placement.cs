using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        internal static bool TryRandomSpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied,
            float radius,
            bool large,
            out Vector2 result
        )
        {
            float edgeMargin = Mathf.Max(0.65f, radius + WallSealGap);
            for (int attempt = 0; attempt < 28; attempt++)
            {
                var candidate = new Vector2(
                    Mathf.Lerp(
                        -archetype.HalfWidth + edgeMargin,
                        archetype.HalfWidth - edgeMargin,
                        (float)random.NextDouble()
                    ),
                    Mathf.Lerp(
                        -archetype.HalfDepth + edgeMargin,
                        archetype.HalfDepth - edgeMargin,
                        (float)random.NextDouble()
                    )
                );
                if (Mathf.Abs(candidate.x) < 1.55f || Mathf.Abs(candidate.y) < 1.55f)
                    continue;
                if (OverlapsInteriorWall(candidate, radius, archetype))
                    continue;
                bool clear = true;
                foreach (OccupiedSpot other in occupied)
                {
                    float separation = radius + other.Radius + PropGap;
                    if (large && other.Large)
                        separation = Mathf.Max(separation, LargePropSeparation);
                    clear &= (candidate - other.Position).sqrMagnitude >= separation * separation;
                }
                if (!clear)
                    continue;
                result = candidate;
                return true;
            }
            result = default;
            return false;
        }

        internal static bool TryClusterSpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            List<OccupiedSpot> occupied,
            float radius,
            out Vector2 result
        )
        {
            for (int attempt = 0; attempt < 48; attempt++)
            {
                var candidate = new Vector2(
                    Mathf.Lerp(
                        -archetype.HalfWidth + radius + WallSealGap,
                        archetype.HalfWidth - radius - WallSealGap,
                        (float)random.NextDouble()
                    ),
                    Mathf.Lerp(
                        -archetype.HalfDepth + radius + WallSealGap,
                        archetype.HalfDepth - radius - WallSealGap,
                        (float)random.NextDouble()
                    )
                );
                if (Mathf.Abs(candidate.x) < 1.55f)
                    continue;
                if (Mathf.Abs(candidate.y) < 1.55f)
                    continue;
                if (OverlapsInteriorWall(candidate, radius, archetype))
                    continue;

                bool clear = true;
                foreach (OccupiedSpot other in occupied)
                {
                    float separation = radius + other.Radius + PropGap;
                    clear &= (candidate - other.Position).sqrMagnitude >= separation * separation;
                }
                if (!clear)
                    continue;
                result = candidate;
                return true;
            }
            result = default;
            return false;
        }

        internal static bool IsOnDivider(Vector2 point, DungeonLayout.RoomArchetype archetype)
        {
            return OverlapsInteriorWall(point, 0.65f, archetype);
        }

        internal static Vector2 PoolSpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random
        )
        {
            Vector2 corner = new Vector2(
                (archetype.Variant & 1) == 0 ? -1f : 1f,
                (archetype.Variant & 2) == 0 ? -1f : 1f
            );
            float x = Mathf.Lerp(
                archetype.HalfWidth * 0.2f,
                archetype.HalfWidth * 0.62f,
                (float)random.NextDouble()
            );
            float z = Mathf.Lerp(
                archetype.HalfDepth * 0.2f,
                archetype.HalfDepth * 0.58f,
                (float)random.NextDouble()
            );
            return new Vector2(x * corner.x, z * corner.y);
        }

        internal static Vector2 RotateQuarterTurns(Vector2 point, int turns)
        {
            return ((turns % 4 + 4) % 4) switch
            {
                1 => new Vector2(point.y, -point.x),
                2 => -point,
                3 => new Vector2(-point.y, point.x),
                _ => point,
            };
        }
    }
}
