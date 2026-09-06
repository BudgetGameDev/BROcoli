using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Handles particle system creation, configuration, and runtime updates for the spray weapon.
    /// Uses multi-layer particle system for realistic 3D PBR-style spray effect.
    /// Manages velocity compensation for isometric view and spray direction updates.
    /// </summary>
    public partial class SprayParticleController
    {
        private ParticleSystem sprayParticles;
        private Transform parentTransform;
        private Vector2 currentDirection = Vector2.right;

        // Multi-layer particle system for realistic spray
        private SprayParticleLayers particleLayers;
        private bool useLayeredParticles = true;

        public ParticleSystem Particles =>
            useLayeredParticles ? particleLayers?.CoreSpray : sprayParticles;

        /// <summary>
        /// Get the current particle speed (for damage timing calculations)
        /// </summary>
        public float GetParticleSpeed()
        {
            if (useLayeredParticles && particleLayers != null)
                return particleLayers.GetParticleSpeed();
            if (sprayParticles == null)
                return SpraySettings.BaseSprayRange / SpraySettings.ParticleLifetimeBase;
            return sprayParticles.main.startSpeed.constantMax;
        }

        public SprayParticleController(Transform parent)
        {
            parentTransform = parent;
        }

        /// <summary>
        /// Set an existing particle system (assigned via inspector)
        /// </summary>
        public void SetParticleSystem(ParticleSystem particles)
        {
            sprayParticles = particles;
            useLayeredParticles = false; // Use provided particle system instead
        }

        /// <summary>
        /// Create a new particle system programmatically - uses layered system for realism
        /// </summary>
        public void CreateParticleSystem()
        {
            // Create the multi-layer realistic spray system
            particleLayers = new SprayParticleLayers(parentTransform);
            particleLayers.CreateAllLayers();
            useLayeredParticles = true;
        }
    }
}
