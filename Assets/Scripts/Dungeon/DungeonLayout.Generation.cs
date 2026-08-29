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

        if (TryGetMegaCluster(room, out Vector2Int anchor, out _))
        {
            // The whole cluster shares one theme rolled at its anchor, so a
            // merged hall reads as one authored space, not a patchwork.
            System.Random megaRandom = RoomRandom(anchor, MegaThemeSalt);
            RoomTheme megaTheme = megaRandom.NextDouble() switch
            {
                < 0.30 => RoomTheme.Arena,
                < 0.50 => RoomTheme.Banquet,
                < 0.68 => RoomTheme.Flooded,
                < 0.84 => RoomTheme.Storage,
                _ => RoomTheme.Sparse,
            };
            return CreateArchetype(RoomShape.MegaSection, megaTheme, megaRandom.Next(0, 4));
        }

        System.Random themeRandom = RoomRandom(room, 808);
        double themeRoll = themeRandom.NextDouble();
        RoomTheme theme = themeRoll switch
        {
            < 0.08 => RoomTheme.Empty,
            < 0.20 => RoomTheme.Sparse,
            < 0.34 => RoomTheme.Storage,
            < 0.46 => RoomTheme.Banquet,
            < 0.58 => RoomTheme.Armory,
            < 0.68 => RoomTheme.Shrine,
            < 0.78 => RoomTheme.Flooded,
            < 0.85 => RoomTheme.TreasureVault,
            < 0.92 => RoomTheme.Collapsed,
            _ => RoomTheme.Arena,
        };

        System.Random shapeRandom = RoomRandom(room, 809);
        double shapeRoll = shapeRandom.NextDouble();
        RoomShape shape;
        switch (theme)
        {
            case RoomTheme.Arena:
                shape = RoomShape.GrandArena;
                break;
            case RoomTheme.Banquet:
                shape =
                    shapeRoll < 0.32 ? RoomShape.LongHorizontal
                    : shapeRoll < 0.58 ? RoomShape.LongVertical
                    : shapeRoll < 0.74 ? RoomShape.NarrowHorizontal
                    : shapeRoll < 0.88 ? RoomShape.NarrowVertical
                    : RoomShape.LargeSquare;
                break;
            case RoomTheme.Shrine:
            case RoomTheme.TreasureVault:
                shape =
                    shapeRoll < 0.20 ? RoomShape.Tiny
                    : shapeRoll < 0.56 ? RoomShape.Compact
                    : shapeRoll < 0.75 ? RoomShape.LargeSquare
                    : shapeRoll < 0.85 ? RoomShape.NarrowHorizontal
                    : shapeRoll < 0.95 ? RoomShape.NarrowVertical
                    : RoomShape.OpenHall;
                break;
            case RoomTheme.Flooded:
                shape =
                    shapeRoll < 0.34 ? RoomShape.OpenHall
                    : shapeRoll < 0.58 ? RoomShape.LargeSquare
                    : shapeRoll < 0.72 ? RoomShape.LongHorizontal
                    : shapeRoll < 0.86 ? RoomShape.LongVertical
                    : shapeRoll < 0.93 ? RoomShape.NarrowHorizontal
                    : RoomShape.Divided;
                break;
            case RoomTheme.Collapsed:
                shape =
                    shapeRoll < 0.28 ? RoomShape.Divided
                    : shapeRoll < 0.42 ? RoomShape.Tiny
                    : shapeRoll < 0.56 ? RoomShape.NarrowHorizontal
                    : shapeRoll < 0.70 ? RoomShape.NarrowVertical
                    : shapeRoll < 0.84 ? RoomShape.OpenHall
                    : RoomShape.LargeSquare;
                break;
            case RoomTheme.Empty:
                shape =
                    shapeRoll < 0.20 ? RoomShape.Tiny
                    : shapeRoll < 0.40 ? RoomShape.NarrowHorizontal
                    : shapeRoll < 0.60 ? RoomShape.NarrowVertical
                    : shapeRoll < 0.80 ? RoomShape.Divided
                    : RoomShape.OpenHall;
                break;
            default:
                shape = shapeRoll switch
                {
                    < 0.12 => RoomShape.Tiny,
                    < 0.25 => RoomShape.Compact,
                    < 0.36 => RoomShape.NarrowHorizontal,
                    < 0.47 => RoomShape.NarrowVertical,
                    < 0.60 => RoomShape.LargeSquare,
                    < 0.72 => RoomShape.LongHorizontal,
                    < 0.84 => RoomShape.LongVertical,
                    < 0.92 => RoomShape.Divided,
                    _ => RoomShape.OpenHall,
                };
                break;
        }

        return CreateArchetype(shape, theme, shapeRandom.Next(0, 4));
    }

    /// <summary>
    /// Rolls a room's population archetype. Counts are capacity-relative, so
    /// the same distribution packs a tiny chamber wall-to-wall and floods an
    /// open hall: some rooms are empty, most hold a small or medium group,
    /// some are packed, a few fill to capacity as swarms, and rare rooms are
    /// elite dens (a couple of low-tier enemies promoted to elites).
    /// </summary>
    public RoomPopulation Population(Vector2Int room)
    {
        int ring = Ring(room);
        if (ring == 0)
            return new RoomPopulation(0, false);
        RoomArchetype archetype = Archetype(room);
        if (archetype.Theme == RoomTheme.Empty)
            return new RoomPopulation(0, false);

        System.Random random = RoomRandom(room, 303);
        if (archetype.Shape == RoomShape.GrandArena)
        {
            int hordeSize = 13 + random.Next(0, 6) + Mathf.Min(ring, 3);
            return new RoomPopulation(Mathf.Min(archetype.EnemyCapacity, hordeSize), false);
        }

        if (archetype.Shape == RoomShape.MegaSection)
            return MegaCellPopulation(room, random, ring, archetype.EnemyCapacity);

        int capacity = archetype.EnemyCapacity;
        double roll = random.NextDouble();
        if (roll < 0.14)
            return new RoomPopulation(0, false);
        if (roll < 0.24 && ring >= 2 && capacity >= RoomPopulation.SpiderSwarmSize)
            return RoomPopulation.SpiderSwarm();
        if (roll < 0.46)
            return new RoomPopulation(1 + random.Next(0, 2) + ring / 4, false);
        if (roll < 0.72)
            return new RoomPopulation(
                Mathf.Min(capacity, 3 + random.Next(0, 3) + Mathf.Min(ring, 5)),
                false
            );
        if (roll < 0.86)
            return new RoomPopulation(
                Mathf.Min(capacity, Mathf.Max(3, capacity * 3 / 4 + random.Next(0, 3))),
                false
            );
        if (roll < 0.94)
            // A swarm fills the room to capacity, however small the room is.
            return new RoomPopulation(capacity, false);
        return new RoomPopulation(2 + random.Next(0, 2), true);
    }

    /// <summary>
    /// Population for one cell of a mega room. The cluster rolls its character
    /// once at its anchor — deserted, scattered, an elite patrol, packed, or a
    /// wall-to-wall horde — and every cell jitters around that, so a horde
    /// hall is a horde in every corner rather than a patchwork.
    /// </summary>
    private RoomPopulation MegaCellPopulation(
        Vector2Int room,
        System.Random cellRandom,
        int ring,
        int capacity
    )
    {
        TryGetMegaCluster(room, out Vector2Int anchor, out _);
        double style = RoomRandom(anchor, MegaPopulationSalt).NextDouble();
        if (style < 0.10)
            return new RoomPopulation(0, false);
        if (style < 0.40)
            return new RoomPopulation(2 + cellRandom.Next(0, 3), false);
        if (style < 0.52)
            return new RoomPopulation(1 + cellRandom.Next(0, 2), true);
        if (style < 0.85)
            return new RoomPopulation(
                Mathf.Min(capacity, 7 + cellRandom.Next(0, 4) + Mathf.Min(ring, 4)),
                false
            );
        return new RoomPopulation(capacity - cellRandom.Next(0, 3), false);
    }

    private static RoomArchetype CreateArchetype(RoomShape shape, RoomTheme theme, int variant)
    {
        return shape switch
        {
            RoomShape.GrandArena => new RoomArchetype(shape, theme, 12f, 8.2f, variant),
            RoomShape.MegaSection => new RoomArchetype(shape, theme, 12f, 8.2f, variant),
            RoomShape.Tiny => new RoomArchetype(shape, theme, 2.8f, 2.8f, variant),
            RoomShape.Compact => new RoomArchetype(shape, theme, 4.7f, 4.7f, variant),
            RoomShape.NarrowHorizontal => new RoomArchetype(shape, theme, 10.2f, 2.8f, variant),
            RoomShape.NarrowVertical => new RoomArchetype(shape, theme, 2.8f, 8.2f, variant),
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
    /// The doors this room forces open on top of the base rolls, one bit per
    /// direction. A room whose edges all rolled closed forces two exits (just
    /// one when it is a mega-room cell, which already reaches more exits
    /// through its cluster), and a room left with a single door usually forces
    /// a second, so dead ends stay rare instead of common. Cluster-internal
    /// edges are already open and are never counted or forced.
    /// </summary>
    private int ForcedDoorMask(Vector2Int room)
    {
        int closedMask = 0;
        int openCount = 0;
        for (int direction = 0; direction < 4; direction++)
        {
            DungeonEdge edge = EdgeBetween(room, direction);
            if (IsClusterInternalEdge(edge))
                continue;
            if (IsEdgeBaseOpen(edge))
                openCount++;
            else
                closedMask |= 1 << direction;
        }

        if (openCount >= 2 || closedMask == 0)
            return 0;

        if (openCount == 1)
        {
            bool breakDeadEnd =
                Hash(room.x, room.y, DeadEndBreakSalt) / (float)uint.MaxValue < SecondDoorChance;
            return breakDeadEnd ? PickDirectionBit(room, SecondDoorSalt, closedMask) : 0;
        }

        int forced = PickDirectionBit(room, ForcedDoorSalt, closedMask);
        if (!IsMegaRoomCell(room))
            forced |= PickDirectionBit(room, SecondDoorSalt, closedMask & ~forced);
        return forced;
    }

    /// <summary>One deterministic direction bit out of a candidate mask.</summary>
    private int PickDirectionBit(Vector2Int room, int salt, int candidateMask)
    {
        int count = 0;
        for (int direction = 0; direction < 4; direction++)
        {
            if ((candidateMask & (1 << direction)) != 0)
                count++;
        }
        if (count == 0)
            return 0;

        int chosen = (int)(Hash(room.x, room.y, salt) % (uint)count);
        for (int direction = 0; direction < 4; direction++)
        {
            if ((candidateMask & (1 << direction)) == 0)
                continue;
            if (chosen-- == 0)
                return 1 << direction;
        }
        return 0;
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
