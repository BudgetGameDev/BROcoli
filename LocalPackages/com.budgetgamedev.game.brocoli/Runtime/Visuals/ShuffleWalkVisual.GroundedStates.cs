using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ShuffleWalkVisual
    {
        private void UpdateIdle(float dt, Vector2 input, bool wantsToMove, ref FrameOutput output)
        {
            idleSwayTimer -= dt;
            if (idleSwayTimer <= 0f)
            {
                idleSwayTarget = Random.Range(-IdleSwayMaxAngle, IdleSwayMaxAngle);
                idleSwayTimer = Random.Range(1.2f, 2.5f);
            }

            idleSwayAngle = Mathf.Lerp(idleSwayAngle, idleSwayTarget, IdleSwaySpeed * dt);
            leanMultiplier = 0f;
            float breathPhase = idleTime * IdleBreathSpeed * Mathf.PI * 2f;
            float breath = (Mathf.Sin(breathPhase) + 1f) * 0.5f;
            output.SquashStretch = breath * 0.015f;

            if (wantsToMove)
            {
                State = HopState.Charging;
                stateTimer = 0f;
                currentPower = 0f;
                committedDirection = input.normalized;
                releasedDuringCharge = false;
                idleSwayAngle = 0f;
            }
        }

        private void UpdateCharging(
            float dt,
            Vector2 input,
            bool wantsToMove,
            ref FrameOutput output
        )
        {
            stateTimer += dt;
            float chargeT = Mathf.Clamp01(stateTimer / MaxChargeTime);
            currentPower = Mathf.Lerp(MinJumpPower, MaxJumpPower, chargeT);
            output.Height = -Mathf.Lerp(MinChargeDip, MaxChargeDip, chargeT);
            output.SquashStretch = -ChargeSquash * chargeT;
            leanMultiplier = -chargeT * 0.3f;

            if (wantsToMove)
                committedDirection = input.normalized;
            else
                releasedDuringCharge = true;

            bool maxCharged = chargeT >= 1f;
            bool minChargeReached = stateTimer >= MinChargeTime;
            if (maxCharged || (minChargeReached && releasedDuringCharge))
                LaunchJump();
        }

        private void UpdateLanding(
            float dt,
            Vector2 input,
            bool wantsToMove,
            ref FrameOutput output
        )
        {
            stateTimer += dt;
            float landT = Mathf.Clamp01(stateTimer / 0.1f);
            float landSquash = Mathf.Sin(landT * Mathf.PI);
            output.Height = -MinChargeDip * landSquash;
            output.SquashStretch = -LandSquash * 0.5f * landSquash;
            leanMultiplier = 0f;

            if (landT < 1f)
                return;

            currentPower = 0f;
            if (wantsToMove)
            {
                State = HopState.Charging;
                stateTimer = 0f;
                committedDirection = input.normalized;
                releasedDuringCharge = false;
            }
            else
            {
                State = HopState.Idle;
                stateTimer = 0f;
                idleSwayTimer = 0f;
                idleSwayAngle = 0f;
            }
        }
    }
}
