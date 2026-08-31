using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class SprayHandVisuals
    {
        private float ResolveMovementAmount() => ResolveMovementAmount(Time.deltaTime);

        internal float ResolveMovementAmount(float deltaTime)
        {
            if (playerTransform == null || deltaTime <= 0f)
                return 0f;

            Vector2 currentPosition = playerTransform.position.ToGround();
            if (!hasPreviousPlayerPosition)
            {
                previousPlayerPosition = currentPosition;
                hasPreviousPlayerPosition = true;
                return 0f;
            }

            Vector2 movementDelta = currentPosition - previousPlayerPosition;
            float distanceMoved = movementDelta.magnitude;
            previousPlayerPosition = currentPosition;
            float referenceSpeed = Mathf.Max(
                0.1f,
                playerStats != null ? playerStats.CurrentMovementSpeed : 4f
            );
            // Scene placement and teleports are not footsteps. A normal rendered-frame
            // displacement is a small fraction of this even at boosted movement speed.
            if (distanceMoved > referenceSpeed * 0.5f)
            {
                UpdateBackpedalBlend(0f);
                return 0f;
            }

            float measuredSpeed = distanceMoved / deltaTime;
            float movementAmount = Mathf.Clamp01(measuredSpeed / referenceSpeed);
            Vector2 movementDirection = movementDelta;
            if (playerBody != null)
            {
                Vector2 groundVelocity = playerBody.linearVelocity.ToGround();
                if (groundVelocity.sqrMagnitude > 0.0001f)
                    movementDirection = groundVelocity;
            }
            UpdateBackpedalBlend(
                SprayRecoil.ResolveBackpedalAmount(
                    movementDirection,
                    CurrentDirection,
                    movementAmount
                )
            );
            return movementAmount;
        }

        private void UpdateBackpedalBlend(float target)
        {
            backpedalBlend = Mathf.MoveTowards(
                backpedalBlend,
                Mathf.Clamp01(target),
                Time.deltaTime * SpraySettings.HandBackpedalBlendSpeed
            );
        }
    }
}
