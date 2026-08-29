using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Property tests over the procedurally generated dungeon walls. Each one
/// sweeps a fixed corpus of seeds and rooms and asserts an invariant that must
/// hold for every generated room, so a layout change that breaks one is caught
/// arithmetically rather than by noticing it in play.
/// </summary>
public sealed class DungeonWallGeometryTests
{
    private const float Epsilon = 0.001f;
    private const float JunctionTolerance = 0.5f;

    /// <summary>
    /// Two walls running the same way must never occupy the same space. Walls
    /// that cross at right angles legitimately overlap at a junction.
    /// </summary>
    [Test]
    public void ParallelWallsNeverIntersect()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            for (int i = 0; i < block.Walls.Count; i++)
            for (int j = i + 1; j < block.Walls.Count; j++)
            {
                DungeonWallPiece first = block.Walls[i];
                DungeonWallPiece second = block.Walls[j];
                if (first.AlongX != second.AlongX)
                    continue;

                Rect a = first.Footprint;
                Rect b = second.Footprint;
                float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                float overlapZ = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                Assert.That(
                    overlapX <= Epsilon || overlapZ <= Epsilon,
                    $"seed {block.Seed}: {first} intersects parallel {second}"
                );
            }
        }
    }

    /// <summary>Nothing is ever built twice in the same place.</summary>
    [Test]
    public void NoWallVolumeIsDuplicated()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            var seen = new HashSet<(long, long, bool)>();
            foreach (DungeonWallPiece piece in block.Walls)
            {
                Rect footprint = piece.Footprint;
                var key = (
                    Mathf.RoundToInt(footprint.center.x * 100f),
                    Mathf.RoundToInt(footprint.center.y * 100f),
                    piece.AlongX
                );
                Assert.That(seen.Add(key), $"seed {block.Seed}: duplicate wall volume at {piece}");
            }
        }
    }

    /// <summary>
    /// The four runs that meet at a grid corner must all reach it, or the
    /// dungeon shell has a hole where two rooms meet.
    /// </summary>
    [Test]
    public void WallRunsReachTheirJunctions()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            foreach (Vector2Int room in block.Rooms)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    List<DungeonWallPiece> run = block.EdgeWalls(room, direction);
                    Assert.That(run, Is.Not.Empty, $"seed {block.Seed}: empty run {room}");

                    bool horizontal = DungeonGeometryModel.IsHorizontalSide(direction);
                    float min = float.MaxValue;
                    float max = float.MinValue;
                    foreach (DungeonWallPiece piece in run)
                    {
                        Rect footprint = piece.Footprint;
                        min = Mathf.Min(min, horizontal ? footprint.xMin : footprint.yMin);
                        max = Mathf.Max(max, horizontal ? footprint.xMax : footprint.yMax);
                    }

                    int slots = DungeonGeometryModel.SlotCount(direction);
                    Vector2 first = DungeonGeometryModel.SlotCenter(room, direction, 0);
                    Vector2 last = DungeonGeometryModel.SlotCenter(room, direction, slots - 1);
                    float half = DungeonLayout.TileSize / 2f;
                    float start = (horizontal ? first.x : first.y) - half;
                    float end = (horizontal ? last.x : last.y) + half;

                    Assert.That(
                        min,
                        Is.EqualTo(start).Within(JunctionTolerance),
                        $"seed {block.Seed}: run {room}/{direction} stops short of its junction"
                    );
                    Assert.That(
                        max,
                        Is.EqualTo(end).Within(JunctionTolerance),
                        $"seed {block.Seed}: run {room}/{direction} stops short of its junction"
                    );
                }
            }
        }
    }

    /// <summary>Every boundary slot that is not a doorway is walled shut.</summary>
    [Test]
    public void ClosedBoundarySlotsAreWalled()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            foreach (Vector2Int room in block.Rooms)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    DungeonPassage passage = block.Passage(room, direction);
                    List<DungeonWallPiece> run = block.EdgeWalls(room, direction);
                    int slots = DungeonGeometryModel.SlotCount(direction);
                    for (int slot = 0; slot < slots; slot++)
                    {
                        if (passage.HasOpening(slot))
                            continue;

                        Vector2 point = SlabPoint(room, direction, slot);
                        Assert.That(
                            Covers(run, point),
                            $"seed {block.Seed}: room {room} side {direction} slot {slot} "
                                + "is neither a doorway nor a wall"
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Interior runs stay inside their own room, and shell runs stay on the
    /// boundary they belong to. Nothing escapes into a neighbour.
    /// </summary>
    [Test]
    public void GeometryStaysWithinItsExpectedBounds()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            foreach (Vector2Int room in block.Rooms)
            {
                Rect bounds = DungeonRoomGeometry.RoomFloorBounds(room);
                foreach (DungeonWallPiece piece in block.InteriorWalls(room))
                {
                    Rect footprint = piece.Footprint;
                    Assert.That(
                        footprint.xMin >= bounds.xMin - Epsilon
                            && footprint.xMax <= bounds.xMax + Epsilon
                            && footprint.yMin >= bounds.yMin - Epsilon
                            && footprint.yMax <= bounds.yMax + Epsilon,
                        $"seed {block.Seed}: interior {piece} leaves room {room}"
                    );
                }

                for (int direction = 0; direction < 4; direction++)
                {
                    bool horizontal = DungeonGeometryModel.IsHorizontalSide(direction);
                    float boundary = horizontal
                        ? DungeonGeometryModel.SlotCenter(room, direction, 0).y
                        : DungeonGeometryModel.SlotCenter(room, direction, 0).x;
                    foreach (DungeonWallPiece piece in block.EdgeWalls(room, direction))
                    {
                        Rect footprint = piece.Footprint;
                        float near = horizontal ? footprint.yMin : footprint.xMin;
                        float far = horizontal ? footprint.yMax : footprint.xMax;
                        Assert.That(
                            near,
                            Is.EqualTo(boundary - DungeonWallPiece.SlabHalfThickness)
                                .Within(Epsilon),
                            $"seed {block.Seed}: {piece} is off its boundary"
                        );
                        Assert.That(
                            far,
                            Is.EqualTo(boundary + DungeonWallPiece.SlabHalfThickness)
                                .Within(Epsilon),
                            $"seed {block.Seed}: {piece} is off its boundary"
                        );
                    }
                }
            }
        }
    }

    private static Vector2 SlabPoint(Vector2Int room, int direction, int slot)
    {
        return DungeonGeometryModel.SlotCenter(room, direction, slot);
    }

    private static bool Covers(List<DungeonWallPiece> run, Vector2 point)
    {
        foreach (DungeonWallPiece piece in run)
        {
            Rect footprint = piece.Footprint;
            if (
                point.x >= footprint.xMin - Epsilon
                && point.x <= footprint.xMax + Epsilon
                && point.y >= footprint.yMin - Epsilon
                && point.y <= footprint.yMax + Epsilon
            )
                return true;
        }
        return false;
    }
}
