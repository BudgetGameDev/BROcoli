using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Handles particle collision events for spray damage.
    /// Attach this to the particle system GameObject to receive OnParticleCollision callbacks.
    /// Deals damage immediately when particles hit enemies - no delay.
    /// </summary>
    public class SprayParticleCollisionHandler : MonoBehaviour
    {
        private ParticleSystem sprayParticles;
        private readonly List<ParticleCollisionEvent> collisionEvents =
            new List<ParticleCollisionEvent>();
        private readonly List<Vector3> collisionPoints = new List<Vector3>();
        private ParticleSystem.Particle[] liveParticles;
        private SprayHitSplash hitSplash;

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
                playerStats = PlayerStats.Resolve();

            // Configure collision module if not already set up
            if (sprayParticles != null)
            {
                ConfigureCollision();
                liveParticles = new ParticleSystem.Particle[
                    Mathf.Max(1, sprayParticles.main.maxParticles)
                ];
                hitSplash = new SprayHitSplash(transform);
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
            collision.collidesWith = LayerMask.GetMask("Enemy", "Wall");
            collision.maxCollisionShapes = 64;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.enableDynamicColliders = true;
            collision.radiusScale = 1.35f;
            collision.dampen = 0f;
            collision.bounce = 0f;
            // The callback removes the exact colliding core particles. Leaving lifetime
            // loss at zero keeps them available long enough to identify and remove.
            collision.lifetimeLoss = 0f;
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
            if (numEvents <= 0)
                return;
            ProcessCollision(other, collisionEvents, numEvents);
        }

        internal void ProcessCollision(
            GameObject other,
            IReadOnlyList<ParticleCollisionEvent> events,
            int numEvents
        )
        {
            if (other == null || events == null || numEvents <= 0)
                return;

            collisionPoints.Clear();
            for (int i = 0; i < numEvents; i++)
                collisionPoints.Add(events[i].intersection);

            ConsumeParticlesNear(collisionPoints);
            hitSplash?.Emit(events, numEvents);

            // Check if it's an enemy
            EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
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
                // Preserve the authored per-particle ratio as the player's damage changes.
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
        /// Removes one live source particle for each world-space contact point. Particle
        /// collision callbacks do not expose particle indices, so the closest particle
        /// at each collision point is the exact, stable mapping Unity makes available.
        /// </summary>
        public int ConsumeParticlesNear(IReadOnlyList<Vector3> worldContactPoints)
        {
            if (sprayParticles == null)
                sprayParticles = GetComponent<ParticleSystem>();
            if (sprayParticles == null || worldContactPoints == null)
                return 0;

            int requiredCapacity = Mathf.Max(1, sprayParticles.main.maxParticles);
            if (liveParticles == null || liveParticles.Length < requiredCapacity)
                liveParticles = new ParticleSystem.Particle[requiredCapacity];

            int liveCount = sprayParticles.GetParticles(liveParticles);
            int removed = 0;
            for (int contactIndex = 0; contactIndex < worldContactPoints.Count; contactIndex++)
            {
                int nearestIndex = FindNearestParticle(worldContactPoints[contactIndex], liveCount);
                if (nearestIndex < 0)
                    continue;

                liveCount--;
                liveParticles[nearestIndex] = liveParticles[liveCount];
                removed++;
            }

            if (removed > 0)
                sprayParticles.SetParticles(liveParticles, liveCount);
            return removed;
        }

        private int FindNearestParticle(Vector3 worldContactPoint, int liveCount)
        {
            int nearestIndex = -1;
            float nearestDistance = float.PositiveInfinity;
            ParticleSystem.MainModule main = sprayParticles.main;

            for (int i = 0; i < liveCount; i++)
            {
                Vector3 worldPosition = liveParticles[i].position;
                if (main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    worldPosition = sprayParticles.transform.TransformPoint(worldPosition);
                else if (
                    main.simulationSpace == ParticleSystemSimulationSpace.Custom
                    && main.customSimulationSpace != null
                )
                {
                    worldPosition = main.customSimulationSpace.TransformPoint(worldPosition);
                }

                float distance = (worldPosition - worldContactPoint).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestIndex = i;
            }

            return nearestIndex;
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
}
