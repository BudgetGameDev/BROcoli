using System;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class SprayDamageHandler
    {
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

            bool anyKilled = ProcessTriggeredParticles(
                enter,
                numEnter,
                position => GroundPlane.OverlapPoint(position)
            );
            ApplyTriggerResults(sprayParticles, enter, anyKilled);
        }

        internal bool ProcessTriggeredParticles(
            List<ParticleSystem.Particle> enter,
            int count,
            Func<Vector3, Collider> overlapPoint
        )
        {
            bool anyKilled = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 particlePos = enter[i].position;
                Collider hit = overlapPoint(particlePos);
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
            return anyKilled;
        }

        internal static void ApplyTriggerResults(
            ParticleSystem sprayParticles,
            List<ParticleSystem.Particle> enter,
            bool anyKilled
        )
        {
            if (anyKilled)
                sprayParticles.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
        }
    }
}
