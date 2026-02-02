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
    
    // Cooldown to prevent same enemy being hit too rapidly by multiple particles
    private Dictionary<EnemyBase, float> lastHitTime = new Dictionary<EnemyBase, float>();
    private const float HitCooldown = 0.05f; // 50ms between hits on same enemy
    
    // Reference to player stats for damage scaling
    private PlayerStats playerStats;
    
    void Awake()
    {
        sprayParticles = GetComponent<ParticleSystem>();
        
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
        collision.mode = ParticleSystemCollisionMode.Collision2D;
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
        if (sprayParticles == null) return;
        
        // Get collision events
        int numEvents = sprayParticles.GetCollisionEvents(other, collisionEvents);
        
        // Check if it's an enemy
        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy == null) return;
        
        // Check cooldown - prevent rapid multi-hit
        float currentTime = Time.time;
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
            totalDamage *= playerStats.CurrentSprayDamageMultiplier;
        }
        
        // Apply damage immediately with knockback in spray direction
        enemy.TakeDamage(totalDamage, sprayDirection);
    }
    
    /// <summary>
    /// Clear cooldown tracking (call when spray stops)
    /// </summary>
    public void ClearCooldowns()
    {
        lastHitTime.Clear();
    }
    
    void OnDisable()
    {
        ClearCooldowns();
    }
}
