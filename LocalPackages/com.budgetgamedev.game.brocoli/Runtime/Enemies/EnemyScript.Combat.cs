using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class EnemyScript
    {
        private void PerformMeleeAttack()
        {
            // The player may have moved away or around the enemy during windup.
            // In that case the completed animation is a miss and causes no damage.
            if (!IsPlayerWithinAttackContact(activeAttackReach, attackDirection))
                return;

            // Deal damage to player - only proceed if damage was actually dealt
            var playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // Calculate knockback direction (away from enemy)
                Vector2 knockbackDir = (
                    player.position.ToGround() - transform.position.ToGround()
                ).normalized;

                if (playerController.TakeMeleeDamage(Damage, knockbackDir))
                {
                    // Play melee sound
                    if (meleeAudio != null)
                    {
                        meleeAudio.PlayMeleeSound();
                    }
                }
            }
        }

        /// <summary>
        /// Check if enemy can start a new attack (not attacking, cooldown expired)
        /// </summary>
        private bool CanStartAttack()
        {
            // An enemy walking back to its room has given up on this player; brushing
            // past it on the way should not cost a hit.
            return IsPursuing && !isAttacking && !isKnockedBack && Time.time >= nextMeleeAttackTime;
        }

        private bool IsPlayerInAttackStartRange()
        {
            // Collider gap works at every enemy scale. A centre-distance check does
            // not: a smaller enemy could begin an attack that cannot visually reach.
            return IsPlayerWithinColliderGap(GetAttackReach());
        }

        private float GetAttackReach()
        {
            float visualReach = Mathf.Clamp(attackLungeDistance, 0f, MaxAttackLungeDistance);
            return Mathf.Min(Mathf.Max(0f, meleeRange), visualReach);
        }

        protected override void PrepareForIncomingKnockback()
        {
            if (!isAttacking)
                return;

            // A real weapon hit interrupts the telegraphed attack. Restore the
            // visual first, then release the pinned body so recoil is immediate.
            isAttacking = false;
            hasDamagedThisAttack = false;
            attackPhase = 0;
            attackTimer = 0f;

            if (visualTransform != null)
            {
                visualTransform.localPosition = attackStartPos;
                visualTransform.localRotation = attackStartRotation;
                visualTransform.localScale = baseLocalScale;
            }
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;

            walkAnimation?.SetAttackOverride(false);
            UnlockBodyAfterAttack(false);
        }

        /// <summary>
        /// Reset enemy state for reuse from pool.
        /// </summary>
        public override void ResetForPool()
        {
            base.ResetForPool();

            // Reset attack animation state
            isAttacking = false;
            hasDamagedThisAttack = false;
            attackPhase = 0;
            attackTimer = 0f;
            nextMeleeAttackTime = 0f;
            attackDirection = Vector2.zero;
            activeAttackReach = 0f;
            walkAnimation?.SetAttackOverride(false);

            // Reset visual state - critical for fixing invisible enemies!
            if (visualTransform != null)
            {
                // Don't reset localPosition - preserve prefab's Z offset for 3D models

                // Safety: ensure we never set zero scale
                if (baseLocalScale.sqrMagnitude < 0.0001f)
                {
                    baseLocalScale = Vector3.one;
                    Debug.LogWarning(
                        $"[EnemyScript.ResetForPool] {name}: baseLocalScale was zero, using Vector3.one"
                    );
                }
                visualTransform.localScale = baseLocalScale;
            }

            // Reset sprite color to original (fixes enemy stuck in attack flash color)
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true; // Ensure sprite is enabled
                spriteRenderer.color = originalColor;
            }
        }
    }
}
