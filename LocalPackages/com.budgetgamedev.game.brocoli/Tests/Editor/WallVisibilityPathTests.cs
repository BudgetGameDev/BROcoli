using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Targeted walks through the situations the wall-visibility system has to get
    /// right: alongside a wall, across the seam between two built runs, into a
    /// corner, through a doorway, and past a room the player has already left.
    /// Each one asserts the general invariants and the behaviour specific to it.
    /// </summary>
    public sealed class WallVisibilityPathTests
    {
        /// <summary>How far from a post counts as clear of the next room's run.</summary>
        private const float Reach = 8f;

        /// <summary>
        /// The wall the player is walking beside stays down for the whole walk.
        /// A run that let go halfway along reads as the wall popping back up.
        /// </summary>
        [Test]
        public void WalkingParallelToAWallKeepsItLoweredThroughout()
        {
            WallVisibilitySimulation.Result result = AlongSouthWall(
                out WallVisibilityWorld world,
                out int group,
                8f
            );
            WallVisibilityInvariants.AssertAll(result);

            List<WallVisibilityEpisode> episodes = WallVisibilityEpisode.Of(result, group);
            Assert.That(
                episodes.Count,
                Is.EqualTo(1),
                WallVisibilityDiagnostics.Report(
                    result,
                    0,
                    $"the wall the player follows lowered in {episodes.Count} separate episodes",
                    new[] { group }
                )
            );
            Assert.That(
                episodes[0].StartFrame == 0 && episodes[0].EndFrame == result.Frames.Count - 1,
                WallVisibilityDiagnostics.Report(
                    result,
                    episodes[0].EndFrame,
                    "the wall the player follows did not stay lowered for the whole walk",
                    new[] { group }
                )
            );
            Assert.That(world.GroupOf(group).Pieces, Is.Not.Empty);
        }

        /// <summary>
        /// Hugging a wall must not fade the same wall in the next room. A grid post
        /// is always walled, so the player cannot walk on along that line - the run
        /// beyond the post is a wall in a room they would have to go around to
        /// reach, and lowering it shows them a room they are not in.
        /// </summary>
        [Test]
        public void WalkingAlongAWallLeavesTheNextRoomsWallStanding()
        {
            Assert.That(
                WallVisibilityFixtures.TryFindClosedSouthWallRun(out int seed, out Vector2Int room),
                "no seed in the corpus has two neighbouring rooms with unbroken south walls"
            );

            var world = WallVisibilityFixtures.World(seed, room, 2);
            int near = WallVisibilityFixtures.SouthEdgeGroup(world, room);
            int next = WallVisibilityFixtures.SouthEdgeGroup(world, room + Vector2Int.right);
            Assert.That(near, Is.Not.EqualTo(-1).And.Not.EqualTo(next));

            Vector2 center = DungeonLayout.RoomCenter(room);
            float seam = center.x + DungeonLayout.RoomWidth / 2f;
            float z =
                WallVisibilityFixtures.SouthBoundaryZ(room) + WallVisibilityFixtures.WallStandoff;
            WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
                world,
                WallVisibilityPaths.Polyline(
                    new Vector3(center.x - 8f, 0f, z),
                    new Vector3(seam - Reach, 0f, z)
                ),
                WallVisibilitySimulation.CameraConfig.Dungeon
            );
            WallVisibilityInvariants.AssertAll(result);

            foreach (WallVisibilitySimulation.Frame frame in result.Frames)
            {
                Assert.That(
                    frame.IsLowered(next),
                    Is.False,
                    WallVisibilityDiagnostics.Report(
                        result,
                        frame.Index,
                        "the next room's south wall lowered while the player was still walking "
                            + "along this room's, showing a room they cannot reach along that line",
                        new[] { near, next }
                    )
                );
            }
        }

        /// <summary>
        /// The two runs still hand over cleanly where they meet: walking across the
        /// post, one of them is always down, so the wall in front of the player
        /// never stands back up for a frame at the seam.
        /// </summary>
        [Test]
        public void CrossingASeamHandsTheFadeOverWithoutAGap()
        {
            Assert.That(
                WallVisibilityFixtures.TryFindClosedSouthWallRun(out int seed, out Vector2Int room),
                "no seed in the corpus has two neighbouring rooms with unbroken south walls"
            );

            var world = WallVisibilityFixtures.World(seed, room, 2);
            int near = WallVisibilityFixtures.SouthEdgeGroup(world, room);
            int next = WallVisibilityFixtures.SouthEdgeGroup(world, room + Vector2Int.right);
            Vector2 center = DungeonLayout.RoomCenter(room);
            float seam = center.x + DungeonLayout.RoomWidth / 2f;
            float z =
                WallVisibilityFixtures.SouthBoundaryZ(room) + WallVisibilityFixtures.WallStandoff;
            WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
                world,
                WallVisibilityPaths.Polyline(
                    new Vector3(seam - Reach, 0f, z),
                    new Vector3(seam + Reach, 0f, z)
                ),
                WallVisibilitySimulation.CameraConfig.Dungeon
            );
            WallVisibilityInvariants.AssertAll(result);

            foreach (WallVisibilitySimulation.Frame frame in result.Frames)
            {
                Assert.That(
                    frame.IsLowered(near) || frame.IsLowered(next),
                    WallVisibilityDiagnostics.Report(
                        result,
                        frame.Index,
                        "the wall in front of the player stood back up while they crossed the "
                            + "post between its two runs",
                        new[] { near, next }
                    )
                );
            }
        }

        private static WallVisibilitySimulation.Result AlongSouthWall(
            out WallVisibilityWorld world,
            out int group,
            float halfLength
        )
        {
            Assert.That(
                WallVisibilityFixtures.TryFindClosedSouthWall(out int seed, out Vector2Int room),
                "no seed in the corpus has a room with an unbroken south wall"
            );

            world = WallVisibilityFixtures.World(seed, room, 1);
            group = WallVisibilityFixtures.SouthEdgeGroup(world, room);
            Assert.That(group, Is.Not.EqualTo(-1), $"seed {seed}: room {room} has no south run");

            Vector2 center = DungeonLayout.RoomCenter(room);
            float z =
                WallVisibilityFixtures.SouthBoundaryZ(room) + WallVisibilityFixtures.WallStandoff;
            return WallVisibilitySimulation.Run(
                world,
                WallVisibilityPaths.Polyline(
                    new Vector3(center.x - halfLength, 0f, z),
                    new Vector3(center.x + halfLength, 0f, z)
                ),
                WallVisibilitySimulation.CameraConfig.Dungeon
            );
        }
    }
}
