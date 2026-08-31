using System;
using System.Collections;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public abstract partial class EnemyBase
    {
        private System.Collections.IEnumerator HitFlash()
        {
            if (cachedSpriteRenderer != null)
            {
                cachedSpriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.05f);
                cachedSpriteRenderer.color = originalSpriteColor;
            }
            else if (cachedMeshRenderer != null)
            {
                EnemyRendererColor.Set(cachedMeshRenderer, meshColorProperties, Color.white);
                yield return new WaitForSeconds(0.05f);
                EnemyRendererColor.Set(cachedMeshRenderer, meshColorProperties, originalMeshColor);
            }
            else
            {
                yield break;
            }
        }

        void Start()
        {
            DisableWorldHealthBar();
            DiabloHud.EnsurePresent();
        }

        public virtual void Update()
        {
            if (player == null)
                return;

            if (isKnockedBack)
            {
                knockbackTimer -= Time.deltaTime;
                if (knockbackTimer <= 0f)
                {
                    isKnockedBack = false;
                    activeKnockbackForce = 0f;
                    activeKnockbackDirection = Vector2.zero;
                    activeDamageKnockbackRoll = -1f;
                    activeDamageKnockbackMultiplier = 1f;
                }
            }
        }

        private void DisableWorldHealthBar()
        {
            if (healthBar == null)
            {
                foreach (Bar candidate in GetComponentsInChildren<Bar>(true))
                {
                    if (candidate.gameObject.name != "HealthBar")
                        continue;

                    healthBar = candidate;
                    break;
                }
            }

            healthBarVisable = false;
            if (healthBar == null)
                return;

            Canvas worldCanvas = healthBar.GetComponentInParent<Canvas>(true);
            if (
                worldCanvas != null
                && worldCanvas.renderMode == RenderMode.WorldSpace
                && worldCanvas.transform.IsChildOf(transform)
            )
            {
                worldCanvas.gameObject.SetActive(false);
            }
            else
            {
                healthBar.HideBar();
            }
        }

        protected virtual void FixedUpdate()
        {
            EnemySpatialHash.Instance?.UpdatePosition(this);

            // Contacts with the player or a packed crowd can consume a velocity
            // assigned only once. Reassert the bounded recoil for its short active
            // window so a qualifying hit produces real, visible displacement.
            if (isKnockedBack && rb != null)
            {
                rb.SetGroundVelocity(activeKnockbackDirection * activeKnockbackForce);
                return;
            }

            ApplySeparation();
        }

        /// <summary>Handles score, XP drop, animation, and pooling when this enemy dies.</summary>
        public virtual void Die()
        {
            if (isQuitting)
                return;
            if (!gameObject.scene.isLoaded)
                return;
            if (isDying)
                return;

            isDying = true;
            Health = 0f;
            DiabloHud.NotifyEnemyDefeated(this);
            PrepareForIncomingKnockback();
            ClearQueuedKnockback();
            UnlockBodyAfterAttack(false);

            // The enemy stops participating in combat immediately, while its
            // rendered body remains briefly to play the implosion.
            player = null;
            EnemySpatialHash.Instance?.Unregister(this);
            if (bodyCollider != null)
                bodyCollider.enabled = false;
            if (rb != null)
            {
                rb.SetSimulated(false);
            }
            if (healthBar != null)
                healthBar.HideBar();

            var context = GameContext.Instance;
            if (context?.GameStates != null)
            {
                context.GameStates.score += ScoreValue;
                context.GameStates.RecordEnemyKilled();
            }

            SpawnExpGain();

            OnDeath?.Invoke(this);

            EnemyDeathAudio.Play(transform.position, isElite);
            StartCoroutine(PlayDeathAnimation());
        }

        private IEnumerator PlayDeathAnimation()
        {
            Vector3 startScale = transform.localScale;
            Quaternion startRotation = transform.localRotation;
            float minDuration = Mathf.Max(0.05f, deathAnimationDurationRange.x);
            float maxDuration = Mathf.Max(minDuration, deathAnimationDurationRange.y);
            float duration = UnityEngine.Random.Range(minDuration, maxDuration);
            float anticipationDuration =
                duration * Mathf.Clamp(deathAnticipationFraction, 0.1f, 0.4f);
            float collapseDuration = Mathf.Max(0.05f, duration - anticipationDuration);
            float spin = 0f;

            if (UnityEngine.Random.value < deathSpinChance)
            {
                float minSpin = Mathf.Min(deathSpinDegreesRange.x, deathSpinDegreesRange.y);
                float maxSpin = Mathf.Max(deathSpinDegreesRange.x, deathSpinDegreesRange.y);
                spin =
                    UnityEngine.Random.Range(minSpin, maxSpin)
                    * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            }

            Vector3 squashScale = Vector3.Scale(startScale, deathSquashScale);
            float elapsed = 0f;
            while (elapsed < anticipationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / anticipationDuration);
                float eased = t * t * (3f - 2f * t);
                transform.localScale = Vector3.LerpUnclamped(startScale, squashScale, eased);
                transform.localRotation =
                    startRotation * Quaternion.Euler(0f, spin * 0.08f * eased, 0f);
                SetDeathFlash(Mathf.Sin(t * Mathf.PI));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < collapseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / collapseDuration);
                float collapse = t * t;
                float pop = Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.22f;
                Vector3 scale = Vector3.LerpUnclamped(squashScale, Vector3.zero, collapse);
                scale += Vector3.Scale(startScale, Vector3.one * pop);

                float spinEase = 1f - Mathf.Pow(1f - t, 3f);
                transform.localScale = scale;
                transform.localRotation =
                    startRotation
                    * Quaternion.Euler(0f, -Mathf.Lerp(-spin * 0.08f, spin, spinEase), 0f);
                SetDeathFlash(Mathf.Clamp01(1f - t * 4f));
                yield return null;
            }

            transform.localScale = Vector3.zero;
            SetDeathFlash(0f);
            CompleteDeath();
        }

        private void SetDeathFlash(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (cachedSpriteRenderer != null)
            {
                cachedSpriteRenderer.color = Color.Lerp(originalSpriteColor, Color.white, amount);
            }
            else if (cachedMeshRenderer != null)
            {
                EnemyRendererColor.Set(
                    cachedMeshRenderer,
                    meshColorProperties,
                    Color.Lerp(originalMeshColor, Color.white, amount)
                );
            }
        }

        private void CompleteDeath() => CompleteDeath(PoolManager.Instance, Destroy);

        internal void CompleteDeath(PoolManager poolManager, System.Action<GameObject> destroy)
        {
            // Return to pool or destroy only after the visible death finishes.
            if (_isPooled)
            {
                if (poolManager != null)
                    poolManager.ReturnEnemy(this);
                else
                    destroy(gameObject);
            }
            else
            {
                destroy(gameObject);
            }
        }
    }
}
