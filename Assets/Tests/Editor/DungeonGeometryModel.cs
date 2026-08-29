using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assembles the wall geometry of a block of rooms exactly the way
/// <see cref="DungeonManager"/> does: interior runs per room, and each shared
/// edge run built once for the two rooms beside it. The property tests reason
/// about this, so they are reasoning about the geometry the game builds rather
/// than a restatement of it.
/// </summary>
internal sealed class DungeonGeometryModel
{
    /// <summary>The player capsule's radius, as authored in the Dungeon scene.</summary>
    public const float PlayerRadius = 0.43f;

    /// <summary>Seeds the property tests sweep. Fixed, so failures reproduce.</summary>
    public static readonly int[] Seeds =
    {
        1,
        17,
        101,
        2029,
        55555,
        987654321,
        int.MaxValue,
        12345,
    };

    public readonly DungeonLayout Layout;
    public readonly int Seed;

    /// <summary>Every wall piece in the block, shared edges included once.</summary>
    public readonly List<DungeonWallPiece> Walls = new();

    /// <summary>Every archway in the block.</summary>
    public readonly List<DungeonArchway> Archways = new();

    private readonly HashSet<DungeonEdge> builtEdges = new();
    private readonly List<Vector2Int> rooms = new();

    public DungeonGeometryModel(int seed, Vector2Int center, int radius)
    {
        Seed = seed;
        Layout = new DungeonLayout(seed);
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
            Add(center + new Vector2Int(dx, dy));
    }

    /// <summary>The rooms whose geometry this block contains.</summary>
    public IReadOnlyList<Vector2Int> Rooms => rooms;

    /// <summary>The shared edges this block built, each exactly once.</summary>
    public IReadOnlyCollection<DungeonEdge> Edges => builtEdges;

    /// <summary>The rooms that property tests sweep, for one seed.</summary>
    public static IEnumerable<(Vector2Int Center, int Radius)> SampleBlocks()
    {
        yield return (Vector2Int.zero, 2);
        yield return (new Vector2Int(37, -53), 1);
        yield return (new Vector2Int(-9, 14), 1);
    }

    public DungeonPassage Passage(Vector2Int room, int direction)
    {
        DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
        return Layout.Passage(edge, Layout.IsDoorOpen(room, direction));
    }

    public List<DungeonWallPiece> InteriorWalls(Vector2Int room)
    {
        var walls = new List<DungeonWallPiece>();
        DungeonRoomGeometry.AppendInteriorWalls(walls, room, Layout.Archetype(room));
        return walls;
    }

    public List<DungeonWallPiece> EdgeWalls(Vector2Int room, int direction)
    {
        var walls = new List<DungeonWallPiece>();
        DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
        DungeonRoomGeometry.AppendEdgeWalls(walls, edge, Passage(room, direction));
        return walls;
    }

    /// <summary>
    /// The number of slots along an edge on the given side of a room.
    /// </summary>
    public static int SlotCount(int direction)
    {
        return direction == DungeonLayout.North || direction == DungeonLayout.South
            ? DungeonLayout.RoomTilesX
            : DungeonLayout.RoomTilesZ;
    }

    /// <summary>
    /// The ground-plane centre of one boundary slot, measured on the boundary
    /// line itself rather than on the wall slab.
    /// </summary>
    public static Vector2 SlotCenter(Vector2Int room, int direction, int slot)
    {
        Vector2 center = DungeonLayout.RoomCenter(room);
        float offset = DungeonPassage.SlotOffset(slot, SlotCount(direction));
        float halfWidth = DungeonLayout.RoomWidth / 2f;
        float halfDepth = DungeonLayout.RoomDepth / 2f;
        return direction switch
        {
            DungeonLayout.North => new Vector2(center.x + offset, center.y + halfDepth),
            DungeonLayout.South => new Vector2(center.x + offset, center.y - halfDepth),
            DungeonLayout.East => new Vector2(center.x + halfWidth, center.y + offset),
            _ => new Vector2(center.x - halfWidth, center.y + offset),
        };
    }

    /// <summary>True when the boundary on this side runs along world X.</summary>
    public static bool IsHorizontalSide(int direction)
    {
        return direction == DungeonLayout.North || direction == DungeonLayout.South;
    }

    private void Add(Vector2Int room)
    {
        rooms.Add(room);
        DungeonRoomGeometry.AppendInteriorWalls(Walls, room, Layout.Archetype(room));
        for (int direction = 0; direction < 4; direction++)
        {
            DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
            if (!builtEdges.Add(edge))
                continue;

            DungeonPassage passage = Passage(room, direction);
            DungeonRoomGeometry.AppendEdgeWalls(Walls, edge, passage);
            DungeonRoomGeometry.AppendEdgeArchways(Archways, edge, passage);
        }
    }
}
