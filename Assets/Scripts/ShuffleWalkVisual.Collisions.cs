using UnityEngine;

public partial class ShuffleWalkVisual
{
    private float ClampHopOffsetAgainstWalls(float desiredOffset)
    {
        float distance = Mathf.Abs(desiredOffset);
        if (distance <= Mathf.Epsilon || playerCollider == null || wallLayerMask == 0)
            return desiredOffset;

        Bounds playerBounds = playerCollider.bounds;
        Vector3 castCenter = new(
            playerCollider.transform.position.x,
            playerBounds.center.y,
            playerCollider.transform.position.z
        );
        Vector3 castHalfExtents = new(
            PlayerMovement.WallCollisionRadius,
            playerBounds.extents.y,
            PlayerMovement.WallCollisionRadius
        );
        Vector3 direction = desiredOffset > 0f ? Vector3.forward : Vector3.back;
        int hitCount = Physics.BoxCastNonAlloc(
            castCenter,
            castHalfExtents,
            direction,
            wallHits,
            Quaternion.identity,
            distance + WallVisualSkin,
            wallLayerMask,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = wallHits[i];
            if (hit.collider != null && hit.collider != playerCollider)
                closestDistance = Mathf.Min(closestDistance, hit.distance);
        }

        if (float.IsPositiveInfinity(closestDistance))
            return desiredOffset;

        float allowedDistance = Mathf.Clamp(closestDistance - WallVisualSkin, 0f, distance);
        return Mathf.Sign(desiredOffset) * allowedDistance;
    }

    private float GetWallPoseFactor(Vector2 poseDirection)
    {
        if (
            playerCollider == null
            || wallLayerMask == 0
            || poseDirection.sqrMagnitude <= DeadZone * DeadZone
        )
            return 1f;

        Bounds playerBounds = playerCollider.bounds;
        Vector3 castCenter = new(
            playerCollider.transform.position.x,
            playerBounds.center.y,
            playerCollider.transform.position.z
        );
        Vector3 castHalfExtents = new(
            PlayerMovement.WallCollisionRadius,
            playerBounds.extents.y,
            PlayerMovement.WallCollisionRadius
        );
        int hitCount = Physics.BoxCastNonAlloc(
            castCenter,
            castHalfExtents,
            poseDirection.normalized.ToWorld(),
            wallHits,
            Quaternion.identity,
            WallAnimationClearance + WallVisualSkin,
            wallLayerMask,
            QueryTriggerInteraction.Ignore
        );
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = wallHits[i];
            if (hit.collider != null && hit.collider != playerCollider)
                closestDistance = Mathf.Min(closestDistance, hit.distance);
        }

        if (float.IsPositiveInfinity(closestDistance))
            return 1f;

        return Mathf.Clamp01((closestDistance - WallVisualSkin) / WallAnimationClearance);
    }

    /// <summary>
    /// Apply stumble penalty after being hit. Reduces speed until next landing.
    /// </summary>
    /// <param name="intensity">Hit intensity from 0 (light) to 1 (heavy).</param>
    public void ApplyStumble(float intensity)
    {
        // Add to stumble, capped at 1
        stumblePenalty = Mathf.Clamp01(stumblePenalty + intensity * 0.7f);
    }

    private void LaunchJump()
    {
        State = HopState.Airborne;
        stateTimer = 0f;

        // Lock input magnitude at launch - no changes during flight
        launchInputMagnitude = Mathf.Max(inputMagnitude, 0.5f); // Minimum 50% power on tap

        // Get speed-scaled animation parameters
        float minHeight = GetScaledMinJumpHeight();
        float maxHeight = GetScaledMaxJumpHeight();
        float baseJumpTime = GetScaledJumpTime();

        // Scale jump height and power by input magnitude (how far stick is pushed)
        float scaledPower = currentPower * launchInputMagnitude;
        currentJumpHeight = Mathf.Lerp(
            minHeight,
            maxHeight,
            (scaledPower - MinJumpPower) / (MaxJumpPower - MinJumpPower)
        );
        currentJumpHeight *= launchInputMagnitude; // Further scale height
        currentJumpTime = Mathf.Lerp(baseJumpTime * 0.6f, baseJumpTime, launchInputMagnitude); // Shorter hops when input is low
        bhopTwistTarget = Random.Range(-BhopTwistMax, BhopTwistMax);
    }
}
