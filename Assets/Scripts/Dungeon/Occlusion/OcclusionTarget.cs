using UnityEngine;

/// <summary>
/// A character the camera has to keep readable, described by where it is and
/// what rectangle of the screen it fills. Building one is the only step that
/// needs renderers; every decision after it is arithmetic on these values.
/// </summary>
public readonly struct OcclusionTarget
{
    public readonly OcclusionTargetKind Kind;
    public readonly Vector3 Position;
    public readonly Bounds Bounds;
    public readonly Rect ViewportRect;

    /// <summary>
    /// How much of this target a wall must cover before it is worth lowering.
    /// The enemy threshold is deliberately lower than the player's, so even a
    /// partly hidden enemy stays readable.
    /// </summary>
    public readonly float MinimumCoverage;

    public OcclusionTarget(
        OcclusionTargetKind kind,
        Vector3 position,
        Bounds bounds,
        Rect viewportRect,
        float minimumCoverage
    )
    {
        Kind = kind;
        Position = position;
        Bounds = bounds;
        ViewportRect = viewportRect;
        MinimumCoverage = minimumCoverage;
    }

    public static bool TryCreate(
        in OcclusionCameraModel camera,
        OcclusionTargetKind kind,
        Vector3 position,
        Bounds bounds,
        float minimumCoverage,
        out OcclusionTarget target
    )
    {
        if (!WallOcclusionMath.TryProjectBounds(camera, bounds, out Rect viewportRect))
        {
            target = default;
            return false;
        }

        target = new OcclusionTarget(kind, position, bounds, viewportRect, minimumCoverage);
        return true;
    }
}
