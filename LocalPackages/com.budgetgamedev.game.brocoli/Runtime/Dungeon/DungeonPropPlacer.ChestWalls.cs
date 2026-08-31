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
                DungeonLayout.RoomShape.Tiny => OverlapsVerticalRuns(point, clearance, 4f),
                DungeonLayout.RoomShape.Compact => OverlapsVerticalRuns(point, clearance, 6f),
                DungeonLayout.RoomShape.NarrowVertical => OverlapsVerticalRuns(
                    point,
                    clearance,
                    4f
                ),
                DungeonLayout.RoomShape.LargeSquare => OverlapsVerticalRuns(point, clearance, 10f),
                DungeonLayout.RoomShape.LongVertical => OverlapsVerticalRuns(point, clearance, 6f),
                DungeonLayout.RoomShape.Divided => OverlapsDivider(point, clearance),
                _ => false,
            };
        }

        private static bool OverlapsVerticalRuns(Vector2 point, float clearance, float x)
        {
            return OverlapsVerticalWall(point, clearance, x)
                || OverlapsVerticalWall(point, clearance, -x);
        }

        private static bool OverlapsVerticalWall(Vector2 point, float clearance, float x)
        {
            bool besideGap = point.y - clearance <= -1.8f || point.y + clearance >= 1.8f;
            return besideGap
                && point.x + clearance >= x + WallFrontFaceOffset
                && point.x - clearance <= x + WallBackFaceOffset;
        }

        private static bool OverlapsDivider(Vector2 point, float clearance)
        {
            bool onSegment =
                (point.y + clearance >= -6.2f && point.y - clearance <= -1.8f)
                || (point.y + clearance >= 1.8f && point.y - clearance <= 6.2f);
            return onSegment
                && point.x + clearance >= WallFrontFaceOffset
                && point.x - clearance <= WallBackFaceOffset;
        }
    }
}
