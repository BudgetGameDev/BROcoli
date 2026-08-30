using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Lowering a wall is visible from anywhere on screen, so a wall that drops
    /// because of an enemy the player has not reached announces the contents of a
    /// room before the player enters it. These tests hold the gate that stops
    /// that, and check it still lets the enemies that matter through.
    /// </summary>
    public sealed class WallVisibilityEnemyRevealTests
    {
        /// <summary>
        /// An enemy two rooms away changes nothing: the walk lowers exactly the
        /// same walls with the enemy present as without it.
        /// </summary>
        [Test]
        public void AnEnemyInAnUnvisitedRoomNeverLowersAWall()
        {
            Assert.That(
                WallVisibilityFixtures.TryFindClosedSouthWall(out int seed, out Vector2Int room),
                "no seed in the corpus has a room with an unbroken south wall"
            );

            var world = WallVisibilityFixtures.World(seed, room, 2);
            Vector2 center = DungeonLayout.RoomCenter(room);
            Vector2 distant = DungeonLayout.RoomCenter(room + new Vector2Int(0, -2));
            var enemy = new Vector3(distant.x, 0f, distant.y);
            List<Vector3> path = WallVisibilityPaths.Polyline(
                new Vector3(center.x - 8f, 0f, center.y),
                new Vector3(center.x + 8f, 0f, center.y)
            );

            WallVisibilitySimulation.Result alone = WallVisibilitySimulation.Run(
                world,
                path,
                WallVisibilitySimulation.CameraConfig.Dungeon
            );
            WallVisibilitySimulation.Result watched = WallVisibilitySimulation.Run(
                world,
                path,
                WallVisibilitySimulation.CameraConfig.Dungeon,
                new[] { enemy }
            );
            WallVisibilityInvariants.AssertAll(watched);

            for (int index = 0; index < alone.Frames.Count; index++)
            {
                Assert.That(
                    alone.Frames[index].PlayerRoom,
                    Is.Not.EqualTo(DungeonLayout.RoomAt(new Vector2(enemy.x, enemy.z))),
                    "the fixture walk entered the enemy's room, so it does not test the gate"
                );
                CollectionAssert.AreEqual(
                    alone.Frames[index].LoweredGroups,
                    watched.Frames[index].LoweredGroups,
                    WallVisibilityDiagnostics.Report(
                        watched,
                        index,
                        "an enemy the player has not reached changed which walls are lowered, "
                            + "giving away that the room ahead is occupied"
                    )
                );
            }
        }

        /// <summary>
        /// The gate is not a blanket refusal: an enemy in the player's own room
        /// still gets the wall in front of it out of the way. The player stands in
        /// front of an interior run and the enemy behind it, so the wall hides the
        /// enemy without hiding the player.
        /// </summary>
        [Test]
        public void AnEnemyInThePlayersRoomStillLowersTheWallInFrontOfIt()
        {
            Assert.That(
                WallVisibilityFixtures.TryFindInteriorScreen(
                    out int seed,
                    out Vector2Int room,
                    out Vector2 anchor
                ),
                "no seed in the corpus has a room with an interior run to hide behind"
            );

            var world = WallVisibilityFixtures.World(seed, room, 1);
            int screen = GroupAt(world, anchor);
            Assert.That(screen, Is.Not.EqualTo(-1), $"seed {seed}: no interior run at {anchor}");

            var player = new Vector3(anchor.x, 0f, anchor.y - 3f);
            var enemy = new Vector3(anchor.x, 0f, anchor.y + 2.5f);
            WallVisibilitySimulation.Result alone = WallVisibilitySimulation.Run(
                world,
                WallVisibilityPaths.Hold(player, 1f),
                WallVisibilitySimulation.CameraConfig.Dungeon
            );
            WallVisibilitySimulation.Result withEnemy = WallVisibilitySimulation.Run(
                world,
                WallVisibilityPaths.Hold(player, 1f),
                WallVisibilitySimulation.CameraConfig.Dungeon,
                new[] { enemy }
            );
            WallVisibilityInvariants.AssertAll(withEnemy);

            Assert.That(
                alone.Frames[^1].IsLowered(screen),
                Is.False,
                "the fixture already lowers the run without an enemy, so it cannot show "
                    + "that the enemy is what lowered it"
            );
            Assert.That(
                withEnemy.Frames[^1].IsLowered(screen),
                WallVisibilityDiagnostics.Report(
                    withEnemy,
                    withEnemy.Frames.Count - 1,
                    "an enemy in the player's own room stayed hidden behind a wall",
                    new[] { screen }
                )
            );
        }

        private static int GroupAt(WallVisibilityWorld world, Vector2 anchor)
        {
            foreach (WallVisibilityWorld.Piece piece in world.Pieces)
            {
                if (piece.IsWall && Vector2.Distance(piece.Plan.Anchor, anchor) < 0.01f)
                    return piece.GroupId;
            }
            return -1;
        }

        /// <summary>The gate opens only when both characters share a room.</summary>
        [Test]
        public void TheRevealGateNeverOpensAcrossARoomBoundary()
        {
            Vector2 center = DungeonLayout.RoomCenter(Vector2Int.zero);
            var player = new Vector3(center.x, 0f, center.y);
            Assert.That(
                EnemyRevealGate.IsRevealed(player, player + new Vector3(6f, 0f, 0f)),
                "an enemy in the player's own room must be visible"
            );

            Vector2 neighbour = DungeonLayout.RoomCenter(new Vector2Int(0, 1));
            Assert.That(
                EnemyRevealGate.IsRevealed(player, new Vector3(neighbour.x, 0f, neighbour.y)),
                Is.False,
                "an enemy in the next room must not lower a wall before the player gets there"
            );

            var justOverTheBoundary = new Vector3(
                center.x,
                0f,
                center.y + DungeonLayout.RoomDepth / 2f + 1f
            );
            Assert.That(
                EnemyRevealGate.IsRevealed(player + new Vector3(0f, 0f, 6f), justOverTheBoundary),
                Is.False,
                "an enemy just across the wall lowered it before the player entered the room"
            );
        }
    }
}
