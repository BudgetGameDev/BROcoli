using System;
using UnityEngine;

/// <summary>
/// Identifies one shared wall between two adjacent dungeon rooms. A horizontal
/// edge sits between room (x, y) and (x, y + 1); a vertical edge sits between
/// room (x, y) and (x + 1, y). Both neighbouring rooms resolve to the same key,
/// so door state and built geometry can be shared.
/// </summary>
public readonly struct DungeonEdge : IEquatable<DungeonEdge>
{
    public readonly int X;
    public readonly int Y;
    public readonly bool Horizontal;

    public DungeonEdge(int x, int y, bool horizontal)
    {
        X = x;
        Y = y;
        Horizontal = horizontal;
    }

    public bool Equals(DungeonEdge other)
    {
        return X == other.X && Y == other.Y && Horizontal == other.Horizontal;
    }

    public override bool Equals(object obj)
    {
        return obj is DungeonEdge other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (X * 397) ^ (Y * 31) ^ (Horizontal ? 1 : 0);
    }
}

/// <summary>
/// Describes the openings cut into one shared wall run. An opening is a
/// missing wall piece, optionally framed by an archway prefab. A closed edge
/// has no openings at all and reads as an unbroken wall.
/// </summary>
public readonly struct DungeonPassage
{
    public readonly bool Open;
    public readonly int OpeningMask;
    public readonly int ArchwayMask;

    public DungeonPassage(bool open, int openingMask, int archwayMask)
    {
        Open = open;
        OpeningMask = openingMask;
        ArchwayMask = archwayMask;
    }

    public bool HasOpening(int slot)
    {
        return (OpeningMask & (1 << slot)) != 0;
    }

    public bool HasArchway(int slot)
    {
        return (ArchwayMask & (1 << slot)) != 0;
    }

    /// <summary>The wall-run offset of a slot, measured from the run's centre.</summary>
    public static float SlotOffset(int slot, int slotCount)
    {
        return (slot - slotCount / 2) * DungeonLayout.TileSize;
    }

    /// <summary>
    /// Whether an object mounted <paramref name="offset"/> along this wall run
    /// would stand in one of its openings. <paramref name="clearance"/> is the
    /// object's own half width.
    /// </summary>
    public bool OverlapsOpening(float offset, int slotCount, float clearance)
    {
        float reach = DungeonLayout.TileSize * 0.5f + clearance;
        for (int slot = 0; slot < slotCount; slot++)
        {
            if (!HasOpening(slot))
                continue;
            if (Mathf.Abs(offset - SlotOffset(slot, slotCount)) < reach)
                return true;
        }
        return false;
    }

    public int OpeningCount
    {
        get
        {
            int count = 0;
            int remaining = OpeningMask;
            while (remaining != 0)
            {
                count += remaining & 1;
                remaining >>= 1;
            }
            return count;
        }
    }
}

/// <summary>
/// Pure deterministic layout math for the infinite dungeon. Every query is a
/// function of the run seed and the room coordinate, so any room can be
/// regenerated identically after being unloaded, and both rooms beside a
/// doorway always agree on whether it is open or blocked.
/// </summary>
public sealed partial class DungeonLayout
{
    public const float TileSize = 4f;
    public const int RoomTilesX = 7;
    public const int RoomTilesZ = 5;
    public const float RoomWidth = TileSize * RoomTilesX;
    public const float RoomDepth = TileSize * RoomTilesZ;

    public const int North = 0;
    public const int East = 1;
    public const int South = 2;
    public const int West = 3;

    public static readonly Vector2Int[] DirectionOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private const float DoorOpenChance = 0.62f;
    private const int EdgeSalt = 101;
    private const int ForcedDoorSalt = 202;

    private readonly int seed;

    public DungeonLayout(int seed)
    {
        this.seed = seed;
    }

    /// <summary>Ground-plane centre of a room.</summary>
    public static Vector2 RoomCenter(Vector2Int room)
    {
        return new Vector2(room.x * RoomWidth, room.y * RoomDepth);
    }

