using UnityEngine;

public partial class PlayerMovement
{
    /// <summary>
    /// Cast the player's body before moving and slide along obstacles on contact.
    /// Enemies retain their extra stand-off gap. Both enemy and wall checks use
    /// the player's vertical capsule, giving the body the same circular
    /// footprint from every approach direction.
    /// </summary>
    private Vector2 ResolveNavigationCollisions(Vector2 desiredDelta)
    {
        if (
            _collider == null
            || (_enemyLayerMask | _wallLayerMask) == 0
            || desiredDelta.sqrMagnitude < 0.000001f
        )
            return desiredDelta;

        if (
            !TryGetNavigationCapsule(
                _collider,
                out Vector3 castTop,
                out Vector3 castBottom,
                out float castRadius
            )
        )
            return desiredDelta;

        Vector2 resolvedDelta = Vector2.zero;
        Vector2 remainingDelta = desiredDelta;

        for (int i = 0; i < MaxCollisionSlides; i++)
        {
            float distance = remainingDelta.magnitude;
            if (distance < 0.0001f)
                break;

            Vector2 direction = remainingDelta / distance;
            bool hasEnemyHit = TryGetBlockingHit(
                castTop,
                castBottom,
                castRadius,
                direction,
                distance + CollisionSkin + EnemyStandOffGap,
                _enemyLayerMask,
                out RaycastHit enemyHit,
                out Vector2 enemyNormal
            );
            bool hasWallHit = TryGetBlockingHit(
                castTop,
                castBottom,
                castRadius,
                direction,
                distance + CollisionSkin,
                _wallLayerMask,
                out RaycastHit wallHit,
                out Vector2 wallNormal
            );
            if (!hasEnemyHit && !hasWallHit)
            {
                resolvedDelta += remainingDelta;
                break;
            }

            float enemyTravelDistance = hasEnemyHit
                ? enemyHit.distance - CollisionSkin - EnemyStandOffGap
                : float.PositiveInfinity;
            float wallTravelDistance = hasWallHit
                ? wallHit.distance - CollisionSkin
                : float.PositiveInfinity;
            bool wallBlocksFirst = wallTravelDistance <= enemyTravelDistance;
            Vector2 hitNormal = wallBlocksFirst ? wallNormal : enemyNormal;
            float travelDistance = Mathf.Clamp(
                wallBlocksFirst ? wallTravelDistance : enemyTravelDistance,
                0f,
                distance
            );
            Vector2 travel = direction * travelDistance;
            resolvedDelta += travel;
            Vector3 worldTravel = travel.ToWorld();
            castTop += worldTravel;
            castBottom += worldTravel;

            Vector2 untraveled = direction * (distance - travelDistance);
            float intoSurface = Vector2.Dot(untraveled, hitNormal);
            if (intoSurface < 0f)
                untraveled -= hitNormal * intoSurface;

            remainingDelta = untraveled;
        }

        return resolvedDelta;
    }

    /// <summary>
    /// The nearest obstacle on <paramref name="layerMask"/> that actually stands
    /// in the way, with the ground-plane normal of the surface it presents. The
    /// normal is reported separately because the sweep's own normal cannot be
    /// trusted at zero distance, and the caller slides along it.
    /// </summary>
    private bool TryGetBlockingHit(
        Vector3 castTop,
        Vector3 castBottom,
        float castRadius,
        Vector2 direction,
        float distance,
        int layerMask,
        out RaycastHit closestHit,
        out Vector2 surfaceNormal
    )
    {
        closestHit = default;
        surfaceNormal = Vector2.zero;
        if (layerMask == 0)
            return false;

        int hitCount = Physics.CapsuleCastNonAlloc(
            castTop,
            castBottom,
            castRadius,
            direction.ToWorld(),
            _collisionHits,
            distance,
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = _collisionHits[i];
            if (candidate.collider == null || candidate.collider == _collider)
                continue;

            if (!TryGetContactNormal(candidate, castTop, castBottom, direction, out Vector2 normal))
                continue;

            // A surface has to face us to stop us. Anything we are only grazing
            // is something we slide past, and treating it as blocking is what
            // used to catch the player on the seam between two wall pieces: the
            // corner of the next slab is reached edge-on, at right angles to the
            // way we are travelling, and clamping the travel against it froze
            // the run flush against an otherwise smooth wall.
            if (Vector2.Dot(direction, normal) >= -GrazingApproach)
                continue;

            if (candidate.distance < closestDistance)
            {
                closestDistance = candidate.distance;
                closestHit = candidate;
                surfaceNormal = normal;
            }
        }

        return closestHit.collider != null;
    }

    /// <summary>
    /// The ground-plane normal of the surface a sweep hit. A sweep that starts
    /// in contact reports the sweep direction back at us rather than a surface
    /// normal, so that case is measured against the collider instead. A body we
    /// are already inside reports nothing, leaving the caller free to move out
    /// of it rather than becoming stuck in it.
    /// </summary>
    private static bool TryGetContactNormal(
        RaycastHit hit,
        Vector3 castTop,
        Vector3 castBottom,
        Vector2 direction,
        out Vector2 normal
    )
    {
        Vector2 candidate;
        if (hit.distance > 0f)
            candidate = hit.normal.ToGround();
        else
        {
            Vector3 castCenter = (castTop + castBottom) * 0.5f;
            candidate = castCenter.ToGround() - hit.collider.ClosestPoint(castCenter).ToGround();
        }

        // A purely vertical normal - the top of a wall, the lip of a step -
        // cannot oppose movement across the ground, and a zero-length one means
        // the cast centre is inside the collider.
        if (candidate.sqrMagnitude < 0.000001f)
        {
            normal = Vector2.zero;
            return false;
        }

        normal = candidate.normalized;
        return true;
    }

    /// <summary>
    /// Converts the player's authored collider bounds into the upright capsule
    /// used by predictive movement and hop-visual collision checks.
    /// </summary>
    internal static bool TryGetNavigationCapsule(
        Collider collider,
        out Vector3 top,
        out Vector3 bottom,
        out float radius
    )
    {
        if (collider == null || !collider.enabled)
        {
            top = default;
            bottom = default;
            radius = 0f;
            return false;
        }

        Bounds bounds = collider.bounds;
        radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        if (radius <= 0.0001f)
        {
            top = default;
            bottom = default;
            return false;
        }

        float capOffset = Mathf.Max(0f, bounds.extents.y - radius);
        top = bounds.center + Vector3.up * capOffset;
        bottom = bounds.center - Vector3.up * capOffset;
        return true;
    }
}
