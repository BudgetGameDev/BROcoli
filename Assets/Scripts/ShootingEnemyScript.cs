using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ShootingEnemyScript : EnemyBase
{
    [Header("Shooting")]
    public float stopDistance = 6f;          // stop moving when within this distance from player
    public float fireRate = 1.0f;            // shots per second (1 = one shot per second)
    public float projectileDamage = 10f;
    public GameObject projectilePrefab;
    public Transform shootPoint;             // optional: where bullets spawn (defaults to this transform)

    [Header("Projectile Spawn Offset")]
    [SerializeField] private float projectileSpawnForwardOffset = 0.35f; // Forward offset from body
    [SerializeField] private float projectileSpawnSideOffset = 0.2f;     // Side offset from body
    [SerializeField] private float projectileVisualHeight = -0.5f;       // Z offset for visual "height"

    [Header("Audio")]
    [SerializeField] private ProceduralEnemyGunAudio gunAudio;

    private float nextShootTime = 0f;

    void Start()
    {
        if (shootPoint == null)
            shootPoint = transform;
        
        // Try to get gun audio component if not assigned
        if (gunAudio == null)
            gunAudio = GetComponent<ProceduralEnemyGunAudio>();
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

        Vector2 toPlayer = (Vector2)player.position - rb.position;
        float dist = toPlayer.magnitude;

        // If far away -> move towards player
        if (dist > stopDistance)
        {
            if (dist < 0.0001f) return;

            Vector2 dir = toPlayer / dist; // normalized
            Vector2 targetVel = dir * Speed;

            // Smooth acceleration towards target velocity
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVel, acceleration * Time.fixedDeltaTime);
        }
        else if (dist < playerSeparationRadius)
        {
            // Too close to player - move away
            Vector2 dir = -toPlayer / dist; // away from player
            float urgency = 1f - (dist / playerSeparationRadius);
            Vector2 targetVel = dir * Speed * urgency;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVel, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            // Within stop range but not too close -> stop moving
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);
        }
        
        // Apply separation AFTER movement
        base.FixedUpdate();
    }

    public override void Update()
    {
        // Shooting logic (only shoot when within stop distance)
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        if (distToPlayer <= stopDistance)
        {
            TryShoot();
        }
        base.Update();
    }

    void TryShoot()
    {
        if (projectilePrefab == null) return;
        if (fireRate <= 0f) return;
        if (Time.time < nextShootTime) return;
        nextShootTime = Time.time + (1f / fireRate);
        
        // Calculate direction to player
        Vector2 direction = ((Vector2)player.position - (Vector2)shootPoint.position).normalized;
        
        // Calculate spawn position with offset (similar to player projectile spawning)
        Vector2 spawnPos2D = (Vector2)shootPoint.position;
        
        // Offset to the side (perpendicular to firing direction)
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        spawnPos2D += perpendicular * projectileSpawnSideOffset;
        
        // Offset forward in the firing direction (away from body)
        spawnPos2D += direction * projectileSpawnForwardOffset;
        
        // Use Z position for visual "height" - this doesn't affect 2D collision
        Vector3 spawnPos = new Vector3(spawnPos2D.x, spawnPos2D.y, projectileVisualHeight);
        
        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        EnemyProjectile ep = proj.GetComponent<EnemyProjectile>();
        if (ep != null)
        {
            ep.Init(direction);
        }
        
        // Play procedural gun sound
        if (gunAudio != null)
        {
            gunAudio.PlayGunSound();
        }
    }
}