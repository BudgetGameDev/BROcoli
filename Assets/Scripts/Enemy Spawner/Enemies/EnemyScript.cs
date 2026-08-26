using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyScript : EnemyBase
{
    private const float MaxAttackLungeDistance = 0.42f;
    private const float MaxAttackPullBackDistance = 0.22f;

    [Header("Melee Attack")]
    [SerializeField]
    private float meleeRange = 0.25f; // Distance to trigger attack (very close range)

    [SerializeField]
    private float meleeAttackCooldown = 0.6f; // Time between attacks
    private float nextMeleeAttackTime = 0f;

    [Header("Attack Animation")]
    [SerializeField]
    private float attackWindupDuration = 0.28f; // Longer anticipation before striking

    [SerializeField]
    private float attackStrikeDuration = 0.14f; // Fast release after the charge

    [SerializeField]
    private float attackRecoverDuration = 0.26f;

    [SerializeField]
    private float attackPullBackDistance = 0.18f;

    [SerializeField]
    private float attackLungeDistance = 0.42f;

    [SerializeField]
    private Color attackFlashColor = Color.red; // Color flash on attack
    private bool isAttacking = false;
    private bool hasDamagedThisAttack = false; // Prevents double-damage per attack
    private float attackTimer = 0f;
    private int attackPhase = 0; // 0=idle, 1=windup, 2=strike, 3=recover
    private Vector3 attackStartPos;
    private Vector3 attackWindupPos;
    private Vector3 attackTargetPos;
    private Quaternion attackStartRotation;
    private Vector2 attackDirection;
    private float activeAttackReach;
    private Vector3 baseLocalScale;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Transform visualTransform;
    private EnemyWalkAnimation walkAnimation;

    [Header("Melee Audio")]
    [SerializeField]
    private ProceduralEnemyMeleeAudio meleeAudio;

    protected override void Awake()
    {
        base.Awake();
        walkAnimation = GetComponent<EnemyWalkAnimation>();

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
        if (visualRenderer != null && visualRenderer.transform != transform)
        {
            visualTransform = visualRenderer.transform;
            baseLocalScale = visualTransform.localScale;

            // Safety check: if scale is zero (shouldn't happen), use a reasonable default
            if (baseLocalScale.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning(
                    $"[EnemyScript] {name}: visualTransform '{visualTransform.name}' has zero scale! Using Vector3.one as fallback."
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
            // Never animate the Rigidbody/collider root. Color feedback can
            // still play, but moving or scaling this transform would move the
            // solid body directly through the player.
            visualTransform = null;
            baseLocalScale = Vector3.one;
        }
    }

    protected override void FixedUpdate()
    {
        if (player == null)
            return;

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

        // Don't move toward player during the brief, rate-limited hit recoil.
        if (isKnockedBack)
        {
            base.FixedUpdate();
            return;
        }

        Vector2 dir = (Vector2)player.position - rb.position;
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

        // Smooth acceleration towards target velocity
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            targetVel,
            acceleration * EnemyTimeScale * Time.fixedDeltaTime
        );

        // Apply separation AFTER movement (so it can push away)
        base.FixedUpdate();
    }

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

        LockBodyForAttack();
        walkAnimation?.SetAttackOverride(true);

        isAttacking = true;
        hasDamagedThisAttack = false; // Reset damage flag for new attack
        attackPhase = 1; // Start with windup
        attackTimer = 0f;
        nextMeleeAttackTime = Time.time + meleeAttackCooldown / Mathf.Max(0.1f, EnemyTimeScale);
        // Calculate lunge direction toward player
        attackDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        activeAttackReach = GetAttackReach();

        if (visualTransform != null)
        {
            attackStartPos = visualTransform.localPosition;
            attackStartRotation = visualTransform.localRotation;

            // attackDirection and attackLungeDistance are world-space values.
            // Convert them through the renderer's parent before changing its
            // localPosition; imported FBX scales otherwise magnify the lunge.
            Vector3 worldLunge = (Vector3)(attackDirection * activeAttackReach);
            Vector3 worldPullBack = (Vector3)(
                -attackDirection
                * Mathf.Clamp(attackPullBackDistance, 0f, MaxAttackPullBackDistance)
            );
            Vector3 localLunge =
                visualTransform.parent != null
                    ? visualTransform.parent.InverseTransformVector(worldLunge)
                    : worldLunge;
            Vector3 localPullBack =
                visualTransform.parent != null
                    ? visualTransform.parent.InverseTransformVector(worldPullBack)
                    : worldPullBack;
            attackWindupPos = attackStartPos + localPullBack;
            attackTargetPos = attackStartPos + localLunge;
        }
    }

    private void UpdateAttackAnimation()
    {
        if (!isAttacking)
            return;

        attackTimer += Time.deltaTime * EnemyTimeScale;

        switch (attackPhase)
        {
            case 1: // Windup - pull back slightly and prepare
                float windupT = attackTimer / attackWindupDuration;
                if (windupT >= 1f)
                {
                    if (visualTransform != null)
                    {
                        visualTransform.localPosition = attackWindupPos;
                        visualTransform.localRotation = attackStartRotation;
                        visualTransform.localScale = baseLocalScale;
                    }
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

    private void PerformMeleeAttack()
    {
        // The player may have moved away or around the enemy during windup.
        // In that case the completed animation is a miss and causes no damage.
        if (!IsPlayerWithinAttackContact(activeAttackReach, attackDirection))
            return;

        // Deal damage to player - only proceed if damage was actually dealt
        var playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            // Calculate knockback direction (away from enemy)
            Vector2 knockbackDir = (
                (Vector2)player.position - (Vector2)transform.position
            ).normalized;

            if (playerController.TakeMeleeDamage(Damage, knockbackDir))
            {
                // Play melee sound
                if (meleeAudio != null)
                {
                    meleeAudio.PlayMeleeSound();
                }
            }
        }
    }

    /// <summary>
    /// Check if enemy can start a new attack (not attacking, cooldown expired)
    /// </summary>
    private bool CanStartAttack()
    {
        return !isAttacking && !isKnockedBack && Time.time >= nextMeleeAttackTime;
    }

    private bool IsPlayerInAttackStartRange()
    {
        // Collider gap works for every wave scale. A centre-distance check does
        // not: a smaller enemy could begin an attack that cannot visually reach.
        return IsPlayerWithinColliderGap(GetAttackReach());
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

        // A real weapon hit interrupts the telegraphed attack. Restore the
        // visual first, then release the pinned body so recoil is immediate.
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
        attackDirection = Vector2.zero;
        activeAttackReach = 0f;
        walkAnimation?.SetAttackOverride(false);

        // Reset visual state - critical for fixing invisible enemies!
        if (visualTransform != null)
        {
            // Don't reset localPosition - preserve prefab's Z offset for 3D models

            // Safety: ensure we never set zero scale
            if (baseLocalScale.sqrMagnitude < 0.0001f)
            {
                baseLocalScale = Vector3.one;
                Debug.LogWarning(
                    $"[EnemyScript.ResetForPool] {name}: baseLocalScale was zero, using Vector3.one"
                );
            }
            visualTransform.localScale = baseLocalScale;
        }

        // Reset sprite color to original (fixes enemy stuck in attack flash color)
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true; // Ensure sprite is enabled
            spriteRenderer.color = originalColor;
        }
    }
}
