using UnityEngine;

/// <summary>
/// The geometric predicates behind every wall-visibility decision. They are
/// static and take only values, so the runtime fader and the property tests
/// ask exactly the same questions of exactly the same arithmetic.
/// </summary>
public static class WallOcclusionMath
{
    /// <summary>
    /// The camera's forward direction flattened onto the ground, or zero when
    /// it looks straight down and there is no such direction.
    /// </summary>
    public static Vector3 GroundForward(in OcclusionCameraModel camera)
    {
        Vector3 groundForward = Vector3.ProjectOnPlane(camera.Forward, Vector3.up);
        return groundForward.sqrMagnitude <= 0.0001f ? Vector3.zero : groundForward.normalized;
    }

    /// <summary>How far along the view axis a point stands from the camera.</summary>
    public static float GroundDepth(
        in OcclusionCameraModel camera,
        Vector3 groundForward,
        Vector3 point
    )
    {
        return Vector3.Dot(point - camera.Position, groundForward);
    }

    /// <summary>The depth of a box's rear-most ground corner.</summary>
    public static float RearGroundDepth(
        in OcclusionCameraModel camera,
        Vector3 groundForward,
        Bounds bounds
    )
    {
        return GroundDepth(camera, groundForward, bounds.center)
            + HalfGroundDepth(groundForward, bounds);
    }

    /// <summary>The depth of a box's nearest ground corner.</summary>
    public static float FrontGroundDepth(
        in OcclusionCameraModel camera,
        Vector3 groundForward,
        Bounds bounds
    )
    {
        return GroundDepth(camera, groundForward, bounds.center)
            - HalfGroundDepth(groundForward, bounds);
    }

    /// <summary>
    /// True when <paramref name="bounds"/> stands in the gap between the camera
    /// and the target, which is the only place a wall can hide anything.
    ///
    /// A wall that reaches beside or past the target is not an occluder even
    /// when its centre is slightly camera-side, so the rear-most point of its
    /// ground footprint is what gets compared. A wall behind the camera hides
    /// nothing at all, so its front-most point has to be in view too: without
    /// that, a run linked to the one the player is beside could fade a segment
    /// far behind the camera.
    /// </summary>
    public static bool IsBetweenCameraAndTarget(
        Bounds bounds,
        in OcclusionCameraModel camera,
        Vector3 targetPosition
    )
    {
        Vector3 groundForward = GroundForward(camera);
        if (groundForward == Vector3.zero)
            return IsAboveTargetOnScreen(bounds, camera, targetPosition);

        return IsWithinDepth(
            bounds,
            camera,
            groundForward,
            GroundDepth(camera, groundForward, targetPosition)
        );
    }

    /// <summary>
    /// True when a box lies wholly inside the gap between the camera and a
    /// target at <paramref name="limit"/>. This is the strict test, and it
    /// decides whether a wall group is worth lowering at all.
    /// </summary>
    public static bool IsWithinDepth(
        Bounds bounds,
        in OcclusionCameraModel camera,
        Vector3 groundForward,
        float limit
    )
    {
        return RearGroundDepth(camera, groundForward, bounds) <= limit
            && FrontGroundDepth(camera, groundForward, bounds) >= 0f;
    }

    /// <summary>
    /// True when any part of a box lies in that gap. This is the loose test,
    /// and it decides which pieces of an already-lowered group take part.
    ///
    /// A piece opts out only when it is unambiguously out of the way, so an
    /// arch frame straddling the player - too long to be wholly in front of
    /// them - still fades with the run it stands in instead of staying up for
    /// the moment it takes to walk through.
    /// </summary>
    public static bool OverlapsDepthRange(
        Bounds bounds,
        in OcclusionCameraModel camera,
        Vector3 groundForward,
        float limit
    )
    {
        return FrontGroundDepth(camera, groundForward, bounds) <= limit
            && RearGroundDepth(camera, groundForward, bounds) >= 0f;
    }

    private static float HalfGroundDepth(Vector3 groundForward, Bounds bounds)
    {
        return Mathf.Abs(groundForward.x) * bounds.extents.x
            + Mathf.Abs(groundForward.z) * bounds.extents.z;
    }

    /// <summary>
    /// How much of the target's screen rectangle an occluder's screen
    /// rectangle covers, as a fraction of the target's area.
    /// </summary>
    public static float CoverageOf(Rect targetRect, Rect occluderRect)
    {
        float overlapWidth = Mathf.Max(
            0f,
            Mathf.Min(targetRect.xMax, occluderRect.xMax)
                - Mathf.Max(targetRect.xMin, occluderRect.xMin)
        );
        float overlapHeight = Mathf.Max(
            0f,
            Mathf.Min(targetRect.yMax, occluderRect.yMax)
                - Mathf.Max(targetRect.yMin, occluderRect.yMin)
        );
        float targetArea = Mathf.Max(0.000001f, targetRect.width * targetRect.height);
        return overlapWidth * overlapHeight / targetArea;
    }

    /// <summary>The screen rectangle a world-space box occupies.</summary>
    public static bool TryProjectBounds(
        in OcclusionCameraModel camera,
        Bounds bounds,
        out Rect viewportRect
    )
    {
        Vector3 minimum = bounds.min;
        Vector3 maximum = bounds.max;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool hasVisiblePoint = false;

        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            var corner = new Vector3(
                x == 0 ? minimum.x : maximum.x,
                y == 0 ? minimum.y : maximum.y,
                z == 0 ? minimum.z : maximum.z
            );
            Vector3 viewport = camera.WorldToViewportPoint(corner);
            if (viewport.z <= camera.NearClip)
                continue;

            hasVisiblePoint = true;
            minX = Mathf.Min(minX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x);
            maxY = Mathf.Max(maxY, viewport.y);
        }

        viewportRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return hasVisiblePoint && maxX > 0f && minX < 1f && maxY > 0f && minY < 1f;
    }

    /// <summary>True when a position stands inside a box's ground footprint.</summary>
    public static bool ContainsGroundPoint(Bounds bounds, Vector3 position)
    {
        return position.x >= bounds.min.x
            && position.x <= bounds.max.x
            && position.z >= bounds.min.z
            && position.z <= bounds.max.z;
    }

    private static bool IsAboveTargetOnScreen(
        Bounds bounds,
        in OcclusionCameraModel camera,
        Vector3 targetPosition
    )
    {
        Vector3 candidatePosition = bounds.center;
        candidatePosition.y = targetPosition.y;
        Vector3 candidateViewport = camera.WorldToViewportPoint(candidatePosition);
        Vector3 targetViewport = camera.WorldToViewportPoint(targetPosition);
        return candidateViewport.z > camera.NearClip && candidateViewport.y <= targetViewport.y;
    }
}
