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
/// Pure deterministic layout math for the infinite dungeon. Every query is a
/// function of the run seed and the room coordinate, so any room can be
/// regenerated identically after being unloaded, and both rooms beside a
/// doorway always agree on whether it is open or blocked.
/// </summary>
public sealed class DungeonLayout
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
        Compact,
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

        public int EnemyCapacity => Shape switch
        {
            RoomShape.Compact => 5,
            RoomShape.LongHorizontal => 8,
            RoomShape.LongVertical => 8,
            RoomShape.Divided => 9,
            _ => 12,
        };

        public override string ToString()
        {
            return $"{Shape} / {Theme}";
        }
    }

    /// <summary>
    /// Chooses a repeatable room shape and theme. Theme affinities make the
    /// combinations feel authored: feasts use long halls, vaults use focused
    /// square chambers, and flooded/collapsed rooms favour broader footprints.
    /// </summary>
    public RoomArchetype Archetype(Vector2Int room)
    {
        if (Ring(room) == 0)
            return CreateArchetype(RoomShape.OpenHall, RoomTheme.Sparse, 0);

        System.Random themeRandom = RoomRandom(room, 808);
        double themeRoll = themeRandom.NextDouble();
        RoomTheme theme = themeRoll switch
        {
            < 0.11 => RoomTheme.Empty,
            < 0.27 => RoomTheme.Sparse,
            < 0.43 => RoomTheme.Storage,
            < 0.55 => RoomTheme.Banquet,
            < 0.67 => RoomTheme.Armory,
            < 0.77 => RoomTheme.Shrine,
            < 0.87 => RoomTheme.Flooded,
            < 0.93 => RoomTheme.TreasureVault,
            _ => RoomTheme.Collapsed,
        };

        System.Random shapeRandom = RoomRandom(room, 809);
        double shapeRoll = shapeRandom.NextDouble();
        RoomShape shape;
        switch (theme)
        {
            case RoomTheme.Banquet:
                shape = shapeRoll < 0.5
                    ? RoomShape.LongHorizontal
                    : RoomShape.LongVertical;
                break;
            case RoomTheme.Shrine:
            case RoomTheme.TreasureVault:
                shape = shapeRoll < 0.46 ? RoomShape.Compact : RoomShape.LargeSquare;
                break;
            case RoomTheme.Flooded:
                shape = shapeRoll < 0.58 ? RoomShape.OpenHall : RoomShape.LargeSquare;
                break;
            case RoomTheme.Collapsed:
                shape = shapeRoll < 0.45
                    ? RoomShape.Divided
                    : shapeRoll < 0.72
                        ? RoomShape.OpenHall
                        : RoomShape.LargeSquare;
                break;
            default:
                shape = shapeRoll switch
                {
                    < 0.24 => RoomShape.OpenHall,
                    < 0.41 => RoomShape.Compact,
                    < 0.58 => RoomShape.LargeSquare,
                    < 0.73 => RoomShape.LongHorizontal,
                    < 0.88 => RoomShape.LongVertical,
                    _ => RoomShape.Divided,
                };
                break;
        }

        return CreateArchetype(shape, theme, shapeRandom.Next(0, 4));
    }

    /// <summary>
    /// Rolls a room's population archetype: some rooms are empty, most hold a
    /// small or medium group, a few are packed, and rare rooms are elite dens
    /// (a couple of low-tier enemies promoted to elites).
    /// </summary>
    public RoomPopulation Population(Vector2Int room)
    {
        int ring = Ring(room);
        if (ring == 0)
            return new RoomPopulation(0, false);
        if (Archetype(room).Theme == RoomTheme.Empty)
            return new RoomPopulation(0, false);

        System.Random random = RoomRandom(room, 303);
        double roll = random.NextDouble();
        if (roll < 0.18)
            return new RoomPopulation(0, false);
        if (roll < 0.52)
            return new RoomPopulation(1 + random.Next(0, 2) + ring / 3, false);
        if (roll < 0.82)
            return new RoomPopulation(3 + random.Next(0, 3) + Mathf.Min(ring, 5), false);
        if (roll < 0.95)
            return new RoomPopulation(
                Mathf.Min(12, 6 + random.Next(0, 4) + Mathf.Min(ring, 6)),
                false
            );
        return new RoomPopulation(2 + random.Next(0, 2), true);
    }

    private static RoomArchetype CreateArchetype(
        RoomShape shape,
        RoomTheme theme,
        int variant
    )
    {
        return shape switch
        {
            RoomShape.Compact => new RoomArchetype(shape, theme, 4.7f, 4.7f, variant),
            RoomShape.LargeSquare => new RoomArchetype(shape, theme, 8.2f, 6.4f, variant),
            RoomShape.LongHorizontal => new RoomArchetype(shape, theme, 10.2f, 4.5f, variant),
            RoomShape.LongVertical => new RoomArchetype(shape, theme, 4.5f, 6.4f, variant),
            RoomShape.Divided => new RoomArchetype(shape, theme, 10.2f, 6.4f, variant),
            _ => new RoomArchetype(shape, theme, 10.2f, 6.4f, variant),
        };
    }

    /// <summary>Health multiplier applied to enemies the deeper the player goes.</summary>
    public float EnemyHealthScale(Vector2Int room)
    {
        return 1f + 0.15f * Mathf.Max(0, Ring(room) - 1);
    }

    private bool IsEdgeBaseOpen(DungeonEdge edge)
    {
        uint hash = Hash(edge.X, edge.Y, edge.Horizontal ? EdgeSalt : EdgeSalt + 1);
        return hash / (float)uint.MaxValue < DoorOpenChance;
    }

    /// <summary>
    /// When all four of a room's edges roll closed, the room deterministically
    /// forces one of them open so it can never seal itself (or a neighbour) in.
    /// </summary>
    private bool TryGetForcedEdge(Vector2Int room, out DungeonEdge forcedEdge)
    {
        for (int direction = 0; direction < 4; direction++)
        {
            if (IsEdgeBaseOpen(EdgeBetween(room, direction)))
            {
                forcedEdge = default;
                return false;
            }
        }

        int forcedDirection = (int)(Hash(room.x, room.y, ForcedDoorSalt) % 4);
        forcedEdge = EdgeBetween(room, forcedDirection);
        return true;
    }

    private uint Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B1u;
            h = (h << 13) | (h >> 19);
            h *= 0x85EBCA6Bu;
            h ^= (uint)y * 0xC2B2AE35u;
            h = (h << 11) | (h >> 21);
            h *= 0x27D4EB2Fu;
            h ^= (uint)salt * 0x165667B1u;
            h ^= h >> 15;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            return h;
        }
    }
}
