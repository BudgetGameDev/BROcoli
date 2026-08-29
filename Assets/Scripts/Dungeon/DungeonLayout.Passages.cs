using UnityEngine;

public sealed partial class DungeonLayout
{
    private const int PassagePatternSalt = 911;

    // Most doorways are simply a missing wall piece. Only a minority get the
    // decorative arch frame so it stays a highlight instead of the norm.
    private const double ArchwayEdgeChance = 0.22;

    private static readonly int[] HorizontalDoubleOpeningMasks =
    {
        (1 << 1) | (1 << 3),
        (1 << 1) | (1 << 4),
        (1 << 1) | (1 << 5),
        (1 << 2) | (1 << 4),
        (1 << 2) | (1 << 5),
        (1 << 3) | (1 << 5),
    };

    private static readonly int[] HorizontalTripleOpeningMasks =
    {
        (1 << 1) | (1 << 3) | (1 << 5),
        (1 << 1) | (1 << 2) | (1 << 4),
        (1 << 2) | (1 << 4) | (1 << 5),
    };

    /// <summary>
    /// Chooses the repeatable openings for a shared edge. A closed edge is a
    /// solid wall run with no gateway at all; an open edge drops one to three
    /// wall pieces, of which at most one is framed by an archway.
    /// </summary>
    public DungeonPassage Passage(DungeonEdge edge, bool open)
    {
        if (!open)
            return new DungeonPassage(false, 0, 0);

        int slotCount = edge.Horizontal ? RoomTilesX : RoomTilesZ;
        int salt = edge.Horizontal ? PassagePatternSalt : PassagePatternSalt + 1;
        var random = new System.Random((int)Hash(edge.X, edge.Y, salt));
        double countRoll = random.NextDouble();
        int openingCount =
            countRoll < 0.55 ? 1
            : countRoll < 0.88 ? 2
            : 3;
        int openingMask = PickOpeningMask(slotCount, openingCount, random);

        int archwayMask =
            random.NextDouble() < ArchwayEdgeChance
                ? PickSingleArchwayMask(openingMask, slotCount, random)
                : 0;
        return new DungeonPassage(true, openingMask, archwayMask);
    }

    /// <summary>The four passages around a room, indexed for wall dressing.</summary>
    public RoomDoorways Doorways(Vector2Int room)
    {
        return new RoomDoorways(
            Passage(EdgeBetween(room, North), IsDoorOpen(room, North)),
            Passage(EdgeBetween(room, East), IsDoorOpen(room, East)),
            Passage(EdgeBetween(room, South), IsDoorOpen(room, South)),
            Passage(EdgeBetween(room, West), IsDoorOpen(room, West))
        );
    }

    private static int PickOpeningMask(int slotCount, int openingCount, System.Random random)
    {
        if (openingCount <= 1)
            return 1 << random.Next(1, slotCount - 1);

        if (slotCount == RoomTilesZ)
            return openingCount == 2 ? (1 << 1) | (1 << 3) : (1 << 1) | (1 << 2) | (1 << 3);

        int[] masks =
            openingCount == 2 ? HorizontalDoubleOpeningMasks : HorizontalTripleOpeningMasks;
        return masks[random.Next(masks.Length)];
    }

    private static int PickSingleArchwayMask(int openingMask, int slotCount, System.Random random)
    {
        int openings = 0;
        for (int slot = 0; slot < slotCount; slot++)
        {
            if ((openingMask & (1 << slot)) != 0)
                openings++;
        }
        if (openings == 0)
            return 0;

        int chosen = random.Next(openings);
        for (int slot = 0; slot < slotCount; slot++)
        {
            if ((openingMask & (1 << slot)) == 0)
                continue;
            if (chosen-- == 0)
                return 1 << slot;
        }
        return 0;
    }

    /// <summary>
    /// The passages on a room's four outer walls. Wall-mounted dressing asks
    /// this before hanging anything, so torches and banners never end up
    /// floating in a doorway.
    /// </summary>
    public readonly struct RoomDoorways
    {
        // Anything this far past the room's half extent belongs to the outer
        // wall on that side rather than to an interior wall run.
        private const float OuterWallBand = TileSize * 0.5f;

        public readonly DungeonPassage North;
        public readonly DungeonPassage East;
        public readonly DungeonPassage South;
        public readonly DungeonPassage West;

        public RoomDoorways(
            DungeonPassage north,
            DungeonPassage east,
            DungeonPassage south,
            DungeonPassage west
        )
        {
            North = north;
            East = east;
            South = south;
            West = west;
        }

        /// <summary>
        /// Whether a room-local point on an outer wall falls inside one of that
        /// wall's openings, allowing <paramref name="clearance"/> for the width
        /// of the mounted object.
        /// </summary>
        public bool BlocksDoorway(Vector2 local, float clearance)
        {
            if (local.y >= RoomDepth * 0.5f - OuterWallBand)
                return North.OverlapsOpening(local.x, RoomTilesX, clearance);
            if (local.y <= -(RoomDepth * 0.5f - OuterWallBand))
                return South.OverlapsOpening(local.x, RoomTilesX, clearance);
            if (local.x >= RoomWidth * 0.5f - OuterWallBand)
                return East.OverlapsOpening(local.y, RoomTilesZ, clearance);
            if (local.x <= -(RoomWidth * 0.5f - OuterWallBand))
                return West.OverlapsOpening(local.y, RoomTilesZ, clearance);
            return false;
        }
    }
}
