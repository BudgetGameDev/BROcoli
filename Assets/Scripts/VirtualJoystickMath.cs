using UnityEngine;

/// <summary>Pure response math shared by the runtime joystick and its tests.</summary>
public static class VirtualJoystickMath
{
    public static Vector2 AnalogInput(
        Vector2 displacement,
        float range,
        float deadZone,
        float responseExponent
    )
    {
        if (range <= Mathf.Epsilon)
            return Vector2.zero;

        Vector2 normalized = Vector2.ClampMagnitude(displacement / range, 1f);
        float magnitude = normalized.magnitude;
        float clampedDeadZone = Mathf.Clamp01(deadZone);
        if (magnitude <= clampedDeadZone)
            return Vector2.zero;

        float remappedMagnitude = Mathf.InverseLerp(clampedDeadZone, 1f, magnitude);
        float analogMagnitude = Mathf.Pow(remappedMagnitude, Mathf.Max(0.1f, responseExponent));
        return normalized.normalized * analogMagnitude;
    }
}
