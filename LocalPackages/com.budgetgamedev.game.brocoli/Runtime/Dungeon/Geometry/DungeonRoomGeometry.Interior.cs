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

        // Where a room's full-height feature wall stands and how far its hidden
        // band reaches. The wall is scenery: through the 45-degree gameplay
        // camera its occlusion shadow falls north-east of it, and the keep-out
        // covers that whole sheared band so nothing walkable is ever hidden.
        private const float FeatureWallZ = 3.5f;
        private const float FeatureWallHalfSpan = 4f;
        private static readonly Rect FeatureKeepOutLocal = Rect.MinMaxRect(
            -FeatureWallHalfSpan - 0.2f,
            FeatureWallZ + DungeonWallPiece.SlabHalfThickness,
            FeatureWallHalfSpan + 2f,
            FeatureWallZ + 2.3f
        );

        /// <summary>
        /// Whether this archetype carries a full-height interior feature wall.
        /// Deliberately rare - one variant of two broad shapes in three themes -
        /// so tall interior masonry stays a landmark, not a habit.
        /// </summary>
        public static bool HasFeatureWall(DungeonLayout.RoomArchetype archetype)
        {
            bool shapeCarries =
                archetype.Shape == DungeonLayout.RoomShape.OpenHall
                || archetype.Shape == DungeonLayout.RoomShape.LargeSquare;
            bool themeCarries =
                archetype.Theme == DungeonLayout.RoomTheme.Storage
                || archetype.Theme == DungeonLayout.RoomTheme.Armory
                || archetype.Theme == DungeonLayout.RoomTheme.Shrine;
            return shapeCarries && themeCarries && archetype.Variant == 1;
        }

        /// <summary>
        /// The ground rectangles the player must be kept out of because a
        /// feature wall would hide anyone standing there. The builder seals
        /// them with collision and the prop placer dresses them shut, so the
        /// tall wall can exist without ever obscuring the player.
        /// </summary>
        public static void AppendFeatureKeepOuts(
            List<Rect> keepOuts,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype
        )
        {
            if (!HasFeatureWall(archetype))
                return;

            Vector2 center = DungeonLayout.RoomCenter(room);
            Rect local = FeatureKeepOutLocal;
            keepOuts.Add(
                Rect.MinMaxRect(
                    center.x + local.xMin,
                    center.y + local.yMin,
                    center.x + local.xMax,
                    center.y + local.yMax
                )
            );
        }

        /// <summary>
        /// The interior runs that reshape a room's fixed grid shell. These are
        /// collision plans: the room builder realizes them as half walls and
        /// railings, except for the rare <see cref="DungeonWallKind.InteriorFeature"/>
        /// pieces, which stand at full height and are sealed off behind by
        /// <see cref="AppendFeatureKeepOuts"/>. Runs leave a central circulation
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
            if (HasFeatureWall(archetype))
            {
                foreach (float x in new[] { -2f, 2f })
                {
                    walls.Add(
                        new DungeonWallPiece(
                            new Vector2(center.x + x, center.y + FeatureWallZ),
                            true,
                            DungeonWallKind.InteriorFeature,
                            "Feature Wall"
                        )
                    );
                }
            }

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
