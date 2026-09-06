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

            // Also create legacy single system as fallback reference
            CreateLegacyParticleSystem();
        }

        private void CreateLegacyParticleSystem()
        {
            GameObject particleObj = new GameObject("SprayParticlesLegacy");
            particleObj.transform.SetParent(parentTransform);
            particleObj.transform.localPosition = new Vector3(0, 0.5f, 0);
            particleObj.transform.localRotation = Quaternion.identity;
            particleObj.SetActive(false); // Disabled, just for reference

            sprayParticles = particleObj.AddComponent<ParticleSystem>();

            ConfigureMainModule();
            ConfigureEmission();
            ConfigureShape();
            ConfigureSizeOverLifetime();
            ConfigureColorOverLifetime();
            ConfigureVelocityOverLifetime();
            ConfigureNoise();
            ConfigureRenderer(particleObj);
        }

        private void ConfigureMainModule()
        {
            var main = sprayParticles.main;
            main.duration = SpraySettings.BurstDuration;
            main.loop = false;
            main.startLifetime = SpraySettings.ParticleLifetimeBase;
            // Calculate speed so particles travel full range during their lifetime
            float targetSpeed = SpraySettings.BaseSprayRange / SpraySettings.ParticleLifetimeBase;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                targetSpeed * 0.9f,
                targetSpeed * 1.1f
            );
            main.startSize = new ParticleSystem.MinMaxCurve(
                SpraySettings.ParticleMinSize,
                SpraySettings.ParticleMaxSize
            );
            main.startColor = SpraySettings.SprayColor;
            main.maxParticles = SpraySettings.MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // World space - direction set via shape rotation
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.gravityModifier = 0f;
            main.gravityModifierMultiplier = 0f;
        }

        private void ConfigureEmission()
        {
            var emission = sprayParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
        }

        private void ConfigureShape()
        {
            var shape = sprayParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = SpraySettings.BaseSprayAngle * 0.5f; // Half angle for cone
            shape.radius = 0.1f; // Small spawn area at nozzle
            shape.radiusThickness = 1f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero; // No shape rotation - direction handled by transform
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
        }

        private void ConfigureSizeOverLifetime()
        {
            var sizeOverLifetime = sprayParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.6f);
            sizeCurve.AddKey(0.2f, 1f);
            sizeCurve.AddKey(0.8f, 1f);
            sizeCurve.AddKey(1f, 0.5f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        }

        private void ConfigureColorOverLifetime()
        {
            var colorOverLifetime = sprayParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(SpraySettings.SprayColor, 0f),
                    new GradientColorKey(SpraySettings.SprayColor, 0.8f),
                    new GradientColorKey(Color.white, 1f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.8f, 0.7f),
                    new GradientAlphaKey(0.3f, 1f),
                }
            );
            colorOverLifetime.color = gradient;
        }

        private void ConfigureVelocityOverLifetime()
        {
            // Velocity is handled by cone shape + startSpeed, so just disable this
            var velocityOverLifetime = sprayParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = false;
        }

        private void ConfigureNoise()
        {
            var noise = sprayParticles.noise;
            noise.enabled = true;
            noise.strength = SpraySettings.NoiseStrength;
            noise.frequency = SpraySettings.NoiseFrequency;
            noise.scrollSpeed = SpraySettings.NoiseScrollSpeed;
            noise.damping = true;
            noise.separateAxes = true;
            noise.strengthX = SpraySettings.NoiseStrengthX;
            noise.strengthY = SpraySettings.NoiseStrengthY;
            noise.strengthZ = SpraySettings.NoiseStrengthZ;
        }

        private void ConfigureRenderer(GameObject particleObj)
        {
            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = SpraySettings.ParticleSortingOrder;

            var material = new Material(Shader.Find("Sprites/Default"));
            material.color = SpraySettings.SprayColor;
            renderer.sharedMaterial = material;
        }
    }
}
