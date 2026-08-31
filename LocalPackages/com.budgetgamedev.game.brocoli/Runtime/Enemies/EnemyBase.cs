using System;
using System.Collections;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public abstract partial class EnemyBase : MonoBehaviour
    {
        private const float MaxSafeSeparationRadius = 1.15f;
        private const float MaxSafeSeparationForce = 8f;
        private const float MaxSafeSeparationSpeed = 2f;
        private const float MaxSafeKnockbackForce = 3f;
        private const float MaxSafeKnockbackDuration = 0.16f;
        private const float MinSafeKnockbackCooldown = 0.2f;
        private static PhysicsMaterial sharedEnemyBodyMaterial;

        [SerializeField]
        Bar healthBar;

        [SerializeField]
        protected ExpGain expGainPrefab;

        public event Action<EnemyBase> OnDeath;

        public bool healthBarVisable = false;
        public bool alwaysShowHealthBar = false;
        public float TimeToStartSpawning = 0f;
        public float TimeToEndSpawning = 60f;
        public int ScoreValue = 100;
        public float Damage = 0f;

        private static bool isQuitting = false;

        [Header("Enemy Stats")]
        public float Speed = 2f;
        public float Health = 50f;
        public float MaxHealth = 50f;

        [Header("Elite Settings")]
        public bool isElite = false;
        public float eliteHealthMultiplier = 3f;
        public float eliteScaleMultiplier = 1.3f;

        public event Action<Vector3> OnEliteDeath;

        [Header("Physics Tuning")]
        public float acceleration = 25f;

        [Header("Separation")]
        [SerializeField]
        protected float separationRadius = 1.15f; // Roughly one enemy body diameter

        [SerializeField]
        protected float separationForce = 8f; // Gentle steering, never an impulse

        [SerializeField]
        protected float playerSeparationRadius = 0.4f; // Minimum distance from player (reduced for close melee)

        [SerializeField]
        protected float playerSeparationForce = 8f; // How hard to avoid player overlap (gentle push)

        [SerializeField]
        protected float maxSeparationSpeed = 2f; // Separation should never look like knockback

        [Tooltip("Collider-to-collider gap melee enemies maintain from the player.")]
        [SerializeField]
        protected float playerStandOffGap = 0.4f;

        [Header("Walk Audio (Optional)")]
        [SerializeField]
        protected ProceduralEnemyWalkAudio walkAudio;

        public Transform player;

        public Rigidbody rb;
        protected float EnemyTimeScale => PlayerStats.ActiveEnemyTimeScale;
        protected Collider bodyCollider;
        private Collider playerCollider;

        protected SpriteRenderer cachedSpriteRenderer;
        protected Color originalSpriteColor;
        protected MeshRenderer cachedMeshRenderer;
        protected Color originalMeshColor;
        private MaterialPropertyBlock meshColorProperties;
        private bool _isPooled = false;
        private bool attackBodyLocked = false;
        private RigidbodyConstraints constraintsBeforeAttack;
        private bool hasQueuedKnockback;
        private Vector2 queuedKnockbackDirection;
        private float queuedKnockbackForce;
        private Vector3 baseLocalScale;
        private float baseHealth;
        private float baseMaxHealth;
        private float baseSpeed;
        private float baseDamage;
        private int baseScoreValue;
        private Quaternion baseLocalRotation;

        [Header("Death Animation")]
        [Tooltip("Randomized squash-and-pop duration keeps groups from disappearing in lockstep.")]
        [SerializeField]
        private Vector2 deathAnimationDurationRange = new Vector2(0.32f, 0.42f);

        [Tooltip("How much of the death is spent on the bright anticipation squash.")]
        [SerializeField, Range(0.1f, 0.4f)]
        private float deathAnticipationFraction = 0.24f;

        [Tooltip("Local scale multiplier at the end of the anticipation squash.")]
        [SerializeField]
        private Vector3 deathSquashScale = new Vector3(1.18f, 0.72f, 1.18f);

        [Tooltip("Fraction of deaths that add a small randomized spin while shrinking.")]
        [SerializeField, Range(0f, 1f)]
        private float deathSpinChance = 0.6f;

        [SerializeField]
        private Vector2 deathSpinDegreesRange = new Vector2(35f, 110f);
        private bool isDying;

        public bool IsDying => isDying;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            EnforceSafePhysicsLimits();

            ConfigureSolidBody();

            baseLocalScale = transform.localScale;
            baseLocalRotation = transform.localRotation;
            baseHealth = Health;
            baseMaxHealth = MaxHealth;
            baseSpeed = Speed;
            baseDamage = Damage;
            baseScoreValue = ScoreValue;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.enabled)
                {
                    cachedSpriteRenderer = sr;
                    break;
                }
            }

            if (cachedSpriteRenderer == null)
            {
                foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.enabled)
                    {
                        cachedMeshRenderer = mr;
                        meshColorProperties = new MaterialPropertyBlock();
                        originalMeshColor = EnemyRendererColor.Get(mr, meshColorProperties);
                        break;
                    }
                }
            }
            else
            {
                originalSpriteColor = cachedSpriteRenderer.color;
            }

            if (walkAudio == null)
                walkAudio = GetComponent<ProceduralEnemyWalkAudio>();

            DisableWorldHealthBar();
        }

        /// <summary>Make this enemy an elite variant with increased HP and visual effects.</summary>
        public void MakeElite()
        {
            if (isElite)
                return;

            isElite = true;
            Health *= eliteHealthMultiplier;
            MaxHealth *= eliteHealthMultiplier;
            ScoreValue = Mathf.RoundToInt(ScoreValue * 2.5f);
            transform.localScale *= eliteScaleMultiplier;

            var effects = GetComponent<EliteEnemyEffects>();
            if (effects == null)
                effects = gameObject.AddComponent<EliteEnemyEffects>();
            effects.ApplyEliteVisuals();

            alwaysShowHealthBar = true;
        }

        protected virtual void OnEnable()
        {
            // OnEnable also runs after an editor script reload. Clamp live enemies
            EnforceSafePhysicsLimits();
            ConfigureSolidBody();

            var context = GameContext.Instance;
            if (context != null)
            {
                player = context.PlayerTransform;
            }

            playerCollider = FindSolidCollider(player);

            if (rb != null)
            {
                rb.linearDamping = 3f;
            }

            EnemySpatialHash.Instance?.Register(this);
            DisableWorldHealthBar();
            DiabloHud.EnsurePresent();
        }

        private void EnforceSafePhysicsLimits()
        {
            separationRadius = Mathf.Clamp(separationRadius, 0f, MaxSafeSeparationRadius);
            separationForce = Mathf.Clamp(separationForce, 0f, MaxSafeSeparationForce);
            maxSeparationSpeed = Mathf.Clamp(maxSeparationSpeed, 0f, MaxSafeSeparationSpeed);
            playerStandOffGap = Mathf.Clamp(playerStandOffGap, 0f, 0.6f);
            enemyKnockbackForce = Mathf.Clamp(enemyKnockbackForce, 0f, MaxSafeKnockbackForce);
            minimumEnemyKnockbackForce = Mathf.Clamp(
                minimumEnemyKnockbackForce,
                0f,
                enemyKnockbackForce
            );
            minimumDamageFractionForKnockback = Mathf.Clamp01(minimumDamageFractionForKnockback);
            damageFractionForMaxKnockback = Mathf.Clamp(damageFractionForMaxKnockback, 0.01f, 1f);
            rareMaximumKnockbackChance = Mathf.Clamp(rareMaximumKnockbackChance, 0f, 0.2f);
            knockbackRandomBias = Mathf.Clamp(knockbackRandomBias, 1f, 8f);
            enemyKnockbackDuration = Mathf.Clamp(
                enemyKnockbackDuration,
                0f,
                MaxSafeKnockbackDuration
            );
            enemyKnockbackCooldown = Mathf.Max(enemyKnockbackCooldown, MinSafeKnockbackCooldown);
        }

        private void ConfigureSolidBody()
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider>();

            // Enemy body colliders are for navigation/blocking. Combat damage is
            // validated separately by the attack animation at its impact frame.
            if (bodyCollider != null)
            {
                bodyCollider.isTrigger = false;

                // Solid crowd contacts should slide, never bounce or grip and
                // convert tangential movement into a shove.
                if (sharedEnemyBodyMaterial == null)
                {
                    sharedEnemyBodyMaterial = new PhysicsMaterial("Enemy Body - No Bounce")
                    {
                        dynamicFriction = 0f,
                        staticFriction = 0f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                }
                bodyCollider.sharedMaterial = sharedEnemyBodyMaterial;
            }

            // Enemy bodies must collide with one another so they cannot overlap.
            // Explicitly restore this because older play sessions may still have
            // the Enemy/Enemy layer pair disabled by the previous workaround.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0 && Physics.GetIgnoreLayerCollision(enemyLayer, enemyLayer))
                Physics.IgnoreLayerCollision(enemyLayer, enemyLayer, false);
        }

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
