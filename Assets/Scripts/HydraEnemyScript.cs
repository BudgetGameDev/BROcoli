using UnityEngine;

/// <summary>
/// Hydra enemy that splits into smaller copies when killed.
/// Each generation is smaller and weaker until minimum generation is reached.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HydraEnemyScript : EnemyBase
{
    [Header("Hydra Split Settings")]
    [SerializeField] private int splitCount = 2;           // How many children spawn on death
    [SerializeField] private int currentGeneration = 0;    // 0 = original, increases with each split
    [SerializeField] private int maxGenerations = 2;       // Max splits (0->1->2, then dies for real)
    [SerializeField] private float childScaleMultiplier = 0.7f;  // Each generation is 70% the size
    [SerializeField] private float childHealthMultiplier = 0.5f; // Each generation has 50% health
    [SerializeField] private float childDamageMultiplier = 0.7f; // Each generation does 70% damage
    [SerializeField] private float childSpeedMultiplier = 1.1f;  // Each generation is 10% faster
    [SerializeField] private float splitSpawnRadius = 0.5f;      // How far apart children spawn
    
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
    private float attackTimer = 0f;
    private int attackPhase = 0;
    private Vector3 attackStartPos;
    private Vector3 attackTargetPos;
    private Vector3 baseLocalScale;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Transform visualTransform;
    
    [Header("Melee Audio")]
    [SerializeField] private ProceduralEnemyMeleeAudio meleeAudio;
    
    private static bool isQuitting = false;
    private bool hasSpawnedChildren = false;
    private GameStates gameStates;

    protected override void Awake()
    {
        base.Awake();
        
        if (meleeAudio == null)
            meleeAudio = GetComponent<ProceduralEnemyMeleeAudio>();
        
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            visualTransform = spriteRenderer.transform;
            baseLocalScale = visualTransform.localScale;
        }
        else
        {
            visualTransform = transform;
            baseLocalScale = transform.localScale;
        }
    }

    protected override void FixedUpdate()
    {
        if (player == null) return;
        
        if (isKnockedBack)
        {
            base.FixedUpdate();
            return;
        }

        Vector2 dir = (Vector2)player.position - rb.position;
        float distToPlayer = dir.magnitude;
        
        if (distToPlayer < 0.0001f) return;

        dir.Normalize();
        Vector2 targetVel = dir * Speed;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVel, acceleration * Time.fixedDeltaTime);
        
        base.FixedUpdate();
    }

    public override void Update()
    {
        base.Update();
        
        UpdateAttackAnimation();
        
        if (player != null && !isAttacking)
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
    public void InitAsChild(int generation, float parentHealth, float parentDamage, float parentSpeed, Vector3 parentScale)
    {
        currentGeneration = generation;
        
        // Scale down stats based on generation
        float healthMult = Mathf.Pow(childHealthMultiplier, generation);
        float damageMult = Mathf.Pow(childDamageMultiplier, generation);
        float speedMult = Mathf.Pow(childSpeedMultiplier, generation);
        float scaleMult = Mathf.Pow(childScaleMultiplier, generation);
        
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

    void OnDestroy()
    {
        if (isQuitting) return;
        if (!gameObject.scene.isLoaded) return;
        if (hasSpawnedChildren) return;
        
        // Spawn children if we haven't reached max generations
        if (currentGeneration < maxGenerations)
        {
            SpawnChildren();
        }
    }
    
    void OnApplicationQuit()
    {
        isQuitting = true;
    }
    
    private void SpawnChildren()
    {
        hasSpawnedChildren = true;
        
        for (int i = 0; i < splitCount; i++)
        {
            // Calculate spawn position in a circle around death position
            float angle = (360f / splitCount) * i + Random.Range(-15f, 15f);
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
                childHydra.hasSpawnedChildren = false; // Reset so it can spawn its own children
                childHydra.InitAsChild(
                    currentGeneration + 1,
                    MaxHealth,
                    Damage,
                    Speed,
                    transform.localScale
                );
            }
            
            // Give child a small impulse away from spawn point
            Rigidbody2D childRb = child.GetComponent<Rigidbody2D>();
            if (childRb != null)
            {
                childRb.linearVelocity = offset.normalized * 3f;
            }
        }
    }

    private void StartAttackAnimation()
    {
        if (player == null) return;
        
        isAttacking = true;
        attackPhase = 1;
        attackTimer = 0f;
        attackStartPos = visualTransform.localPosition;
        
        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        attackTargetPos = attackStartPos + (Vector3)(toPlayer * attackLungeDistance);
    }
    
    private void UpdateAttackAnimation()
    {
        if (!isAttacking) return;
        
        attackTimer += Time.deltaTime;
        
        switch (attackPhase)
        {
            case 1: // Windup
                float windupT = attackTimer / attackWindupDuration;
                if (windupT >= 1f)
                {
                    attackPhase = 2;
                    attackTimer = 0f;
                    PerformMeleeAttack();
                }
                else
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
                if (strikeT >= 1f)
                {
                    attackPhase = 3;
                    attackTimer = 0f;
                }
                else
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
                    visualTransform.localPosition = attackStartPos;
                    visualTransform.localScale = baseLocalScale;
                    if (spriteRenderer != null)
                        spriteRenderer.color = originalColor;
                }
                else
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
                nextMeleeAttackTime = Time.time + meleeAttackCooldown;
                
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
