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
                out RaycastHit enemyHit
            );
            bool hasWallHit = TryGetBlockingHit(
                castTop,
                castBottom,
                castRadius,
                direction,
                distance + CollisionSkin,
                _wallLayerMask,
                out RaycastHit wallHit
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
            RaycastHit hit = wallBlocksFirst ? wallHit : enemyHit;
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
            Vector2 hitNormal = hit.normal.ToGround();
            float intoSurface = Vector2.Dot(untraveled, hitNormal);
            if (intoSurface < 0f)
                untraveled -= hitNormal * intoSurface;

            remainingDelta = untraveled;
        }

        return resolvedDelta;
    }

    private bool TryGetBlockingHit(
        Vector3 castTop,
        Vector3 castBottom,
        float castRadius,
        Vector2 direction,
        float distance,
        int layerMask,
        out RaycastHit closestHit
    )
    {
        if (layerMask == 0)
        {
            closestHit = default;
            return false;
        }

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

        closestHit = default;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = _collisionHits[i];
            if (candidate.collider == null || candidate.collider == _collider)
                continue;

            // Casts that begin in contact can report a zero-distance hit even
            // while moving away or tangentially. Ignore that contact so the
            // player can always disengage instead of becoming stuck.
            if (candidate.distance <= CollisionSkin)
            {
                Vector3 castCenter = (castTop + castBottom) * 0.5f;
                Vector2 closestPoint = candidate.collider.ClosestPoint(castCenter).ToGround();
                Vector2 awayFromObstacle = castCenter.ToGround() - closestPoint;
                if (Vector2.Dot(direction, awayFromObstacle) >= 0f)
                    continue;
            }

            if (candidate.distance < closestDistance)
            {
                closestDistance = candidate.distance;
                closestHit = candidate;
            }
        }

        return closestHit.collider != null;
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
