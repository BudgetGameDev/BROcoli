using System.Collections.Generic;
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
            var walls = new List<DungeonWallPiece>();
            DungeonRoomGeometry.AppendInteriorWalls(walls, Vector2Int.zero, archetype);
            foreach (DungeonWallPiece wall in walls)
            {
                Rect footprint = wall.Footprint;
                if (
                    point.x >= footprint.xMin - clearance
                    && point.x <= footprint.xMax + clearance
                    && point.y >= footprint.yMin - clearance
                    && point.y <= footprint.yMax + clearance
                )
                    return true;
            }
            return false;
        }
    }
}
