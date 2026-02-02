using UnityEngine;

/// <summary>
/// Handles player movement physics, knockback, and animator updates.
/// Discovers Rigidbody2D, Animator, and Collider2D via GetComponent.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    private const float DefaultKnockbackForce = 12f;
    private const float KnockbackDecay = 8f; // How fast knockback velocity decays per second

    private Rigidbody2D _body;
    private Animator _animator;
    private Collider2D _collider;
    private ShuffleWalkVisual _hopVisual;
    private PlayerStats _playerStats;
    private PlayerInputHandler _inputHandler;

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
        
        _body.MovePosition(_body.position + totalDelta);

        // Update animator
        UpdateAnimator(moveDir);
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

        // Add to existing knockback - multiple hits push harder
        _knockbackVelocity += direction.normalized * force;
        
        // Cap max knockback velocity to prevent absurd speeds
        float maxKnockback = 25f;
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
