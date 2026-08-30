using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles enemy detection, targeting, and weapon firing.
/// Supports both projectile and sanitizer spray weapons.
/// </summary>
public partial class PlayerCombat : MonoBehaviour
{
    /// <summary>
    /// Available weapon types.
    /// </summary>
    public enum WeaponType
    {
        Projectile,
        SanitizerSpray,
    }

    private const float DefaultEnemyDetectionRadius = 12f;
    private const float DefaultInitialAttackDelay = 0.75f;
    private const float ProjectileSpawnForwardOffset = 0.4f;
    private const float ProjectileSpawnSideOffset = 0.25f;
    private const float ProjectileVisualHeight = 0.5f;
    private const string ProjectilePrefabPath = "CursedDevolpmentStudioAss Assets/Projectile";

    private PlayerStats _playerStats;
    private PlayerMovement _playerMovement;
    private SanitizerSpray _sanitizerSpray;
    private ProceduralGunAudio _gunAudio;
    private GameObject _projectilePrefab;
    private LayerMask _enemyLayer;

    private float _nextAllowedAttack;
    private WeaponType _currentWeapon = WeaponType.SanitizerSpray;

    /// <summary>
    /// The currently equipped weapon type.
    /// </summary>
    public WeaponType CurrentWeapon
    {
        get => _currentWeapon;
        set => _currentWeapon = value;
    }

    /// <summary>
    /// Radius for detecting enemies.
    /// </summary>
    public float EnemyDetectionRadius { get; set; } = DefaultEnemyDetectionRadius;

    private void Awake()
    {
        _playerStats = GetComponentInChildren<PlayerStats>(); // May be on child prefab
        _playerMovement = GetComponent<PlayerMovement>();
        _sanitizerSpray = GetComponentInChildren<SanitizerSpray>(); // May be on child object
        _gunAudio = GetComponentInChildren<ProceduralGunAudio>();

        // Load projectile prefab from Resources
        _projectilePrefab = Resources.Load<GameObject>(ProjectilePrefabPath);
        if (_projectilePrefab == null)
        {
            Debug.LogWarning(
                $"PlayerCombat: Could not load projectile prefab from '{ProjectilePrefabPath}'"
            );
        }

        // Get enemy layer mask programmatically
        _enemyLayer = LayerMask.GetMask("Enemy");
        if (_enemyLayer == 0)
        {
            Debug.LogWarning(
                "PlayerCombat: 'Enemy' layer not found! Combat detection will not work."
            );
        }
    }

    private void Start()
    {
        // Set initial attack delay so player doesn't immediately fire on spawn
        _nextAllowedAttack = Time.time + DefaultInitialAttackDelay;
    }

    /// <summary>
    /// Check for enemies and attack if possible.
    /// Should be called from FixedUpdate or Update.
    /// </summary>
    public void HandleCombat()
    {
        if (Time.time < _nextAllowedAttack)
            return;
        if (_playerMovement == null || _playerMovement.Body == null)
            return;

        Vector2 playerPos = _playerMovement.Position;

        // Use spray range for detection when using spray weapon
        float detectionRange = EnemyDetectionRadius;
        if (_currentWeapon == WeaponType.SanitizerSpray && _sanitizerSpray != null)
        {
            detectionRange = _sanitizerSpray.SprayRange;
        }

        Collider[] hits = GroundPlane.OverlapCircleAll(playerPos, detectionRange, _enemyLayer);
        if (hits.Length == 0)
            return;

        Transform target = FindTarget(hits, playerPos, detectionRange);
        if (target == null)
            return;

        if (_playerStats == null)
        {
            Debug.LogWarning("PlayerCombat: PlayerStats is null - cannot attack!");
            return;
        }

        _nextAllowedAttack = Time.time + _playerStats.CurrentAttackSpeed;
        AttackTarget(target);
    }

    /// <summary>
    /// Calculate total damage to all enemies if aiming in given direction.
    /// Uses same physics model as SprayDamageHandler.
    /// </summary>
    /// <param name="emitPos">The nozzle emission position (not player position)</param>
    private float CalculateSprayDamage(
        List<(Transform t, EnemyBase e, Vector2 predicted, float dist)> enemies,
        Vector2 emitPos,
        Vector2 aimDir,
        float halfAngle,
        float sprayRange
    )
    {
        float totalDamage = 0f;

        foreach (var enemy in enemies)
        {
            Vector2 toEnemy = (enemy.predicted - emitPos);
            float dist = toEnemy.magnitude;
            if (dist < 0.01f || dist > sprayRange)
                continue;

            toEnemy /= dist;
            float angle = Vector2.Angle(aimDir, toEnemy);

            if (angle <= halfAngle)
            {
                // Same falloff as SprayDamageHandler
                float distanceFalloff = 1f - Mathf.Pow(dist / sprayRange, 0.7f);
                float angleFalloff = 1f - Mathf.Pow(angle / halfAngle, 0.5f);
                totalDamage += distanceFalloff * angleFalloff;
            }
        }

        return totalDamage;
    }

    private void AttackTarget(Transform target)
    {
        switch (_currentWeapon)
        {
            case WeaponType.SanitizerSpray:
                FireSprayAt(target);
                break;
            case WeaponType.Projectile:
            default:
                FireProjectileAt(target);
                break;
        }
    }

    private void FireSprayAt(Transform target)
    {
        if (target == null || _sanitizerSpray == null)
            return;

        // Pass the target to SanitizerSpray - it handles aim tracking,
        // direction calculation, validation, and firing when ready
        _sanitizerSpray.FireSprayBurstAtTarget(target);
    }

    private void FireProjectileAt(Transform target)
    {
        if (target == null || _projectilePrefab == null)
            return;

        Collider col = target.GetComponent<Collider>();
        if (col == null)
            return;

        Vector2 targetPoint = col.bounds.center.ToGround();
        Vector2 playerPos = transform.position.ToGround();
        Vector2 direction = (targetPoint - playerPos).normalized;

        // Calculate spawn position
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 spawnPos2D =
            playerPos
            + (perpendicular * ProjectileSpawnSideOffset)
            + (direction * ProjectileSpawnForwardOffset);

        Vector3 spawnPos = spawnPos2D.ToWorld(ProjectileVisualHeight);

        GameObject proj = Object.Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
        Projectile projectile = proj?.GetComponent<Projectile>();
        if (projectile != null && _playerStats != null)
        {
            projectile.Init(direction, _playerStats.CurrentDamage);
        }

        _gunAudio?.PlayGunSound();
    }
}
