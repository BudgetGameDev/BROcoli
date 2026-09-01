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

            // Curved and diagonal railings occupy floor just like axis runs do,
            // and the sealed band behind a feature wall must never receive a
            // chest or prop the player could not reach.
            var railings = new List<DungeonRailingSegment>();
            DungeonRoomGeometry.AppendInteriorRailings(railings, Vector2Int.zero, archetype);
            foreach (DungeonRailingSegment railing in railings)
            {
                if (
                    railing.DistanceTo(point)
                    <= clearance + DungeonRailingSegment.SlabHalfThickness
                )
                    return true;
            }

            var keepOuts = new List<Rect>();
            DungeonRoomGeometry.AppendFeatureKeepOuts(keepOuts, Vector2Int.zero, archetype);
            foreach (Rect keepOut in keepOuts)
            {
                if (
                    point.x >= keepOut.xMin - clearance
                    && point.x <= keepOut.xMax + clearance
                    && point.y >= keepOut.yMin - clearance
                    && point.y <= keepOut.yMax + clearance
                )
                    return true;
            }
            return false;
        }
    }
}
