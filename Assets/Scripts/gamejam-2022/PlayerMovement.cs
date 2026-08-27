using UnityEngine;

/// <summary>
/// Handles player movement physics, knockback, and animator updates.
/// Discovers Rigidbody, Animator, and Collider via GetComponent.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public partial class PlayerMovement : MonoBehaviour
{
    private const float DefaultKnockbackForce = 2.25f;
    private const float KnockbackDecay = 15f; // Softer recoil that returns control quickly
    private const float MaxKnockbackForce = 2.75f;
    private const float CollisionSkin = 0.02f;
    private const float EnemyStandOffGap = 0.4f;
    internal const float WallCollisionRadius = 0.68f;
    private const int MaxCollisionSlides = 2;

    private Rigidbody _body;
    private Animator _animator;
    private Collider _collider;
    private ShuffleWalkVisual _hopVisual;
    private PlayerStats _playerStats;
    private PlayerInputHandler _inputHandler;
    private int _enemyLayerMask;
    private int _wallLayerMask;
    private readonly RaycastHit[] _collisionHits = new RaycastHit[16];

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
    /// The Rigidbody used for physics.
    /// </summary>
    public Rigidbody Body => _body;

    /// <summary>
    /// Current ground-plane position of the player.
    /// </summary>
    public Vector2 Position =>
        _body != null ? _body.GroundPosition() : transform.position.ToGround();

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<Collider>();
        _hopVisual = GetComponentInChildren<ShuffleWalkVisual>();
        _playerStats = GetComponentInChildren<PlayerStats>(); // May be on child prefab
        _inputHandler = GetComponent<PlayerInputHandler>();
        _enemyLayerMask = LayerMask.GetMask("Enemy");
        _wallLayerMask = LayerMask.GetMask("Wall");

        // This collider is the player's solid navigation body. Trigger-based
        // pickups and projectiles still work because their own colliders are triggers.
        if (_collider != null)
            _collider.isTrigger = false;

        if (_body == null)
        {
            Debug.LogError("PlayerMovement: No Rigidbody found!");
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
        if (_body == null)
            return;

        // Decay knockback velocity over time
        if (_knockbackVelocity.sqrMagnitude > 0.01f)
        {
            _knockbackVelocity = Vector2.MoveTowards(
                _knockbackVelocity,
                Vector2.zero,
                KnockbackDecay * Time.fixedDeltaTime
            );
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

        _body.MoveGroundPosition(_body.GroundPosition() + ResolveNavigationCollisions(totalDelta));

        // Update animator
        UpdateAnimator(moveDir);
    }

    private void UpdateAnimator(Vector2 moveDir)
    {
        if (_animator == null)
            return;

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
        if (_body == null || direction == Vector2.zero)
            return;

        // Add to existing knockback, but keep the result in the small-recoil range.
        _knockbackVelocity += direction.normalized * force;

        if (_knockbackVelocity.magnitude > MaxKnockbackForce)
        {
            _knockbackVelocity = _knockbackVelocity.normalized * MaxKnockbackForce;
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
            _body.SetGroundPosition(position);
        }
        else
        {
            transform.position = position.ToWorld(transform.position.y);
        }
    }

    /// <summary>
    /// Stop all velocity immediately.
    /// </summary>
    public void StopMovement()
    {
        if (_body != null)
        {
            _body.linearVelocity = Vector3.zero;
        }
        _knockbackVelocity = Vector2.zero;
    }
}
