using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles enemy detection, targeting, and weapon firing.
/// Supports both projectile and sanitizer spray weapons.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    /// <summary>
    /// Available weapon types.
    /// </summary>
    public enum WeaponType
    {
        Projectile,
        SanitizerSpray
    }

    private const float DefaultEnemyDetectionRadius = 12f;
    private const float DefaultInitialAttackDelay = 0.75f;
    private const float ProjectileSpawnForwardOffset = 0.4f;
    private const float ProjectileSpawnSideOffset = 0.25f;
    private const float ProjectileVisualHeight = -0.5f;
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
        _playerStats = GetComponentInChildren<PlayerStats>();  // May be on child prefab
        _playerMovement = GetComponent<PlayerMovement>();
        _sanitizerSpray = GetComponentInChildren<SanitizerSpray>();  // May be on child object
        _gunAudio = GetComponentInChildren<ProceduralGunAudio>();

        // Load projectile prefab from Resources
        _projectilePrefab = Resources.Load<GameObject>(ProjectilePrefabPath);
        if (_projectilePrefab == null)
        {
            Debug.LogWarning($"PlayerCombat: Could not load projectile prefab from '{ProjectilePrefabPath}'");
        }

        // Get enemy layer mask programmatically
        _enemyLayer = LayerMask.GetMask("Enemy");
        if (_enemyLayer == 0)
        {
            Debug.LogWarning("PlayerCombat: 'Enemy' layer not found! Combat detection will not work.");
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
        if (Time.time < _nextAllowedAttack) return;
        if (_playerMovement == null || _playerMovement.Body == null) return;

        Vector2 playerPos = _playerMovement.Position;

        // Use spray range for detection when using spray weapon
        float detectionRange = EnemyDetectionRadius;
        if (_currentWeapon == WeaponType.SanitizerSpray && _sanitizerSpray != null)
        {
            detectionRange = _sanitizerSpray.SprayRange;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(playerPos, detectionRange, _enemyLayer);
        if (hits.Length == 0) return;

        Transform target = FindTarget(hits, playerPos, detectionRange);
        if (target == null) return;

        if (_playerStats == null)
        {
            Debug.LogWarning("PlayerCombat: PlayerStats is null - cannot attack!");
            return;
        }

        _nextAllowedAttack = Time.time + _playerStats.CurrentAttackSpeed;
        AttackTarget(target);
    }

    private Transform FindTarget(Collider2D[] hits, Vector2 playerPos, float range)
    {
        // Use smart targeting for spray weapon
        if (_currentWeapon == WeaponType.SanitizerSpray && _sanitizerSpray != null)
        {
            Transform sprayTarget = FindBestSprayTarget(hits, playerPos, range);
            if (sprayTarget != null) return sprayTarget;
        }

        // Fallback: find closest enemy (prefer real enemies over projectiles)
        return FindClosestEnemy(hits, playerPos);
    }

    private Transform FindClosestEnemy(Collider2D[] hits, Vector2 playerPos)
    {
        Transform closestEnemy = null;
        Transform closestProjectile = null;
        float closestEnemySqrDist = float.MaxValue;
        float closestProjectileSqrDist = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            float sqrDist = ((Vector2)hit.transform.position - playerPos).sqrMagnitude;
            EnemyBase enemyComponent = hit.GetComponent<EnemyBase>();

            if (enemyComponent != null)
            {
                if (sqrDist < closestEnemySqrDist)
                {
                    closestEnemySqrDist = sqrDist;
                    closestEnemy = hit.transform;
                }
            }
            else
            {
                if (sqrDist < closestProjectileSqrDist)
                {
                    closestProjectileSqrDist = sqrDist;
                    closestProjectile = hit.transform;
                }
            }
        }

        return closestEnemy ?? closestProjectile;
    }

    private Transform FindBestSprayTarget(Collider2D[] hits, Vector2 playerPos, float sprayRange)
    {
        if (hits == null || hits.Length == 0) return null;

        float sprayAngle = _sanitizerSpray?.SprayWidth ?? 60f;
        float halfAngle = sprayAngle * 0.5f;
        float particleSpeed = _sanitizerSpray?.GetParticleSpeed()
            ?? (SpraySettings.BaseSprayRange / SpraySettings.ParticleLifetimeBase);

        // Collect enemies with predicted positions using dynamic velocity-based prediction
        var enemies = new List<(Transform t, EnemyBase e, Vector2 predicted, float dist)>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null) continue;

            Vector2 enemyPos = (Vector2)hit.bounds.center;
            float dist = Vector2.Distance(playerPos, enemyPos);

            if (dist <= sprayRange && dist > 0.1f)
            {
                Vector2 predicted = GetPredictedEnemyPosition(enemy, enemyPos, dist, particleSpeed);
                enemies.Add((hit.transform, enemy, predicted, dist));
            }
        }

        if (enemies.Count == 0) return null;
        if (enemies.Count == 1) return enemies[0].t;

        // Nozzle offset for accurate damage calculations
        float nozzleOffset = SpraySettings.HandOffset + SpraySettings.NozzleLocalPos.x;

        // Find optimal aim direction for maximum total damage
        Transform bestTarget = null;
        float bestTotalDamage = float.MinValue;

        // Sample directions: toward each enemy
        foreach (var primary in enemies)
        {
            // Calculate aim direction from PLAYER to predicted target (consistent with SprayHandVisuals)
            Vector2 aimDir = (primary.predicted - playerPos).normalized;
            // Nozzle position is along the aim ray from player
            Vector2 nozzlePos = playerPos + aimDir * nozzleOffset;
            float totalDamage = CalculateSprayDamage(enemies, nozzlePos, aimDir, halfAngle, sprayRange);

            if (totalDamage > bestTotalDamage)
            {
                bestTotalDamage = totalDamage;
                bestTarget = primary.t;
            }
        }

        // Also sample directions BETWEEN enemy pairs (cluster centers)
        for (int i = 0; i < enemies.Count && i < 5; i++)
        {
            for (int j = i + 1; j < enemies.Count && j < 5; j++)
            {
                Vector2 midpoint = (enemies[i].predicted + enemies[j].predicted) * 0.5f;
                Vector2 aimDir = (midpoint - playerPos).normalized;
                Vector2 nozzlePos = playerPos + aimDir * nozzleOffset;
                float totalDamage = CalculateSprayDamage(enemies, nozzlePos, aimDir, halfAngle, sprayRange);

                if (totalDamage > bestTotalDamage)
                {
                    bestTotalDamage = totalDamage;
                    bestTarget = enemies[i].dist < enemies[j].dist ? enemies[i].t : enemies[j].t;
                }
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Calculate predicted enemy position using dynamic velocity-based prediction.
    /// - Stationary enemies: no prediction (aim dead center)
    /// - Close-range enemies: no prediction (aim dead center)
    /// - Moving enemies: prediction scales with velocity
    /// </summary>
    private Vector2 GetPredictedEnemyPosition(EnemyBase enemy, Vector2 currentPos, float distance, float particleSpeed)
    {
        // Close-range: aim dead center (no prediction)
        if (distance < SpraySettings.CloseRangeThreshold)
            return currentPos;
        
        // No rigidbody or stationary: aim dead center
        if (enemy.rb == null)
            return currentPos;
        
        Vector2 velocity = enemy.rb.linearVelocity;
        float enemySpeed = velocity.magnitude;
        
        // Stationary or nearly stationary: aim dead center
        if (enemySpeed < 0.5f)
            return currentPos;
        
        // Dynamic prediction: scale with enemy speed
        // At reference speed, use full base prediction time
        // Faster enemies get more prediction, slower get less
        float speedRatio = enemySpeed / SpraySettings.PredictionReferenceSpeed;
        float predictionTime = SpraySettings.BasePredictionTime * speedRatio;
        
        // Also factor in particle travel time for very fast enemies
        float travelTime = distance / particleSpeed;
        predictionTime = Mathf.Min(predictionTime + travelTime * 0.5f, SpraySettings.MaxPredictionTime);
        
        return currentPos + velocity * predictionTime;
    }

    /// <summary>
    /// Calculate total damage to all enemies if aiming in given direction.
    /// Uses same physics model as SprayDamageHandler.
    /// </summary>
    /// <param name="emitPos">The nozzle emission position (not player position)</param>
    private float CalculateSprayDamage(
        List<(Transform t, EnemyBase e, Vector2 predicted, float dist)> enemies,
        Vector2 emitPos, Vector2 aimDir, float halfAngle, float sprayRange)
    {
        float totalDamage = 0f;

        foreach (var enemy in enemies)
        {
            Vector2 toEnemy = (enemy.predicted - emitPos);
            float dist = toEnemy.magnitude;
            if (dist < 0.01f || dist > sprayRange) continue;

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
        if (target == null || _sanitizerSpray == null) return;
        
        // Pass the target to SanitizerSpray - it handles aim tracking,
        // direction calculation, validation, and firing when ready
        _sanitizerSpray.FireSprayBurstAtTarget(target);
    }

    private void FireProjectileAt(Transform target)
    {
        if (target == null || _projectilePrefab == null) return;

        Collider2D col = target.GetComponent<Collider2D>();
        if (col == null) return;

        Vector2 targetPoint = col.bounds.center;
        Vector2 playerPos = (Vector2)transform.position;
        Vector2 direction = (targetPoint - playerPos).normalized;

        // Calculate spawn position
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 spawnPos2D = playerPos
            + (perpendicular * ProjectileSpawnSideOffset)
            + (direction * ProjectileSpawnForwardOffset);

        Vector3 spawnPos = new Vector3(spawnPos2D.x, spawnPos2D.y, ProjectileVisualHeight);

        GameObject proj = Object.Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
        Projectile projectile = proj?.GetComponent<Projectile>();
        if (projectile != null && _playerStats != null)
        {
            projectile.Init(direction, _playerStats.CurrentDamage);
        }

        _gunAudio?.PlayGunSound();
    }
}
