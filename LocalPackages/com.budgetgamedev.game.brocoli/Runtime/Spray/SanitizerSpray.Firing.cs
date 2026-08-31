using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class SanitizerSpray
    {
        /// <summary>
        /// Handle pending spray: validate target and fire when ready.
        /// </summary>
        private void HandlePendingSpray()
        {
            float waitTime = Time.time - aimStartTime;

            // Cancel if target died/disabled or out of range
            if (handVisuals != null && (!handVisuals.HasTarget || !handVisuals.IsTargetInRange))
            {
                CancelPendingSpray();
                return;
            }

            bool minTimePassed = waitTime >= SpraySettings.AimDelayBeforeSpray;
            bool aimed = handVisuals?.IsAimedAtTarget ?? true;
            bool tookTooLong = waitTime >= SpraySettings.MaxAimTime;

            // Fire when hand is aimed at target (or timeout) - range already validated
            if (minTimePassed && (aimed || tookTooLong))
            {
                ExecutePendingSpray();
            }
        }

        void OnParticleTrigger()
        {
            damageHandler?.ProcessParticleTrigger(sprayParticles);
        }

        public void StartSpray(Vector2 direction)
        {
            // For continuous spray, we'd need a different approach
            // This is mainly used for burst mode now
            if (!isSpraying)
            {
                isSpraying = true;
                damageHandler?.ResetDamageTick();
                particleController?.Play();
                sprayAudio?.StartSpray();
                handVisuals?.TriggerRecoil();
                handVisuals?.SetVisible(true);
            }
            // Particle position updated in Update() via UpdateParticlePosition()
        }

        public void StopSpray()
        {
            if (isSpraying)
            {
                isSpraying = false;
                particleController?.Stop();
                sprayAudio?.StopSpray();
                damageHandler?.ResolveConeKnockback();
                damageHandler?.ClearHits();
                Invoke(nameof(HideHand), 0.2f);
            }
        }

        /// <summary>
        /// Fire a spray burst at a specific target. Hand will track and aim.
        /// </summary>
        public bool FireSprayBurstAtTarget(Transform target)
        {
            if (target == null)
                return false;
            if (Time.time < lastBurstTime + SpraySettings.BurstCooldown)
                return false;
            if (Time.time < currentBurstEndTime)
                return false;
            if (hasPendingSpray)
                return false;

            // Range check before starting to aim - use collider bounds center for consistency
            if (playerTransform != null)
            {
                Collider col = target.GetComponent<Collider>();
                Vector2 targetPos =
                    (col != null && col.enabled)
                        ? col.bounds.center.ToGround()
                        : target.position.ToGround();
                float dist = Vector2.Distance(playerTransform.position.ToGround(), targetPos);
                if (dist > currentRange || dist < SpraySettings.MinTargetDistance)
                    return false;
            }

            // Tell hand to track this target - it does ALL the aiming
            handVisuals?.SetTarget(target);

            aimStartTime = Time.time;
            hasPendingSpray = true;
            handVisuals?.SetVisible(true);

            return true;
        }

        /// <summary>
        /// Legacy direction-based burst - fires immediately in hand's current direction.
        /// </summary>
        public bool FireSprayBurst(Vector2 direction, float duration = 0.25f)
        {
            if (Time.time < lastBurstTime + SpraySettings.BurstCooldown)
                return false;
            if (Time.time < currentBurstEndTime)
                return false;
            if (hasPendingSpray)
                return false;

            // No target tracking - fire immediately
            aimStartTime = Time.time;
            hasPendingSpray = true;
            handVisuals?.SetVisible(true);

            return true;
        }

        /// <summary>Cancel a pending spray without firing.</summary>
        private void CancelPendingSpray()
        {
            hasPendingSpray = false;
            handVisuals?.ClearTarget();

            if (!SpraySettings.ShowHandAlways)
                Invoke(nameof(HideHand), 0.1f);
        }

        private void ExecutePendingSpray()
        {
            if (!hasPendingSpray)
                return;

            hasPendingSpray = false;
            lastBurstTime = Time.time;
            currentBurstEndTime = Time.time + SpraySettings.BurstDuration;
            isInBurst = true;

            Vector2 direction = handVisuals?.CurrentDirection ?? Vector2.right;
            Vector3 nozzle = handVisuals?.GetNozzleWorldPosition() ?? transform.position;
            particleController?.SetSprayDirectionAndPosition(
                direction,
                nozzle,
                currentRange,
                currentWidth
            );
            particleController?.PlayBurst();
            sprayAudio?.PlaySprayBurst();
            handVisuals?.TriggerRecoil();

            if (!SpraySettings.ShowHandAlways)
                Invoke(nameof(HideHand), SpraySettings.BurstDuration + 0.1f);

            damageHandler?.ResetDamageTick();
        }

        private void HideHand()
        {
            if (!SpraySettings.ShowHandAlways && !isSpraying && !isInBurst && !hasPendingSpray)
                handVisuals?.SetVisible(false);
        }
    }
}
