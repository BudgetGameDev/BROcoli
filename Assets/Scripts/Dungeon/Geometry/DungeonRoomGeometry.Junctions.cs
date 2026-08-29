using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A post standing where two wall runs cross. Every slab straddles the line it
/// was planned on, so perpendicular runs necessarily interpenetrate where they
/// meet: one run's end cap sits inside the other and pushes through its face,
/// and the two coplanar surfaces fight over the same pixels. Nothing can be
/// trimmed away without opening a hole, so the junction is capped instead - the
/// way a modular kit is meant to be assembled, with a column owning the corner.
/// </summary>
public readonly struct DungeonJunctionPost
{
    /// <summary>The column prefab's own footprint and height.</summary>
    public const float HalfWidth = 0.55f;
    public const float Height = 2.42f;

    public readonly Vector2 Position;

    /// <summary>
    /// The wall section this post fades with, or null when it stands on its
    /// own. A post between two boundary runs belongs to neither: they are
    /// deliberately kept apart so a room's south wall never drops its east
    /// wall, and the post is judged on its own merit instead.
    /// </summary>
    public readonly string Section;

    public DungeonJunctionPost(Vector2 position, string section = null)
    {
        Position = position;
        Section = section;
    }

    /// <summary>The ground rectangle the post's mesh covers.</summary>
    public Rect Footprint =>
        Rect.MinMaxRect(
            Position.x - HalfWidth,
            Position.y - HalfWidth,
            Position.x + HalfWidth,
            Position.y + HalfWidth
        );
}

public static partial class DungeonRoomGeometry
{
    /// <summary>How much two slabs must share before they count as crossing.</summary>
    private const float JunctionOverlap = 0.001f;

    /// <summary>
    /// The grid post at this edge's far end, where its run crosses the two
    /// boundary runs at right angles to it. A horizontal edge owns the corner
    /// it ends at, which pairs every grid corner with exactly one edge: the
    /// post is built once however many of the four rooms around it are loaded.
    /// The end slot of a run is never a doorway, so all four runs always reach
    /// the corner and the post always has something to cap.
    /// </summary>
    public static void AppendEdgeJunctions(List<DungeonJunctionPost> posts, DungeonEdge edge)
    {
        if (!edge.Horizontal)
            return;

        Vector2 roomCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
        posts.Add(
            new DungeonJunctionPost(
                new Vector2(roomCenter.x + HalfRoomWidth, roomCenter.y + HalfRoomDepth)
            )
        );
    }

    /// <summary>
    /// The posts where a room's interior runs cross or meet each other. Runs
    /// that touch are one structure and already fade together, so a post takes
    /// the section of the run it stands on rather than fading by itself.
    /// </summary>
    public static void AppendInteriorJunctions(
        List<DungeonJunctionPost> posts,
        IReadOnlyList<DungeonWallPiece> walls
    )
    {
        int firstAdded = posts.Count;
        foreach (DungeonWallPiece alongX in walls)
        {
            if (!alongX.AlongX)
                continue;

            foreach (DungeonWallPiece alongZ in walls)
            {
                if (alongZ.AlongX || !Cross(alongX.Footprint, alongZ.Footprint))
                    continue;

                // Both slabs straddle their own centre line, so the junction is
                // where those two lines meet - not the centre of the overlap,
                // which sits off to one side when a run ends on another's seam.
                var position = new Vector2(alongZ.Anchor.x, alongX.Anchor.y);
                if (!AlreadyPosted(posts, firstAdded, position))
                    posts.Add(new DungeonJunctionPost(position, alongX.Section));
            }
        }
    }

    private static bool Cross(Rect a, Rect b)
    {
        return Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > JunctionOverlap
            && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > JunctionOverlap;
    }

    private static bool AlreadyPosted(
        List<DungeonJunctionPost> posts,
        int firstAdded,
        Vector2 position
    )
    {
        for (int i = firstAdded; i < posts.Count; i++)
        {
            if ((posts[i].Position - position).sqrMagnitude < JunctionOverlap)
                return true;
        }
        return false;
    }
}
