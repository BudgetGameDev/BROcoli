using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The dungeon's architecture arranged into the visibility groups the builder
/// actually creates: one group per built boundary run - archway included - and
/// one per freestanding interior structure after touching runs are fused.
///
/// It distinguishes what a ray can hit from what the camera sees fade, because
/// the game does: sight lines are cast against colliders, while the fade is
/// applied to meshes that reach further. It answers the one question the
/// decision layer asks of a scene - what lies along this ray - so the property
/// tests exercise the production rules rather than a restatement of them.
/// </summary>
internal sealed class WallVisibilityWorld : IOcclusionCandidateSource
{
    /// <summary>The height the built wall slabs stand at.</summary>
    public const float WallHeight = DungeonWallPiece.SlabHeight;

    internal sealed class Group
    {
        public int Id;
        public string Name;
        public Vector2Int Room;
        public DungeonWallKind Kind;
        public bool IsEdge;
        public bool AlongX;
        public readonly List<int> Pieces = new();
    }

    /// <summary>One mesh the camera can see fade.</summary>
    internal readonly struct Piece
    {
        public readonly int Id;
        public readonly int GroupId;
        public readonly string Label;
        public readonly bool IsGateway;

        /// <summary>What the camera sees fade, decoration included.</summary>
        public readonly Bounds Render;

        /// <summary>
        /// What is solid about it. The wall mesh carries floor moulding that
        /// reaches well past the slab, so the fade decision is taken on the
        /// collider and applied to the mesh, exactly as the game does it.
        /// </summary>
        public readonly Bounds Structure;

        /// <summary>Meaningful only when this piece is a wall, not a frame.</summary>
        public readonly DungeonWallPiece Plan;

        public Piece(
            int id,
            int groupId,
            string label,
            bool isGateway,
            Bounds render,
            Bounds structure,
            DungeonWallPiece plan
        )
        {
            Id = id;
            GroupId = groupId;
            Label = label;
            IsGateway = isGateway;
            Render = render;
            Structure = structure;
            Plan = plan;
        }

        public bool IsWall => !IsGateway;
    }

    /// <summary>
    /// Something a sight line can be stopped by: a collider, or an arch crown's
    /// volume, which has no collider and nothing of its own to draw.
    /// </summary>
    private readonly struct Blocker
    {
        public readonly int GroupId;
        public readonly Bounds Bounds;

        public Blocker(int groupId, Bounds bounds)
        {
            GroupId = groupId;
            Bounds = bounds;
        }
    }

    private readonly List<Group> groups = new();
    private readonly List<Piece> pieces = new();
    private readonly List<Blocker> blockers = new();
    private readonly List<Blocker> crowns = new();
    private readonly List<int> order = new();

    public readonly DungeonGeometryModel Block;

    public WallVisibilityWorld(DungeonGeometryModel block)
    {
        Block = block;
        foreach (Vector2Int room in block.Rooms)
            AddInterior(room);
        foreach (DungeonEdge edge in block.Edges)
            AddEdge(edge);
        ResetOrder();
    }

    public int Seed => Block.Seed;
    public IReadOnlyList<Group> Groups => groups;
    public IReadOnlyList<Piece> Pieces => pieces;

    public Group GroupOf(int groupId)
    {
        return groups[groupId];
    }

    public Piece PieceOf(int pieceId)
    {
        return pieces[pieceId];
    }

    /// <summary>
    /// Permutes the order query results arrive in. Which groups are selected
    /// must not depend on it.
    /// </summary>
    public void ShuffleQueryOrder(int seed)
    {
        var random = new System.Random(seed);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
    }

    public void ResetOrder()
    {
        order.Clear();
        for (int i = 0; i < blockers.Count; i++)
            order.Add(i);
    }

    public void Collect(Ray ray, float maximumDistance, List<OcclusionCandidate> results)
    {
        foreach (int index in order)
        {
            Blocker blocker = blockers[index];
            if (WallVisibilityBounds.IntersectsRay(blocker.Bounds, ray, maximumDistance))
                results.Add(new OcclusionCandidate(blocker.GroupId, blocker.Bounds));
        }
    }

