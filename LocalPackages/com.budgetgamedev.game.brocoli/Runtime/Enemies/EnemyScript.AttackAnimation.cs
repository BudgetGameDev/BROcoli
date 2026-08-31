using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class EnemyScript
    {
        public override void Update()
        {
            base.Update();

            UpdateAttackAnimation();

            // A solid body collision never deals damage. It only puts the enemy in
            // range to begin a complete, telegraphed attack animation.
            if (player != null && CanStartAttack() && IsPlayerInAttackStartRange())
            {
                StartAttackAnimation();
            }
        }

        private void StartAttackAnimation()
        {
            if (player == null)
                return;

            walkAnimation?.SetAttackOverride(true);

            isAttacking = true;
            hasDamagedThisAttack = false; // Reset damage flag for new attack
            attackPhase = 1; // Start with windup
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
                case 1: // Windup - pull back slightly and prepare
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
                        // Damage is dealt during strike phase at 60% when lunge visually connects
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

                        // Start color flash
                        if (spriteRenderer != null)
                            spriteRenderer.color = Color.Lerp(
                                originalColor,
                                attackFlashColor,
                                windupT * 0.5f
                            );
                    }
                    break;

                case 2: // Strike - damage only after the lunge reaches its contact pose
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
                            // Release across the whole distance from the deep
                            // windup pose to the forward contact pose.
                            visualTransform.localPosition = Vector3.Lerp(
                                attackWindupPos,
                                attackTargetPos,
                                EaseInCubic(strikeT)
                            );
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }

                        // Full color flash
                        if (spriteRenderer != null)
                            spriteRenderer.color = attackFlashColor;
                    }
                    break;

                case 3: // Recover - return to normal
                    float recoverT = attackTimer / attackRecoverDuration;
                    if (recoverT >= 1f)
                    {
                        // Attack finished
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
                            // Return to start position
                            visualTransform.localPosition = Vector3.Lerp(
                                attackTargetPos,
                                attackStartPos,
                                EaseOutQuad(recoverT)
                            );
                            visualTransform.localRotation = attackStartRotation;
                            visualTransform.localScale = baseLocalScale;
                        }

                        // Fade color back
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
    }
}
