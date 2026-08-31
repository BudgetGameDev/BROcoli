using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        private const float AlleyChestChance = 0.52f;
        private const float ChestWallGap = WallSealGap;

        internal static Vector2 ChestSpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            float radius,
            List<OccupiedSpot> occupied
        )
        {
            bool preferAlley = random.NextDouble() < AlleyChestChance;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                bool useAlley = attempt < 32 ? preferAlley : !preferAlley;
                Vector2 candidate = useAlley
                    ? RandomAlleySpot(archetype, random, radius)
                    : RandomMainRoomSpot(archetype, random, radius);
                if (IsChestSpotClear(candidate, archetype, radius, occupied))
                    return candidate;
            }

            return GridFallbackSpot(archetype, random, radius, occupied);
        }

        internal static Vector2 RandomMainRoomSpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            float radius
        )
        {
            float xLimit = Mathf.Max(0.25f, archetype.HalfWidth - radius - 0.3f);
            float zLimit = Mathf.Max(0.25f, archetype.HalfDepth - radius - 0.3f);
            Vector2 candidate = new Vector2(
                RandomRange(random, -xLimit, xLimit),
                RandomRange(random, -zLimit, zLimit)
            );

            if (random.NextDouble() < 0.72)
            {
                if (random.NextDouble() < 0.5)
                    candidate.x = RandomSignedBand(random, xLimit * 0.48f, xLimit);
                else
                    candidate.y = RandomSignedBand(random, zLimit * 0.48f, zLimit);
            }
            return candidate;
        }

        internal static Vector2 RandomAlleySpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            float radius
        )
        {
            float minX = NegativeRoomLimit(HalfRoomWidth, radius);
            float maxX = PositiveRoomLimit(HalfRoomWidth, radius);
            float minZ = NegativeRoomLimit(HalfRoomDepth, radius);
            float maxZ = PositiveRoomLimit(HalfRoomDepth, radius);
            float wallGap = radius + ChestWallGap;

            return archetype.Shape switch
            {
                DungeonLayout.RoomShape.Tiny => RandomCrossAlleySpot(
                    random,
                    4f,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    wallGap
                ),
                DungeonLayout.RoomShape.Compact => RandomCrossAlleySpot(
                    random,
                    6f,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    wallGap
                ),
                DungeonLayout.RoomShape.NarrowHorizontal => RandomHorizontalAlleySpot(
                    random,
                    4f,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    wallGap
                ),
                DungeonLayout.RoomShape.LongHorizontal => RandomHorizontalAlleySpot(
                    random,
                    6f,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    wallGap
                ),
                DungeonLayout.RoomShape.NarrowVertical => RandomVerticalAlleySpot(
                    random,
                    4f,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    wallGap
                ),
                DungeonLayout.RoomShape.LongVertical => RandomVerticalAlleySpot(
                    random,
                    6f,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    wallGap
                ),
                DungeonLayout.RoomShape.LargeSquare => RandomVerticalAlleySpot(
                    random,
                    10f,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    wallGap
                ),
                _ => RandomMainRoomSpot(archetype, random, radius),
            };
        }

        internal static Vector2 RandomCrossAlleySpot(
            System.Random random,
            float wall,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float wallGap
        )
        {
            float innerMin = -wall + WallBackFaceOffset + wallGap;
            float innerMax = wall + WallFrontFaceOffset - wallGap;
            int side = random.Next(0, 4);
            return side switch
            {
                0 => new Vector2(
                    RandomEdgeOfRange(random, innerMin, innerMax),
                    RandomRange(random, minZ, -wall + WallFrontFaceOffset - wallGap)
                ),
                1 => new Vector2(
                    RandomEdgeOfRange(random, innerMin, innerMax),
                    RandomRange(random, wall + WallBackFaceOffset + wallGap, maxZ)
                ),
                2 => new Vector2(
                    RandomRange(random, minX, -wall + WallFrontFaceOffset - wallGap),
                    RandomEdgeOfRange(random, innerMin, innerMax)
                ),
                _ => new Vector2(
                    RandomRange(random, wall + WallBackFaceOffset + wallGap, maxX),
                    RandomEdgeOfRange(random, innerMin, innerMax)
                ),
            };
        }

        internal static Vector2 RandomHorizontalAlleySpot(
            System.Random random,
            float wall,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float wallGap
        )
        {
            float x = RandomEdgeOfRange(random, minX, maxX);
            float z =
                random.NextDouble() < 0.5
                    ? RandomRange(random, minZ, -wall + WallFrontFaceOffset - wallGap)
                    : RandomRange(random, wall + WallBackFaceOffset + wallGap, maxZ);
            return new Vector2(x, z);
        }

        internal static Vector2 RandomVerticalAlleySpot(
            System.Random random,
            float wall,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float wallGap
        )
        {
            float x =
                random.NextDouble() < 0.5
                    ? RandomRange(random, minX, -wall + WallFrontFaceOffset - wallGap)
                    : RandomRange(random, wall + WallBackFaceOffset + wallGap, maxX);
            float z = RandomEdgeOfRange(random, minZ, maxZ);
            return new Vector2(x, z);
        }

        internal static bool IsChestSpotClear(
            Vector2 candidate,
            DungeonLayout.RoomArchetype archetype,
            float radius,
            List<OccupiedSpot> occupied
        )
        {
            if (
                candidate.x < NegativeRoomLimit(HalfRoomWidth, radius)
                || candidate.x > PositiveRoomLimit(HalfRoomWidth, radius)
                || candidate.y < NegativeRoomLimit(HalfRoomDepth, radius)
                || candidate.y > PositiveRoomLimit(HalfRoomDepth, radius)
            )
                return false;
            if (OverlapsInteriorWall(candidate, radius, archetype))
                return false;

            foreach (OccupiedSpot other in occupied)
            {
                float separation = radius + other.Radius + PropGap;
                if ((candidate - other.Position).sqrMagnitude < separation * separation)
                    return false;
            }
            return true;
        }

        internal static Vector2 GridFallbackSpot(
            DungeonLayout.RoomArchetype archetype,
            System.Random random,
            float radius,
            List<OccupiedSpot> occupied
        )
        {
            int start = random.Next(0, 117);
            for (int i = 0; i < 117; i++)
            {
                int cell = (start + i) % 117;
                float x = Mathf.Lerp(-12f, 12f, cell % 13 / 12f);
                float z = Mathf.Lerp(-8f, 8f, cell / 13 / 8f);
                var candidate = new Vector2(x, z);
                if (IsChestSpotClear(candidate, archetype, radius, occupied))
                    return candidate;
            }
            return Vector2.zero;
        }

        private static float RandomSignedBand(System.Random random, float min, float max)
        {
            float value = RandomRange(random, Mathf.Min(min, max), Mathf.Max(min, max));
            return random.NextDouble() < 0.5 ? -value : value;
        }

        private static float RandomEdgeOfRange(System.Random random, float min, float max)
        {
            float edgeBand = (max - min) * 0.42f;
            return random.NextDouble() < 0.5
                ? RandomRange(random, min, min + edgeBand)
                : RandomRange(random, max - edgeBand, max);
        }

        private static float NegativeRoomLimit(float halfExtent, float radius)
        {
            return -halfExtent + WallBackFaceOffset + radius + ChestWallGap;
        }

        private static float PositiveRoomLimit(float halfExtent, float radius)
        {
            return halfExtent + WallFrontFaceOffset - radius - ChestWallGap;
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }
    }
}
