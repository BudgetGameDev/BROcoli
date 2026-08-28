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
