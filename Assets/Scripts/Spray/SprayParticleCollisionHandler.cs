using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles particle collision events for spray damage.
/// Attach this to the particle system GameObject to receive OnParticleCollision callbacks.
/// Deals damage immediately when particles hit enemies - no delay.
/// </summary>
public class SprayParticleCollisionHandler : MonoBehaviour
{
    private ParticleSystem sprayParticles;
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();

    // Damage settings
    private float damagePerParticle = 0.5f;
    private float damageMultiplier = 1f;
    private Vector2 sprayDirection = Vector2.right;
    private bool damageParamsExplicitlySet;

    [SerializeField, Min(0f)]
    private float weaponKnockbackMultiplier = 0.45f;

    // Cooldown to prevent same enemy being hit too rapidly by multiple particles
    private Dictionary<EnemyBase, float> lastHitTime = new Dictionary<EnemyBase, float>();
    private Dictionary<EnemyBase, float> coneDamageTotals = new Dictionary<EnemyBase, float>();
    private HashSet<EnemyBase> coneKnockbackResolved = new HashSet<EnemyBase>();
    private const float HitCooldown = 0.05f; // 50ms between hits on same enemy
    private float lastConeHitTime = -10f;

    // Reference to player stats for damage scaling
    private PlayerStats playerStats;

    void Awake()
    {
        sprayParticles = GetComponent<ParticleSystem>();
        playerStats = GetComponentInParent<PlayerStats>();
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        // Configure collision module if not already set up
        if (sprayParticles != null)
        {
            ConfigureCollision();
        }
    }

    /// <summary>
    /// Configure particle system collision module for enemy detection
    /// </summary>
    private void ConfigureCollision()
    {
        var collision = sprayParticles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.sendCollisionMessages = true;
        collision.collidesWith = LayerMask.GetMask("Enemy");
        collision.maxCollisionShapes = 10;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.radiusScale = 1f;
        collision.dampen = 0f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f; // Particle dies on hit
    }

    /// <summary>
    /// Set damage parameters from player stats
    /// </summary>
    public void SetDamageParams(PlayerStats stats, float baseDamage, float multiplier)
    {
        playerStats = stats;
        damagePerParticle = baseDamage;
        damageMultiplier = multiplier;
        damageParamsExplicitlySet = true;
    }

    /// <summary>
    /// Update spray direction for knockback calculations
    /// </summary>
    public void SetSprayDirection(Vector2 direction)
    {
        sprayDirection = direction.normalized;
    }

    /// <summary>
    /// Called by Unity when particles collide with colliders
    /// </summary>
    void OnParticleCollision(GameObject other)
    {
        if (sprayParticles == null)
            return;

        // Get collision events
        int numEvents = sprayParticles.GetCollisionEvents(other, collisionEvents);

        // Check if it's an enemy
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy == null)
            return;

        // Check cooldown - prevent rapid multi-hit
        float currentTime = Time.time;
        if (currentTime - lastConeHitTime > SpraySettings.BurstDuration)
        {
            coneDamageTotals.Clear();
            coneKnockbackResolved.Clear();
        }
        lastConeHitTime = currentTime;

        if (lastHitTime.TryGetValue(enemy, out float lastTime))
        {
            if (currentTime - lastTime < HitCooldown)
                return;
        }
        lastHitTime[enemy] = currentTime;

        // Calculate damage based on number of particles that hit
        float totalDamage = numEvents * damagePerParticle * damageMultiplier;

        // Scale damage with player stats if available
        if (playerStats != null)
        {
            // The original 10 base damage maps to the original 0.5 damage per
            // particle. Inspector debug tuning scales this legacy path too.
            if (!damageParamsExplicitlySet)
                totalDamage *= playerStats.CurrentDamage / PlayerStats.DefaultBaseDamage;

            totalDamage *= playerStats.CurrentSprayDamageMultiplier;
        }

        // Legacy particle-collision mode follows the same cone aggregation rule:
        // damage each collision, then make at most one knockback roll per cone.
        enemy.TakeDamage(totalDamage);
        if (enemy == null || !enemy.isActiveAndEnabled)
            return;

        coneDamageTotals.TryGetValue(enemy, out float accumulatedDamage);
        accumulatedDamage += totalDamage;
        coneDamageTotals[enemy] = accumulatedDamage;

        if (
            !coneKnockbackResolved.Contains(enemy)
            && enemy.TryApplyDamageKnockback(
                accumulatedDamage,
                sprayDirection,
                weaponKnockbackMultiplier
            )
        )
        {
            coneKnockbackResolved.Add(enemy);
        }
    }

    /// <summary>
    /// Clear cooldown tracking (call when spray stops)
    /// </summary>
    public void ClearCooldowns()
    {
        lastHitTime.Clear();
        coneDamageTotals.Clear();
        coneKnockbackResolved.Clear();
        lastConeHitTime = -10f;
    }

    void OnDisable()
    {
        ClearCooldowns();
    }
}