    /// <summary>
    /// Wall slabs are solid, so nothing stands inside one; an arch crown spans
    /// the doorway, so the player walks right under it.
    /// </summary>
    public void CollectEnclosing(Vector3 targetPosition, List<OcclusionCandidate> results)
    {
        foreach (Blocker crown in crowns)
        {
            if (WallOcclusionMath.ContainsGroundPoint(crown.Bounds, targetPosition))
                results.Add(new OcclusionCandidate(crown.GroupId, crown.Bounds));
        }
    }

    private void AddInterior(Vector2Int room)
    {
        List<DungeonWallPiece> walls = Block.InteriorWalls(room);
        Dictionary<string, string> fused = DungeonWallGrouping.ResolveInteriorGroups(walls);
        var byGroup = new Dictionary<string, Group>();
        foreach (DungeonWallPiece plan in walls)
        {
            string key = fused.TryGetValue(plan.Section, out string resolved)
                ? resolved
                : plan.Section;
            if (!byGroup.TryGetValue(key, out Group group))
            {
                group = NewGroup($"Interior {room} / {key}", room, DungeonWallKind.Interior);
                byGroup.Add(key, group);
            }
            AddWall(group, plan);
        }
    }

    private void AddEdge(DungeonEdge edge)
    {
        var room = new Vector2Int(edge.X, edge.Y);
        int direction = edge.Horizontal ? DungeonLayout.North : DungeonLayout.East;
        Group group = NewGroup(
            $"Edge ({edge.X}, {edge.Y}, {(edge.Horizontal ? "H" : "V")})",
            room,
            DungeonWallKind.Shell
        );
        group.IsEdge = true;
        group.AlongX = edge.Horizontal;
        foreach (DungeonWallPiece plan in Block.EdgeWalls(room, direction))
            AddWall(group, plan);

        var archways = new List<DungeonArchway>();
        DungeonRoomGeometry.AppendEdgeArchways(archways, edge, Block.Passage(room, direction));
        foreach (DungeonArchway archway in archways)
            AddArchway(group, archway);
    }

    private Group NewGroup(string name, Vector2Int room, DungeonWallKind kind)
    {
        var group = new Group
        {
            Id = groups.Count,
            Name = name,
            Room = room,
            Kind = kind,
        };
        groups.Add(group);
        return group;
    }

    private void AddWall(Group group, DungeonWallPiece plan)
    {
        Bounds slab = Box(plan.Footprint, WallHeight);
        AddPiece(group, plan.ToString(), false, Box(plan.RenderFootprint, WallHeight), slab, plan);
        blockers.Add(new Blocker(group.Id, slab));
    }

    /// <summary>
    /// An archway is part of the run it stands in: its posts block sight lines,
    /// its crown spans the doorway, and its one frame mesh fades with the wall
    /// pieces beside it.
    /// </summary>
    private void AddArchway(Group group, DungeonArchway archway)
    {
        Bounds posts = Box(archway.PostFootprint(false), DungeonArchway.MeshHeight);
        Bounds far = Box(archway.PostFootprint(true), DungeonArchway.MeshHeight);
        posts.Encapsulate(far);
        AddPiece(
            group,
            $"Archway at {archway.Position}",
            true,
            Box(archway.RenderFootprint, DungeonArchway.MeshHeight),
            posts,
            default
        );
        blockers.Add(
            new Blocker(group.Id, Box(archway.PostFootprint(false), DungeonArchway.MeshHeight))
        );
        blockers.Add(new Blocker(group.Id, far));

        var crown = new Blocker(
            group.Id,
            WallVisibilityBounds.Lift(
                archway.OcclusionFootprint,
                DungeonArchway.OcclusionVolumeCenter.y,
                DungeonArchway.OcclusionVolumeSize.y
            )
        );
        blockers.Add(crown);
        crowns.Add(crown);
    }

    private void AddPiece(
        Group group,
        string label,
        bool isGateway,
        Bounds render,
        Bounds structure,
        DungeonWallPiece plan
    )
    {
        group.Pieces.Add(pieces.Count);
        pieces.Add(new Piece(pieces.Count, group.Id, label, isGateway, render, structure, plan));
    }

    private static Bounds Box(Rect footprint, float height)
    {
        return WallVisibilityBounds.Lift(footprint, height / 2f, height);
    }
}
