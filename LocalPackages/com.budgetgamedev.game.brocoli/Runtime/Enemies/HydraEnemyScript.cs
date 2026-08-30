using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Hydra enemy that splits into smaller copies when killed.
    /// Each generation is smaller and weaker until minimum generation is reached.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public partial class HydraEnemyScript : EnemyBase
    {
        private const float MaxAttackLungeDistance = 0.42f;
        private const float MaxAttackPullBackDistance = 0.22f;

        [Header("Hydra Split Settings")]
        [SerializeField]
        private int currentGeneration = 0; // 0 = original, increases with each split

        [SerializeField]
        private int maxGenerations = 2; // Max splits (0->1->2, then dies for real)

        [SerializeField]
        private float childScaleMultiplier = 0.7f; // Each generation is 70% the size

        [SerializeField]
        private float childHealthMultiplier = 0.5f; // Each generation has 50% health

        [SerializeField]
        private float childDamageMultiplier = 0.7f; // Each generation does 70% damage

        [SerializeField]
        private float childSpeedMultiplier = 1.1f; // Each generation is 10% faster

        [SerializeField]
        private float splitSpawnRadius = 0.5f; // How far apart children spawn

        [SerializeField]
        private float splitImpulse = 3f;

        public event Action<HydraEnemyScript> OnChildSpawned;

        [Header("Melee Attack")]
        [SerializeField]
        private float meleeRange = 0.9f;

        [SerializeField]
        private float meleeAttackCooldown = 0.5f;
        private float nextMeleeAttackTime = 0f;

        [Header("Attack Animation")]
        [SerializeField]
        private float attackWindupDuration = 0.28f;

        [SerializeField]
        private float attackStrikeDuration = 0.14f;

        [SerializeField]
        private float attackRecoverDuration = 0.26f;

        [SerializeField]
        private float attackPullBackDistance = 0.18f;

        [SerializeField]
        private float attackLungeDistance = 0.42f;

        [SerializeField]
        private Color attackFlashColor = Color.red;
        private bool isAttacking = false;
        private bool hasDamagedThisAttack = false;
        private float attackTimer = 0f;
        private int attackPhase = 0;
        private Vector3 attackStartPos;
        private Vector3 attackWindupPos;
        private Vector3 attackTargetPos;
        private Quaternion attackStartRotation;
        private Vector2 attackDirection;
        private float activeAttackReach;
        private Vector3 baseLocalScale;
        private SpriteRenderer spriteRenderer; // Local sprite renderer for attack animations
        private Color originalColor;
        private Transform visualTransform;
        private EnemyWalkAnimation walkAnimation;

        [Header("Melee Audio")]
        [SerializeField]
        private ProceduralEnemyMeleeAudio meleeAudio;

        private static bool isQuitting = false;
        private bool hasSpawnedChildren = false;

        protected override void Awake()
        {
            base.Awake();
            walkAnimation = GetComponent<EnemyWalkAnimation>();

            if (meleeAudio == null)
                meleeAudio = GetComponent<ProceduralEnemyMeleeAudio>();

            // Find visual transform for attack animation
            // Enemy prefabs use FBX models with MeshRenderer, not SpriteRenderer
            Renderer visualRenderer = null;

            // First check for enabled SpriteRenderer
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.enabled)
                {
                    spriteRenderer = sr;
                    visualRenderer = sr;
                    break;
                }
            }

            // If no enabled SpriteRenderer, find MeshRenderer (FBX models)
            if (visualRenderer == null)
            {
                foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.enabled)
                    {
                        visualRenderer = mr;
                        break;
                    }
                }
            }

            // Set up visual transform from the renderer we found
            if (visualRenderer != null && visualRenderer.transform != transform)
            {
                visualTransform = visualRenderer.transform;
                baseLocalScale = visualTransform.localScale;

                // Safety check: if scale is zero (shouldn't happen), use a reasonable default
                if (baseLocalScale.sqrMagnitude < 0.0001f)
                {
                    Debug.LogWarning(
                        $"[HydraEnemyScript] {name}: visualTransform '{visualTransform.name}' has zero scale! Using Vector3.one as fallback."
                    );
                    baseLocalScale = Vector3.one;
                    visualTransform.localScale = Vector3.one;
                }

                if (spriteRenderer != null)
                {
                    originalColor = spriteRenderer.color;
                }
            }
            else
            {
                // Never animate the Rigidbody/collider root.
                visualTransform = null;
                baseLocalScale = Vector3.one;
            }
        }

        protected override void FixedUpdate()
        {
            if (player == null)
                return;

            // Track the player while winding up, then commit and pin the body for
            // the released strike and recovery.
            if (isAttacking && attackPhase >= 2)
            {
                rb.SetGroundVelocity(Vector2.zero);
                EnemySpatialHash.Instance?.UpdatePosition(this);
                return;
            }

            if (isKnockedBack)
            {
                base.FixedUpdate();
                return;
            }

            Vector2 dir = player.position.ToGround() - rb.GroundPosition();
            float distToPlayer = dir.magnitude;

            if (distToPlayer < 0.0001f)
                return;

            dir.Normalize();
            float colliderGap = GetPlayerColliderGap();
            float standOffGap = Mathf.Max(0f, playerStandOffGap);
            const float standOffDeadZone = 0.025f;
            Vector2 targetVel;

            if (colliderGap > standOffGap + standOffDeadZone)
            {
                targetVel = dir * Speed * EnemyTimeScale;
            }
            else if (colliderGap < standOffGap - standOffDeadZone)
            {
                float retreatSpeed = Mathf.Min(
                    Speed,
                    Mathf.Max(0.35f, (standOffGap - colliderGap) * acceleration)
                );
                targetVel = -dir * retreatSpeed * EnemyTimeScale;
            }
            else
            {
                targetVel = Vector2.zero;
            }
            AccelerateTowards(targetVel);

            base.FixedUpdate();
        }

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

        /// <summary>
        /// Initialize this hydra as a child of another hydra
        /// </summary>
        public void InitAsChild(
            int generation,
            float parentHealth,
            float parentDamage,
            float parentSpeed,
            Vector3 parentScale
        )
        {
            currentGeneration = generation;

            // A pooled child must never retain listeners from a previous life.
            OnChildSpawned = null;
            hasSpawnedChildren = false;

            MaxHealth = parentHealth * childHealthMultiplier;
            Health = MaxHealth;
            Damage = parentDamage * childDamageMultiplier;
            Speed = parentSpeed * childSpeedMultiplier;

            // Scale down visually
            transform.localScale = parentScale * childScaleMultiplier;

            // Reduce score value for smaller enemies
            ScoreValue = Mathf.Max(10, ScoreValue / 2);

            // Update melee range based on scale
            meleeRange *= childScaleMultiplier;
        }

        /// <summary>
        /// Override Die to spawn children before death.
        /// </summary>
        public override void Die()
        {
            if (isQuitting)
                return;
            if (!gameObject.scene.isLoaded)
                return;

            // Spawn children if we haven't reached max generations
            if (!hasSpawnedChildren && currentGeneration < maxGenerations)
            {
                SpawnChildren();
            }

            // Call base Die (handles score, XP, and pooling/destroy)
            base.Die();
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void SpawnChildren()
        {
            hasSpawnedChildren = true;

            const int childrenToSpawn = 2;
            for (int i = 0; i < childrenToSpawn; i++)
            {
                // Calculate spawn position in a circle around death position
                float angle = (360f / childrenToSpawn) * i + Random.Range(-15f, 15f);
                Vector2 offset =
                    new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad))
                    * splitSpawnRadius;

                Vector3 spawnPos = transform.position + offset.ToWorld();

                // Try to get from pool first (use this as prefab template)
                HydraEnemyScript childHydra = null;
                EnemyBase pooledEnemy = PoolManager.Instance?.GetEnemy(
                    this,
                    spawnPos,
                    Quaternion.identity
                );

                if (pooledEnemy != null)
                {
                    childHydra = pooledEnemy as HydraEnemyScript;
                    if (childHydra != null)
                    {
                        childHydra.SetPooled(true);
                    }
                }

                // Fallback to instantiate if pool not available
                if (childHydra == null)
                {
                    GameObject child = Instantiate(gameObject, spawnPos, Quaternion.identity);
                    childHydra = child.GetComponent<HydraEnemyScript>();
                }

                if (childHydra != null)
                {
                    childHydra.hasSpawnedChildren = false; // Reset so it can spawn its own children
                    childHydra.InitAsChild(
                        currentGeneration + 1,
                        MaxHealth,
                        Damage,
                        Speed,
                        transform.localScale
                    );

                    OnChildSpawned?.Invoke(childHydra);

                    // Give child a small impulse away from spawn point
                    if (childHydra.rb != null)
                    {
                        childHydra.rb.SetGroundVelocity(offset.normalized * splitImpulse);
                    }
                }
            }
        }

        /// <summary>
        /// Reset hydra-specific state for pool reuse.
        /// </summary>
        public override void ResetForPool()
        {
            base.ResetForPool();

            hasSpawnedChildren = false;
            OnChildSpawned = null;
            currentGeneration = 0;
            isAttacking = false;
            hasDamagedThisAttack = false;
            attackPhase = 0;
            attackTimer = 0f;
            nextMeleeAttackTime = 0f;
            attackDirection = Vector2.zero;
            activeAttackReach = 0f;
            walkAnimation?.SetAttackOverride(false);

            // Reset visual state
            if (visualTransform != null)
            {
                // Don't reset localPosition - preserve prefab's Z offset for 3D models
                visualTransform.localScale = baseLocalScale;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true; // Ensure sprite is enabled
                spriteRenderer.color = originalColor;
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
                {
                    if (meleeAudio != null)
                    {
                        meleeAudio.PlayMeleeSound();
                    }
                }
            }
        }
    }
}
