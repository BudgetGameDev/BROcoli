using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Hydra enemy that splits into smaller copies when killed.
/// Each generation is smaller and weaker until minimum generation is reached.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HydraEnemyScript : EnemyBase
{
    [Header("Hydra Split Settings")]
    [SerializeField, Min(2)] private int splitCount = 2;
    [SerializeField, Min(0)] private int currentGeneration = 0;
    [SerializeField, Min(0)] private int maxGenerations = 2;
    [SerializeField, Range(0.25f, 0.9f)] private float childScaleMultiplier = 0.7f;
    [SerializeField, Range(0.1f, 1f)] private float childHealthMultiplier = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float childDamageMultiplier = 0.7f;
    [SerializeField, Min(0.1f)] private float childSpeedMultiplier = 1.1f;
    [SerializeField, Min(0f)] private float splitSpawnRadius = 0.5f;
    [SerializeField, Min(0f)] private float splitImpulse = 3f;

    public event Action<HydraEnemyScript> OnChildSpawned;
    
    [Header("Melee Attack")]
    [SerializeField] private float meleeRange = 0.9f;
    [SerializeField] private float meleeAttackCooldown = 0.5f;
    private float nextMeleeAttackTime = 0f;
    
    [Header("Attack Animation")]
    [SerializeField] private float attackWindupDuration = 0.15f;
    [SerializeField] private float attackStrikeDuration = 0.1f;
    [SerializeField] private float attackRecoverDuration = 0.2f;
    [SerializeField] private float attackLungeDistance = 0.4f;
    [SerializeField] private float attackScaleBoost = 1.3f;
    [SerializeField] private Color attackFlashColor = Color.red;
    private bool isAttacking = false;
    private bool hasDamagedThisAttack = false;
    private float attackTimer = 0f;
    private int attackPhase = 0;
    private Vector3 attackStartPos;
    private Vector3 attackTargetPos;
    private Vector3 baseLocalScale;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Transform visualTransform;
    private EnemyWalkAnimation walkAnimation;
    
    [Header("Melee Audio")]
    [SerializeField] private ProceduralEnemyMeleeAudio meleeAudio;
    
    private bool hasSpawnedChildren = false;

    protected override void Awake()
    {
        base.Awake();
        
        if (meleeAudio == null)
            meleeAudio = GetComponent<ProceduralEnemyMeleeAudio>();

        walkAnimation = GetComponent<EnemyWalkAnimation>();
        
        Renderer visualRenderer = null;

        foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!candidate.enabled || candidate.sprite == null) continue;

            spriteRenderer = candidate;
            visualRenderer = candidate;
            originalColor = candidate.color;
            break;
        }

        if (visualRenderer == null)
        {
            foreach (MeshRenderer candidate in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!candidate.enabled) continue;

                visualRenderer = candidate;
                break;
            }
        }

        if (visualRenderer != null && visualRenderer.transform != transform)
        {
            visualTransform = visualRenderer.transform;
            baseLocalScale = visualTransform.localScale;
        }
    }

    protected override void FixedUpdate()
    {
        if (player == null) return;
        
        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            base.FixedUpdate();
            return;
        }

        if (isKnockedBack)
        {
            base.FixedUpdate();
            return;
        }

        Vector2 dir = (Vector2)player.position - rb.position;
        float distToPlayer = dir.magnitude;
        
        if (distToPlayer < 0.0001f) return;

        dir.Normalize();
        Vector2 targetVel = dir * Speed * EnemyTimeScale;
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            targetVel,
            acceleration * EnemyTimeScale * Time.fixedDeltaTime
        );
        
        base.FixedUpdate();
    }

    public override void Update()
    {
        base.Update();
        
        UpdateAttackAnimation();
        
        if (player != null && !isAttacking && !isKnockedBack)
        {
            float distToPlayer = Vector2.Distance(transform.position, player.position);
            if (distToPlayer <= meleeRange && Time.time >= nextMeleeAttackTime)
            {
                StartAttackAnimation();
            }
        }
    }
    
    /// <summary>
    /// Initialize this hydra as a child of another hydra
    /// </summary>
    private void InitAsChild(int generation, float parentHealth, float parentDamage, float parentSpeed, Vector3 parentScale)
    {
        currentGeneration = generation;

        // Runtime delegates are not part of a child hydra's inherited state.
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

    protected override void OnKilled()
    {
        if (!hasSpawnedChildren && currentGeneration < maxGenerations)
        {
            SpawnChildren();
        }
    }
    
    private void SpawnChildren()
    {
        hasSpawnedChildren = true;
        
        int childrenToSpawn = Mathf.Max(2, splitCount);

        for (int i = 0; i < childrenToSpawn; i++)
        {
            // Calculate spawn position in a circle around death position
            float angle = (360f / childrenToSpawn) * i + Random.Range(-15f, 15f);
            Vector2 offset = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ) * splitSpawnRadius;
            
            Vector3 spawnPos = transform.position + (Vector3)offset;
            
            // Instantiate a copy of this prefab
            GameObject child = Instantiate(gameObject, spawnPos, Quaternion.identity);
            
            // Get the hydra component and initialize it as a child
            HydraEnemyScript childHydra = child.GetComponent<HydraEnemyScript>();
            if (childHydra != null)
            {
                childHydra.InitAsChild(
                    currentGeneration + 1,
                    MaxHealth,
                    Damage,
                    Speed,
                    transform.localScale
                );

                OnChildSpawned?.Invoke(childHydra);
            }
            
            // Give child a small impulse away from spawn point
            Rigidbody2D childRb = child.GetComponent<Rigidbody2D>();
            if (childRb != null)
            {
                childRb.linearVelocity = offset.normalized * splitImpulse;
            }
        }
    }

    private void StartAttackAnimation()
    {
        if (player == null) return;
        
        isAttacking = true;
        hasDamagedThisAttack = false;
        walkAnimation?.SetAttackOverride(true);
        attackPhase = 1;
        attackTimer = 0f;
        nextMeleeAttackTime = Time.time + meleeAttackCooldown / Mathf.Max(0.1f, EnemyTimeScale);
        
        if (visualTransform != null)
        {
            attackStartPos = visualTransform.localPosition;
            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            attackTargetPos = attackStartPos + (Vector3)(toPlayer * attackLungeDistance);
        }
    }
    
    private void UpdateAttackAnimation()
    {
        if (!isAttacking) return;
        
        attackTimer += Time.deltaTime * EnemyTimeScale;
        
        switch (attackPhase)
        {
            case 1: // Windup
                float windupT = attackTimer / attackWindupDuration;
                if (windupT >= 1f)
                {
                    attackPhase = 2;
                    attackTimer = 0f;
                }
                else if (visualTransform != null)
                {
                    Vector3 pullBack = attackStartPos - (attackTargetPos - attackStartPos) * 0.3f;
                    visualTransform.localPosition = Vector3.Lerp(attackStartPos, pullBack, EaseOutQuad(windupT));
                    
                    float scaleT = 1f + (attackScaleBoost - 1f) * 0.5f * windupT;
                    visualTransform.localScale = baseLocalScale * scaleT;
                    
                    if (spriteRenderer != null)
                        spriteRenderer.color = Color.Lerp(originalColor, attackFlashColor, windupT * 0.5f);
                }
                break;
                
            case 2: // Strike
                float strikeT = attackTimer / attackStrikeDuration;

                if (strikeT >= 0.6f && !hasDamagedThisAttack)
                {
                    hasDamagedThisAttack = true;
                    PerformMeleeAttack();
                }

                if (strikeT >= 1f)
                {
                    attackPhase = 3;
                    attackTimer = 0f;
                }
                else if (visualTransform != null)
                {
                    Vector3 pullBack = attackStartPos - (attackTargetPos - attackStartPos) * 0.3f;
                    visualTransform.localPosition = Vector3.Lerp(pullBack, attackTargetPos, EaseOutQuad(strikeT));
                    
                    visualTransform.localScale = baseLocalScale * attackScaleBoost;
                    
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
                        visualTransform.localScale = baseLocalScale;
                    }
                    if (spriteRenderer != null)
                        spriteRenderer.color = originalColor;

                    walkAnimation?.SetAttackOverride(false);
                }
                else if (visualTransform != null)
                {
                    visualTransform.localPosition = Vector3.Lerp(attackTargetPos, attackStartPos, EaseOutQuad(recoverT));
                    
                    float scaleT = Mathf.Lerp(attackScaleBoost, 1f, recoverT);
                    visualTransform.localScale = baseLocalScale * scaleT;
                    
                    if (spriteRenderer != null)
                        spriteRenderer.color = Color.Lerp(attackFlashColor, originalColor, recoverT);
                }
                break;
        }
    }
    
    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
    
    private void PerformMeleeAttack()
    {
        if (player == null) return;
        
        var playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            Vector2 knockbackDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
            
            if (playerController.TakeMeleeDamage(Damage, knockbackDir))
            {
                if (meleeAudio != null)
                {
                    meleeAudio.PlayMeleeSound();
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && Time.time >= nextMeleeAttackTime && !isAttacking)
        {
            StartAttackAnimation();
        }
    }
}
