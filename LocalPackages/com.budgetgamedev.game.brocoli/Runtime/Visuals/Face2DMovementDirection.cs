using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Yaws the pivot around the vertical axis so the model faces the movement direction.
    /// Attach to a parent pivot (e.g., YawPivot) above the model.
    /// For a model pointing "down" at rest, movement down = 0°, right = 90°, up = 180°, left = -90°.
    /// </summary>
    public class Face2DMovementDirection : MonoBehaviour
    {
        public PlayerController controller;
        public ShuffleWalkVisual hopVisual;

        [Header("Tuning")]
        private float smoothTime = 0.08f;
        private float deadZone = 0.05f;

        [Header("Offsets")]
        [Tooltip("Add degrees to align model's 'forward' with down direction")]
        private float angleOffsetDegrees = 0f;

        Vector2 lastDir = Vector2.down;
        float angularVel;
        float idleSwayVel;
        float currentIdleSway;
        float bhopTwistVel;
        float currentBhopTwist;

        void LateUpdate()
        {
            if (!controller)
                return;

            // Get direction from raw input for responsive facing
            Vector2 dir = controller.RawInput;

            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            if (dir.sqrMagnitude >= deadZone * deadZone)
                lastDir = dir.normalized;

            // Calculate facing angle
            float targetAngle =
                Mathf.Atan2(lastDir.x, -lastDir.y) * Mathf.Rad2Deg + angleOffsetDegrees;

            float currentAngle = GroundPlane.YawDegrees(transform);

            // Get idle sway from hop visual
            float idleSway = 0f;
            float bhopTwist = 0f;
            if (hopVisual != null)
            {
                if (hopVisual.State == ShuffleWalkVisual.HopState.Idle)
                {
                    idleSway = hopVisual.IdleLeanAngle;
                }
                else if (
                    hopVisual.State == ShuffleWalkVisual.HopState.Airborne
                    || hopVisual.State == ShuffleWalkVisual.HopState.BhopBounce
                )
                {
                    bhopTwist = hopVisual.BhopTwistAngle;
                }
            }

            // Smooth the idle sway separately
            currentIdleSway = Mathf.SmoothDamp(currentIdleSway, idleSway, ref idleSwayVel, 0.15f);
            currentBhopTwist = Mathf.SmoothDamp(
                currentBhopTwist,
                bhopTwist,
                ref bhopTwistVel,
                0.1f
            );

            float newAngle = Mathf.SmoothDampAngle(
                currentAngle,
                targetAngle + currentIdleSway + currentBhopTwist,
                ref angularVel,
                smoothTime
            );

            // Yaw toward the facing direction + idle sway + bhop twist
            transform.localRotation = GroundPlane.YawRotation(newAngle);
        }
    }
}
