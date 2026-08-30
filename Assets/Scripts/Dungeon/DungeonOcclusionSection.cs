using UnityEngine;

/// <summary>
/// Marks one contiguous piece of dungeon architecture as a single occlusion
/// unit. A wall run has to fade as one wall, so its pieces are grouped
/// deliberately here instead of each becoming its own occluder the way a
/// freestanding prop does.
///
/// A section is exactly one built run. Runs on either side of a grid post are
/// separate sections and never drag each other down: the post is always walled,
/// so the player cannot walk from one to the other along the wall, and to them
/// the two are different walls in different rooms.
///
/// Everything a section adds to <see cref="DungeonOccluder"/> is knowledge only
/// the builder has - which rooms a run borders, which of its children are
/// scenery rather than wall, and which pieces have to line their fade up with a
/// neighbour. Measurement is left to the base class.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonOcclusionSection : DungeonOccluder
{
    private Transform excludedRoot;
    private Transform gatewayRoot;
    private float gatewayFadeReferenceMinY;
    private float gatewayFadeReferenceHeight;
    private Vector2Int firstRoom;
    private Vector2Int secondRoom;
    private bool hasRoomOwnership;
    private bool hasSecondRoom;

    /// <summary>Marks an interior section as belonging to one room cell.</summary>
    public void ConfigureRoom(Vector2Int room)
    {
        firstRoom = room;
        secondRoom = default;
        hasRoomOwnership = true;
        hasSecondRoom = false;
    }

    /// <summary>Marks a boundary run as shared by the rooms on both sides.</summary>
    public void ConfigureEdge(DungeonEdge edge)
    {
        firstRoom = new Vector2Int(edge.X, edge.Y);
        secondRoom = firstRoom + (edge.Horizontal ? Vector2Int.up : Vector2Int.right);
        hasRoomOwnership = true;
        hasSecondRoom = true;
    }

    /// <summary>
    /// Whether this section belongs to the room the player occupies. A run
    /// built as a room boundary belongs to both rooms it separates, which is
    /// something its position alone cannot say, so configured ownership wins
    /// over the base class's measurement. Sections without ownership fall back
    /// to standing where they stand.
    /// </summary>
    public override bool BelongsToRoom(Vector2Int room, DungeonLayout layout)
    {
        if (!hasRoomOwnership)
            return base.BelongsToRoom(room, layout);
        if (RoomsMatch(room, firstRoom, layout))
            return true;
        return hasSecondRoom && RoomsMatch(room, secondRoom, layout);
    }

    public void Exclude(Transform root)
    {
        excludedRoot = root;
        InvalidateFadeCandidates();
    }

    public void ConfigureGateway(Transform root)
    {
        gatewayRoot = root;
        CacheGatewayFadeReference();
        InvalidateFadeCandidates();
    }

    /// <summary>
    /// Gateway crowns and grates are measured against the wall they stand in
    /// rather than against themselves, so an arch and the run around it give
    /// way at the same absolute height instead of at the same fraction of two
    /// different renderer heights.
    /// </summary>
    protected override bool TryGetOverrideFadeReference(
        Renderer renderer,
        out float minimumY,
        out float height
    )
    {
        minimumY = gatewayFadeReferenceMinY;
        height = gatewayFadeReferenceHeight;
        return renderer != null && IsGateway(renderer.transform) && gatewayFadeReferenceHeight > 0f;
    }

    protected override bool IsExcluded(Transform candidate)
    {
        return excludedRoot != null
            && (candidate == excludedRoot || candidate.IsChildOf(excludedRoot));
    }

    private bool IsGateway(Transform candidate)
    {
        return gatewayRoot != null
            && (candidate == gatewayRoot || candidate.IsChildOf(gatewayRoot));
    }

    private void CacheGatewayFadeReference()
    {
        gatewayFadeReferenceMinY = 0f;
        gatewayFadeReferenceHeight = 0f;
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>(false))
        {
            if (
                candidate == null
                || IsGateway(candidate.transform)
                || IsExcluded(candidate.transform)
                || candidate.bounds.size.y <= gatewayFadeReferenceHeight
            )
                continue;

            gatewayFadeReferenceMinY = candidate.bounds.min.y;
            gatewayFadeReferenceHeight = candidate.bounds.size.y;
        }
    }
}
