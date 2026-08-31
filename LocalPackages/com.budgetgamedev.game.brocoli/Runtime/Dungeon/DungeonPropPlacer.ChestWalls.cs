using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonPropPlacer
    {
        internal static bool OverlapsInteriorWall(
            Vector2 point,
            float radius,
            DungeonLayout.RoomArchetype archetype
        )
        {
            float clearance = radius + ChestWallGap;
            return archetype.Shape switch
            {
                DungeonLayout.RoomShape.Tiny => OverlapsCrossWalls(point, clearance, 4f, 4f),
                DungeonLayout.RoomShape.Compact => OverlapsCrossWalls(point, clearance, 6f, 6f),
                DungeonLayout.RoomShape.NarrowHorizontal => OverlapsHorizontalRuns(
                    point,
                    clearance,
                    4f
                ),
                DungeonLayout.RoomShape.NarrowVertical => OverlapsVerticalRuns(
                    point,
                    clearance,
                    4f
                ),
                DungeonLayout.RoomShape.LargeSquare => OverlapsVerticalRuns(point, clearance, 10f),
                DungeonLayout.RoomShape.LongHorizontal => OverlapsHorizontalRuns(
                    point,
                    clearance,
                    6f
                ),
                DungeonLayout.RoomShape.LongVertical => OverlapsVerticalRuns(point, clearance, 6f),
                DungeonLayout.RoomShape.Divided => OverlapsDivider(point, clearance, archetype),
                _ => false,
            };
        }

        private static bool OverlapsCrossWalls(
            Vector2 point,
            float clearance,
            float horizontal,
            float vertical
        )
        {
            return OverlapsHorizontalRuns(point, clearance, horizontal)
                || OverlapsVerticalRuns(point, clearance, vertical);
        }

        private static bool OverlapsHorizontalRuns(Vector2 point, float clearance, float z)
        {
            return OverlapsHorizontalWall(point, clearance, z)
                || OverlapsHorizontalWall(point, clearance, -z);
        }

        private static bool OverlapsVerticalRuns(Vector2 point, float clearance, float x)
        {
            return OverlapsVerticalWall(point, clearance, x)
                || OverlapsVerticalWall(point, clearance, -x);
        }

        private static bool OverlapsHorizontalWall(Vector2 point, float clearance, float z)
        {
            bool besideGap = point.x - clearance <= -1.8f || point.x + clearance >= 1.8f;
            return besideGap
                && point.y + clearance >= z + WallFrontFaceOffset
                && point.y - clearance <= z + WallBackFaceOffset;
        }

        private static bool OverlapsVerticalWall(Vector2 point, float clearance, float x)
        {
            bool besideGap = point.y - clearance <= -1.8f || point.y + clearance >= 1.8f;
            return besideGap
                && point.x + clearance >= x + WallFrontFaceOffset
                && point.x - clearance <= x + WallBackFaceOffset;
        }

        private static bool OverlapsDivider(
            Vector2 point,
            float clearance,
            DungeonLayout.RoomArchetype archetype
        )
        {
            if ((archetype.Variant & 1) == 0)
            {
                bool onSegment =
                    (point.y + clearance >= -6.2f && point.y - clearance <= -1.8f)
                    || (point.y + clearance >= 1.8f && point.y - clearance <= 6.2f);
                return onSegment
                    && point.x + clearance >= WallFrontFaceOffset
                    && point.x - clearance <= WallBackFaceOffset;
            }

            bool onHorizontalSegment =
                (point.x + clearance >= -10.2f && point.x - clearance <= -5.8f)
                || (point.x + clearance >= -2.2f && point.x - clearance <= 2.2f)
                || (point.x + clearance >= 5.8f && point.x - clearance <= 10.2f);
            return onHorizontalSegment
                && point.y + clearance >= WallFrontFaceOffset
                && point.y - clearance <= WallBackFaceOffset;
        }
    }
}
