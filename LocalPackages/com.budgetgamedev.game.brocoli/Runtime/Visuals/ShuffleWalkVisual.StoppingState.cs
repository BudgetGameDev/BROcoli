using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ShuffleWalkVisual
    {
        private void UpdateStopping(
            float dt,
            Vector2 input,
            bool wantsToMove,
            ref FrameOutput output
        )
        {
            stateTimer += dt;
            float stopT = Mathf.Clamp01(stateTimer / StoppingTime);
            float leanIntensity = stoppingVelocity.magnitude;

            if (stopT < 0.35f)
            {
                float progress = stopT / 0.35f;
                float leanForward = Mathf.Sin(progress * Mathf.PI * 0.5f);
                output.Height = -MaxChargeDip * leanForward * leanIntensity;
                output.SquashStretch = -0.18f * leanForward * leanIntensity;
                output.Movement = stoppingVelocity * (1f - progress * 0.7f);
                leanMultiplier = leanForward * leanIntensity;
            }
            else if (stopT < 0.7f)
            {
                float progress = (stopT - 0.35f) / 0.35f;
                float leanBack = Mathf.Sin(progress * Mathf.PI);
                output.Height = MinChargeDip * leanBack * leanIntensity * 0.5f;
                output.SquashStretch = 0.08f * leanBack * leanIntensity;
                output.Movement = stoppingVelocity * 0.3f * (1f - progress);
                leanMultiplier = -leanBack * leanIntensity * 0.5f;
            }
            else
            {
                float progress = (stopT - 0.7f) / 0.3f;
                output.Height = Mathf.Lerp(MinChargeDip * 0.2f, 0f, progress);
                output.SquashStretch = Mathf.Lerp(0.02f, 0f, progress);
                leanMultiplier = Mathf.Lerp(-0.15f, 0f, progress);
            }

            if (stopT >= 1f)
            {
                CompleteStop(input, wantsToMove);
            }
            else if (wantsToMove)
            {
                State = HopState.Charging;
                stateTimer = 0f;
                currentPower = 0f;
                committedDirection = input.normalized;
                releasedDuringCharge = false;
            }
        }

        private void CompleteStop(Vector2 input, bool wantsToMove)
        {
            currentPower = 0f;
            stateTimer = 0f;
            if (wantsToMove)
            {
                State = HopState.Charging;
                committedDirection = input.normalized;
                releasedDuringCharge = false;
            }
            else
            {
                State = HopState.Idle;
                idleSwayTimer = 0f;
                idleSwayAngle = 0f;
            }
        }
    }
}
