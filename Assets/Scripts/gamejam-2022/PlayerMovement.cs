using UnityEngine;

/// <summary>
/// Handles player movement physics, knockback, and animator updates.
/// Discovers Rigidbody2D, Animator, and Collider2D via GetComponent.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    private const float DefaultKnockbackForce = 3f;
    private const float KnockbackDecay = 12f; // Short recoil that returns control quickly
    private const float CollisionSkin = 0.02f;
    private const float EnemyStandOffGap = 0.4f;
    private const int MaxCollisionSlides = 2;

    private Rigidbody2D _body;
    private Animator _animator;
    private Collider2D _collider;
    private ShuffleWalkVisual _hopVisual;
    private PlayerStats _playerStats;
    private PlayerInputHandler _inputHandler;
    private int _enemyLayerMask;
    private ContactFilter2D _enemyContactFilter;
    private readonly RaycastHit2D[] _collisionHits = new RaycastHit2D[16];

    // Impulse-based knockback - additive velocity that decays naturally
    private Vector2 _knockbackVelocity;

    /// <summary>
    /// Whether the player is currently being knocked back (has significant knockback velocity).
    /// </summary>
    public bool IsKnockedBack => _knockbackVelocity.sqrMagnitude > 0.5f;

    /// <summary>
    /// Current knockback velocity magnitude.
    /// </summary>
    public float KnockbackMagnitude => _knockbackVelocity.magnitude;

    /// <summary>
    /// The Rigidbody2D used for physics.
    /// </summary>
    public Rigidbody2D Body => _body;

    /// <summary>
    /// Current position of the player.
    /// </summary>
    public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<Collider2D>();
        _hopVisual = GetComponentInChildren<ShuffleWalkVisual>();
        _playerStats = GetComponentInChildren<PlayerStats>();  // May be on child prefab
        _inputHandler = GetComponent<PlayerInputHandler>();
        _enemyLayerMask = LayerMask.GetMask("Enemy");
        _enemyContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _enemyLayerMask,
            useTriggers = false
        };

        // This collider is the player's solid navigation body. Trigger-based
        // pickups and projectiles still work because their own colliders are triggers.
        if (_collider != null)
            _collider.isTrigger = false;

        if (_body == null)
        {
            Debug.LogError("PlayerMovement: No Rigidbody2D found!");
        }
        if (_playerStats == null)
        {
            Debug.LogWarning("PlayerMovement: No PlayerStats found - using default speed!");
        }
    }

    /// <summary>
    /// Process movement for this physics frame.
    /// Should be called from FixedUpdate.
    /// </summary>
    /// <param name="rawInput">The raw input direction from input handler.</param>
    public void ProcessMovement(Vector2 rawInput)
    {
        if (_body == null) return;

        // Decay knockback velocity over time
        if (_knockbackVelocity.sqrMagnitude > 0.01f)
        {
            _knockbackVelocity = Vector2.MoveTowards(_knockbackVelocity, Vector2.zero, KnockbackDecay * Time.fixedDeltaTime);
        }
        else
        {
            _knockbackVelocity = Vector2.zero;
        }

        // Get movement direction from hop visual (handles animation sync) or use raw input
        Vector2 moveDir = _hopVisual != null ? _hopVisual.MovementDirection : rawInput;

        // Prevent faster diagonal movement, preserve analog magnitude
        float magnitude = moveDir.magnitude;
        if (magnitude > 1f)
        {
            moveDir = moveDir.normalized;
        }

        // Get speed from PlayerStats or use default (4 matches original scene value)
        float speed = _playerStats != null ? _playerStats.CurrentMovementSpeed : 4f;

        // Combine player movement with knockback - player always has full control
        Vector2 playerDelta = moveDir * speed * Time.fixedDeltaTime;
        Vector2 knockbackDelta = _knockbackVelocity * Time.fixedDeltaTime;
        Vector2 totalDelta = playerDelta + knockbackDelta;
        
        _body.MovePosition(_body.position + ResolveEnemyCollisions(totalDelta));

        // Update animator
        UpdateAnimator(moveDir);
    }

    /// <summary>
    /// Cast the player's body before moving and slide along enemies on contact.
    /// This prevents the kinematic player from transferring solver velocity to
    /// dynamic enemies while still allowing smooth movement around them.
    /// </summary>
    private Vector2 ResolveEnemyCollisions(Vector2 desiredDelta)
    {
        if (_collider == null || _enemyLayerMask == 0 || desiredDelta.sqrMagnitude < 0.000001f)
            return desiredDelta;

        Bounds bounds = _collider.bounds;
        Vector2 castCenter = bounds.center;
        Vector2 castSize = bounds.size;
        Vector2 resolvedDelta = Vector2.zero;
        Vector2 remainingDelta = desiredDelta;

        for (int i = 0; i < MaxCollisionSlides; i++)
        {
            float distance = remainingDelta.magnitude;
            if (distance < 0.0001f) break;

            Vector2 direction = remainingDelta / distance;
            if (!TryGetBlockingHit(
                    castCenter,
                    castSize,
                    direction,
                    distance + CollisionSkin + EnemyStandOffGap,
                    out RaycastHit2D hit))
            {
                resolvedDelta += remainingDelta;
                break;
            }

            float travelDistance = Mathf.Clamp(
                hit.distance - CollisionSkin - EnemyStandOffGap,
                0f,
                distance);
            Vector2 travel = direction * travelDistance;
            resolvedDelta += travel;
            castCenter += travel;

            Vector2 untraveled = direction * (distance - travelDistance);
            float intoSurface = Vector2.Dot(untraveled, hit.normal);
            if (intoSurface < 0f)
                untraveled -= hit.normal * intoSurface;

            remainingDelta = untraveled;
        }

        return resolvedDelta;
    }

    private bool TryGetBlockingHit(
        Vector2 castCenter,
        Vector2 castSize,
        Vector2 direction,
        float distance,
        out RaycastHit2D closestHit)
    {
        int hitCount = Physics2D.BoxCast(
            castCenter,
            castSize,
            _body.rotation,
            direction,
            _enemyContactFilter,
            _collisionHits,
            distance);

        closestHit = default;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D candidate = _collisionHits[i];
            if (candidate.collider == null || candidate.collider == _collider)
                continue;

            // Casts that begin in contact can report a zero-distance hit even
            // while moving away or tangentially. Ignore that contact so the
            // player can always disengage instead of becoming stuck.
            if (candidate.distance <= CollisionSkin)
            {
                Vector2 awayFromEnemy = castCenter - (Vector2)candidate.collider.bounds.center;
                if (Vector2.Dot(direction, awayFromEnemy) >= 0f)
                    continue;
            }

            if (candidate.distance < closestDistance)
            {
                closestDistance = candidate.distance;
                closestHit = candidate;
            }
        }

        return closestHit.collider != null;
    }

    private void UpdateAnimator(Vector2 moveDir)
    {
        if (_animator == null) return;

        _animator.SetFloat("Horizontal", moveDir.x);
        _animator.SetFloat("Vertical", moveDir.y);
        _animator.SetFloat("Speed", moveDir.sqrMagnitude);
    }

    /// <summary>
    /// Apply knockback impulse in the given direction.
    /// Adds to existing knockback velocity - multiple hits stack.
    /// </summary>
    /// <param name="direction">Direction to knock back (will be normalized).</param>
    public void ApplyKnockbackImpulse(Vector2 direction)
    {
        ApplyKnockbackImpulse(direction, DefaultKnockbackForce);
    }

    /// <summary>
    /// Apply knockback impulse with custom force.
    /// Adds to existing knockback velocity - multiple hits stack.
    /// </summary>
    /// <param name="direction">Direction to knock back (will be normalized).</param>
    /// <param name="force">Force magnitude to add.</param>
    public void ApplyKnockbackImpulse(Vector2 direction, float force)
    {
        if (_body == null || direction == Vector2.zero) return;

        // Add to existing knockback, but keep the result in the small-recoil range.
        _knockbackVelocity += direction.normalized * force;
        
        float maxKnockback = 4f;
        if (_knockbackVelocity.magnitude > maxKnockback)
        {
            _knockbackVelocity = _knockbackVelocity.normalized * maxKnockback;
        }
    }

    /// <summary>
    /// Teleport player to a position.
    /// </summary>
    /// <param name="position">World position to move to.</param>
    public void SetPosition(Vector2 position)
    {
        if (_body != null)
        {
            _body.position = position;
        }
        else
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }

    /// <summary>
    /// Stop all velocity immediately.
    /// </summary>
    public void StopMovement()
    {
        if (_body != null)
        {
            _body.linearVelocity = Vector2.zero;
        }
        _knockbackVelocity = Vector2.zero;
    }
}
