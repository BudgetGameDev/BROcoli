using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyScript : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float meleeRange = 1.5f;        // Distance to player to trigger attack
    [SerializeField] private float meleeAttackCooldown = 0.5f; // Time between attacks
    private float nextMeleeAttackTime = 0f;
    
    [Header("Melee Audio")]
    [SerializeField] private ProceduralEnemyMeleeAudio meleeAudio;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Try to get melee audio component if not assigned
        if (meleeAudio == null)
            meleeAudio = GetComponent<ProceduralEnemyMeleeAudio>();
    }

    void FixedUpdate()
    {
        if (player == null) return;

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
        
        // Distance-based melee attack
        if (player != null)
        {
            float distToPlayer = Vector2.Distance(transform.position, player.position);
            if (distToPlayer <= meleeRange && Time.time >= nextMeleeAttackTime)
            {
                PerformMeleeAttack();
            }
        }
    }
    
    private void PerformMeleeAttack()
    {
        // Deal damage to player - only proceed if damage was actually dealt
        var playerController = player.GetComponent<PlayerController>();
        if (playerController != null && playerController.TakeMeleeDamage(Damage))
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

    void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger immediate attack on contact
        if (other.CompareTag("Player") && Time.time >= nextMeleeAttackTime)
        {
            PerformMeleeAttack();
        }
    }
}
