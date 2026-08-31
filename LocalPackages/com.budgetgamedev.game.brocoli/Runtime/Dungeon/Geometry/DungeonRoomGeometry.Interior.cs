using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public static partial class DungeonRoomGeometry
    {
        // Interior runs stop one tile short of the outer shell on both ends. That
        // leaves a continuous perimeter corridor, so an interior wall can never
        // grow into a doorway or seal a corner off from the rest of the room, no
        // matter which slots the four shared edges opened.
        private const int InteriorRunHalfTilesZ = DungeonLayout.RoomTilesZ / 2 - 1;

        /// <summary>
        /// The interior runs that reshape a room's fixed grid shell. These are
        /// collision plans, not full-height wall instructions: the room builder
        /// always realizes them as half walls. Runs leave a central circulation
        /// gap, so all outer-edge opening patterns stay connected regardless of
        /// the chosen shape.
        /// </summary>
        public static void AppendInteriorWalls(
            List<DungeonWallPiece> walls,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype
        )
        {
            Vector2 center = DungeonLayout.RoomCenter(room);
            switch (archetype.Shape)
            {
                case DungeonLayout.RoomShape.Tiny:
                    AppendVerticalRuns(walls, center, 4f);
                    break;
                case DungeonLayout.RoomShape.Compact:
                    AppendVerticalRuns(walls, center, 6f);
                    break;
                case DungeonLayout.RoomShape.NarrowHorizontal:
                    break;
                case DungeonLayout.RoomShape.NarrowVertical:
                    AppendVerticalRuns(walls, center, 4f);
                    break;
                case DungeonLayout.RoomShape.LargeSquare:
                    AppendVerticalRuns(walls, center, 10f);
                    break;
                case DungeonLayout.RoomShape.LongHorizontal:
                    break;
                case DungeonLayout.RoomShape.LongVertical:
                    AppendVerticalRuns(walls, center, 6f);
                    break;
                case DungeonLayout.RoomShape.Divided:
                    AppendVerticalDivider(walls, center);
                    break;
            }
        }

        private static void AppendVerticalRuns(
            List<DungeonWallPiece> walls,
            Vector2 center,
            float x
        )
        {
            AppendVerticalRun(walls, center, x);
            AppendVerticalRun(walls, center, -x);
        }

        private static void AppendVerticalRun(List<DungeonWallPiece> walls, Vector2 center, float x)
        {
            for (int j = -InteriorRunHalfTilesZ; j <= InteriorRunHalfTilesZ; j++)
            {
                if (j == 0)
                    continue;
                walls.Add(
                    new DungeonWallPiece(
                        new Vector2(center.x + x, center.y + j * Tile),
                        false,
                        DungeonWallKind.Interior,
                        $"Vertical {x:0.##} {(j < 0 ? "Lower" : "Upper")}"
                    )
                );
            }
        }

        private static void AppendVerticalDivider(List<DungeonWallPiece> walls, Vector2 center)
        {
            foreach (int j in new[] { -1, 1 })
            {
                walls.Add(
                    new DungeonWallPiece(
                        new Vector2(center.x, center.y + j * Tile),
                        false,
                        DungeonWallKind.Interior,
                        j < 0 ? "Vertical Divider Lower" : "Vertical Divider Upper"
                    )
                );
            }
        }
    }
}
