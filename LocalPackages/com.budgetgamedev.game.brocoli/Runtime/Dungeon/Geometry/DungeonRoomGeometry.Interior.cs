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
        private const int InteriorRunHalfTilesX = DungeonLayout.RoomTilesX / 2 - 1;
        private const int InteriorRunHalfTilesZ = DungeonLayout.RoomTilesZ / 2 - 1;

        /// <summary>
        /// The interior runs that reshape a room's fixed grid shell. Every run
        /// leaves a central circulation gap, so all outer-edge opening patterns
        /// stay connected regardless of the chosen shape.
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
                    AppendHorizontalRuns(walls, center, 4f);
                    AppendVerticalRuns(walls, center, 4f);
                    break;
                case DungeonLayout.RoomShape.Compact:
                    AppendHorizontalRuns(walls, center, 6f);
                    AppendVerticalRuns(walls, center, 6f);
                    break;
                case DungeonLayout.RoomShape.NarrowHorizontal:
                    AppendHorizontalRuns(walls, center, 4f);
                    break;
                case DungeonLayout.RoomShape.NarrowVertical:
                    AppendVerticalRuns(walls, center, 4f);
                    break;
                case DungeonLayout.RoomShape.LargeSquare:
                    AppendVerticalRuns(walls, center, 10f);
                    break;
                case DungeonLayout.RoomShape.LongHorizontal:
                    AppendHorizontalRuns(walls, center, 6f);
                    break;
                case DungeonLayout.RoomShape.LongVertical:
                    AppendVerticalRuns(walls, center, 6f);
                    break;
                case DungeonLayout.RoomShape.Divided:
                    if ((archetype.Variant & 1) == 0)
                        AppendVerticalDivider(walls, center);
                    else
                        AppendHorizontalDivider(walls, center);
                    break;
            }
        }

        private static void AppendHorizontalRuns(
            List<DungeonWallPiece> walls,
            Vector2 center,
            float z
        )
        {
            AppendHorizontalRun(walls, center, z);
            AppendHorizontalRun(walls, center, -z);
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

        private static void AppendHorizontalRun(
            List<DungeonWallPiece> walls,
            Vector2 center,
            float z
        )
        {
            for (int i = -InteriorRunHalfTilesX; i <= InteriorRunHalfTilesX; i++)
            {
                if (i == 0)
                    continue;
                walls.Add(
                    new DungeonWallPiece(
                        new Vector2(center.x + i * Tile, center.y + z),
                        true,
                        DungeonWallKind.Interior,
                        $"Horizontal {z:0.##} {(i < 0 ? "Left" : "Right")}"
                    )
                );
            }
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

        private static void AppendHorizontalDivider(List<DungeonWallPiece> walls, Vector2 center)
        {
            foreach (int i in new[] { -2, 0, 2 })
            {
                walls.Add(
                    new DungeonWallPiece(
                        new Vector2(center.x + i * Tile, center.y),
                        true,
                        DungeonWallKind.Interior,
                        $"Horizontal Divider {i:+#;-#;0}"
                    )
                );
            }
        }
    }
}
