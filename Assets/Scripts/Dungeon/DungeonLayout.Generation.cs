using UnityEngine;

public sealed partial class DungeonLayout
{
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
                shape = shapeRoll < 0.5 ? RoomShape.LongHorizontal : RoomShape.LongVertical;
                break;
            case RoomTheme.Shrine:
            case RoomTheme.TreasureVault:
                shape = shapeRoll < 0.46 ? RoomShape.Compact : RoomShape.LargeSquare;
                break;
            case RoomTheme.Flooded:
                shape = shapeRoll < 0.58 ? RoomShape.OpenHall : RoomShape.LargeSquare;
                break;
            case RoomTheme.Collapsed:
                shape =
                    shapeRoll < 0.45 ? RoomShape.Divided
                    : shapeRoll < 0.72 ? RoomShape.OpenHall
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

    private static RoomArchetype CreateArchetype(RoomShape shape, RoomTheme theme, int variant)
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
