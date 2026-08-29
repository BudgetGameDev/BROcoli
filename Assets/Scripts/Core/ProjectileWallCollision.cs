using UnityEngine;

/// <summary>
/// Swept wall detection shared by friendly and hostile projectiles. Trigger
/// callbacks remain as a fallback, but the sweep prevents fast projectiles
/// from crossing a thin wall between physics updates.
/// </summary>
public static class ProjectileWallCollision
{
    private const float MinimumSweepRadius = 0.025f;
    private const float MaximumSweepRadius = 0.25f;
    private static int wallMask;

    public static bool IsWall(Collider candidate)
    {
        return candidate != null
            && !candidate.isTrigger
            && (WallMask & (1 << candidate.gameObject.layer)) != 0;
    }

    /// <summary>
    /// Returns whether a shot can travel between two points without crossing
    /// a solid wall. Use the shooter's position rather than an offset muzzle
    /// position so a muzzle cannot begin on the far side of a thin wall.
    /// </summary>
    public static bool HasClearLine(Vector3 origin, Vector3 target)
    {
        Vector3 displacement = target - origin;
        float distance = displacement.magnitude;
        if (distance <= 0.000001f)
            return true;
        if (WallMask == 0)
            return true;

        return !Physics.Raycast(
            origin,
            displacement / distance,
            distance,
            WallMask,
            QueryTriggerInteraction.Ignore
        );
    }

    public static bool Sweep(
        Collider projectileCollider,
        Transform projectileTransform,
        Vector3 displacement,
        out RaycastHit wallHit
    )
    {
        wallHit = default;
        float distance = displacement.magnitude;
        if (projectileTransform == null || distance <= 0.000001f || WallMask == 0)
            return false;

        Vector3 center =
            projectileCollider != null && projectileCollider.enabled
                ? projectileCollider.bounds.center
                : projectileTransform.position;
        float radius = SweepRadius(projectileCollider);

        if (Physics.CheckSphere(center, radius, WallMask, QueryTriggerInteraction.Ignore))
            return true;

        return Physics.SphereCast(
            center,
            radius,
            displacement / distance,
            out wallHit,
            distance,
            WallMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private static int WallMask
    {
        get
        {
            if (wallMask == 0)
                wallMask = LayerMask.GetMask("Wall");
            return wallMask;
        }
    }

    private static float SweepRadius(Collider projectileCollider)
    {
        if (projectileCollider == null || !projectileCollider.enabled)
            return MinimumSweepRadius;

        Vector3 extents = projectileCollider.bounds.extents;
        float groundRadius = Mathf.Min(extents.x, extents.z);
        return Mathf.Clamp(groundRadius, MinimumSweepRadius, MaximumSweepRadius);
    }
}
