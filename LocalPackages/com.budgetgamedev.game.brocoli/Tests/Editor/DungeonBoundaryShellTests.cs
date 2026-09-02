using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Property tests over the platform's outer shell. Where the playable strip
    /// ends, the world has to end with masonry: a parapet at floor level and cliff
    /// courses carrying it down past the floor, in every slot of every boundary
    /// edge, whichever way the edge faces and whatever environment it stands in.
    ///
    /// This is the rule that used to hold only in places. Three of the six
    /// environments had no boundary kit and got an invisible collision line, and
    /// boundaries facing away from the camera got a parapet with nothing under it,
    /// so stretches of the level ended in raw void that a player walking the edge
    /// looks straight into. Every hole of that kind is a hole in this sweep.
    /// </summary>
    public sealed class DungeonBoundaryShellTests
    {
        private const float Epsilon = 0.01f;

        /// <summary>Columns wide enough to cross several environment bands.</summary>
        private const int Columns = 26;

        /// <summary>
        /// Every side of a playable room that faces off the platform is an outer
        /// boundary. Nothing else can be: an interior run has doorways cut into it
        /// and an open crossing builds no wall at all, and either one on the rim
        /// would be a way to walk off the edge of the world.
        /// </summary>
        [Test]
        public void EverySideFacingOffThePlatformIsABoundary()
        {
            int boundaries = 0;
            foreach ((int seed, DungeonLayout layout, Vector2Int room) in PlayableRooms())
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    Vector2Int neighbour = room + DungeonLayout.DirectionOffsets[direction];
                    if (layout.IsPlayableRoom(neighbour))
                        continue;

                    DungeonEdgeStyle style = layout.PlayableEdgeStyle(
                        DungeonLayout.EdgeBetween(room, direction)
                    );
                    Assert.That(
                        DungeonRoomGeometry.IsPlatformBoundary(style),
                        Is.True,
                        $"seed {seed}: room {room} opens onto the void on side {direction} "
                            + $"with a {style} edge"
                    );
                    boundaries++;
                }
            }

            Assert.That(boundaries, Is.GreaterThan(0), "the sweep found no boundary to check");
        }

        /// <summary>
        /// Every slot of every boundary carries a full stack of masonry, and the
        /// stacks are identical everywhere: one knee-high parapet seated at floor
        /// level, and cliff courses under it. A missing slot is a gap in the shell;
        /// a missing course is a railing hanging over nothing.
        /// </summary>
        [Test]
        public void EveryBoundarySlotCarriesTheWholeShell()
        {
            int checkedEdges = 0;
            foreach ((int seed, DungeonLayout layout, Vector2Int room) in PlayableRooms())
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                    if (!DungeonRoomGeometry.IsPlatformBoundary(layout.PlayableEdgeStyle(edge)))
                        continue;

                    AssertThatTheShellIsWhole(seed, room, direction, Facade(edge));
                    checkedEdges++;
                }
            }

            Assert.That(checkedEdges, Is.GreaterThan(0), "the sweep found no boundary to check");
        }

        /// <summary>
        /// The shell reaches every environment. Themes are shuffled per seed and
        /// spread over ten columns each, so a sweep that never left the starting
        /// dungeon band would pass the rule above while saying nothing about the
        /// bands where it used to be broken.
        /// </summary>
        [Test]
        public void TheSweepCrossesEveryEnvironment()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                var reached = new HashSet<DungeonLayout.EnvironmentTheme>();
                foreach (Vector2Int room in PlayableRooms(layout))
                    reached.Add(layout.EnvironmentAt(room));

                Assert.That(
                    reached,
                    Is.EquivalentTo(System.Enum.GetValues(typeof(DungeonLayout.EnvironmentTheme))),
                    $"seed {seed}: the sweep never entered every environment"
                );
            }
        }

        /// <summary>
        /// The stacks are contiguous and flat-faced: each course's top meets the
        /// base of the one above it, and every course of a slot stands on the same
        /// centre line. A step between courses is a ledge the camera sees along,
        /// and a gap between them is a window into the void.
        /// </summary>
        [Test]
        public void TheShellIsOneUnbrokenFace()
        {
            var courses = new List<DungeonBoundaryCourse>();
            DungeonRoomGeometry.AppendBoundaryCourses(
                courses,
                new DungeonWallPiece(Vector2.zero, true, DungeonWallKind.Shell, "Wall Run")
            );
            courses.Sort((first, second) => second.Lift.CompareTo(first.Lift));

            for (int course = 1; course < courses.Count; course++)
            {
                Assert.That(
                    courses[course].Top,
                    Is.EqualTo(courses[course - 1].Lift).Within(Epsilon),
                    "the cliff courses do not meet"
                );
                Assert.That(
                    courses[course].Piece.Anchor,
                    Is.EqualTo(courses[course - 1].Piece.Anchor),
                    "a course stepped off the boundary line"
                );
            }
        }

        /// <summary>One slot's stack, judged against the shape every slot must have.</summary>
        private static void AssertThatTheShellIsWhole(
            int seed,
            Vector2Int room,
            int direction,
            IReadOnlyDictionary<int, List<DungeonBoundaryCourse>> facade
        )
        {
            int slots = DungeonGeometryModel.SlotCount(direction);
            string where = $"seed {seed}: room {room} side {direction}";
            Assert.That(facade, Has.Count.EqualTo(slots), $"{where} leaves a gap in its shell");

            foreach (KeyValuePair<int, List<DungeonBoundaryCourse>> slot in facade)
            {
                List<DungeonBoundaryCourse> courses = slot.Value;
                Assert.That(
                    courses,
                    Has.Count.EqualTo(DungeonRoomGeometry.BoundaryCourses),
                    $"{where} slot {slot.Key} is not the full stack"
                );

                var parapets = courses.FindAll(course => course.Parapet);
                Assert.That(parapets, Has.Count.EqualTo(1), $"{where} slot {slot.Key} has no lip");
                Assert.That(
                    parapets[0].Lift,
                    Is.EqualTo(0f).Within(0.02f),
                    $"{where} slot {slot.Key} seats its parapet off the floor"
                );

                float deepest = 0f;
                foreach (DungeonBoundaryCourse course in courses)
                    deepest = Mathf.Min(deepest, course.Lift);
                Assert.That(
                    deepest,
                    Is.LessThan(-6f),
                    $"{where} slot {slot.Key} stops short of reading as a drop"
                );
            }
        }

        /// <summary>The planned facade of one edge, gathered by the slot it stands in.</summary>
        private static Dictionary<int, List<DungeonBoundaryCourse>> Facade(DungeonEdge edge)
        {
            var pieces = new List<DungeonWallPiece>();
            DungeonRoomGeometry.AppendEdgeWalls(pieces, edge, new DungeonPassage(false, 0, 0));

            var courses = new List<DungeonBoundaryCourse>();
            foreach (DungeonWallPiece piece in pieces)
                DungeonRoomGeometry.AppendBoundaryCourses(courses, piece);

            var facade = new Dictionary<int, List<DungeonBoundaryCourse>>();
            foreach (DungeonBoundaryCourse course in courses)
            {
                int slot = pieces.FindIndex(piece => piece.Anchor == course.Piece.Anchor);
                if (!facade.TryGetValue(slot, out List<DungeonBoundaryCourse> stack))
                    facade[slot] = stack = new List<DungeonBoundaryCourse>();
                stack.Add(course);
            }
            return facade;
        }

        /// <summary>Every playable room of a wide band of the strip, for every seed.</summary>
        private static IEnumerable<(
            int Seed,
            DungeonLayout Layout,
            Vector2Int Room
        )> PlayableRooms()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                foreach (Vector2Int room in PlayableRooms(layout))
                    yield return (seed, layout, room);
            }
        }

        /// <summary>Both rows of the two-room-deep strip, column by column.</summary>
        private static IEnumerable<Vector2Int> PlayableRooms(DungeonLayout layout)
        {
            for (int x = -Columns; x <= Columns; x++)
            {
                Vector2Int south = layout.ClampToPlayableBand(new Vector2Int(x, int.MinValue / 2));
                yield return south;
                yield return south + Vector2Int.up;
            }
        }
    }
}
