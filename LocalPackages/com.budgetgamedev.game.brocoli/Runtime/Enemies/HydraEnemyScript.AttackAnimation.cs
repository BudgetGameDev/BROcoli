using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class HydraEnemyScript
    {
        public override void Update()
        {
            base.Update();

            UpdateAttackAnimation();

            if (player != null && !isAttacking && !isKnockedBack)
            {
                if (IsPlayerWithinColliderGap(GetAttackReach()) && Time.time >= nextMeleeAttackTime)
                {
                    StartAttackAnimation();
                }
            }
        }

        private void StartAttackAnimation()
        {
            if (player == null)
                return;

            walkAnimation?.SetAttackOverride(true);

            isAttacking = true;
            hasDamagedThisAttack = false;
            attackPhase = 1;
            attackTimer = 0f;
            nextMeleeAttackTime = Time.time + meleeAttackCooldown / Mathf.Max(0.1f, EnemyTimeScale);
            activeAttackReach = GetAttackReach();

            if (visualTransform != null)
            {
                attackStartPos = visualTransform.localPosition;
                attackStartRotation = visualTransform.localRotation;
            }

            RefreshAttackAim();
        }

        private void UpdateAttackAnimation()
        {
            if (!isAttacking)
                return;

            attackTimer += Time.deltaTime * EnemyTimeScale;

            switch (attackPhase)
            {
                case 1: // Windup
                    RefreshAttackAim();
                    float windupT = attackTimer / attackWindupDuration;
                    if (windupT >= 1f)
                    {
                        if (visualTransform != null)
                        {
                            visualTransform.localPosition = attackWindupPos;
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }
                        LockBodyForAttack();
                        attackPhase = 2;
                        attackTimer = 0f;
                    }
                    else
                    {
                        if (visualTransform != null)
                        {
                            float anticipationT = Mathf.SmoothStep(0f, 1f, windupT);
                            visualTransform.localPosition = Vector3.Lerp(
                                attackStartPos,
                                attackWindupPos,
                                anticipationT
                            );
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }

                        if (spriteRenderer != null)
                            spriteRenderer.color = Color.Lerp(
                                originalColor,
                                attackFlashColor,
                                windupT * 0.5f
                            );
                    }
                    break;

                case 2: // Strike
                    float strikeT = attackTimer / attackStrikeDuration;
                    if (strikeT >= 1f)
                    {
                        if (visualTransform != null)
                        {
                            visualTransform.localPosition = attackTargetPos;
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }
                        if (spriteRenderer != null)
                            spriteRenderer.color = attackFlashColor;

                        if (!hasDamagedThisAttack)
                        {
                            hasDamagedThisAttack = true;
                            PerformMeleeAttack();
                        }

                        attackPhase = 3;
                        attackTimer = 0f;
                    }
                    else
                    {
                        if (visualTransform != null)
                        {
                            visualTransform.localPosition = Vector3.Lerp(
                                attackWindupPos,
                                attackTargetPos,
                                EaseInCubic(strikeT)
                            );
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }

                        if (spriteRenderer != null)
                            spriteRenderer.color = attackFlashColor;
                    }
                    break;

                case 3: // Recover
                    float recoverT = attackTimer / attackRecoverDuration;
                    if (recoverT >= 1f)
                    {
                        isAttacking = false;
                        attackPhase = 0;
                        if (visualTransform != null)
                        {
                            visualTransform.localPosition = attackStartPos;
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }
                        if (spriteRenderer != null)
                            spriteRenderer.color = originalColor;

                        walkAnimation?.SetAttackOverride(false);
                        UnlockBodyAfterAttack();
                    }
                    else
                    {
                        if (visualTransform != null)
                        {
                            visualTransform.localPosition = Vector3.Lerp(
                                attackTargetPos,
                                attackStartPos,
                                EaseOutQuad(recoverT)
                            );
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }

                        if (spriteRenderer != null)
                            spriteRenderer.color = Color.Lerp(
                                attackFlashColor,
                                originalColor,
                                recoverT
                            );
                    }
                    break;
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private float EaseInCubic(float t)
        {
            return t * t * t;
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

        private void PerformMeleeAttack()
        {
            if (player == null)
                return;
            if (!IsPlayerWithinAttackContact(activeAttackReach, attackDirection))
                return;

            var playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                Vector2 knockbackDir = (
                    player.position.ToGround() - transform.position.ToGround()
                ).normalized;

                if (playerController.TakeMeleeDamage(Damage, knockbackDir))
                    PlaySuccessfulMeleeAudio();
            }
        }

        internal void PlaySuccessfulMeleeAudio()
        {
            if (meleeAudio != null)
                meleeAudio.PlayMeleeSound();
        }
    }
}
