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
                    AppendHorizontalRuns(walls, center, 6f, 1);
                    break;
                case DungeonLayout.RoomShape.NarrowHorizontal:
                    AppendHorizontalRuns(walls, center, 4f, InteriorRunHalfTilesX);
                    break;
                case DungeonLayout.RoomShape.NarrowVertical:
                    AppendVerticalRuns(walls, center, 4f);
                    break;
                case DungeonLayout.RoomShape.LargeSquare:
                    AppendVerticalRuns(walls, center, 10f);
                    AppendHorizontalRuns(walls, center, 6f, InteriorRunHalfTilesX);
                    break;
                case DungeonLayout.RoomShape.LongHorizontal:
                    AppendHorizontalRuns(walls, center, 6f, InteriorRunHalfTilesX);
                    break;
                case DungeonLayout.RoomShape.LongVertical:
                    AppendVerticalRuns(walls, center, 6f);
                    break;
                case DungeonLayout.RoomShape.Divided:
                    AppendVerticalDivider(walls, center);
                    AppendDividerTurns(walls, center);
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

        /// <summary>
        /// Two broken east-west railing runs form a readable lane. The missing
        /// centre piece is a four-unit gate, while stopping short of the shell
        /// leaves a second route around either end.
        /// </summary>
        private static void AppendHorizontalRuns(
            List<DungeonWallPiece> walls,
            Vector2 center,
            float z,
            int halfTiles
        )
        {
            AppendHorizontalRun(walls, center, z, halfTiles);
            AppendHorizontalRun(walls, center, -z, halfTiles);
        }

        private static void AppendHorizontalRun(
            List<DungeonWallPiece> walls,
            Vector2 center,
            float z,
            int halfTiles
        )
        {
            for (int i = -halfTiles; i <= halfTiles; i++)
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

        /// <summary>
        /// Short returns on opposite ends of the divided room turn its two
        /// vertical sections into a loose S. The player can follow the channel or
        /// cut through its broad centre gap, so the shape guides without becoming
        /// a maze.
        /// </summary>
        private static void AppendDividerTurns(List<DungeonWallPiece> walls, Vector2 center)
        {
            walls.Add(
                new DungeonWallPiece(
                    center + new Vector2(-2f, 6f),
                    true,
                    DungeonWallKind.Interior,
                    "Divider Upper Turn"
                )
            );
            walls.Add(
                new DungeonWallPiece(
                    center + new Vector2(2f, -6f),
                    true,
                    DungeonWallKind.Interior,
                    "Divider Lower Turn"
                )
            );
        }
    }
}
