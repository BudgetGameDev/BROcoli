using Pooling;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ShootingEnemyScript : EnemyBase
{
    [Header("Shooting")]
    public float stopDistance = 6f; // stop moving when within this distance from player
    public float fireRate = 0.125f; // shots per second (0.125 = one shot per 8 seconds)
    public float projectileDamage = 10f;
    public GameObject projectilePrefab;
    public Transform shootPoint; // optional: where bullets spawn (defaults to this transform)

    [Header("Projectile Spawn Offset")]
    [SerializeField]
    private float projectileSpawnForwardOffset = 0.35f; // Forward offset from body

    [SerializeField]
    private float projectileSpawnSideOffset = 0.2f; // Side offset from body

    [SerializeField]
    private float projectileVisualHeight = 0.5f; // Y offset for visual height

    [Header("Aim Assistance")]
    [Tooltip(
        "How strongly shots lead the player's current movement (0 = direct, 1 = full intercept)."
    )]
    [SerializeField, Range(0f, 1f)]
    private float movementPrediction = 0.8f;

    [Tooltip("Maximum seconds of movement a shot may predict, keeping long-range shots dodgeable.")]
    [SerializeField, Min(0f)]
    private float maxPredictionTime = 2f;

    [Header("Audio")]
    [SerializeField]
    private ProceduralEnemyGunAudio gunAudio;

    [SerializeField]
    private ProceduralEnemyGunAudio.EnemyGunSoundType gunSoundType = ProceduralEnemyGunAudio
        .EnemyGunSoundType
        .Sneeze;

    private float nextShootTime = 0f;
    private EnemyProjectile _cachedProjectilePrefab;

    void Start()
    {
        if (shootPoint == null)
            shootPoint = transform;

        // Try to get gun audio component if not assigned
        if (gunAudio == null)
            gunAudio = GetComponent<ProceduralEnemyGunAudio>();

        // Cache projectile prefab component for pooling
        if (projectilePrefab != null)
            _cachedProjectilePrefab = projectilePrefab.GetComponent<EnemyProjectile>();
    }

    protected override void FixedUpdate()
    {
        if (player == null)
            return;

        // Don't move toward player during knockback
        if (isKnockedBack)
        {
            // Still apply separation during knockback
            base.FixedUpdate();
            return;
        }

        Vector2 toPlayer = player.position.ToGround() - rb.GroundPosition();
        float dist = toPlayer.magnitude;

        // If far away -> move towards player
        if (dist > stopDistance)
        {
            if (dist < 0.0001f)
                return;

            Vector2 dir = toPlayer / dist; // normalized
            Vector2 targetVel = dir * Speed * EnemyTimeScale;

            // Smooth acceleration towards target velocity
            AccelerateTowards(targetVel);
        }
        else if (dist < playerSeparationRadius)
        {
            // Too close to player - move away
            Vector2 dir = -toPlayer / dist; // away from player
            float urgency = 1f - (dist / playerSeparationRadius);
            Vector2 targetVel = dir * Speed * urgency * EnemyTimeScale;
            AccelerateTowards(targetVel);
        }
        else
        {
            // Within stop range but not too close -> stop moving
            AccelerateTowards(Vector2.zero);
        }

        // Apply separation AFTER movement
        base.FixedUpdate();
    }

    public override void Update()
    {
        if (player == null)
            return;

        // Shooting logic (only shoot when within stop distance)
        float distToPlayer = GroundPlane.GroundDistance(transform.position, player.position);
        if (distToPlayer <= stopDistance)
        {
            TryShoot();
        }
        base.Update();
    }

    void TryShoot()
    {
        if (projectilePrefab == null)
            return;
        if (player == null)
            return;
        if (fireRate <= 0f)
            return;
        if (Time.time < nextShootTime)
            return;

        // Establish the actual projectile origin first. Aiming from the enemy
        // centre and then adding a side offset made every shot travel along a
        // parallel line beside the player.
        Vector2 shooterPosition = shootPoint.position.ToGround();
        Vector2 playerPosition = player.position.ToGround();
        Vector2 directDirection = playerPosition - shooterPosition;
        if (directDirection.sqrMagnitude < 0.0001f)
            return;
        directDirection.Normalize();

        Vector2 perpendicular = new Vector2(-directDirection.y, directDirection.x);
        Vector2 spawnPos2D =
            shooterPosition
            + perpendicular * projectileSpawnSideOffset
            + directDirection * projectileSpawnForwardOffset;

        if (
            !ProjectileWallCollision.HasClearLine(
                spawnPos2D.ToWorld(projectileVisualHeight),
                playerPosition.ToWorld(projectileVisualHeight)
            )
        )
            return;

        nextShootTime = Time.time + (1f / fireRate) / Mathf.Max(0.1f, EnemyTimeScale);

        float projectileSpeed =
            _cachedProjectilePrefab != null ? _cachedProjectilePrefab.speed : 0f;
        Vector2 playerVelocity = GetPlayerGroundVelocity();
        Vector2 direction = CalculateAimDirection(
            spawnPos2D,
            playerPosition,
            playerVelocity,
            projectileSpeed * Mathf.Max(0.1f, EnemyTimeScale),
            movementPrediction,
            maxPredictionTime
        );

        // Lift the projectile off the ground for its visual height.
        Vector3 spawnPos = spawnPos2D.ToWorld(projectileVisualHeight);

        // Try to get projectile from pool first
        EnemyProjectile ep = null;
        if (_cachedProjectilePrefab != null)
        {
            ep = PoolManager.Instance?.GetProjectile(
                _cachedProjectilePrefab,
                spawnPos,
                Quaternion.identity
            );
        }

        // Fallback to instantiate if pool not available
        if (ep == null)
        {
            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            ep = proj.GetComponent<EnemyProjectile>();
        }

        if (ep != null)
        {
            ep.Init(direction);
        }

        // Play procedural gun sound with configured type
        if (gunAudio != null)
        {
            gunAudio.PlayGunSound(gunSoundType);
        }
    }

    private Vector2 GetPlayerGroundVelocity()
    {
        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        if (playerBody != null)
        {
            Vector2 velocity = playerBody.GroundVelocity();
            if (velocity.sqrMagnitude > 0.01f)
                return velocity;
        }

        // MovePosition-driven bodies do not report a useful velocity on every
        // physics configuration, so fall back to the player's current input.
        PlayerController controller = player.GetComponent<PlayerController>();
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (controller == null || stats == null)
            return Vector2.zero;

        return Vector2.ClampMagnitude(controller.RawInput, 1f) * stats.CurrentMovementSpeed;
    }

    /// <summary>Returns a direct or movement-led firing direction.</summary>
    public static Vector2 CalculateAimDirection(
        Vector2 origin,
        Vector2 targetPosition,
        Vector2 targetVelocity,
        float projectileSpeed,
        float predictionStrength,
        float maxLeadTime
    )
    {
        Vector2 toTarget = targetPosition - origin;
        if (toTarget.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        float speed = Mathf.Max(0f, projectileSpeed);
        float strength = Mathf.Clamp01(predictionStrength);
        if (speed < 0.0001f || strength <= 0f || targetVelocity.sqrMagnitude < 0.0001f)
            return toTarget.normalized;

        Vector2 predictedVelocity = targetVelocity * strength;
        float a = predictedVelocity.sqrMagnitude - speed * speed;
        float b = 2f * Vector2.Dot(toTarget, predictedVelocity);
        float c = toTarget.sqrMagnitude;
        float interceptTime = -1f;

        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) > 0.0001f)
            {
                float linearTime = -c / b;
                if (linearTime > 0f)
                    interceptTime = linearTime;
            }
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                float root = Mathf.Sqrt(discriminant);
                float first = (-b - root) / (2f * a);
                float second = (-b + root) / (2f * a);
                if (first > 0f && second > 0f)
                    interceptTime = Mathf.Min(first, second);
                else
                    interceptTime = Mathf.Max(first, second);
            }
        }

        // No exact intercept exists (for example, a faster fleeing target).
        // Leading by the direct travel time is still a better attempt than
        // firing at a position the player has already left.
        if (interceptTime <= 0f)
            interceptTime = Mathf.Sqrt(c) / speed;

        interceptTime = Mathf.Min(interceptTime, Mathf.Max(0f, maxLeadTime));
        Vector2 aim = toTarget + predictedVelocity * interceptTime;
        return aim.sqrMagnitude > 0.0001f ? aim.normalized : toTarget.normalized;
    }
}
