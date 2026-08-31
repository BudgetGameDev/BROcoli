using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonEnvironmentThemeTests
    {
        [Test]
        public void EnvironmentThemesFillTwentyRoomBandsAndCycleWithoutRepeating()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                var cycle = new HashSet<DungeonLayout.EnvironmentTheme>();
                DungeonLayout.EnvironmentTheme previous = default;
                for (int segment = 0; segment < 6; segment++)
                {
                    int firstColumn =
                        segment * DungeonLayout.ColumnsPerEnvironmentTheme
                        - DungeonLayout.ColumnsPerEnvironmentTheme / 2;
                    DungeonLayout.EnvironmentTheme expected = layout.EnvironmentAt(
                        new Vector2Int(firstColumn, 0)
                    );
                    cycle.Add(expected);
                    if (segment > 0)
                        Assert.That(
                            expected,
                            Is.Not.EqualTo(previous),
                            $"seed {seed}, segment {segment}"
                        );

                    int roomCount = 0;
                    for (
                        int x = firstColumn;
                        x < firstColumn + DungeonLayout.ColumnsPerEnvironmentTheme;
                        x++
                    )
                    {
                        int south = SouthY(layout, x);
                        for (int y = south; y <= south + 1; y++)
                        {
                            var room = new Vector2Int(x, y);
                            Assert.That(
                                layout.EnvironmentAt(room),
                                Is.EqualTo(expected),
                                $"seed {seed}: environment changed inside segment {segment}"
                            );
                            Assert.That(layout.Archetype(room).Environment, Is.EqualTo(expected));
                            roomCount++;
                        }
                    }
                    Assert.That(roomCount, Is.EqualTo(DungeonLayout.RoomsPerEnvironmentTheme));
                    previous = expected;
                }

                Assert.That(
                    layout.EnvironmentAt(Vector2Int.zero),
                    Is.EqualTo(DungeonLayout.EnvironmentTheme.Dungeon)
                );
                Assert.That(cycle.Count, Is.EqualTo(6), $"seed {seed}: not every theme appeared");
            }
        }

        [Test]
        public void BothOuterEdgesUseTheEnvironmentOfTheirPlayableRoom()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -40; x <= 40; x++)
                {
                    int south = SouthY(layout, x);
                    var lower = new Vector2Int(x, south);
                    var upper = lower + Vector2Int.up;
                    Assert.That(
                        layout.EnvironmentAt(DungeonLayout.EdgeBetween(lower, DungeonLayout.South)),
                        Is.EqualTo(layout.EnvironmentAt(lower))
                    );
                    Assert.That(
                        layout.EnvironmentAt(DungeonLayout.EdgeBetween(upper, DungeonLayout.North)),
                        Is.EqualTo(layout.EnvironmentAt(upper))
                    );
                }
            }
        }

        [Test]
        public void DungeonBoundariesAreRailingsAndCavesUseGroundedRocks()
        {
            var host = new GameObject("Environment boundary host");
            var root = new GameObject("Environment boundary root");
            try
            {
                DungeonRoomBuilder builder = DungeonPropFixtures.Builder(host);
                GameObject dungeon = builder.BuildEdge(
                    root.transform,
                    new DungeonEdge(0, -1, true),
                    new DungeonPassage(false, 0, 0),
                    DungeonEdgeStyle.SouthCliff,
                    DungeonLayout.EnvironmentTheme.Dungeon
                );
                Assert.That(dungeon.transform.Find("Low Dungeon Railing"), Is.Not.Null);

                GameObject cave = builder.BuildEdge(
                    root.transform,
                    new DungeonEdge(0, 0, true),
                    new DungeonPassage(false, 0, 0),
                    DungeonEdgeStyle.SolidBoundary,
                    DungeonLayout.EnvironmentTheme.Cave
                );
                Assert.That(cave.GetComponentsInChildren<Renderer>(), Is.Empty);
                Assert.That(cave.GetComponentsInChildren<BoxCollider>().Length, Is.EqualTo(1));

                DungeonPropPlacer placer = DungeonPropFixtures.Placer(
                    host,
                    DungeonPropFixtures.AllPrefabs()
                );
                var caveRocks = new GameObject("Cave rocks");
                caveRocks.transform.SetParent(root.transform, false);
                placer.BuildBoundaryDressing(
                    caveRocks.transform,
                    Vector2Int.zero,
                    DungeonLayout.North,
                    DungeonLayout.EnvironmentTheme.Cave,
                    new System.Random(17)
                );
                Physics.SyncTransforms();

                Renderer[] rocks = caveRocks.GetComponentsInChildren<Renderer>();
                Assert.That(rocks.Length, Is.GreaterThan(0));
                foreach (Renderer rock in rocks)
                {
                    Assert.That(rock.bounds.min.y, Is.GreaterThanOrEqualTo(-0.02f));
                    Assert.That(
                        rock.bounds.min.x,
                        Is.GreaterThanOrEqualTo(-DungeonLayout.RoomWidth / 2f)
                    );
                    Assert.That(
                        rock.bounds.max.x,
                        Is.LessThanOrEqualTo(DungeonLayout.RoomWidth / 2f)
                    );
                    Assert.That(
                        rock.bounds.min.z,
                        Is.GreaterThanOrEqualTo(-DungeonLayout.RoomDepth / 2f)
                    );
                    Assert.That(
                        rock.bounds.max.z,
                        Is.LessThanOrEqualTo(DungeonLayout.RoomDepth / 2f)
                    );
                }

                var dungeonDressing = new GameObject("Dungeon dressing");
                dungeonDressing.transform.SetParent(root.transform, false);
                placer.BuildBoundaryDressing(
                    dungeonDressing.transform,
                    Vector2Int.zero,
                    DungeonLayout.North,
                    DungeonLayout.EnvironmentTheme.Dungeon,
                    new System.Random(17)
                );
                Assert.That(dungeonDressing.transform.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(0f, 0f, 1f, true)]
        [TestCase(13.5f, 0f, 1f, false)]
        [TestCase(0f, 9.5f, 1f, false)]
        public void AuthoredPropsMustFitCompletelyOnTheRoomFloor(
            float x,
            float z,
            float radius,
            bool expected
        )
        {
            Assert.That(
                DungeonPropPlacer.FitsOnRoomFloor(new Vector2(x, z), radius),
                Is.EqualTo(expected)
            );
        }

        private static int SouthY(DungeonLayout layout, int x)
        {
            return layout.ClampToPlayableBand(new Vector2Int(x, -1000)).y;
        }
    }
}
