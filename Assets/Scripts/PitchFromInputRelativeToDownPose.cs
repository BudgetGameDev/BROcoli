using UnityEngine;

/// <summary>
/// Tilts the model forward/backward based on movement.
/// Attach to a pitch pivot that is a child of the yaw/facing pivot.
/// Tilts around the X-axis relative to the facing direction.
/// </summary>
public class PitchFromInputRelativeToDownPose : MonoBehaviour
{
    public PlayerController controller;
    public ShuffleWalkVisual hopVisual;

    [Header("Pitch setup")]
    [Tooltip("Neutral X rotation when not moving")]
    private float neutralPitch = 0f;
    [Tooltip("Max tilt angle when moving toward movement direction")]
    private float tiltAmount = 20f;
    private float smooth = 12f;

    float currentPitch;

    void LateUpdate()
    {
        if (!controller) return;

        // Get movement magnitude from hop visual if available
        float forwardAmount = 0f;
        float leanMult = 1f;
        if (hopVisual != null)
        {
            forwardAmount = hopVisual.MovementDirection.magnitude;
            leanMult = hopVisual.LeanMultiplier;
        }
        else
        {
            Vector2 dir = controller.movement;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            forwardAmount = dir.magnitude;
        }

        // Tilt forward (negative X) when moving, modulated by lean multiplier
        // leanMult: 0 at takeoff/landing, 1 at peak of jump, -0.5 during ground contact
        float desiredPitch = neutralPitch - forwardAmount * tiltAmount * leanMult;

        currentPitch = Mathf.Lerp(currentPitch, desiredPitch, smooth * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }
}
