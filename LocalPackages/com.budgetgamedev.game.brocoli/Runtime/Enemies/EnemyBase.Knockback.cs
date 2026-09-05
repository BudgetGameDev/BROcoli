using System;
using System.Collections;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public abstract partial class EnemyBase
    {
        /// <summary>Completely pins the physics body while a visual melee animation plays.</summary>
        protected void LockBodyForAttack()
        {
            if (rb == null || attackBodyLocked)
                return;

            constraintsBeforeAttack = rb.constraints;
            attackBodyLocked = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        protected void UnlockBodyAfterAttack(bool releaseQueuedKnockback = true)
        {
            if (rb == null || !attackBodyLocked)
                return;

            rb.constraints = constraintsBeforeAttack;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            attackBodyLocked = false;

            if (releaseQueuedKnockback && hasQueuedKnockback && isActiveAndEnabled)
            {
                Vector2 direction = queuedKnockbackDirection;
                float force = queuedKnockbackForce;
                ClearQueuedKnockback();
                StartKnockback(direction, force);
            }
            else
            {
                ClearQueuedKnockback();
            }
        }

        protected virtual void OnDisable()
        {
            UnlockBodyAfterAttack(false);
            DiabloHud.NotifyEnemyUnavailable(this);

            if (!gameObject.scene.isLoaded)
                return;

            EnemySpatialHash.Instance?.Unregister(this);
        }

        [Header("Knockback")]
        [Tooltip("Lowest force before the weapon-specific multiplier is applied.")]
        [SerializeField]
        protected float minimumEnemyKnockbackForce = 0.25f;

        [Tooltip("Force reached by a hit dealing this enemy's full knockback damage threshold.")]
        [SerializeField]
        protected float enemyKnockbackForce = 2.75f;

        [Tooltip("Hits below this fraction of Max Health deal damage but cannot cause knockback.")]
        [SerializeField, Range(0f, 1f)]
        protected float minimumDamageFractionForKnockback = 0.1f;

        [Tooltip("A hit dealing this fraction of Max Health always receives maximum knockback.")]
        [SerializeField, Range(0.01f, 1f)]
        protected float damageFractionForMaxKnockback = 0.99f;

        [Tooltip(
            "Chance for any damaging hit to roll the maximum force, including very small hits."
        )]
        [SerializeField, Range(0f, 0.2f)]
        protected float rareMaximumKnockbackChance = 0.005f;

        [Tooltip("Higher values make lucky high-knockback rolls less common.")]
        [SerializeField, Range(1f, 8f)]
        protected float knockbackRandomBias = 4f;

        [SerializeField]
        protected float enemyKnockbackDuration = 0.14f;

        [SerializeField]
        protected float enemyKnockbackCooldown = 0.24f;
        protected float knockbackTimer = 0f;
        protected bool isKnockedBack = false;
        private float nextKnockbackTime = 0f;
        private float activeKnockbackForce = 0f;
        private Vector2 activeKnockbackDirection = Vector2.zero;
        private float activeDamageKnockbackRoll = -1f;
        private float activeDamageKnockbackMultiplier = 1f;

        public void TakeDamage(float damage)
        {
            TakeDamage(damage, Vector2.zero);
        }

        public void TakeDamage(float damage, Vector2 knockbackDirection)
        {
            TakeDamage(damage, knockbackDirection, 1f);
        }

        /// <summary>Applies damage and damage-relative knockback.</summary>
        public void TakeDamage(
            float damage,
            Vector2 knockbackDirection,
            float weaponKnockbackMultiplier
        )
        {
            if (isDying)
                return;

            float appliedDamage = Mathf.Max(0f, damage);
            Health = Mathf.Max(0f, Health - appliedDamage);
            DiabloHud.ReportEnemyHealth(this);
            if (Health <= 0f)
            {
                if (isElite)
                {
                    OnEliteDeath?.Invoke(transform.position);
                }

                Die();
                return;
            }

            if (appliedDamage > 0f)
                AggroFromDamage();

            if (knockbackDirection != Vector2.zero && rb != null)
            {
                TryApplyDamageKnockback(
                    appliedDamage,
                    knockbackDirection,
                    weaponKnockbackMultiplier
                );
            }

            StartCoroutine(HitFlash());
        }

        public void ApplyKnockback(Vector2 direction)
        {
            ApplyKnockback(direction, enemyKnockbackForce);
        }

        /// <summary>Evaluates knockback using a separately accumulated damage amount.</summary>
        public bool TryApplyDamageKnockback(
            float accumulatedDamage,
            Vector2 knockbackDirection,
            float weaponKnockbackMultiplier = 1f
        )
        {
            if (rb == null || knockbackDirection == Vector2.zero)
                return false;

            float roll = UnityEngine.Random.value;
            float safeMultiplier = Mathf.Max(0f, weaponKnockbackMultiplier);
            float force =
                CalculateDamageKnockbackForce(Mathf.Max(0f, accumulatedDamage), roll)
                * safeMultiplier;
            if (force <= 0f)
                return false;

            PrepareForIncomingKnockback();
            if (!ApplyKnockbackInternal(knockbackDirection.normalized, force))
                return false;

            // Cone damage can continue arriving after recoil begins. Remember this
            // one roll so later cone ticks strengthen the same motion without
            // repeatedly rerolling luck or creating a second delayed knockback.
            activeDamageKnockbackRoll = roll;
            activeDamageKnockbackMultiplier = safeMultiplier;
            return true;
        }

        /// <summary>Strengthens running knockback as more damage from the same cone arrives.</summary>
        public void StrengthenActiveDamageKnockback(
            float accumulatedDamage,
            Vector2 knockbackDirection,
            float weaponKnockbackMultiplier = 1f
        )
        {
            if (!isKnockedBack || activeDamageKnockbackRoll < 0f || rb == null)
                return;

            float safeMultiplier = Mathf.Max(0f, weaponKnockbackMultiplier);
            if (!Mathf.Approximately(safeMultiplier, activeDamageKnockbackMultiplier))
                return;

            float force =
                CalculateDamageKnockbackForce(
                    Mathf.Max(0f, accumulatedDamage),
                    activeDamageKnockbackRoll
                ) * safeMultiplier;
            force = Mathf.Clamp(force, 0f, MaxSafeKnockbackForce);

            if (force > activeKnockbackForce)
                activeKnockbackForce = force;
            if (knockbackDirection.sqrMagnitude > 0.0001f)
                activeKnockbackDirection = knockbackDirection.normalized;
        }

        /// <summary>Lets melee enemies cancel a pinned attack before recoil begins.</summary>
        protected virtual void PrepareForIncomingKnockback() { }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            activeDamageKnockbackRoll = -1f;
            activeDamageKnockbackMultiplier = 1f;
            ApplyKnockbackInternal(direction, force);
        }

        private bool ApplyKnockbackInternal(Vector2 direction, float force)
        {
            if (rb == null || direction == Vector2.zero || force <= 0f)
                return false;
            if (Time.time < nextKnockbackTime)
                return false;

            nextKnockbackTime = Time.time + enemyKnockbackCooldown;
            force = Mathf.Clamp(force, 0f, MaxSafeKnockbackForce);

            // Melee attacks pin the body to keep collision contacts stable. Save
            // the strongest hit and release it as soon as that animation unlocks.
            if (attackBodyLocked)
            {
                if (!hasQueuedKnockback || force > queuedKnockbackForce)
                {
                    hasQueuedKnockback = true;
                    queuedKnockbackDirection = direction.normalized;
                    queuedKnockbackForce = force;
                }
                return true;
            }

            StartKnockback(direction, force);
            return true;
        }

        private void StartKnockback(Vector2 direction, float force)
        {
            activeKnockbackDirection = direction.normalized;
            isKnockedBack = true;
            knockbackTimer = enemyKnockbackDuration;
            activeKnockbackForce = force;
            rb.SetGroundVelocity(activeKnockbackDirection * force);
        }

        private float CalculateDamageKnockbackForce(float damage, float roll)
        {
            float maxHealth = Mathf.Max(0.0001f, MaxHealth);
            float relativeDamage = damage / maxHealth;

            // Damage below the threshold is still applied normally, but it never
            // enters the random roll. In particular, zero damage cannot recoil.
            if (relativeDamage < minimumDamageFractionForKnockback)
                return 0f;

            float damageScalar = Mathf.Clamp01(
                relativeDamage / Mathf.Max(0.01f, damageFractionForMaxKnockback)
            );

            if (damageScalar >= 1f)
                return enemyKnockbackForce;

            if (roll >= 1f - rareMaximumKnockbackChance)
                return enemyKnockbackForce;

            float ordinaryRollRange = Mathf.Max(0.0001f, 1f - rareMaximumKnockbackChance);
            float biasedLuck = Mathf.Pow(
                Mathf.Clamp01(roll / ordinaryRollRange),
                knockbackRandomBias
            );

            // Relative damage raises the guaranteed floor. Luck then chooses a
            // point between that floor and maximum, heavily biased toward the floor.
            float forceScalar = Mathf.Lerp(damageScalar, 1f, biasedLuck);
            return Mathf.Lerp(minimumEnemyKnockbackForce, enemyKnockbackForce, forceScalar);
        }

        private void ClearQueuedKnockback()
        {
            hasQueuedKnockback = false;
            queuedKnockbackDirection = Vector2.zero;
            queuedKnockbackForce = 0f;
        }
    }
}
