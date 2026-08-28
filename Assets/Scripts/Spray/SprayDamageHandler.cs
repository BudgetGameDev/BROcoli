using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles damage calculation and enemy detection for the sanitizer spray.
/// Damage is delayed based on particle travel time to sync with visuals.
/// </summary>
public class SprayDamageHandler
{
    private readonly Dictionary<EnemyBase, int> particleHitCounts =
        new Dictionary<EnemyBase, int>();
    private readonly Dictionary<EnemyBase, float> coneDamageTotals =
        new Dictionary<EnemyBase, float>();
    private readonly HashSet<EnemyBase> coneKnockbackResolved = new HashSet<EnemyBase>();
    private readonly Collider[] hitBuffer = new Collider[SpraySettings.HitBufferSize];

    private float nextDamageTick = 0f;
    private PlayerStats playerStats;
    private Transform playerTransform;
    private float particleSpeed = 10f;
    private float weaponKnockbackMultiplier;
    private Vector2 lastConeDirection = Vector2.right;

    public SprayDamageHandler(
        PlayerStats stats,
        Transform player,
        float knockbackMultiplier = 0.45f
    )
    {
        playerStats = stats;
        playerTransform = player;
        weaponKnockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
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
        if (enemy == null)
            return;

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
    public void ProcessDamage(
        Vector2 sprayDirection,
        float currentRange,
        float currentWidth,
        Vector2 nozzleOrigin
    )
    {
        if (Time.time < nextDamageTick)
            return;
        nextDamageTick = Time.time + SpraySettings.DamageTickRate;
        if (sprayDirection.sqrMagnitude > 0.0001f)
            lastConeDirection = sprayDirection.normalized;

        // Detect enemies in cone from nozzle origin
        DetectEnemiesInCone(sprayDirection, currentRange, currentWidth, nozzleOrigin);

        // Queue damage with travel time delay
        float damageMultiplier =
            playerStats != null ? playerStats.CurrentSprayDamageMultiplier : 1f;
        float baseDamage =
            playerStats != null
                ? playerStats.CurrentDamage * SpraySettings.DamagePerSimulatedHitMultiplier
                : SpraySettings.BaseDamagePerParticle * 3f;

        foreach (var kvp in particleHitCounts)
        {
            EnemyBase enemy = kvp.Key;
            int hitCount = kvp.Value;

            if (enemy != null && hitCount > 0)
            {
                float damage = baseDamage * hitCount * damageMultiplier;

                // Damage remains responsive. The cone-wide accumulator below
                // starts recoil as soon as its combined damage reaches 10%.
                enemy.TakeDamage(damage);

                if (enemy == null || !enemy.isActiveAndEnabled)
                    continue;

                coneDamageTotals.TryGetValue(enemy, out float accumulatedDamage);
                accumulatedDamage += damage;
                coneDamageTotals[enemy] = accumulatedDamage;

                Vector2 knockbackDirection =
                    playerTransform != null
                        ? (
                            enemy.transform.position.ToGround()
                            - playerTransform.position.ToGround()
                        ).normalized
                        : lastConeDirection;
                if (knockbackDirection.sqrMagnitude < 0.0001f)
                    knockbackDirection = lastConeDirection;

                if (coneKnockbackResolved.Contains(enemy))
                {
                    // The motion is already underway. Let subsequent damage
                    // strengthen that same recoil without extending its timer.
                    enemy.StrengthenActiveDamageKnockback(
                        accumulatedDamage,
                        knockbackDirection,
                        weaponKnockbackMultiplier
                    );
                }
                else if (
                    enemy.TryApplyDamageKnockback(
                        accumulatedDamage,
                        knockbackDirection,
                        weaponKnockbackMultiplier
                    )
                )
                {
                    coneKnockbackResolved.Add(enemy);
                }
            }
        }

        particleHitCounts.Clear();
    }

    public void SetWeaponKnockbackMultiplier(float multiplier)
    {
        weaponKnockbackMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// Resolve one knockback roll per surviving enemy using all damage dealt by
    /// the completed cone. Called exactly when a burst/continuous spray ends.
    /// </summary>
    public void ResolveConeKnockback()
    {
        foreach (var kvp in coneDamageTotals)
        {
            EnemyBase enemy = kvp.Key;
            if (enemy == null || !enemy.isActiveAndEnabled || coneKnockbackResolved.Contains(enemy))
                continue;

            Vector2 direction =
                playerTransform != null
                    ? (
                        enemy.transform.position.ToGround() - playerTransform.position.ToGround()
                    ).normalized
                    : lastConeDirection;
            if (direction.sqrMagnitude < 0.0001f)
                direction = lastConeDirection;

            if (enemy.TryApplyDamageKnockback(kvp.Value, direction, weaponKnockbackMultiplier))
            {
                coneKnockbackResolved.Add(enemy);
            }
        }

        // A completed cone can never contribute damage to the next cone.
        coneDamageTotals.Clear();
        coneKnockbackResolved.Clear();
    }

    /// <summary>
    /// Detect enemies in spray cone and calculate damage based on particle density.
    /// Damage = baseDamage × distanceFalloff × angleFalloff
    /// - Distance: closer = more particles hit (particles fizzle over lifetime)
    /// - Angle: center = denser spray (cone spreads at edges)
    /// </summary>
    /// <param name="nozzleOrigin">Origin point for damage cone (where spray emits from)</param>
    private void DetectEnemiesInCone(
        Vector2 sprayDirection,
        float currentRange,
        float currentWidth,
        Vector2 nozzleOrigin
    )
    {
        // Gameplay cone begins at the player, while particles still render from
        // the nozzle. This bridges the near field so a close enemy cannot sit
        // behind the visual emission point and be skipped by the cone angle.
        Vector2 origin =
            playerTransform != null ? playerTransform.position.ToGround() : nozzleOrigin;

        float halfAngle = currentWidth * 0.5f;

        // Detection still uses a circle around nozzle for initial broad-phase
        int hitCount = GroundPlane.OverlapCircle(origin, currentRange, hitBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy"))
                continue;

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null)
                continue;

            // Use predicted position for fast-moving enemies
            Vector2 enemyPos = hit.bounds.center.ToGround();
            if (enemy.rb != null && enemy.rb.GroundVelocity().sqrMagnitude > 0.1f)
            {
                float dist = Vector2.Distance(origin, enemyPos);
                float travelTime = dist / particleSpeed;
                enemyPos += enemy.rb.GroundVelocity() * travelTime;
            }

            Vector2 toEnemy = (enemyPos - origin);
            float distance = toEnemy.magnitude;
            if (distance < 0.01f || distance > currentRange)
                continue;

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
        if (sprayParticles == null)
            return;

        // Get particles that entered triggers
        List<ParticleSystem.Particle> enter = new List<ParticleSystem.Particle>();
        int numEnter = sprayParticles.GetTriggerParticles(
            ParticleSystemTriggerEventType.Enter,
            enter
        );

        bool anyKilled = false;

        for (int i = 0; i < numEnter; i++)
        {
            Vector3 particlePos = enter[i].position;

            // Find enemy at this position
            Collider hit = GroundPlane.OverlapPoint(particlePos);
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
        coneDamageTotals.Clear();
        coneKnockbackResolved.Clear();
    }

    /// <summary>
    /// Clear all tracked particle hits
    /// </summary>
    public void ClearHits()
    {
        particleHitCounts.Clear();
        coneDamageTotals.Clear();
        coneKnockbackResolved.Clear();
    }
}
