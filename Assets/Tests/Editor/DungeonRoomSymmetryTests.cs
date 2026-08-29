using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Room-level invariants: that a room is symmetric about its own centre, that
/// it rebuilds identically, and that both rooms beside a wall agree on it.
/// </summary>
public sealed class DungeonRoomSymmetryTests
{
    private const float Epsilon = 0.001f;

    /// <summary>
    /// The property that makes a room easy to reason about: the space a player
    /// can stand in is centred on the room's own centre. Every slab straddles
    /// its boundary line, so the four walls sit the same distance out on all
    /// sides and RoomCenter is the middle of the room in the obvious sense.
    /// </summary>
    [Test]
    public void WalkableSpaceIsCentredOnTheRoomCentre()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            foreach (Vector2Int room in block.Rooms)
            {
                Vector2 center = DungeonLayout.RoomCenter(room);
                float north = float.MaxValue;
                float south = float.MinValue;
                float east = float.MaxValue;
                float west = float.MinValue;
                for (int direction = 0; direction < 4; direction++)
                {
                    foreach (DungeonWallPiece piece in block.EdgeWalls(room, direction))
                    {
                        Rect footprint = piece.Footprint;
                        if (piece.AlongX)
                        {
                            if (footprint.yMin > center.y)
                                north = Mathf.Min(north, footprint.yMin);
                            else
                                south = Mathf.Max(south, footprint.yMax);
                        }
                        else if (footprint.xMin > center.x)
                        {
                            east = Mathf.Min(east, footprint.xMin);
                        }
                        else
                        {
                            west = Mathf.Max(west, footprint.xMax);
                        }
                    }
                }

                Assert.That(
                    north - center.y,
                    Is.EqualTo(center.y - south).Within(Epsilon),
                    $"seed {block.Seed}: room {room} is off centre north to south"
                );
                Assert.That(
                    east - center.x,
                    Is.EqualTo(center.x - west).Within(Epsilon),
                    $"seed {block.Seed}: room {room} is off centre east to west"
                );
            }
        }
    }

    /// <summary>
    /// The same seed must rebuild a room identically, or streaming a room back
    /// in after it unloads would rearrange the dungeon around the player.
    /// </summary>
    [Test]
    public void GeometryIsDeterministicForASeed()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        {
            var first = new DungeonGeometryModel(seed, Vector2Int.zero, 1);
            var second = new DungeonGeometryModel(seed, Vector2Int.zero, 1);
            Assert.That(second.Walls.Count, Is.EqualTo(first.Walls.Count), $"seed {seed}");
            for (int i = 0; i < first.Walls.Count; i++)
            {
                Assert.That(second.Walls[i].Anchor, Is.EqualTo(first.Walls[i].Anchor));
                Assert.That(second.Walls[i].AlongX, Is.EqualTo(first.Walls[i].AlongX));
                Assert.That(second.Walls[i].Length, Is.EqualTo(first.Walls[i].Length));
            }
        }
    }

    /// <summary>
    /// Both rooms beside a doorway must agree on it, or one of them would build
    /// a wall where the other built a gap.
    /// </summary>
    [Test]
    public void NeighbouringRoomsAgreeOnTheirSharedEdge()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            foreach (Vector2Int room in block.Rooms)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    Vector2Int neighbour = room + DungeonLayout.DirectionOffsets[direction];
                    int opposite = (direction + 2) % 4;
                    Assert.That(
                        DungeonLayout.EdgeBetween(neighbour, opposite),
                        Is.EqualTo(DungeonLayout.EdgeBetween(room, direction))
                    );
                    Assert.That(
                        block.Passage(neighbour, opposite).OpeningMask,
                        Is.EqualTo(block.Passage(room, direction).OpeningMask),
                        $"seed {block.Seed}: {room} and {neighbour} disagree"
                    );
                }
            }
        }
    }
}
