public sealed partial class DungeonLayout
{
    private const int PassagePatternSalt = 911;

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
    /// Chooses one to three repeatable openings for an open shared edge. Each
    /// edge independently becomes bare, fully arched, or a mix of both.
    /// </summary>
    public DungeonPassage Passage(DungeonEdge edge, bool open)
    {
        int slotCount = edge.Horizontal ? RoomTilesX : RoomTilesZ;
        int centerMask = 1 << (slotCount / 2);
        if (!open)
            return new DungeonPassage(false, centerMask, centerMask);

        int salt = edge.Horizontal ? PassagePatternSalt : PassagePatternSalt + 1;
        var random = new System.Random((int)Hash(edge.X, edge.Y, salt));
        double countRoll = random.NextDouble();
        int openingCount =
            countRoll < 0.46 ? 1
            : countRoll < 0.78 ? 2
            : 3;
        int openingMask = PickOpeningMask(slotCount, openingCount, random);

        double treatmentRoll = random.NextDouble();
        int archwayMask =
            treatmentRoll < 0.32 ? 0
            : treatmentRoll < 0.68 ? openingMask
            : PickMixedArchwayMask(openingMask, slotCount, random);
        return new DungeonPassage(true, openingMask, archwayMask);
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

    private static int PickMixedArchwayMask(int openingMask, int slotCount, System.Random random)
    {
        int archwayMask = 0;
        bool useArchway = random.Next(0, 2) == 0;
        for (int slot = 1; slot < slotCount - 1; slot++)
        {
            if ((openingMask & (1 << slot)) == 0)
                continue;
            if (useArchway)
                archwayMask |= 1 << slot;
            useArchway = !useArchway;
        }
        return archwayMask;
    }
}