    /// <summary>The room containing a ground-plane position.</summary>
    public static Vector2Int RoomAt(Vector2 groundPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(groundPosition.x / RoomWidth),
            Mathf.RoundToInt(groundPosition.y / RoomDepth)
        );
    }

    /// <summary>Chebyshev ring distance from the starting room.</summary>
    public static int Ring(Vector2Int room)
    {
        return Mathf.Max(Mathf.Abs(room.x), Mathf.Abs(room.y));
    }

    public static DungeonEdge EdgeBetween(Vector2Int room, int direction)
    {
        return direction switch
        {
            North => new DungeonEdge(room.x, room.y, true),
            South => new DungeonEdge(room.x, room.y - 1, true),
            East => new DungeonEdge(room.x, room.y, false),
            _ => new DungeonEdge(room.x - 1, room.y, false),
        };
    }

    /// <summary>
    /// Whether the doorway on the given side of a room is open. A door is open
    /// when its edge rolls open, or when either adjacent room would otherwise
    /// be fully sealed and picked this edge as its forced exit. This guarantees
    /// every room always has at least one active door.
    /// </summary>
    public bool IsDoorOpen(Vector2Int room, int direction)
    {
        DungeonEdge edge = EdgeBetween(room, direction);
        if (IsEdgeBaseOpen(edge))
            return true;
        if (TryGetForcedEdge(room, out DungeonEdge forced) && forced.Equals(edge))
            return true;

        Vector2Int neighbour = room + DirectionOffsets[direction];
        return TryGetForcedEdge(neighbour, out DungeonEdge neighbourForced)
            && neighbourForced.Equals(edge);
    }

    /// <summary>Deterministic per-room random stream for content decisions.</summary>
    public System.Random RoomRandom(Vector2Int room, int salt)
    {
        return new System.Random((int)Hash(room.x, room.y, salt));
    }

    /// <summary>What kind of enemy group a room holds.</summary>
    public readonly struct RoomPopulation
    {
        public readonly int Count;
        public readonly bool Elite;

        public RoomPopulation(int count, bool elite)
        {
            Count = count;
            Elite = elite;
        }
    }

    public enum RoomShape
    {
        OpenHall,
        GrandArena,
        Tiny,
        Compact,
        NarrowHorizontal,
        NarrowVertical,
        LargeSquare,
        LongHorizontal,
        LongVertical,
        Divided,
    }

    public enum RoomTheme
    {
        Empty,
        Sparse,
        Storage,
        Banquet,
        Armory,
        Shrine,
        Flooded,
        TreasureVault,
        Collapsed,
        Arena,
    }

    /// <summary>
    /// A room's deterministic visual and gameplay profile. The outer room grid
    /// never changes (so shared doorways remain compatible), while interior
    /// walls turn that shell into compact, long, square, or divided spaces.
    /// </summary>
    public readonly struct RoomArchetype
    {
        public readonly RoomShape Shape;
        public readonly RoomTheme Theme;
        public readonly float HalfWidth;
        public readonly float HalfDepth;
        public readonly int Variant;

        public RoomArchetype(
            RoomShape shape,
            RoomTheme theme,
            float halfWidth,
            float halfDepth,
            int variant
        )
        {
            Shape = shape;
            Theme = theme;
            HalfWidth = halfWidth;
            HalfDepth = halfDepth;
            Variant = variant;
        }

        public int EnemyCapacity =>
            Shape switch
            {
                RoomShape.Tiny => 3,
                RoomShape.Compact => 5,
                RoomShape.NarrowHorizontal => 6,
                RoomShape.NarrowVertical => 6,
                RoomShape.LongHorizontal => 8,
                RoomShape.LongVertical => 8,
                RoomShape.Divided => 10,
                RoomShape.GrandArena => 20,
                RoomShape.OpenHall => 14,
                _ => 12,
            };

        public override string ToString()
        {
            return $"{Shape} / {Theme}";
        }
    }
}
