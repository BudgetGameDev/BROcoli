using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks one contiguous piece of dungeon architecture as an occlusion unit -
/// the logical wall group the visibility decision works in. Everything under a
/// section fades together; which of its pieces actually fade is decided by
/// <see cref="WallVisibilityResolver.IsPieceInTheWay"/>, so a run passing the
/// player never lowers the part standing behind them.
///
/// A section is exactly one built run. Runs on either side of a grid post are
/// separate sections and never drag each other down: the post is always walled,
/// so the player cannot walk from one to the other along the wall, and to them
/// the two are different walls in different rooms.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonOcclusionSection : MonoBehaviour
{
    private static readonly Dictionary<int, DungeonOcclusionSection> Registry = new();

    /// <summary>
    /// A renderer paired with the solid extent it is judged by. The wall mesh
    /// carries floor moulding that reaches well past the slab; deciding on that
    /// would fade a wall the player has not reached yet, so the decision uses
    /// the colliders and the fade is applied to the mesh.
    /// </summary>
    private readonly struct FadeCandidate
    {
        public readonly Renderer Renderer;
        public readonly Bounds Structure;

        /// <summary>The piece identity the resolver settles fades under.</summary>
        public readonly int PieceId;

        public FadeCandidate(Renderer renderer, Bounds structure)
        {
            Renderer = renderer;
            Structure = structure;
            PieceId = renderer.GetInstanceID();
        }
    }

    private FadeCandidate[] fadeCandidates;
    private Transform excludedRoot;
    private Transform gatewayRoot;
    private float gatewayFadeReferenceMinY;
    private float gatewayFadeReferenceHeight;
    private Vector2Int firstRoom;
    private Vector2Int secondRoom;
    private bool hasRoomOwnership;
    private bool hasSecondRoom;

    /// <summary>
    /// The visibility group this section is. Stable for the section's life and
    /// unique across sections, so the decision layer can reason in plain ints.
    /// </summary>
    public int GroupId => GetInstanceID();

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
    /// Whether this section belongs to the room the player occupies. Legacy
    /// or manually authored sections without ownership remain eligible.
    /// </summary>
    public bool BelongsToRoom(Vector2Int room, DungeonLayout layout)
    {
        if (!hasRoomOwnership)
            return true;
        if (RoomsMatch(room, firstRoom, layout))
            return true;
        return hasSecondRoom && RoomsMatch(room, secondRoom, layout);
    }

    public void Exclude(Transform root)
    {
        excludedRoot = root;
    }

    public void ConfigureGateway(Transform root)
    {
        gatewayRoot = root;
        CacheGatewayFadeReference();
    }

    public bool TryGetGatewayFadeReference(Renderer renderer, out float minimumY, out float height)
    {
        minimumY = gatewayFadeReferenceMinY;
        height = gatewayFadeReferenceHeight;
        return renderer != null && IsGateway(renderer.transform) && gatewayFadeReferenceHeight > 0f;
    }

    /// <summary>The section a physics hit belongs to, if any.</summary>
    public static DungeonOcclusionSection Owning(Component candidate)
    {
        DungeonOcclusionSection section = candidate.GetComponentInParent<DungeonOcclusionSection>();
        return section != null && !section.IsExcluded(candidate.transform) ? section : null;
    }

    /// <summary>The section owning a group id, or null once it is gone.</summary>
    public static DungeonOcclusionSection ForGroup(int groupId)
    {
        return Registry.TryGetValue(groupId, out DungeonOcclusionSection section) && section != null
            ? section
            : null;
    }

    /// <summary>
    /// The renderers of this section that should fade while it is lowered. The
    /// gateway frame is judged by the same rule as the wall pieces around it,
    /// so an arch and the run it stands in transition on the same frame.
    /// </summary>
    public void CollectFadeRenderers(
        WallVisibilityResolver resolver,
        Plane[] frustumPlanes,
        HashSet<Renderer> results,
        List<Renderer> rendererBuffer
    )
    {
        fadeCandidates ??= BuildFadeCandidates(transform, this, rendererBuffer);
        foreach (FadeCandidate candidate in fadeCandidates)
        {
            if (
                candidate.Renderer != null
                && candidate.Renderer.enabled
                && GeometryUtility.TestPlanesAABB(frustumPlanes, candidate.Renderer.bounds)
                && resolver.IsPieceInTheWay(candidate.PieceId, candidate.Structure)
            )
                results.Add(candidate.Renderer);
        }
    }

    /// <summary>
    /// Pairs each renderer with the solid extent of the prefab it belongs to.
    /// Dungeon architecture never moves once built, so this is worked out once.
    /// </summary>
    private static FadeCandidate[] BuildFadeCandidates(
        Transform root,
        DungeonOcclusionSection section,
        List<Renderer> rendererBuffer
    )
    {
        rendererBuffer.Clear();
        root.GetComponentsInChildren(false, rendererBuffer);
        var candidates = new List<FadeCandidate>(rendererBuffer.Count);
        foreach (Renderer candidate in rendererBuffer)
        {
            if (candidate == null || (section != null && section.IsExcluded(candidate.transform)))
                continue;
            candidates.Add(new FadeCandidate(candidate, StructureOf(candidate)));
        }
        return candidates.ToArray();
    }

    /// <summary>
    /// What is solid about the prefab a renderer belongs to, or the mesh itself
    /// when nothing about it is solid.
    /// </summary>
    private static Bounds StructureOf(Renderer renderer)
    {
        Transform prefabRoot =
            renderer.transform.parent != null ? renderer.transform.parent : renderer.transform;
        Bounds structure = default;
        bool any = false;
        foreach (Collider collider in prefabRoot.GetComponentsInChildren<Collider>())
        {
            if (collider == null || collider.isTrigger)
                continue;
            if (!any)
            {
                structure = collider.bounds;
                any = true;
            }
            else
                structure.Encapsulate(collider.bounds);
        }
        return any ? structure : renderer.bounds;
    }

    private bool IsExcluded(Transform candidate)
    {
        return excludedRoot != null
            && (candidate == excludedRoot || candidate.IsChildOf(excludedRoot));
    }

    private bool IsGateway(Transform candidate)
    {
        return gatewayRoot != null
            && (candidate == gatewayRoot || candidate.IsChildOf(gatewayRoot));
    }

    private static bool RoomsMatch(Vector2Int first, Vector2Int second, DungeonLayout layout)
    {
        return layout != null ? layout.AreInSameRoom(first, second) : first == second;
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

    private void OnEnable()
    {
        Registry[GroupId] = this;
    }

    private void OnDisable()
    {
        Registry.Remove(GroupId);
    }
}
