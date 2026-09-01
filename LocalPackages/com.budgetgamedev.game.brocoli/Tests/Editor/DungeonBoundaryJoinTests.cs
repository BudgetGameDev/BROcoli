using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Where a room's shared wall run reaches the grid corner that the
    /// platform's boundary parapet turns on, the two must be one continuous
    /// line of masonry. These tests pin the join: which run ends the layout
    /// reports as boundary joins, and that the builder actually lands them at
    /// the parapet's height.
    /// </summary>
    public sealed class DungeonBoundaryJoinTests
    {
        [Test]
        public void SharedRunsReportABoundaryJoinWhereThePlatformEnds()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                int checkedNorth = 0;
                int checkedSouth = 0;
                for (int x = -4; x <= 4; x++)
                {
                    if (
                        layout.EnvironmentAt(new Vector2Int(x, 0))
                        != DungeonLayout.EnvironmentTheme.Dungeon
                    )
                        continue;

                    if (TryEastEdge(layout, x, north: true, out DungeonEdge northEdge))
                    {
                        Assert.That(
                            layout.BoundaryParapetJoinMask(northEdge) & DungeonLayout.RunEndHigh,
                            Is.Not.Zero,
                            $"seed {seed}: north-row run at x {x} ignores the boundary above it"
                        );
                        checkedNorth++;
                    }

                    if (TryEastEdge(layout, x, north: false, out DungeonEdge southEdge))
                    {
                        Assert.That(
                            layout.BoundaryParapetJoinMask(southEdge) & DungeonLayout.RunEndLow,
                            Is.Not.Zero,
                            $"seed {seed}: south-row run at x {x} ignores the cliff below it"
                        );
                        checkedSouth++;
                    }
                }

                Assert.That(
                    checkedNorth,
                    Is.GreaterThan(0),
                    $"seed {seed}: no north-row run found"
                );
                Assert.That(
                    checkedSouth,
                    Is.GreaterThan(0),
                    $"seed {seed}: no south-row run found"
                );
            }
        }

        [Test]
        public void ARockLineBoundaryLeavesTheSharedRunAtItsOwnHeight()
        {
            var layout = new DungeonLayout(1);

            // Caves dress their boundary with loose rock rather than masonry, so
            // there is no parapet at the corner for a run to match.
            int caveColumn = ColumnOfTheme(layout, DungeonLayout.EnvironmentTheme.Cave);
            Assert.That(
                TryEastEdge(layout, caveColumn, north: true, out DungeonEdge caveEdge),
                Is.True,
                "no cave run to test"
            );
            Assert.That(layout.BoundaryParapetJoinMask(caveEdge), Is.Zero);
        }

        [Test]
        public void TheEndPieceMeetsTheBoundaryParapetAtTheSameHeight()
        {
            var layout = new DungeonLayout(1);
            int column = -4;
            while (column < 4 && !TryEastEdge(layout, column, north: true, out _))
                column++;
            Assert.That(TryEastEdge(layout, column, north: true, out DungeonEdge shared), Is.True);
            Assert.That(
                layout.EnvironmentAt(shared),
                Is.EqualTo(DungeonLayout.EnvironmentTheme.Dungeon),
                "the run has to be in the masonry-boundary band"
            );

            var room = new Vector2Int(column, EdgeRow(layout, north: true, column));
            var boundary = new DungeonEdge(shared.X, shared.Y + 1, false);
            Assert.That(
                layout.PlayableEdgeStyle(boundary),
                Is.Not.EqualTo(DungeonEdgeStyle.Interior),
                "the run north of the top row should be a platform boundary"
            );

            var host = new GameObject("Boundary join host");
            var root = new GameObject("Boundary join root");
            try
            {
                DungeonRoomBuilder builder = DungeonPropFixtures.Builder(host);
                GameObject sharedRun = builder.BuildEdge(
                    root.transform,
                    shared,
                    layout.PlayablePassage(room, DungeonLayout.East),
                    DungeonEdgeStyle.Interior,
                    layout.EnvironmentAt(shared),
                    layout.BoundaryParapetJoinMask(shared)
                );
                GameObject boundaryRun = builder.BuildEdge(
                    root.transform,
                    boundary,
                    new DungeonPassage(false, 0, 0),
                    layout.PlayableEdgeStyle(boundary),
                    layout.EnvironmentAt(boundary)
                );

                Vector2 center = DungeonLayout.RoomCenter(room);
                var corner = new Vector2(
                    center.x + DungeonLayout.RoomWidth / 2f,
                    center.y + DungeonLayout.RoomDepth / 2f
                );
                float sharedTop = TopAt(sharedRun, corner);
                float parapetTop = TopAt(
                    boundaryRun.transform.Find("Low Dungeon Railing").gameObject,
                    corner
                );

                Assert.That(
                    sharedTop,
                    Is.EqualTo(parapetTop).Within(0.02f),
                    "the shared run steps where it meets the boundary parapet"
                );
                Assert.That(
                    sharedTop,
                    Is.LessThan(
                        DungeonWallPiece.SlabHeight
                            * DungeonRoomBuilder.SharedEdgeRailingHeightScale
                            - 0.1f
                    ),
                    "the joining piece kept the taller shared-railing height"
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnlyTheJoiningEndDropsToParapetHeight()
        {
            var host = new GameObject("Run height host");
            var root = new GameObject("Run height root");
            try
            {
                DungeonRoomBuilder builder = DungeonPropFixtures.Builder(host);
                GameObject run = builder.BuildEdge(
                    root.transform,
                    new DungeonEdge(0, 0, false),
                    new DungeonPassage(false, 0, 0),
                    DungeonEdgeStyle.Interior,
                    DungeonLayout.EnvironmentTheme.Dungeon,
                    DungeonLayout.RunEndHigh
                );

                Vector2 center = DungeonLayout.RoomCenter(Vector2Int.zero);
                float x = center.x + DungeonLayout.RoomWidth / 2f;
                float parapet =
                    DungeonWallPiece.SlabHeight * DungeonRoomBuilder.BoundaryParapetHeightScale;
                float railing =
                    DungeonWallPiece.SlabHeight * DungeonRoomBuilder.SharedEdgeRailingHeightScale;

                Assert.That(
                    TopAt(run, new Vector2(x, center.y + DungeonLayout.RoomDepth / 2f)),
                    Is.EqualTo(parapet).Within(0.02f),
                    "the joining end kept its own height"
                );
                Assert.That(
                    TopAt(run, new Vector2(x, center.y - DungeonLayout.RoomDepth / 2f)),
                    Is.EqualTo(railing).Within(0.02f),
                    "the far end dropped without a boundary to meet"
                );
                Assert.That(
                    TopAt(run, new Vector2(x, center.y)),
                    Is.EqualTo(railing).Within(0.02f),
                    "a middle piece dropped without a boundary to meet"
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>The top of the built masonry nearest a ground-plane point.</summary>
        private static float TopAt(GameObject run, Vector2 point)
        {
            float best = float.MaxValue;
            float top = float.NaN;
            foreach (Renderer renderer in run.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.bounds;
                float distance = Vector2.Distance(
                    new Vector2(bounds.center.x, bounds.center.z),
                    point
                );
                if (distance >= best)
                    continue;
                best = distance;
                top = bounds.max.y;
            }
            Assert.That(top, Is.Not.NaN, $"no masonry was built near {point}");
            return top;
        }

        private static bool TryEastEdge(
            DungeonLayout layout,
            int x,
            bool north,
            out DungeonEdge edge
        )
        {
            var room = new Vector2Int(x, EdgeRow(layout, north, x));
            edge = DungeonLayout.EdgeBetween(room, DungeonLayout.East);
            return layout.PlayableEdgeStyle(edge) == DungeonEdgeStyle.Interior;
        }

        private static int EdgeRow(DungeonLayout layout, bool north, int x)
        {
            return layout.ClampToPlayableBand(new Vector2Int(x, north ? 1000 : -1000)).y;
        }

        private static int ColumnOfTheme(DungeonLayout layout, DungeonLayout.EnvironmentTheme theme)
        {
            for (int x = -200; x <= 200; x++)
            {
                if (layout.EnvironmentAt(new Vector2Int(x, 0)) != theme)
                    continue;
                if (TryEastEdge(layout, x, north: true, out _))
                    return x;
            }
            Assert.Fail($"no {theme} column carried a shared run");
            return 0;
        }
    }
}
