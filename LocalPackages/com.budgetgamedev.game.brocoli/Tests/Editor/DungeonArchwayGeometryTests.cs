using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Property tests for the arch frames set into doorways. A frame has to read
    /// as architecture grown out of the wall run around it rather than a mesh
    /// pushed into a gap, so it is checked against the run it stands in.
    /// </summary>
    public sealed class DungeonArchwayGeometryTests
    {
        private const float Epsilon = 0.001f;

        /// <summary>
        /// Every arch is a frame set into a wall, never a free-standing gate. Its
        /// posts stand wider than the doorway they span, so each one has to land on
        /// a wall piece: the slot on either side of an arch must be walled.
        /// </summary>
        [Test]
        public void ArchwaysAreFlankedByWallsOnBothSides()
        {
            int archways = 0;
            foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
            {
                foreach (DungeonArchway archway in block.Archways)
                {
                    archways++;
                    Vector2 along = archway.AlongX ? Vector2.right : Vector2.up;
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector2 neighbour =
                            archway.Position + along * (side * DungeonLayout.TileSize);
                        if (!Covers(block.Walls, neighbour, archway.AlongX))
                            Assert.Fail(
                                $"seed {block.Seed}: the arch at {archway.Position} has no wall on "
                                    + $"its {(side < 0 ? "near" : "far")} side"
                            );
                    }
                }
            }

            Assert.That(archways, Is.GreaterThan(0), "the corpus builds no archways to test");
        }

        /// <summary>
        /// An arch's posts have to reach the walls beside it. The frame is planned
        /// wider than its slot for exactly this reason; if that ever stops being
        /// true the posts float in the doorway with a seam on each side.
        /// </summary>
        [Test]
        public void ArchwayPostsOverlapTheWallsTheyMeet()
        {
            Assert.That(
                DungeonArchway.PostOuterHalfWidth,
                Is.GreaterThan(DungeonLayout.TileSize / 2f),
                "the arch frame is narrower than its slot, so its posts cannot meet the wall"
            );
        }

        /// <summary>Whether any piece running the given way covers a point.</summary>
        private static bool Covers(List<DungeonWallPiece> walls, Vector2 point, bool alongX)
        {
            foreach (DungeonWallPiece piece in walls)
            {
                Rect footprint = piece.Footprint;
                if (
                    piece.AlongX == alongX
                    && point.x >= footprint.xMin - Epsilon
                    && point.x <= footprint.xMax + Epsilon
                    && point.y >= footprint.yMin - Epsilon
                    && point.y <= footprint.yMax + Epsilon
                )
                    return true;
            }
            return false;
        }
    }
}
