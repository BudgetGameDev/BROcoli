using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Property tests over the unit the visibility system reasons in: the logical
    /// wall group, not the individual prefab. A freestanding structure has to be
    /// one group so a cross never drops one arm and keeps the other, while a
    /// room's boundary runs have to stay separate so lowering the south wall does
    /// not take the east and west walls with it.
    /// </summary>
    public sealed class WallVisibilityGroupingTests
    {
        /// <summary>
        /// Two interior runs that meet are one object as far as the player is
        /// concerned, so they lower together.
        /// </summary>
        [Test]
        public void TouchingInteriorRunsShareOneWallGroup()
        {
            foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
            {
                var world = new WallVisibilityWorld(block);
                for (int i = 0; i < world.Pieces.Count; i++)
                for (int j = i + 1; j < world.Pieces.Count; j++)
                {
                    WallVisibilityWorld.Piece first = world.PieceOf(i);
                    WallVisibilityWorld.Piece second = world.PieceOf(j);
                    if (
                        !first.IsWall
                        || !second.IsWall
                        || first.Plan.Kind != DungeonWallKind.Interior
                        || second.Plan.Kind != DungeonWallKind.Interior
                        || !DungeonWallGrouping.AreInContact(first.Plan, second.Plan)
                    )
                        continue;

                    Assert.That(
                        second.GroupId,
                        Is.EqualTo(first.GroupId),
                        $"seed {block.Seed}: {first.Plan} touches {second.Plan} but they fade "
                            + $"as separate groups "
                            + $"\"{world.GroupOf(first.GroupId).Name}\" and "
                            + $"\"{world.GroupOf(second.GroupId).Name}\""
                    );
                }
            }
        }

        /// <summary>
        /// Crossing interior structures necessarily contain an east-west arm with
        /// playable floor behind it, so procedural rooms must no longer emit one.
        /// </summary>
        [Test]
        public void TheCorpusContainsNoCrossingInteriorStructure()
        {
            Assert.That(
                WallVisibilityFixtures.TryFindInteriorStructure(out int seed, out Vector2Int room),
                Is.False,
                $"seed {seed}: room {room} still builds crossing interior runs"
            );
        }

        /// <summary>Every group is one connected structure, not a scattered set.</summary>
        [Test]
        public void EveryInteriorGroupIsOneConnectedStructure()
        {
            foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
            {
                var world = new WallVisibilityWorld(block);
                foreach (WallVisibilityWorld.Group group in world.Groups)
                {
                    if (group.Kind != DungeonWallKind.Interior || group.Pieces.Count == 0)
                        continue;

                    Assert.That(
                        ConnectedCount(world, group),
                        Is.EqualTo(group.Pieces.Count),
                        $"seed {block.Seed}: group \"{group.Name}\" fades {group.Pieces.Count} "
                            + "pieces that are not all part of one structure"
                    );
                }
            }
        }

        /// <summary>
        /// Crossing the seam between two prefabs of one planned run must not change
        /// which group is active, so a run is never split across groups.
        /// </summary>
        [Test]
        public void EveryPieceOfAPlannedRunSharesItsGroup()
        {
            foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
            {
                var world = new WallVisibilityWorld(block);
                var bySection = new Dictionary<(string, Vector2Int), int>();
                foreach (WallVisibilityWorld.Piece piece in world.Pieces)
                {
                    if (!piece.IsWall || piece.Plan.Kind != DungeonWallKind.Interior)
                        continue;
                    var key = (piece.Plan.Section, world.GroupOf(piece.GroupId).Room);
                    if (!bySection.TryAdd(key, piece.GroupId))
                        Assert.That(
                            piece.GroupId,
                            Is.EqualTo(bySection[key]),
                            $"seed {block.Seed}: run \"{piece.Plan.Section}\" is split across "
                                + "two wall groups, so its prefabs can transition apart"
                        );
                }
            }
        }

        /// <summary>
        /// The reason a wall group never runs on into the next room: a grid post is
        /// always walled, on every run that meets it. The player can never walk
        /// along a wall from one room into the next in the same line, so the runs
        /// on either side of a post are two different walls and fade separately.
        ///
        /// If the generator ever opens an end slot, this fails - and the case for
        /// keeping the runs apart has to be revisited.
        /// </summary>
        [Test]
        public void BoundaryRunsAreAlwaysWalledAtTheirPosts()
        {
            foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
            {
                foreach (Vector2Int room in block.Rooms)
                {
                    for (int direction = 0; direction < 4; direction++)
                    {
                        DungeonPassage passage = block.Passage(room, direction);
                        int slots = DungeonGeometryModel.SlotCount(direction);
                        Assert.That(
                            passage.HasOpening(0) || passage.HasOpening(slots - 1),
                            Is.False,
                            $"seed {block.Seed}: room {room} side {direction} opens a doorway at a "
                                + "grid post, so a player could walk along this wall into the next "
                                + "room and the two runs would have to fade as one"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// A boundary group is exactly one built run, standing on one boundary
        /// line. Nothing in another room shares it, so a fade can never carry
        /// across a post into the next room's wall.
        /// </summary>
        [Test]
        public void BoundaryGroupsStayOnTheirOwnBoundary()
        {
            foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
            {
                var world = new WallVisibilityWorld(block);
                foreach (WallVisibilityWorld.Group group in world.Groups)
                {
                    if (!group.IsEdge)
                        continue;

                    Vector2 center = DungeonLayout.RoomCenter(group.Room);
                    float boundary = group.AlongX
                        ? center.y + DungeonLayout.RoomDepth / 2f
                        : center.x + DungeonLayout.RoomWidth / 2f;
                    foreach (int pieceId in group.Pieces)
                    {
                        WallVisibilityWorld.Piece piece = world.PieceOf(pieceId);
                        if (!piece.IsWall)
                            continue;

                        Assert.That(
                            piece.Plan.AlongX,
                            Is.EqualTo(group.AlongX),
                            $"seed {block.Seed}: \"{group.Name}\" contains {piece.Plan}, which "
                                + "runs across the boundary rather than along it"
                        );
                        Assert.That(
                            group.AlongX ? piece.Plan.Anchor.y : piece.Plan.Anchor.x,
                            Is.EqualTo(boundary).Within(0.001f),
                            $"seed {block.Seed}: \"{group.Name}\" contains {piece.Plan}, which "
                                + "stands on a different boundary and would fade with it"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// An archway is part of the run it stands in, not a group of its own, so
        /// it cannot fade a moment before the wall around it.
        /// </summary>
        [Test]
        public void ArchwaysShareTheGroupOfTheRunTheyStandIn()
        {
            int archways = 0;
            foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
            {
                var world = new WallVisibilityWorld(block);
                foreach (WallVisibilityWorld.Piece piece in world.Pieces)
                {
                    if (!piece.IsGateway)
                        continue;

                    archways++;
                    WallVisibilityWorld.Group group = world.GroupOf(piece.GroupId);
                    Assert.That(
                        group.IsEdge,
                        $"seed {block.Seed}: {piece.Label} is not part of a boundary run"
                    );
                    Assert.That(
                        group.Pieces.Count,
                        Is.GreaterThan(1),
                        $"seed {block.Seed}: {piece.Label} fades as a group of its own"
                    );
                }
            }

            Assert.That(archways, Is.GreaterThan(0), "the corpus builds no archways to test");
        }

        private static int ConnectedCount(
            WallVisibilityWorld world,
            WallVisibilityWorld.Group group
        )
        {
            var reached = new HashSet<int> { group.Pieces[0] };
            var queue = new Queue<int>();
            queue.Enqueue(group.Pieces[0]);
            while (queue.Count > 0)
            {
                DungeonWallPiece current = world.PieceOf(queue.Dequeue()).Plan;
                foreach (int candidateId in group.Pieces)
                {
                    if (
                        reached.Contains(candidateId)
                        || !DungeonWallGrouping.AreInContact(
                            current,
                            world.PieceOf(candidateId).Plan
                        )
                    )
                        continue;
                    reached.Add(candidateId);
                    queue.Enqueue(candidateId);
                }
            }
            return reached.Count;
        }
    }
}
