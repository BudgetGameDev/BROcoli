using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyScript : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float meleeRange = 0.25f;       // Distance to trigger attack (very close range)
    [SerializeField] private float meleeAttackCooldown = 0.6f; // Time between attacks
    private float nextMeleeAttackTime = 0f;
    
    [Header("Attack Animation")]
    [SerializeField] private float attackWindupDuration = 0.15f;  // Time to pull back before striking
    [SerializeField] private float attackStrikeDuration = 0.1f;   // Time for the lunge forward
    [SerializeField] private float attackRecoverDuration = 0.2f;  // Time to return to normal
    [SerializeField] private float attackLungeDistance = 0.2f;    // How far to lunge toward player (reduced)
    [SerializeField] private float attackScaleBoost = 1.3f;       // Scale up during attack
    [SerializeField] private Color attackFlashColor = Color.red;  // Color flash on attack
    private bool isAttacking = false;
    private bool hasDamagedThisAttack = false; // Prevents double-damage per attack
    private float attackTimer = 0f;
    private int attackPhase = 0; // 0=idle, 1=windup, 2=strike, 3=recover
    private Vector3 attackStartPos;
    private Vector3 attackTargetPos;
    private Vector3 baseLocalScale;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Transform visualTransform;
    
    [Header("Melee Audio")]
    [SerializeField] private ProceduralEnemyMeleeAudio meleeAudio;

    protected override void Awake()
    {
        base.Awake();
        
        // Try to get melee audio component if not assigned
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
        if (visualRenderer != null)
        {
            visualTransform = visualRenderer.transform;
            baseLocalScale = visualTransform.localScale;
            
            // Safety check: if scale is zero (shouldn't happen), use a reasonable default
            if (baseLocalScale.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning($"[EnemyScript] {name}: visualTransform '{visualTransform.name}' has zero scale! Using Vector3.one as fallback.");
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
            visualTransform = transform;
            baseLocalScale = transform.localScale;
            
            // Safety check for root transform too
            if (baseLocalScale.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning($"[EnemyScript] {name}: root transform has zero scale! Using Vector3.one as fallback.");
                baseLocalScale = Vector3.one;
                transform.localScale = Vector3.one;
            }
        }
    }

    protected override void FixedUpdate()
    {
        if (player == null) return;
        
        // Don't move toward player during knockback - skip separation too to prevent flying
        if (isKnockedBack)
        {
            // Update spatial hash position but skip separation forces during knockback
            EnemySpatialHash.Instance?.UpdatePosition(this);
            return;
        }
        
        // Stop movement during attack animation - only visual transform moves, not the collider
        // This prevents physics conflicts when lunge animation plays
        if (isAttacking)
        {
            // Update spatial hash but don't move or apply separation during attack
            EnemySpatialHash.Instance?.UpdatePosition(this);
            // Zero out velocity during attack to prevent drift
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 dir = (Vector2)player.position - rb.position;
        float distToPlayer = dir.magnitude;
        
        if (distToPlayer < 0.0001f) return;

        dir.Normalize();

        // Move towards player at full speed - separation handles preventing overlap
        Vector2 targetVel = dir * Speed;

        // Smooth acceleration towards target velocity
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVel, acceleration * Time.fixedDeltaTime);
        
        // Apply separation AFTER movement (so it can push away)
        base.FixedUpdate();
    }

    public override void Update()
    {
        base.Update();
        
        // Update attack animation only - attacks start via trigger collision
        UpdateAttackAnimation();
    }
    
    private void StartAttackAnimation()
    {
        if (player == null) return;
        
        isAttacking = true;
        hasDamagedThisAttack = false; // Reset damage flag for new attack
        attackPhase = 1; // Start with windup
        attackTimer = 0f;
        attackStartPos = visualTransform.localPosition;
        
        // Calculate lunge direction toward player
        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        attackTargetPos = attackStartPos + (Vector3)(toPlayer * attackLungeDistance);
    }
    
    private void UpdateAttackAnimation()
    {
        if (!isAttacking) return;
        
        attackTimer += Time.deltaTime;
        
        switch (attackPhase)
        {
            case 1: // Windup - pull back slightly and prepare
                float windupT = attackTimer / attackWindupDuration;
                if (windupT >= 1f)
                {
                    attackPhase = 2;
                    attackTimer = 0f;
                    // Damage is dealt during strike phase at 60% when lunge visually connects
                }
                else
                {
                    // Pull back slightly (opposite of lunge direction)
                    Vector3 pullBack = attackStartPos - (attackTargetPos - attackStartPos) * 0.3f;
                    visualTransform.localPosition = Vector3.Lerp(attackStartPos, pullBack, EaseOutQuad(windupT));
                    
                    // Scale up slightly during windup
                    float scaleT = 1f + (attackScaleBoost - 1f) * 0.5f * windupT;
                    visualTransform.localScale = baseLocalScale * scaleT;
                    
                    // Start color flash
                    if (spriteRenderer != null)
                        spriteRenderer.color = Color.Lerp(originalColor, attackFlashColor, windupT * 0.5f);
                }
                break;
                
            case 2: // Strike - lunge forward, damage at midpoint
                float strikeT = attackTimer / attackStrikeDuration;
                
                // Deal damage at 60% through strike when visual lunge connects
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
                else
                {
                    // Lunge toward player
                    Vector3 pullBack = attackStartPos - (attackTargetPos - attackStartPos) * 0.3f;
                    visualTransform.localPosition = Vector3.Lerp(pullBack, attackTargetPos, EaseOutQuad(strikeT));
                    
                    // Full scale boost at peak
                    visualTransform.localScale = baseLocalScale * attackScaleBoost;
                    
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
                    visualTransform.localPosition = attackStartPos;
                    visualTransform.localScale = baseLocalScale;
                    if (spriteRenderer != null)
                        spriteRenderer.color = originalColor;
                }
                else
                {
                    // Return to start position
                    visualTransform.localPosition = Vector3.Lerp(attackTargetPos, attackStartPos, EaseOutQuad(recoverT));
                    
                    // Scale back down
                    float scaleT = Mathf.Lerp(attackScaleBoost, 1f, recoverT);
                    visualTransform.localScale = baseLocalScale * scaleT;
                    
                    // Fade color back
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
        // Deal damage to player - only proceed if damage was actually dealt
        var playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            // Calculate knockback direction (away from enemy)
            Vector2 knockbackDir = ((Vector2)player.position - (Vector2)transform.position).normalized;
            
            if (playerController.TakeMeleeDamage(Damage, knockbackDir))
            {
                // Only set cooldown and play sound if we actually hit
                nextMeleeAttackTime = Time.time + meleeAttackCooldown;
                
                // Play melee sound
                if (meleeAudio != null)
                {
                    meleeAudio.PlayMeleeSound();
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger attack animation on contact (animation will deal damage)
        if (other.CompareTag("Player") && CanStartAttack())
        {
            StartAttackAnimation();
        }
    }
    
    void OnTriggerStay2D(Collider2D other)
    {
        // Re-trigger attack if player stays inside and cooldown expired
        // This handles cases where player walks into enemy center and stays there
        if (other.CompareTag("Player") && CanStartAttack())
        {
            StartAttackAnimation();
        }
    }
    
    /// <summary>
    /// Check if enemy can start a new attack (not attacking, cooldown expired)
    /// </summary>
    private bool CanStartAttack()
    {
        return !isAttacking && Time.time >= nextMeleeAttackTime;
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
        
        // Reset visual state - critical for fixing invisible enemies!
        if (visualTransform != null)
        {
            // Don't reset localPosition - preserve prefab's Z offset for 3D models
            
            // Safety: ensure we never set zero scale
            if (baseLocalScale.sqrMagnitude < 0.0001f)
            {
                baseLocalScale = Vector3.one;
                Debug.LogWarning($"[EnemyScript.ResetForPool] {name}: baseLocalScale was zero, using Vector3.one");
            }
            visualTransform.localScale = baseLocalScale;
        }
        
        // Reset sprite color to original (fixes enemy stuck in attack flash color)
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;  // Ensure sprite is enabled
            spriteRenderer.color = originalColor;
        }
    }
}
