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

                    // An edge with playable floor on neither side is off the map
                    // rather than on its rim. Nothing the generator builds asks
                    // about one, but the answer still has to be an environment
                    // rather than whichever theme the enum happens to start on.
                    var offMap = new Vector2Int(x, south + 4);
                    Assert.That(layout.IsPlayableRoom(offMap), Is.False);
                    Assert.That(
                        layout.EnvironmentAt(
                            DungeonLayout.EdgeBetween(offMap, DungeonLayout.North)
                        ),
                        Is.EqualTo(layout.EnvironmentAt(offMap))
                    );
                }
            }
        }

        /// <summary>
        /// Every environment answers for itself. A theme with no profile of its own
        /// would silently borrow the dungeon's carpentry, which is exactly the bug
        /// the profile table exists to prevent.
        /// </summary>
        [Test]
        public void EveryEnvironmentHasAProfileOfItsOwn()
        {
            foreach (
                DungeonLayout.EnvironmentTheme theme in System.Enum.GetValues(
                    typeof(DungeonLayout.EnvironmentTheme)
                )
            )
            {
                DungeonEnvironmentProfile profile = DungeonEnvironmentProfile.Of(theme);
                Assert.That(
                    profile.RubbleTokens,
                    Is.Not.Empty,
                    $"{theme} has no terrain debris of its own"
                );
                Assert.That(
                    profile.ClutterTokens,
                    Is.Not.Empty,
                    $"{theme} has no clutter of its own"
                );
                Assert.That(
                    profile.PathwayTokens,
                    Is.Not.Empty,
                    $"{theme} has nothing to dress a route with"
                );
                Assert.That(
                    profile.BoundaryTokens,
                    profile.BoundaryStyle == DungeonBoundaryStyle.RockLine
                        ? Is.Not.Empty
                        : Is.Empty,
                    $"{theme} names boundary props it will never place, or none it needs"
                );
            }
        }

        /// <summary>
        /// Every environment builds the same structural shell, and every boundary
        /// style builds it: the theme decides what stands on the masonry, never
        /// whether there is masonry. A theme whose own boundary kit is still
        /// missing used to get an invisible collision line, which is a stretch of
        /// level that simply ends.
        /// </summary>
        [Test]
        public void EveryEnvironmentAndEdgeStyleBuildsTheSameOuterShell()
        {
            var host = new GameObject("Outer shell host");
            var root = new GameObject("Outer shell root");
            try
            {
                DungeonRoomBuilder builder = DungeonPropFixtures.Builder(host);
                foreach (
                    DungeonLayout.EnvironmentTheme theme in System.Enum.GetValues(
                        typeof(DungeonLayout.EnvironmentTheme)
                    )
                )
                foreach (
                    DungeonEdgeStyle style in new[]
                    {
                        DungeonEdgeStyle.SolidBoundary,
                        DungeonEdgeStyle.SouthCliff,
                        DungeonEdgeStyle.SideCliff,
                    }
                )
                {
                    bool horizontal = style != DungeonEdgeStyle.SideCliff;
                    GameObject built = builder.BuildEdge(
                        root.transform,
                        new DungeonEdge(0, -1, horizontal),
                        new DungeonPassage(false, 0, 0),
                        style,
                        theme
                    );

                    Transform parapet = built.transform.Find("Low Dungeon Railing");
                    Transform cliff = built.transform.Find("Cliff Face Below Floor");
                    Assert.That(parapet, Is.Not.Null, $"{theme} {style} has no parapet");
                    Assert.That(cliff, Is.Not.Null, $"{theme} {style} has no cliff face");

                    int slots = horizontal ? DungeonLayout.RoomTilesX : DungeonLayout.RoomTilesZ;
                    Assert.That(parapet.childCount, Is.EqualTo(slots), $"{theme} {style} lip");
                    Assert.That(
                        cliff.childCount,
                        Is.EqualTo(slots * DungeonRoomGeometry.CliffCourses),
                        $"{theme} {style} cliff courses"
                    );
                    Assert.That(
                        built.GetComponentsInChildren<Renderer>(),
                        Is.Not.Empty,
                        $"{theme} {style} builds nothing anyone can see"
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CavesDressTheirBoundaryWithGroundedRocksAndDungeonsDoNot()
        {
            var host = new GameObject("Environment boundary host");
            var root = new GameObject("Environment boundary root");
            try
            {
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
