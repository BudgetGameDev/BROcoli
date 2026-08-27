using UnityEngine;

public partial class PlayerMovement
{
    /// <summary>
    /// Cast the player's body before moving and slide along obstacles on contact.
    /// Enemies retain their extra stand-off gap, while walls use a body-sized
    /// footprint so diagonal approaches cannot push the visible model through
    /// the architecture.
    /// </summary>
    private Vector2 ResolveNavigationCollisions(Vector2 desiredDelta)
    {
        if (
            _collider == null
            || (_enemyLayerMask | _wallLayerMask) == 0
            || desiredDelta.sqrMagnitude < 0.000001f
        )
            return desiredDelta;

        Bounds bounds = _collider.bounds;
        Vector3 enemyCastCenter = bounds.center;
        Vector3 enemyCastHalfExtents = bounds.extents;
        Vector3 wallCastCenter = new(_body.position.x, bounds.center.y, _body.position.z);
        Vector3 wallCastHalfExtents = new(
            WallCollisionRadius,
            bounds.extents.y,
            WallCollisionRadius
        );
        Vector2 resolvedDelta = Vector2.zero;
        Vector2 remainingDelta = desiredDelta;

        for (int i = 0; i < MaxCollisionSlides; i++)
        {
            float distance = remainingDelta.magnitude;
            if (distance < 0.0001f)
                break;

            Vector2 direction = remainingDelta / distance;
            bool hasEnemyHit = TryGetBlockingHit(
                enemyCastCenter,
                enemyCastHalfExtents,
                direction,
                distance + CollisionSkin + EnemyStandOffGap,
                _enemyLayerMask,
                out RaycastHit enemyHit
            );
            bool hasWallHit = TryGetBlockingHit(
                wallCastCenter,
                wallCastHalfExtents,
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
            enemyCastCenter += worldTravel;
            wallCastCenter += worldTravel;

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
        Vector3 castCenter,
        Vector3 castHalfExtents,
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

        int hitCount = Physics.BoxCastNonAlloc(
            castCenter,
            castHalfExtents,
            direction.ToWorld(),
            _collisionHits,
            Quaternion.identity,
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
}
