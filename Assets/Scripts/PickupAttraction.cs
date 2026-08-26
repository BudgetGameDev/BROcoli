using UnityEngine;

/// <summary>
/// Shared movement for the always-on nearby pickup pull and the temporary
/// map-wide magnet. Nearby pickups latch once the player enters the radius so
/// the pull cannot flicker if the player changes direction.
/// </summary>
public static class PickupAttraction
{
    public const float LocalRadius = 1.6f;

    private const float LocalMinimumSpeed = 4.5f;
    private const float LocalMaximumSpeed = 15f;
    private const float LocalAcceleration = 38f;
    private const float GlobalMinimumSpeed = 18f;
    private const float GlobalMaximumSpeed = 60f;
    private const float GlobalAcceleration = 120f;

    public static void Reset(
        Rigidbody2D body,
        ref float currentSpeed,
        ref bool localAttractionLocked,
        PickupVisual3D visual
    )
    {
        currentSpeed = 0f;
        localAttractionLocked = false;
        visual?.ResetAttraction();
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    public static void UpdateMotion(
        Rigidbody2D body,
        ref float currentSpeed,
        ref bool localAttractionLocked,
        PickupVisual3D visual
    )
    {
        if (body == null)
            return;

        Transform target = PlayerStats.ActiveMagnetTarget;
        bool globalAttraction = target != null;

        if (!globalAttraction)
        {
            target = PlayerStats.ActivePlayerTarget;
            if (target != null && !localAttractionLocked)
            {
                Vector2 toPlayer = (Vector2)target.position - body.position;
                localAttractionLocked = toPlayer.sqrMagnitude <= LocalRadius * LocalRadius;
            }

            if (!localAttractionLocked)
                target = null;
        }

        bool isAttracted = target != null;
        visual?.SetAttracted(isAttracted);
        if (!isAttracted)
        {
            SlowToRest(body, ref currentSpeed);
            return;
        }

        Vector2 offset = (Vector2)target.position - body.position;
        float distance = offset.magnitude;
        if (distance <= 0.001f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        float targetSpeed;
        float acceleration;
        if (globalAttraction)
        {
            targetSpeed = Mathf.Clamp(distance * 4f, GlobalMinimumSpeed, GlobalMaximumSpeed);
            acceleration = GlobalAcceleration;
        }
        else
        {
            float closeness = 1f - Mathf.Clamp01(distance / LocalRadius);
            targetSpeed = Mathf.Lerp(LocalMinimumSpeed, LocalMaximumSpeed, closeness);
            acceleration = LocalAcceleration;
        }

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            acceleration * Time.fixedDeltaTime
        );
        float arrivalSpeed = distance * 0.75f / Mathf.Max(Time.fixedDeltaTime, 0.001f);
        body.WakeUp();
        body.linearVelocity = offset / distance * Mathf.Min(currentSpeed, arrivalSpeed);
    }

    private static void SlowToRest(Rigidbody2D body, ref float currentSpeed)
    {
        if (currentSpeed <= 0f)
            return;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            0f,
            GlobalAcceleration * Time.fixedDeltaTime
        );
        if (currentSpeed <= 0.1f)
        {
            body.linearVelocity = Vector2.zero;
            currentSpeed = 0f;
        }
    }
}
