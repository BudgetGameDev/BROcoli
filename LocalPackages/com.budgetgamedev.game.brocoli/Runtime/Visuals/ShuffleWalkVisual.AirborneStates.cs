using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ShuffleWalkVisual
    {
        private void UpdateAirborne(
            float dt,
            Vector2 input,
            bool wantsToMove,
            ref FrameOutput output
        )
        {
            stateTimer += dt;
            float jumpT = Mathf.Clamp01(stateTimer / currentJumpTime);
            float parabola = 4f * jumpT * (1f - jumpT);
            output.Height = currentJumpHeight * parabola;
            output.SquashStretch = AirStretch * parabola * launchInputMagnitude;
            leanMultiplier = Mathf.Sin(jumpT * Mathf.PI) * 0.6f;
            bhopTwistAngle = Mathf.Lerp(bhopTwistAngle, bhopTwistTarget, 4f * dt);

            if (wantsToMove)
                committedDirection = Vector2.Lerp(committedDirection, input.normalized, 8f * dt);

            float stumbleMultiplier = Mathf.Lerp(1f, StumbleSpeedMultiplier, stumblePenalty);
            output.Movement =
                committedDirection * currentPower * inputMagnitude * stumbleMultiplier;

            if (jumpT < 1f)
                return;

            stumblePenalty = 0f;
            landingQuality = Random.Range(0.5f, 1f);
            currentBounceTime = Mathf.Lerp(0.1f, 0.05f, landingQuality);
            stateTimer = 0f;

            if (wantsToMove)
            {
                State = HopState.BhopBounce;
            }
            else
            {
                State = HopState.Stopping;
                stoppingVelocity = committedDirection * currentPower * launchInputMagnitude;
            }
        }

        private void UpdateBhopBounce(
            float dt,
            Vector2 input,
            bool wantsToMove,
            ref FrameOutput output
        )
        {
            stateTimer += dt;
            float bounceT = Mathf.Clamp01(stateTimer / currentBounceTime);
            float bounceDown = Mathf.Sin(bounceT * Mathf.PI);
            float dipAmount = Mathf.Lerp(0.8f, 0.3f, landingQuality);
            output.Height = -MaxChargeDip * dipAmount * bounceDown;
            output.SquashStretch = -LandSquash * dipAmount * bounceDown;
            float leanBackAmount = Mathf.Lerp(0.3f, 0.1f, landingQuality);
            leanMultiplier = -leanBackAmount * bounceDown;

            float stumbleMultiplier = Mathf.Lerp(1f, StumbleSpeedMultiplier, stumblePenalty);
            output.Movement =
                committedDirection * currentPower * inputMagnitude * stumbleMultiplier;

            if (wantsToMove)
            {
                currentPower = Mathf.MoveTowards(
                    currentPower,
                    MaxJumpPower,
                    (MaxJumpPower - MinJumpPower) * 2f * dt
                );
                committedDirection = Vector2.Lerp(committedDirection, input.normalized, 15f * dt);
            }

            if (bounceT < 1f)
                return;

            stateTimer = 0f;
            if (!wantsToMove)
            {
                State = HopState.Stopping;
                stoppingVelocity = committedDirection * currentPower * launchInputMagnitude;
                return;
            }

            float minHeight = GetScaledMinJumpHeight();
            float maxHeight = GetScaledMaxJumpHeight();
            float baseJumpTime = GetScaledJumpTime();
            launchInputMagnitude = inputMagnitude;
            currentJumpHeight = Mathf.Lerp(minHeight, maxHeight, currentPower);
            currentJumpHeight *= landingQuality * Random.Range(0.9f, 1.1f);
            currentJumpTime = baseJumpTime * Mathf.Lerp(0.8f, 1.1f, landingQuality);
            bhopTwistTarget = Random.Range(-BhopTwistMax, BhopTwistMax);
            State = HopState.Airborne;
        }
    }
}
