using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyScript : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float meleeRange = 0.9f;        // Distance to player to trigger attack
    [SerializeField] private float meleeAttackCooldown = 0.5f; // Time between attacks
    private float nextMeleeAttackTime = 0f;
    
    [Header("Attack Animation")]
    [SerializeField] private float attackWindupDuration = 0.15f;  // Time to pull back before striking
    [SerializeField] private float attackStrikeDuration = 0.1f;   // Time for the lunge forward
    [SerializeField] private float attackRecoverDuration = 0.2f;  // Time to return to normal
    [SerializeField] private float attackLungeDistance = 0.4f;    // How far to lunge toward player
    [SerializeField] private float attackScaleBoost = 1.3f;       // Scale up during attack
    [SerializeField] private Color attackFlashColor = Color.red;  // Color flash on attack
    private bool isAttacking = false;
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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Try to get melee audio component if not assigned
        if (meleeAudio == null)
            meleeAudio = GetComponent<ProceduralEnemyMeleeAudio>();
        
        // Find visual transform and sprite renderer for attack animation
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

    void FixedUpdate()
    {
        if (player == null) return;
        
        // Don't move toward player during knockback
        if (isKnockedBack)
        {
            // Still apply separation during knockback
            base.FixedUpdate();
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
        
        // Update attack animation
        UpdateAttackAnimation();
        
        // Distance-based melee attack (only start if not already attacking)
        if (player != null && !isAttacking)
        {
            float distToPlayer = Vector2.Distance(transform.position, player.position);
            if (distToPlayer <= meleeRange && Time.time >= nextMeleeAttackTime)
            {
                StartAttackAnimation();
            }
        }
    }
    
    private void StartAttackAnimation()
    {
        if (player == null) return;
        
        isAttacking = true;
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
                    // Deal damage at the start of strike phase
                    PerformMeleeAttack();
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
                
            case 2: // Strike - lunge forward
                float strikeT = attackTimer / attackStrikeDuration;
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
        if (other.CompareTag("Player") && Time.time >= nextMeleeAttackTime && !isAttacking)
        {
            StartAttackAnimation();
        }
    }
}
