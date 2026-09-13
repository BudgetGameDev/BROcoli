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
    public void ProcessDamage(Vector2 sprayDirection, float currentRange, float currentWidth)
    {
        // Always process pending damages first
        ProcessPendingDamages();
        
        if (Time.time < nextDamageTick) return;
        nextDamageTick = Time.time + SpraySettings.DamageTickRate;
        
        // Detect enemies in cone
        DetectEnemiesInCone(sprayDirection, currentRange, currentWidth);
        
        // Queue damage with travel time delay
        float damageMultiplier = playerStats != null ? playerStats.CurrentSprayDamageMultiplier : 1f;
        float baseDamage = playerStats != null 
            ? playerStats.CurrentDamage * 0.35f 
            : SpraySettings.BaseDamagePerParticle * 3f;
        
        Vector2 playerPos = playerTransform != null ? (Vector2)playerTransform.position : Vector2.zero;
        
        foreach (var kvp in particleHitCounts)
        {
            EnemyBase enemy = kvp.Key;
            int hitCount = kvp.Value;
            
            if (enemy != null && hitCount > 0)
            {
                // Calculate travel time based on distance
                float distance = Vector2.Distance(playerPos, enemy.transform.position);
                float travelTime = distance / particleSpeed;
                
                float damage = baseDamage * hitCount * damageMultiplier;
                
                pendingDamages.Add(new PendingDamage
                {
                    enemy = enemy,
                    damage = damage,
                    knockbackDir = sprayDirection,
                    applyTime = Time.time + travelTime
                });
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
    /// Detect enemies in spray cone and register hits (backup for particle collision)
    /// </summary>
    private void DetectEnemiesInCone(Vector2 sprayDirection, float currentRange, float currentWidth)
    {
        Vector2 origin = playerTransform != null 
            ? (Vector2)playerTransform.position 
            : Vector2.zero;
        
        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, currentRange, hitBuffer);
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;
            
            // Check if enemy is within cone angle
            Vector2 toEnemy = ((Vector2)hit.transform.position - origin).normalized;
            float angleToEnemy = Vector2.Angle(sprayDirection, toEnemy);
            
            if (angleToEnemy <= currentWidth * 0.5f)
            {
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    // Register multiple hits based on how centered they are in the cone
                    float centeredness = 1f - (angleToEnemy / (currentWidth * 0.5f));
                    int simulatedHits = Mathf.Max(1, Mathf.RoundToInt(3 * centeredness));
                    
                    for (int j = 0; j < simulatedHits; j++)
                    {
                        RegisterParticleHit(enemy);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Process particle trigger events and register hits
    /// </summary>
    /// <param name="sprayParticles">The particle system to check</param>
    public void ProcessParticleTrigger(ParticleSystem sprayParticles)
    {
        if (sprayParticles == null) return;
        
        // Get particles that entered triggers
        List<ParticleSystem.Particle> enter = new List<ParticleSystem.Particle>();
        int numEnter = sprayParticles.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
        
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
                }
            }
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
