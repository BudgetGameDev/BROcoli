using UnityEngine;

/// <summary>What a wall piece is doing in the layout.</summary>
public enum DungeonWallKind
{
    /// <summary>Part of a shared room-boundary run.</summary>
    Shell,

    /// <summary>An interior run that reshapes a room without sealing it.</summary>
    Interior,
}

/// <summary>
/// One wall piece the builder will instantiate, described purely in
/// ground-plane coordinates. Separating the placement decision from the
/// instantiation lets the whole dungeon layout be verified arithmetically,
/// without a scene, prefabs, or a rendered frame.
/// </summary>
public readonly struct DungeonWallPiece
{
    // The Kenney wall mesh is asymmetric around its prefab origin: the upright
    // structural slab occupies local Z 0.4..1.0 and everything else in its
    // renderer bounds is floor-level moulding. Every solid-geometry question
    // about a wall - what it blocks, what it can be mounted on, where it meets
    // its neighbour - is a question about that slab.
    //
    // A piece is planned by its slab's centre line, so the slab straddles the
    // line it was asked for and a room is symmetric about its own centre.
    // PrefabPosition converts back to where the prefab root has to go.
    public const float SlabThickness = 0.6f;

    /// <summary>
    /// How tall the slab stands above the floor. Sight lines are the reason
    /// this matters: a character the camera can see over the wall is not
    /// hidden by it, so the occlusion tests need the same height the prefab
    /// actually builds.
    /// </summary>
    public const float SlabHeight = 2.28f;
    public const float SlabHalfThickness = SlabThickness / 2f;
    public const float SlabCenterOffset = 0.7f;

    /// <summary>The wall prefab's untrimmed length, one floor tile.</summary>
    public const float NominalLength = DungeonLayout.TileSize;

    // The mesh is wider than the slab: floor moulding skirts the base on the
    // side the slab's normal points away from. Occlusion works on what is
    // drawn, not on what collides, so the visible footprint is its own
    // question - a wall stops fading only once its whole mesh is past you.
    public const float MeshHalfLength = NominalLength / 2f;
    public const float MeshDepthAlongNormal = 0.3f;
    public const float MeshDepthAgainstNormal = 1.7f;

    /// <summary>The centre line of this piece's solid slab.</summary>
    public readonly Vector2 Anchor;

    /// <summary>True when the run travels along world X, false along world Z.</summary>
    public readonly bool AlongX;

    public readonly DungeonWallKind Kind;

    /// <summary>Occlusion grouping key; pieces sharing it fade together.</summary>
    public readonly string Section;

    public DungeonWallPiece(Vector2 anchor, bool alongX, DungeonWallKind kind, string section)
    {
        Anchor = anchor;
        AlongX = alongX;
        Kind = kind;
        Section = section;
    }

    public float Length => NominalLength;

    /// <summary>The direction the slab's thickness runs in.</summary>
    public Vector2 Normal => AlongX ? Vector2.up : Vector2.right;

    /// <summary>
    /// Where the prefab root goes so the slab lands on <see cref="Anchor"/>.
    /// The prefab's slab sits <see cref="SlabCenterOffset"/> ahead of its root,
    /// which is the one place that offset is allowed to matter.
    /// </summary>
    public Vector2 PrefabPosition => Anchor - Normal * SlabCenterOffset;

    /// <summary>
    /// The ground-plane rectangle the structural slab actually occupies. This
    /// is the piece's collision and sight-line footprint.
    /// </summary>
    public Rect Footprint
    {
        get
        {
            float half = Length / 2f;
            return AlongX
                ? Rect.MinMaxRect(
                    Anchor.x - half,
                    Anchor.y - SlabHalfThickness,
                    Anchor.x + half,
                    Anchor.y + SlabHalfThickness
                )
                : Rect.MinMaxRect(
                    Anchor.x - SlabHalfThickness,
                    Anchor.y - half,
                    Anchor.x + SlabHalfThickness,
                    Anchor.y + half
                );
        }
    }

    /// <summary>
    /// The ground-plane rectangle this piece's mesh occupies, moulding
    /// included. This is what the camera sees fade.
    /// </summary>
    public Rect RenderFootprint
    {
        get
        {
            Vector2 normal = Normal;
            Vector2 far = Anchor + normal * MeshDepthAlongNormal;
            Vector2 near = Anchor - normal * MeshDepthAgainstNormal;
            return AlongX
                ? Rect.MinMaxRect(
                    Anchor.x - MeshHalfLength,
                    near.y,
                    Anchor.x + MeshHalfLength,
                    far.y
                )
                : Rect.MinMaxRect(
                    near.x,
                    Anchor.y - MeshHalfLength,
                    far.x,
                    Anchor.y + MeshHalfLength
                );
        }
    }

    public override string ToString()
    {
        return $"{Kind} {(AlongX ? "X" : "Z")} wall at {Anchor} ({Section})";
    }
}
