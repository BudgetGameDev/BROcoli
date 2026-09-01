using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Property tests over the curved and diagonal railing chains. Railings are
    /// the one piece of dungeon geometry allowed off the grid axes, so these
    /// pin the envelope that keeps them safe: they stay inside the interior
    /// box (preserving the perimeter corridor and with it doorway
    /// connectivity), they are buildable lengths of the modular masonry, and
    /// they regenerate identically.
    /// </summary>
    public sealed class DungeonRailingGeometryTests
    {
        private const float Epsilon = 0.01f;

        private static IEnumerable<(int Seed, Vector2Int Room, DungeonLayout Layout)> Rooms()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -12; x <= 12; x++)
                for (int y = -3; y <= 3; y++)
                    yield return (seed, new Vector2Int(x, y), layout);
            }
        }

        private static List<DungeonRailingSegment> RailingsOf(DungeonLayout layout, Vector2Int room)
        {
            var railings = new List<DungeonRailingSegment>();
            DungeonRoomGeometry.AppendInteriorRailings(railings, room, layout.Archetype(room));
            return railings;
        }

        /// <summary>
        /// Every railing stays at least one tile clear of the outer shell, the
        /// same envelope interior wall runs obey. This is what guarantees a
        /// chain can never reach a doorway or cut the perimeter corridor that
        /// keeps all of a room's doors mutually reachable.
        /// </summary>
        [Test]
        public void RailingsStayInsideTheInteriorEnvelope()
        {
            foreach ((int seed, Vector2Int room, DungeonLayout layout) in Rooms())
            {
                Vector2 center = DungeonLayout.RoomCenter(room);
                foreach (DungeonRailingSegment segment in RailingsOf(layout, room))
                {
                    foreach (Vector2 point in new[] { segment.Start, segment.End })
                    {
                        Vector2 local = point - center;
                        Assert.That(
                            Mathf.Abs(local.x),
                            Is.LessThanOrEqualTo(
                                DungeonRoomGeometry.InteriorHalfWidthLimit + Epsilon
                            ),
                            $"seed {seed}: {segment} leaves room {room}'s interior envelope"
                        );
                        Assert.That(
                            Mathf.Abs(local.y),
                            Is.LessThanOrEqualTo(
                                DungeonRoomGeometry.InteriorHalfDepthLimit + Epsilon
                            ),
                            $"seed {seed}: {segment} leaves room {room}'s interior envelope"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Chains are built from the one modular wall mesh, trimmed by scaling.
        /// Pieces much shorter than a metre read as pebbles and much longer
        /// than a tile defeats the point of curving, so lengths stay in a band.
        /// </summary>
        [Test]
        public void RailingPiecesAreBuildableLengths()
        {
            foreach ((int seed, Vector2Int room, DungeonLayout layout) in Rooms())
            {
                foreach (DungeonRailingSegment segment in RailingsOf(layout, room))
                {
                    Assert.That(
                        segment.Length,
                        Is.InRange(1f, DungeonWallPiece.NominalLength),
                        $"seed {seed}: {segment} in room {room} is not a buildable length"
                    );
                }
            }
        }

        /// <summary>
        /// Neighbouring pieces of one chain must never seat on the same lift
        /// plane, or their overlapping base aprons z-fight at the joint.
        /// </summary>
        [Test]
        public void TouchingRailingPiecesSeatOnDifferentLiftPlanes()
        {
            foreach ((int seed, Vector2Int room, DungeonLayout layout) in Rooms())
            {
                List<DungeonRailingSegment> railings = RailingsOf(layout, room);
                for (int i = 0; i < railings.Count; i++)
                for (int j = i + 1; j < railings.Count; j++)
                {
                    DungeonRailingSegment first = railings[i];
                    DungeonRailingSegment second = railings[j];
                    bool touching =
                        first.DistanceTo(second.Start) < 0.05f
                        || first.DistanceTo(second.End) < 0.05f;
                    if (!touching)
                        continue;
                    Assert.That(
                        first.BaseLift,
                        Is.Not.EqualTo(second.BaseLift).Within(0.00001f),
                        $"seed {seed}: touching railing pieces share a lift plane in {room}"
                    );
                }
            }
        }

        /// <summary>A rebuilt room plans exactly the same chains.</summary>
        [Test]
        public void RailingPlansAreDeterministic()
        {
            foreach ((int seed, Vector2Int room, DungeonLayout layout) in Rooms())
            {
                List<DungeonRailingSegment> first = RailingsOf(layout, room);
                List<DungeonRailingSegment> second = RailingsOf(layout, room);
                Assert.That(second.Count, Is.EqualTo(first.Count));
                for (int i = 0; i < first.Count; i++)
                {
                    Assert.That(second[i].Start, Is.EqualTo(first[i].Start));
                    Assert.That(second[i].End, Is.EqualTo(first[i].End));
                }
            }
        }

        /// <summary>
        /// A causeway's parapets always leave the aligned central break, so the
        /// bridge can be entered from north and south as well as ridden along.
        /// </summary>
        [Test]
        public void CausewayParapetsKeepTheirCentralBreak()
        {
            var archetype = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Causeway,
                DungeonLayout.RoomTheme.Flooded,
                10.2f,
                2.4f,
                0
            );
            var railings = new List<DungeonRailingSegment>();
            DungeonRoomGeometry.AppendInteriorRailings(railings, Vector2Int.zero, archetype);

            Assert.That(railings, Is.Not.Empty);
            foreach (DungeonRailingSegment segment in railings)
            {
                Assert.That(
                    Mathf.Abs(segment.Center.y),
                    Is.EqualTo(1.95f).Within(Epsilon),
                    "causeway railing is not a parapet line"
                );
                bool coversCenter =
                    Mathf.Min(segment.Start.x, segment.End.x) < 0.9f
                    && Mathf.Max(segment.Start.x, segment.End.x) > -0.9f;
                Assert.That(coversCenter, Is.False, $"{segment} closes the causeway's break");
            }
        }

        /// <summary>
        /// The level breathes: pinch columns exist, recur, and every non-mega
        /// room in one narrows into a lane shape, while most other columns
        /// stay open. Progression alternates constriction and release.
        /// </summary>
        [Test]
        public void PinchColumnsConstrictEveryRoomInTheColumn()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                int pinchColumns = 0;
                for (int x = -18; x <= 18; x++)
                {
                    if (!layout.IsPinchColumn(x))
                        continue;
                    pinchColumns++;
                    for (int y = -2; y <= 2; y++)
                    {
                        var room = new Vector2Int(x, y);
                        if (DungeonLayout.Ring(room) == 0)
                            continue;
                        if (layout.TryGetMegaCluster(room, out _, out _))
                            continue;
                        DungeonLayout.RoomShape shape = layout.Archetype(room).Shape;
                        Assert.That(
                            shape,
                            Is.EqualTo(DungeonLayout.RoomShape.Causeway)
                                .Or.EqualTo(DungeonLayout.RoomShape.NarrowHorizontal)
                                .Or.EqualTo(DungeonLayout.RoomShape.SerpentineHall),
                            $"seed {seed}: pinch column {x} holds open shape {shape}"
                        );
                    }
                }
                Assert.That(pinchColumns, Is.EqualTo(37 / 6).Within(1), $"seed {seed}");
            }
        }
    }
}
