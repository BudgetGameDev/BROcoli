using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class SprayParticleController
    {
        /// <summary>
        /// Update particle system parameters for current range and width stats
        /// </summary>
        public void UpdateForStats(float currentRange, float currentWidth)
        {
            // Update layered particles if using them
            if (useLayeredParticles && particleLayers != null)
            {
                particleLayers.UpdateForStats(currentRange, currentWidth);
            }

            // Also update legacy system if it exists
            if (sprayParticles == null)
                return;

            var main = sprayParticles.main;
            // Calculate speed so particles travel full range during their lifetime
            float targetSpeed = currentRange / SpraySettings.ParticleLifetimeBase;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                targetSpeed * 0.9f,
                targetSpeed * 1.1f
            );
            main.startLifetime = SpraySettings.ParticleLifetimeBase;
            main.gravityModifier = 0f;

            // Update cone angle based on spray width
            var shape = sprayParticles.shape;
            shape.angle = currentWidth * 0.5f; // Half angle for cone
        }

        /// <summary>
        /// Set the spray direction and origin position.
        /// Direction is ground-plane only; the origin retains the modeled nozzle height.
        /// Unity's Cone shape emits along local +Z axis by default.
        /// </summary>
        public void SetSprayDirectionAndPosition(
            Vector2 direction,
            Vector3 nozzleWorldPos,
            float currentRange,
            float currentWidth
        )
        {
            currentDirection = direction.normalized;

            // Update layered particles
            if (useLayeredParticles && particleLayers != null)
            {
                particleLayers.SetDirectionAndPosition(direction, nozzleWorldPos);
                particleLayers.UpdateForStats(currentRange, currentWidth);
            }

            // Also update legacy system if active
            if (sprayParticles == null || !sprayParticles.gameObject.activeInHierarchy)
                return;

            // Update speed based on current range
            var main = sprayParticles.main;
            float targetSpeed = currentRange / SpraySettings.ParticleLifetimeBase;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                targetSpeed * 0.9f,
                targetSpeed * 1.1f
            );

            // Update cone angle
            var shape = sprayParticles.shape;
            shape.angle = currentWidth * 0.5f;

            // Position the particle system at the nozzle world position
            sprayParticles.transform.position = nozzleWorldPos;

            // Unity cone emits along local +Z. Use LookRotation to point +Z toward the
            // spray direction. Vector3.up as up keeps the spray flat on the ground plane.
            Vector3 sprayDir3D = direction.normalized.ToWorld();
            if (sprayDir3D.sqrMagnitude > 0.001f)
                sprayParticles.transform.rotation = Quaternion.LookRotation(sprayDir3D, Vector3.up);

            // Reset shape position/rotation - transform handles everything
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
        }

        /// <summary>
        /// Apply velocity and direction - rotates particle transform to spray in the given world direction.
        /// Unity's Cone shape emits along local +Z axis.
        /// </summary>
        public void ApplyVelocityCompensation(
            Vector2 sprayDirection,
            float currentRange,
            float currentWidth
        )
        {
            if (sprayParticles == null)
                return;

            currentDirection = sprayDirection.normalized;

            // Update speed based on current range
            var main = sprayParticles.main;
            float targetSpeed = currentRange / SpraySettings.ParticleLifetimeBase;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                targetSpeed * 0.9f,
                targetSpeed * 1.1f
            );

            // Update cone angle
            var shape = sprayParticles.shape;
            shape.angle = currentWidth * 0.5f;

            // Unity's Cone shape emits particles along the local +Z axis.
            // Rotate the transform so +Z points in the spray direction on the ground plane.
            Vector3 sprayDir3D = sprayDirection.ToWorld();
            sprayParticles.transform.rotation = Quaternion.LookRotation(sprayDir3D, Vector3.up);

            // Reset shape rotation - transform handles direction
            shape.rotation = Vector3.zero;
        }

        /// <summary>
        /// Update shape position for nozzle offset based on current direction
        /// </summary>
        public void UpdateNozzlePosition()
        {
            if (sprayParticles == null || !sprayParticles.gameObject.activeInHierarchy)
                return;

            // Position the spawn point at the nozzle location relative to player
            // The nozzle moves with the hand which rotates around the player
            var shape = sprayParticles.shape;
            Vector3 nozzleOffset = (currentDirection * SpraySettings.NozzleOffset).ToWorld();
            shape.position = nozzleOffset;
        }

        /// <summary>Play a spray burst with specified particle count.</summary>
        public void PlayBurst()
        {
            // Play on layered system
            if (useLayeredParticles && particleLayers != null)
            {
                int baseCount = (int)(
                    SpraySettings.VisualEmissionRate * SpraySettings.BurstDuration * 1.5f
                );
                particleLayers.PlayBurst(baseCount);
            }

            // Also play on legacy if active
            if (sprayParticles != null && sprayParticles.gameObject.activeInHierarchy)
            {
                sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var emission = sprayParticles.emission;
                short burstCount = (short)(
                    SpraySettings.EmissionRate * SpraySettings.BurstDuration * 1.5f
                );
                emission.SetBursts(
                    new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, burstCount) }
                );

                sprayParticles.Play();
            }
        }

        /// <summary>
        /// Start continuous spray emission
        /// </summary>
        public void Play()
        {
            // Layered system uses burst mode
            if (useLayeredParticles && particleLayers != null)
            {
                int baseCount = (int)(
                    SpraySettings.VisualEmissionRate * SpraySettings.BurstDuration
                );
                particleLayers.PlayBurst(baseCount);
            }
            sprayParticles?.Play();
        }

        /// <summary>
        /// Stop particle emission
        /// </summary>
        public void Stop()
        {
            particleLayers?.Stop();
            sprayParticles?.Stop();
        }

        /// <summary>
        /// Check if particle system exists
        /// </summary>
        public bool HasParticles =>
            (useLayeredParticles && particleLayers != null && particleLayers.HasParticles)
            || sprayParticles != null;
    }
}
