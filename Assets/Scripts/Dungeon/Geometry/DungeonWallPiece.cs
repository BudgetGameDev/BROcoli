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
    public const float SlabNearFace = 0.4f;
    public const float SlabFarFace = 1f;
    public const float SlabThickness = SlabFarFace - SlabNearFace;
    public const float SlabCenterOffset = (SlabNearFace + SlabFarFace) / 2f;

    /// <summary>The wall prefab's untrimmed length, one floor tile.</summary>
    public const float NominalLength = DungeonLayout.TileSize;

    /// <summary>Where the builder instantiates the prefab.</summary>
    public readonly Vector2 Anchor;

    /// <summary>True when the run travels along world X, false along world Z.</summary>
    public readonly bool AlongX;

    /// <summary>
    /// Signed change to the piece's length. Negative pulls the piece back from
    /// a junction, positive pushes it through one so perpendicular runs meet
    /// without exposing a bevelled end face.
    /// </summary>
    public readonly float LengthAdjustment;

    /// <summary>
    /// Which end of the run the adjustment moves, as a world-axis direction
    /// along <see cref="AlongX"/>. Zero when the piece is untrimmed.
    /// </summary>
    public readonly float AdjustmentEnd;

    public readonly DungeonWallKind Kind;

    /// <summary>Occlusion grouping key; pieces sharing it fade together.</summary>
    public readonly string Section;

    public DungeonWallPiece(
        Vector2 anchor,
        bool alongX,
        DungeonWallKind kind,
        string section,
        float lengthAdjustment = 0f,
        float adjustmentEnd = 0f
    )
    {
        Anchor = anchor;
        AlongX = alongX;
        Kind = kind;
        Section = section;
        LengthAdjustment = lengthAdjustment;
        AdjustmentEnd = adjustmentEnd;
    }

    public float Length => NominalLength + LengthAdjustment;

    /// <summary>How far the piece slides along its run axis to stay anchored at
    /// its unadjusted end.</summary>
    public float RunShift => AdjustmentEnd * LengthAdjustment * 0.5f;

    /// <summary>
    /// The ground-plane rectangle the structural slab actually occupies. This
    /// is the piece's collision and sight-line footprint.
    /// </summary>
    public Rect Footprint
    {
        get
        {
            float half = Length / 2f;
            float shift = RunShift;
            return AlongX
                ? Rect.MinMaxRect(
                    Anchor.x + shift - half,
                    Anchor.y + SlabNearFace,
                    Anchor.x + shift + half,
                    Anchor.y + SlabFarFace
                )
                : Rect.MinMaxRect(
                    Anchor.x + SlabNearFace,
                    Anchor.y + shift - half,
                    Anchor.x + SlabFarFace,
                    Anchor.y + shift + half
                );
        }
    }

    /// <summary>The centre line's start point, along the run axis.</summary>
    public Vector2 RunStart =>
        AlongX
            ? new Vector2(Anchor.x + RunShift - Length / 2f, Anchor.y + SlabCenterOffset)
            : new Vector2(Anchor.x + SlabCenterOffset, Anchor.y + RunShift - Length / 2f);

    /// <summary>The centre line's end point, along the run axis.</summary>
    public Vector2 RunEnd =>
        AlongX
            ? new Vector2(Anchor.x + RunShift + Length / 2f, Anchor.y + SlabCenterOffset)
            : new Vector2(Anchor.x + SlabCenterOffset, Anchor.y + RunShift + Length / 2f);

    public override string ToString()
    {
        return $"{Kind} {(AlongX ? "X" : "Z")} wall at {Anchor} ({Section})";
    }
}
