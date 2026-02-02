using UnityEngine;

/// <summary>
/// Main player controller facade. Coordinates all player subsystems and provides
/// backwards-compatible public API for external scripts.
/// 
/// All functionality is delegated to specialized components:
/// - PlayerInputHandler: Input collection and smoothing
/// - PlayerMovement: Physics, knockback, animation
/// - PlayerCombat: Enemy detection, targeting, weapons
/// - PlayerDamageHandler: Damage, invincibility, death
/// - PlayerAudioHandler: All audio playback
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerDamageHandler))]
[RequireComponent(typeof(PlayerAudioHandler))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// Available weapon types (kept here for backwards compatibility).
    /// </summary>
    public enum WeaponType
    {
        Projectile,
        SanitizerSpray
    }

    [Header("Weapon Selection")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.SanitizerSpray;

    // Component references (discovered at runtime)
    private PlayerInputHandler _inputHandler;
    private PlayerMovement _movement;
    private PlayerCombat _combat;
    private PlayerDamageHandler _damageHandler;
    private PlayerAudioHandler _audioHandler;
    private PlayerStats _playerStats;

    // Backwards compatibility: public fields that external scripts may access
    /// <summary>
    /// Input direction for animation sync. Returns smoothed input for consistent animation behavior.
    /// (Original code returned smoothed input via ExecMove(), so this maintains that behavior)
    /// </summary>
    public Vector2 RawInput => _inputHandler?.SmoothedInput ?? Vector2.zero;

    /// <summary>
    /// Movement vector (for backwards compatibility with external scripts).
    /// </summary>
    public Vector2 movement => _inputHandler?.SmoothedInput ?? Vector2.zero;

    /// <summary>
    /// Animator reference (for backwards compatibility).
    /// </summary>
    public Animator animator { get; private set; }

    private void Awake()
    {
        // Discover all components
        _inputHandler = GetComponent<PlayerInputHandler>();
        _movement = GetComponent<PlayerMovement>();
        _combat = GetComponent<PlayerCombat>();
        _damageHandler = GetComponent<PlayerDamageHandler>();
        _audioHandler = GetComponent<PlayerAudioHandler>();
        _playerStats = GetComponent<PlayerStats>();
        animator = GetComponent<Animator>();

        // Sync weapon type to combat component
        if (_combat != null)
        {
            _combat.CurrentWeapon = (PlayerCombat.WeaponType)(int)currentWeapon;
        }
    }

    private void Start()
    {
        // Spawn player at world center
        SpawnAtCenter();
    }

    private void Update()
    {
        if (_damageHandler != null && _damageHandler.IsGameOver) return;

        // Sync weapon type changes
        if (_combat != null)
        {
            _combat.CurrentWeapon = (PlayerCombat.WeaponType)(int)currentWeapon;
        }
    }

    private void FixedUpdate()
    {
        if (_damageHandler != null && _damageHandler.IsGameOver) return;

        // Handle combat (enemy detection and attacking)
        _combat?.HandleCombat();

        // Update input BEFORE using it (same timing as original ExecMove() call)
        _inputHandler?.UpdateInput();

        // Process movement with current input (knockback is handled internally)
        Vector2 input = _inputHandler?.RawInput ?? Vector2.zero;
        _movement?.ProcessMovement(input);

        // Update lava ambient based on Y position
        if (_movement != null && _audioHandler != null)
        {
            _audioHandler.UpdateLavaAmbient(_movement.Position.y);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Collided with " + other.name);
        _damageHandler?.HandleCollision(other);
    }

    private void SpawnAtCenter()
    {
        Camera mainCam = Camera.main;
        Vector3 cameraOffset = Vector3.zero;
        
        if (mainCam != null)
        {
            cameraOffset = mainCam.transform.position - transform.position;
        }

        Vector3 spawnCenter = new Vector3(0f, 0f, transform.position.z);
        transform.position = spawnCenter;

        if (mainCam != null)
        {
            mainCam.transform.position = spawnCenter + cameraOffset;
        }
    }

    #region Backwards Compatible Public API

    /// <summary>
    /// Check if game is over. Backwards compatible method.
    /// </summary>
    public bool getGameOver()
    {
        return _damageHandler?.IsGameOver ?? false;
    }

    /// <summary>
    /// Trigger game over. Backwards compatible method.
    /// </summary>
    public void setGameOver()
    {
        _damageHandler?.TriggerGameOver();
    }

    /// <summary>
    /// Apply melee damage to player. Backwards compatible method.
    /// </summary>
    /// <param name="damage">Amount of damage.</param>
    /// <returns>True if damage was applied.</returns>
    public bool TakeMeleeDamage(float damage)
    {
        return _damageHandler?.TakeMeleeDamage(damage) ?? false;
    }

/// <summary>
    /// Apply melee damage with knockback. Backwards compatible method.
    /// </summary>
    /// <param name="damage">Amount of damage.</param>
    /// <param name="knockbackDirection">Direction to knock player.</param>
    /// <returns>True if damage was applied.</returns>
    public bool TakeMeleeDamage(float damage, Vector2 knockbackDirection)
    {
        return _damageHandler?.TakeMeleeDamage(damage, knockbackDirection) ?? false;
    }

    /// <summary>
    /// Apply knockback force. Backwards compatible method.
    /// </summary>
    /// <param name="direction">Knockback direction.</param>
    public void ApplyKnockback(Vector2 direction)
    {
        _movement?.ApplyKnockbackImpulse(direction);
    }

    /// <summary>
    /// Legacy input update method. Now handled by PlayerInputHandler.
    /// Kept for backwards compatibility - does nothing.
    /// </summary>
    public void ExecMove()
    {
        // Input is now handled automatically by PlayerInputHandler
        // This method exists only for backwards compatibility
    }

    #endregion
}
