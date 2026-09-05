using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Property tests for what hangs on the walls. The rule under test is that a
    /// doorway stays a doorway: nothing is ever mounted in one.
    /// </summary>
    public sealed class DungeonWallDressingTests
    {
        private const int MaxTorchesPerRoom = 6;

        // A torch bracket sits flat against the slab face.
        private const float TorchMountDepth = 0.05f;

        /// <summary>A torch must never end up floating in a doorway.</summary>
        [Test]
        public void TorchesNeverHangInADoorway()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                foreach (Vector2Int room in SweepRooms())
                {
                    DungeonLayout.RoomArchetype archetype = layout.Archetype(room);
                    DungeonLayout.RoomDoorways doorways = layout.Doorways(room);
                    var random = new System.Random(
                        seed ^ (room.x * 73856093) ^ (room.y * 19349663)
                    );
                    foreach (
                        DungeonWallMount mount in DungeonWallDressing.TorchMounts(
                            archetype,
                            doorways,
                            MaxTorchesPerRoom,
                            random
                        )
                    )
                    {
                        Assert.That(
                            doorways.BlocksDoorway(
                                mount.Local,
                                DungeonWallDressing.TorchDoorwayClearance
                            ),
                            Is.False,
                            $"seed {seed}: room {room} ({archetype}) mounts a torch at "
                                + $"{mount.Local}, inside a doorway"
                        );
                    }
                }
            }
        }

        /// <summary>A room always keeps enough mounting points to stay lit.</summary>
        [Test]
        public void EveryRoomKeepsSomewhereToHangATorch()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                foreach (Vector2Int room in SweepRooms())
                {
                    var random = new System.Random(seed ^ room.x ^ room.y);
                    Assert.That(
                        DungeonWallDressing
                            .TorchMounts(
                                layout.Archetype(room),
                                layout.Doorways(room),
                                MaxTorchesPerRoom,
                                random
                            )
                            .Count,
                        Is.GreaterThan(0),
                        $"seed {seed}: room {room} has nowhere to mount a torch"
                    );
                }
            }
        }

        /// <summary>
        /// A closed edge is a plain wall: it carries no opening and so no archway.
        /// </summary>
        [Test]
        public void ClosedEdgesHaveNoOpeningAndNoArchway()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                foreach (Vector2Int room in SweepRooms())
                {
                    for (int direction = 0; direction < 4; direction++)
                    {
                        DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                        if (layout.IsDoorOpen(room, direction))
                            continue;

                        DungeonPassage passage = layout.Passage(edge, false);
                        Assert.That(passage.OpeningMask, Is.Zero, $"seed {seed}: room {room}");
                        Assert.That(passage.ArchwayMask, Is.Zero, $"seed {seed}: room {room}");
                    }
                }
            }
        }

        /// <summary>An open edge frames at most one of its doorways with an arch.</summary>
        [Test]
        public void OpenEdgesFrameAtMostOneDoorway()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                foreach (Vector2Int room in SweepRooms())
                {
                    for (int direction = 0; direction < 4; direction++)
                    {
                        if (!layout.IsDoorOpen(room, direction))
                            continue;

                        DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                        DungeonPassage passage = layout.Passage(edge, true);
                        int archways = 0;
                        int slots = DungeonGeometryModel.SlotCount(direction);
                        for (int slot = 0; slot < slots; slot++)
                        {
                            if (!passage.HasArchway(slot))
                                continue;
                            archways++;
                            Assert.That(
                                passage.HasOpening(slot),
                                $"seed {seed}: room {room} frames a solid wall piece with an arch"
                            );
                        }
                        Assert.That(archways, Is.LessThanOrEqualTo(1), $"seed {seed}: room {room}");
                    }
                }
            }
        }

        /// <summary>
        /// Independent of the doorway predicate the placement code uses: every
        /// fitting must have an actual wall piece behind it. A torch hanging in a
        /// gap fails this even if the predicate that should have caught it is the
        /// thing that broke.
        /// </summary>
        [Test]
        public void WallFittingsAlwaysHaveAWallBehindThem()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                foreach (Vector2Int room in SweepRooms())
                {
                    var block = new DungeonGeometryModel(seed, room, 0);
                    DungeonLayout.RoomArchetype archetype = block.Layout.Archetype(room);
                    DungeonLayout.RoomDoorways doorways = block.Layout.Doorways(room);
                    Vector2 center = DungeonLayout.RoomCenter(room);
                    var random = new System.Random(seed);
                    var railings = new List<DungeonRailingSegment>();
                    DungeonRoomGeometry.AppendInteriorRailings(railings, room, archetype);

                    foreach (
                        DungeonWallMount mount in DungeonWallDressing.TorchMounts(
                            archetype,
                            doorways,
                            MaxTorchesPerRoom,
                            random
                        )
                    )
                    {
                        Assert.That(
                            HasWallBehind(
                                center + mount.Local,
                                mount.Yaw,
                                block.Walls,
                                TorchMountDepth
                            )
                                || railings.Exists(railing =>
                                    Mathf.Abs(
                                        railing.DistanceTo(center + mount.Local)
                                            - DungeonRailingSegment.SlabHalfThickness
                                    ) < TorchMountDepth
                                ),
                            $"seed {seed}: room {room} ({archetype}) mounts a torch at "
                                + $"{mount.Local} with no wall behind it"
                        );
                    }
                }
            }
        }

        [Test]
        public void CurrentPlatformRoomsHaveLitMountsOnTheirBuiltInteriorMasonry()
        {
            foreach (int seed in new[] { 1846759163, 12345, 42, 117074, 221803 })
            {
                var layout = new DungeonLayout(seed);
                foreach (Vector2Int room in SweepRooms())
                {
                    if (!layout.IsPlayableRoom(room))
                        continue;
                    var archetype = layout.Archetype(room);
                    var walls = new List<DungeonWallPiece>();
                    var railings = new List<DungeonRailingSegment>();
                    DungeonRoomGeometry.AppendInteriorWalls(walls, room, archetype);
                    DungeonRoomGeometry.AppendInteriorRailings(railings, room, archetype);
                    // Empty cells of a merged hall have no wall to carry a fitting.
                    if (walls.Count + railings.Count == 0)
                        continue;
                    var mounts = DungeonWallDressing.TorchMounts(
                        archetype,
                        layout.PlayableDoorways(room),
                        4,
                        layout.RoomRandom(room, 707),
                        layout.ShellWallMask(room)
                    );
                    Assert.That(
                        mounts.Count,
                        Is.GreaterThan(0),
                        $"Actual platform seed {seed}, room {room}, {archetype} has no fire."
                    );
                    foreach (var mount in mounts)
                    {
                        Assert.That(
                            layout
                                .PlayableDoorways(room)
                                .BlocksDoorway(
                                    mount.Local,
                                    DungeonWallDressing.TorchDoorwayClearance
                                ),
                            Is.False
                        );
                        Assert.That(mount.HeightOffset, Is.InRange(-1.4f, 0.01f));
                    }
                }
            }
        }

        /// <summary>
        /// Whether a fitting facing <paramref name="yaw"/> has a wall piece within
        /// <paramref name="depth"/> behind it, spanning the point it hangs at.
        /// </summary>
        private static bool HasWallBehind(
            Vector2 point,
            float yaw,
            List<DungeonWallPiece> walls,
            float depth
        )
        {
            // Yaw 0 and 180 face across a wall that runs along X; +-90 face across
            // one that runs along Z.
            bool alongX = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) < 0.5f;
            foreach (DungeonWallPiece piece in walls)
            {
                if (piece.AlongX != alongX)
                    continue;

                Rect footprint = piece.Footprint;
                float along = alongX ? point.x : point.y;
                float alongMin = alongX ? footprint.xMin : footprint.yMin;
                float alongMax = alongX ? footprint.xMax : footprint.yMax;
                if (along < alongMin || along > alongMax)
                    continue;

                float normal = alongX ? point.y : point.x;
                float normalMin = alongX ? footprint.yMin : footprint.xMin;
                float normalMax = alongX ? footprint.yMax : footprint.xMax;
                float distance = Mathf.Max(normalMin - normal, normal - normalMax, 0f);
                if (distance <= depth)
                    return true;
            }
            return false;
        }

        private static IEnumerable<Vector2Int> SweepRooms()
        {
            for (int x = -3; x <= 3; x++)
            for (int y = -3; y <= 3; y++)
                yield return new Vector2Int(x, y);
        }
    }
}
