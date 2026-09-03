using System;
using System.Collections;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public abstract partial class EnemyBase
    {
        /// <summary>Marks this enemy as pooled.</summary>
        public void SetPooled(bool pooled)
        {
            _isPooled = pooled;
        }

        /// <summary>Resets enemy state for reuse from the pool.</summary>
        public virtual void ResetForPool()
        {
            StopAllCoroutines();
            ClearQueuedKnockback();
            UnlockBodyAfterAttack(false);

            // Elite instances are pooled. Restore the prefab's baseline before the
            // placer rolls elite status for this spawn.
            var eliteEffects = GetComponent<EliteEnemyEffects>();
            if (eliteEffects != null)
                eliteEffects.RemoveEliteVisuals();

            ResetLeash();
            isElite = false;
            alwaysShowHealthBar = false;
            isDying = false;
            transform.localScale = baseLocalScale;
            transform.localRotation = baseLocalRotation;
            Health = baseHealth;
            MaxHealth = baseMaxHealth;
            Speed = baseSpeed;
            Damage = baseDamage;
            ScoreValue = baseScoreValue;
            healthBarVisable = false;
            isKnockedBack = false;
            knockbackTimer = 0f;
            nextKnockbackTime = 0f;
            activeKnockbackForce = 0f;
            activeKnockbackDirection = Vector2.zero;
            activeDamageKnockbackRoll = -1f;
            activeDamageKnockbackMultiplier = 1f;

            if (cachedSpriteRenderer != null)
            {
                cachedSpriteRenderer.enabled = true; // Ensure sprite is enabled
                cachedSpriteRenderer.color = originalSpriteColor;
            }
            else if (cachedMeshRenderer != null)
            {
                cachedMeshRenderer.enabled = true; // Ensure mesh is enabled
                EnemyRendererColor.Set(cachedMeshRenderer, meshColorProperties, originalMeshColor);
            }

            if (healthBar != null)
            {
                healthBar.UpdateBar(Health, MaxHealth);
                healthBar.HideBar();
            }
            DisableWorldHealthBar();
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }

        /// <summary>Accelerates the ground velocity toward the target chase velocity.</summary>
        protected void AccelerateTowards(Vector2 targetVelocity)
        {
            rb.SetGroundVelocity(
                Vector2.MoveTowards(
                    rb.GroundVelocity(),
                    targetVelocity,
                    acceleration * EnemyTimeScale * Time.fixedDeltaTime
                )
            );
        }

        /// <summary>Applies spatial-hash separation so enemies do not overlap.</summary>
        protected virtual void ApplySeparation()
        {
            if (rb == null)
                return;

            Vector2 separationVelocity = Vector2.zero;
            Vector2 myPos = rb.GroundPosition();

            var spatialHash = EnemySpatialHash.Instance;
            if (spatialHash != null)
            {
                var nearbyEnemies = spatialHash.GetNearbyEnemies(myPos, separationRadius);
                for (int i = 0; i < nearbyEnemies.Count; i++)
                {
                    EnemyBase other = nearbyEnemies[i];
                    if (other == null || other == this)
                        continue;

                    Vector2 otherPos =
                        other.rb != null
                            ? other.rb.GroundPosition()
                            : other.transform.position.ToGround();
                    Vector2 toMe = myPos - otherPos;
                    float dist = toMe.magnitude;

                    if (dist > 0.001f && dist < separationRadius)
                    {
                        float t = 1f - (dist / separationRadius);
                        float strength = t * t; // Quadratic for stronger close-range push
                        separationVelocity += toMe.normalized * strength * separationForce;
                    }
                }
            }

            // Player/enemy separation is handled by solid colliders. Applying an
            // additional repulsion velocity here was the source of launch spikes
            // when the player pressed into an enemy.

            // Apply enemy/enemy separation as a bounded steering velocity. This is
            // deliberately a direct steering contribution rather than a tiny
            // per-second impulse; otherwise chase acceleration immediately erases
            // it and every enemy chooses the same line into the player.
            if (separationVelocity.sqrMagnitude > 0.01f)
            {
                if (separationVelocity.magnitude > maxSeparationSpeed)
                {
                    separationVelocity = separationVelocity.normalized * maxSeparationSpeed;
                }
                rb.SetGroundVelocity(rb.GroundVelocity() + separationVelocity);
            }

            // Never allow steering or a collision correction to accumulate into a
            // launch velocity. Explicit attack knockback has its own bounded state.
            float speedLimit = isKnockedBack
                ? Mathf.Max(activeKnockbackForce, Speed)
                : Mathf.Max(maxSeparationSpeed, Speed);
            rb.SetGroundVelocity(Vector2.ClampMagnitude(rb.GroundVelocity(), speedLimit));
        }

        /// <summary>
        /// The gap a melee enemy holds from the player, never wider than the distance
        /// it can actually strike from.
        ///
        /// These two were authored independently and disagreed: the ordinary enemy
        /// held four tenths of a unit of personal space and could swing from two and a
        /// half, so one that reached its preferred station could never attack from it.
        /// A balance run measured the player standing at full health in the middle of
        /// a dozen of them. Landing a hit was left to whichever enemy the crowd
        /// happened to shove inside its own stand-off.
        /// </summary>
        internal static float StandOffInsideReach(float authoredGap, float attackReach) =>
            Mathf.Min(Mathf.Max(0f, authoredGap), Mathf.Max(0f, attackReach) * 0.6f);

        /// <summary>Checks whether the player and enemy colliders are within a world-space gap.</summary>
        protected bool IsPlayerWithinColliderGap(float maxGap)
        {
            return GetPlayerColliderGap() <= Mathf.Max(0f, maxGap);
        }

        /// <summary>Returns the edge-to-edge distance between the enemy and player bodies.</summary>
        protected float GetPlayerColliderGap()
        {
            if (bodyCollider == null || player == null)
                return float.PositiveInfinity;

            if (playerCollider == null || !playerCollider.enabled)
                playerCollider = FindSolidCollider(player);

            if (playerCollider == null)
                return float.PositiveInfinity;

            return GroundPlane.ColliderGap(bodyCollider, playerCollider);
        }

        /// <summary>Validates the attack lunge's reach and direction.</summary>
        protected bool IsPlayerWithinAttackContact(
            float attackReach,
            Vector2 attackDirection,
            float minimumFacingDot = 0.5f
        )
        {
            if (!IsPlayerWithinColliderGap(attackReach))
                return false;

            Vector2 toPlayer =
                playerCollider.bounds.center.ToGround() - bodyCollider.bounds.center.ToGround();
            if (toPlayer.sqrMagnitude < 0.0001f || attackDirection.sqrMagnitude < 0.0001f)
                return true;

            return Vector2.Dot(attackDirection.normalized, toPlayer.normalized) >= minimumFacingDot;
        }

        private static Collider FindSolidCollider(Transform target)
        {
            if (target == null)
                return null;

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger)
                    return candidate;
            }

            return null;
        }
    }
}
