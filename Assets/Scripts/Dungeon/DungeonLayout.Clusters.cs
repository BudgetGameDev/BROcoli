using UnityEngine;

public sealed partial class DungeonLayout
{
    private const int MegaBlockSalt = 813;
    private const int MegaThemeSalt = 814;
    private const int MegaPopulationSalt = 304;

    /// <summary>
    /// How the four cells of one aligned 2x2 block of the room grid merge into
    /// a mega room. Blocks are disjoint, so two clusters can never overlap and
    /// every cell can resolve its own membership locally.
    /// </summary>
    private enum MegaBlockLayout
    {
        None,
        BottomWide,
        TopWide,
        LeftDeep,
        RightDeep,
        Full,
    }

    /// <summary>
    /// The mega-room cluster a room belongs to, if any. Merged cells drop the
    /// walls between them entirely (see <see cref="Passage"/>), so a 2x1
    /// cluster reads as one long hall, a 1x2 as one deep hall, and a 2x2 as
    /// one huge chamber. <paramref name="anchor"/> is the cluster's lower-left
    /// cell and <paramref name="size"/> is measured in cells.
    /// </summary>
    public bool TryGetMegaCluster(Vector2Int room, out Vector2Int anchor, out Vector2Int size)
    {
        anchor = room;
        size = Vector2Int.one;

        // Arithmetic shift floors toward negative infinity, so cells -2 and -1
        // share block -1 exactly as cells 0 and 1 share block 0.
        var block = new Vector2Int(room.x >> 1, room.y >> 1);
        // The spawn block stays four ordinary rooms, so the run never opens
        // inside a mega room.
        if (block == Vector2Int.zero)
            return false;

        int localX = room.x & 1;
        int localY = room.y & 1;
        Vector2Int origin = block * 2;
        switch (LayoutOfBlock(block))
        {
            case MegaBlockLayout.BottomWide when localY == 0:
                anchor = origin;
                size = new Vector2Int(2, 1);
                return true;
            case MegaBlockLayout.TopWide when localY == 1:
                anchor = origin + Vector2Int.up;
                size = new Vector2Int(2, 1);
                return true;
            case MegaBlockLayout.LeftDeep when localX == 0:
                anchor = origin;
                size = new Vector2Int(1, 2);
                return true;
            case MegaBlockLayout.RightDeep when localX == 1:
                anchor = origin + Vector2Int.right;
                size = new Vector2Int(1, 2);
                return true;
            case MegaBlockLayout.Full:
                anchor = origin;
                size = new Vector2Int(2, 2);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Whether this room is one cell of a merged mega room.</summary>
    public bool IsMegaRoomCell(Vector2Int room)
    {
        return TryGetMegaCluster(room, out _, out _);
    }

    /// <summary>
    /// Whether two grid cells are part of the same playable room. Ordinary
    /// rooms contain one cell; mega rooms contain every cell sharing their
    /// cluster anchor.
    /// </summary>
    public bool AreInSameRoom(Vector2Int first, Vector2Int second)
    {
        if (first == second)
            return true;

        return TryGetMegaCluster(first, out Vector2Int firstAnchor, out _)
            && TryGetMegaCluster(second, out Vector2Int secondAnchor, out _)
            && firstAnchor == secondAnchor;
    }

    /// <summary>
    /// Whether both rooms beside an edge are cells of the same mega room. Such
    /// an edge builds no wall at all: every slot opens, so the merged cells
    /// read as one continuous space.
    /// </summary>
    public bool IsClusterInternalEdge(DungeonEdge edge)
    {
        var roomA = new Vector2Int(edge.X, edge.Y);
        Vector2Int roomB = roomA + (edge.Horizontal ? Vector2Int.up : Vector2Int.right);
        return TryGetMegaCluster(roomA, out Vector2Int anchorA, out _)
            && TryGetMegaCluster(roomB, out Vector2Int anchorB, out _)
            && anchorA == anchorB;
    }

    private MegaBlockLayout LayoutOfBlock(Vector2Int block)
    {
        float roll = Hash(block.x, block.y, MegaBlockSalt) / (float)uint.MaxValue;
        return roll switch
        {
            < 0.86f => MegaBlockLayout.None,
            < 0.895f => MegaBlockLayout.BottomWide,
            < 0.93f => MegaBlockLayout.TopWide,
            < 0.955f => MegaBlockLayout.LeftDeep,
            < 0.98f => MegaBlockLayout.RightDeep,
            _ => MegaBlockLayout.Full,
        };
    }
}
