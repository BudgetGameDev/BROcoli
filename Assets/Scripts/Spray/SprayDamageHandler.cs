using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles damage calculation and enemy detection for the sanitizer spray.
/// Damage is delayed based on particle travel time to sync with visuals.
/// </summary>
public class SprayDamageHandler
{
    private readonly Dictionary<EnemyBase, int> particleHitCounts = new Dictionary<EnemyBase, int>();
    private readonly Collider2D[] hitBuffer = new Collider2D[SpraySettings.HitBufferSize];
    private readonly List<PendingDamage> pendingDamages = new List<PendingDamage>();
    
    private float nextDamageTick = 0f;
    private PlayerStats playerStats;
    private Transform playerTransform;
    private float particleSpeed = 10f;

    private struct PendingDamage
    {
        public EnemyBase enemy;
        public float damage;
        public Vector2 knockbackDir;
        public float applyTime;
    }

    public SprayDamageHandler(PlayerStats stats, Transform player)
    {
        playerStats = stats;
        playerTransform = player;
    }

    /// <summary>
    /// Update references (call when player stats might have changed)
    /// </summary>
    public void UpdateReferences(PlayerStats stats, Transform player)
    {
        playerStats = stats;
        playerTransform = player;
    }

    /// <summary>
    /// Set particle speed for travel time calculations
    /// </summary>
    public void SetParticleSpeed(float speed)
    {
        particleSpeed = Mathf.Max(1f, speed);
    }

    /// <summary>
    /// Register a particle hit on an enemy for splash damage calculation
    /// </summary>
    public void RegisterParticleHit(EnemyBase enemy)
    {
        if (enemy == null) return;
        
        if (!particleHitCounts.ContainsKey(enemy))
        {
            particleHitCounts[enemy] = 0;
        }
        particleHitCounts[enemy]++;
    }

    /// <summary>
    /// Process damage - queue with delay based on particle travel time.
    /// </summary>
    /// <param name="sprayDirection">Direction the spray is aimed</param>
    /// <param name="currentRange">Current spray range</param>
    /// <param name="currentWidth">Current spray cone width in degrees</param>
    /// <param name="nozzleOrigin">Origin point for damage cone (nozzle position)</param>
    public void ProcessDamage(Vector2 sprayDirection, float currentRange, float currentWidth, Vector2 nozzleOrigin)
    {
        // Always process pending damages first
        ProcessPendingDamages();
        
        if (Time.time < nextDamageTick) return;
        nextDamageTick = Time.time + SpraySettings.DamageTickRate;
        
        // Detect enemies in cone from nozzle origin
        DetectEnemiesInCone(sprayDirection, currentRange, currentWidth, nozzleOrigin);
        
        // Queue damage with travel time delay
        float damageMultiplier = playerStats != null ? playerStats.CurrentSprayDamageMultiplier : 1f;
        float baseDamage = playerStats != null 
            ? playerStats.CurrentDamage * 0.35f 
            : SpraySettings.BaseDamagePerParticle * 3f;
        
        foreach (var kvp in particleHitCounts)
        {
            EnemyBase enemy = kvp.Key;
            int hitCount = kvp.Value;
            
            if (enemy != null && hitCount > 0)
            {
                float damage = baseDamage * hitCount * damageMultiplier;
                
                // Apply damage immediately for responsive hit registration
                // (travel time delay removed - visual particles are cosmetic)
                enemy.TakeDamage(damage, sprayDirection);
            }
        }
        
        particleHitCounts.Clear();
    }

    private void ProcessPendingDamages()
    {
        for (int i = pendingDamages.Count - 1; i >= 0; i--)
        {
            var pending = pendingDamages[i];
            if (Time.time >= pending.applyTime)
            {
                if (pending.enemy != null)
                {
                    pending.enemy.TakeDamage(pending.damage, pending.knockbackDir);
                }
                pendingDamages.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Detect enemies in spray cone and calculate damage based on particle density.
    /// Damage = baseDamage × distanceFalloff × angleFalloff
    /// - Distance: closer = more particles hit (particles fizzle over lifetime)
    /// - Angle: center = denser spray (cone spreads at edges)
    /// </summary>
    /// <param name="nozzleOrigin">Origin point for damage cone (where spray emits from)</param>
    private void DetectEnemiesInCone(Vector2 sprayDirection, float currentRange, float currentWidth, Vector2 nozzleOrigin)
    {
        // Use nozzle as the origin point (where spray actually comes from)
        Vector2 origin = nozzleOrigin;
        
        float halfAngle = currentWidth * 0.5f;
        
        // Detection still uses a circle around nozzle for initial broad-phase
        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, currentRange, hitBuffer);
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;
            
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null) continue;
            
            // Use predicted position for fast-moving enemies
            Vector2 enemyPos = (Vector2)hit.bounds.center;
            if (enemy.rb != null && enemy.rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                float dist = Vector2.Distance(origin, enemyPos);
                float travelTime = dist / particleSpeed;
                enemyPos += enemy.rb.linearVelocity * travelTime;
            }
            
            Vector2 toEnemy = (enemyPos - origin);
            float distance = toEnemy.magnitude;
            if (distance < 0.01f || distance > currentRange) continue;
            
            toEnemy /= distance; // normalize
            float angleToEnemy = Vector2.Angle(sprayDirection, toEnemy);
            
            if (angleToEnemy <= halfAngle)
            {
                // Physics-based damage: particles fizzle over distance, spread over angle
                float distanceRatio = distance / currentRange;
                float distanceFalloff = 1f - Mathf.Pow(distanceRatio, 0.7f);
                
                float angleRatio = angleToEnemy / halfAngle;
                float angleFalloff = 1f - Mathf.Pow(angleRatio, 0.5f);
                
                float particleDensity = distanceFalloff * angleFalloff;
                int simulatedHits = Mathf.Max(1, Mathf.RoundToInt(5f * particleDensity));
                
                for (int j = 0; j < simulatedHits; j++)
                {
                    RegisterParticleHit(enemy);
                }
            }
        }
    }

    /// <summary>
    /// Process particle trigger events, register hits, and kill particles on impact.
    /// Particles stop when they hit enemies (no piercing).
    /// </summary>
    /// <param name="sprayParticles">The particle system to check</param>
    public void ProcessParticleTrigger(ParticleSystem sprayParticles)
    {
        if (sprayParticles == null) return;
        
        // Get particles that entered triggers
        List<ParticleSystem.Particle> enter = new List<ParticleSystem.Particle>();
        int numEnter = sprayParticles.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
        
        bool anyKilled = false;
        
        for (int i = 0; i < numEnter; i++)
        {
            Vector3 particlePos = enter[i].position;
            
            // Find enemy at this position
            Collider2D hit = Physics2D.OverlapPoint(particlePos);
            if (hit != null && hit.CompareTag("Enemy"))
            {
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    RegisterParticleHit(enemy);
                    
                    // Kill particle on impact - no piercing through enemies
                    var particle = enter[i];
                    particle.remainingLifetime = 0f;
                    enter[i] = particle;
                    anyKilled = true;
                }
            }
        }
        
        // Write back modified particles
        if (anyKilled)
        {
            sprayParticles.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
        }
    }

    /// <summary>
    /// Reset the next damage tick timer (useful when starting a burst)
    /// </summary>
    public void ResetDamageTick()
    {
        nextDamageTick = 0f;
    }

    /// <summary>
    /// Clear all tracked particle hits
    /// </summary>
    public void ClearHits()
    {
        particleHitCounts.Clear();
    }
}
