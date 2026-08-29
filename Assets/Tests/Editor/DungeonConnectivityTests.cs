using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression tests for the walkable shape of a room. Walls are inflated by
/// the player's capsule radius before flooding, so "can the player get there"
/// and "is that corridor wide enough" are the same assertion.
/// </summary>
public sealed class DungeonConnectivityTests
{
    // A wall slab always sits on the positive side of its boundary line, so a
    // room's north and east walls stand outside its floor rect while its south
    // and west walls stand inside it. The fill domain therefore only extends
    // past the north and east boundaries, and only as far as the slab reaches:
    // far enough to see a fill escape through a hole, not far enough to walk
    // into the neighbouring room. Every wall is some room's north or east wall,
    // so sweeping all rooms still checks all four sides of each.
    private const float DomainMargin = DungeonWallPiece.SlabFarFace;

    private static IEnumerable<(DungeonGeometryModel Block, Vector2Int Room)> Rooms()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        foreach ((Vector2Int center, int radius) in DungeonGeometryModel.SampleBlocks())
        {
            var block = new DungeonGeometryModel(seed, center, radius + 1);
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                yield return (block, center + new Vector2Int(dx, dy));
        }
    }

    /// <summary>
    /// The regression that mattered: an interior wall must never cut a doorway
    /// off from the rest of its room. Every doorway of a room has to be
    /// reachable from every other doorway of that room.
    /// </summary>
    [Test]
    public void EveryDoorwayReachesEveryOtherDoorway()
    {
        foreach ((DungeonGeometryModel block, Vector2Int room) in Rooms())
        {
            List<Vector2> doorways = Doorways(block, room);
            Assert.That(
                doorways,
                Is.Not.Empty,
                $"seed {block.Seed}: room {room} has no way in or out"
            );

            DungeonWalkableSpace space = Space(block, room);
            space.Flood(doorways[0]);
            foreach (Vector2 doorway in doorways)
            {
                Assert.That(
                    space.IsReached(doorway),
                    $"seed {block.Seed}: room {room} doorway at {doorway} is walled off from "
                        + $"the doorway at {doorways[0]}"
                );
            }
        }
    }

    /// <summary>
    /// A doorway must be standable, not merely present. A wall growing into
    /// the gap would leave the opening too narrow for the player capsule.
    /// </summary>
    [Test]
    public void DoorwaysAreWideEnoughToWalkThrough()
    {
        foreach ((DungeonGeometryModel block, Vector2Int room) in Rooms())
        {
            DungeonWalkableSpace space = Space(block, room);
            foreach (Vector2 doorway in Doorways(block, room))
            {
                Assert.That(
                    space.IsFree(doorway),
                    $"seed {block.Seed}: room {room} doorway at {doorway} is obstructed"
                );
            }
        }
    }

    /// <summary>
    /// The shell has no player-sized hole: any point outside the room the
    /// player can walk to has to be inside one of that room's doorways.
    /// </summary>
    [Test]
    public void RoomsCanOnlyBeLeftThroughADoorway()
    {
        foreach ((DungeonGeometryModel block, Vector2Int room) in Rooms())
        {
            List<Vector2> doorways = Doorways(block, room);
            if (doorways.Count == 0)
                continue;

            DungeonWalkableSpace space = Space(block, room);
            space.Flood(doorways[0]);

            Rect bounds = DungeonRoomGeometry.RoomFloorBounds(room);
            float reach = DungeonLayout.TileSize / 2f + DungeonGeometryModel.PlayerRadius;
            foreach (Vector2 point in space.ReachedPoints())
            {
                if (bounds.Contains(point))
                    continue;

                bool nearDoorway = false;
                foreach (Vector2 doorway in doorways)
                    nearDoorway |= Vector2.Distance(point, doorway) <= reach;

                Assert.That(
                    nearDoorway,
                    $"seed {block.Seed}: room {room} leaks at {point}, which is outside the "
                        + "room and not in a doorway"
                );
            }
        }
    }

    private static DungeonWalkableSpace Space(DungeonGeometryModel block, Vector2Int room)
    {
        Rect bounds = DungeonRoomGeometry.RoomFloorBounds(room);
        Rect domain = Rect.MinMaxRect(
            bounds.xMin,
            bounds.yMin,
            bounds.xMax + DomainMargin,
            bounds.yMax + DomainMargin
        );
        return new DungeonWalkableSpace(domain, block.Walls, DungeonGeometryModel.PlayerRadius);
    }

    private static List<Vector2> Doorways(DungeonGeometryModel block, Vector2Int room)
    {
        var doorways = new List<Vector2>();
        for (int direction = 0; direction < 4; direction++)
        {
            DungeonPassage passage = block.Passage(room, direction);
            int slots = DungeonGeometryModel.SlotCount(direction);
            for (int slot = 0; slot < slots; slot++)
            {
                if (passage.HasOpening(slot))
                    doorways.Add(DungeonGeometryModel.SlotCenter(room, direction, slot));
            }
        }
        return doorways;
    }
}
