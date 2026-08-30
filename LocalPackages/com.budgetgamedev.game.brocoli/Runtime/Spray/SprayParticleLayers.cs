using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Creates and manages multiple particle system layers for a realistic 3D spray effect.
    /// Combines core spray, mist, droplets, and glow for PBR-like appearance.
    /// </summary>
    public class SprayParticleLayers
    {
        // Particle system layers
        private ParticleSystem coreSpray; // Dense center spray
        private ParticleSystem mistLayer; // Outer fog/mist
        private ParticleSystem dropletLayer; // Individual droplets
        private ParticleSystem glowLayer; // Bright highlights

        private Transform parentTransform;
        private GameObject containerObj;

        // Cached textures
        private Texture2D softCircleTex;
        private Texture2D dropletTex;

        public ParticleSystem CoreSpray => coreSpray;
        public bool HasParticles => coreSpray != null;

        public SprayParticleLayers(Transform parent)
        {
            parentTransform = parent;
        }

        /// <summary>
        /// Create all particle layers for realistic spray
        /// </summary>
        public void CreateAllLayers()
        {
            // Create container
            containerObj = new GameObject("SprayParticleLayers");
            containerObj.transform.SetParent(parentTransform);
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;

            // Create textures
            softCircleTex = SprayMaterialCreator.GetSoftCircleTexture();
            dropletTex = SprayMaterialCreator.GetDropletTexture();

            // Create layers using factory (order matters for rendering)
            mistLayer = SprayLayerFactory.CreateMistLayer(containerObj.transform, softCircleTex);
            coreSpray = SprayLayerFactory.CreateCoreLayer(containerObj.transform, softCircleTex);
            dropletLayer = SprayLayerFactory.CreateDropletLayer(containerObj.transform, dropletTex);
            glowLayer = SprayLayerFactory.CreateGlowLayer(containerObj.transform, softCircleTex);
        }

        /// <summary>
        /// Play burst on all layers
        /// </summary>
        public void PlayBurst(int baseCount)
        {
            int coreCount = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    baseCount
                        * (SpraySettings.EmissionRate / (float)SpraySettings.VisualEmissionRate)
                )
            );
            PlayBurstOnSystem(coreSpray, (short)coreCount);
            PlayBurstOnSystem(mistLayer, (short)(baseCount * 0.75f));
            PlayBurstOnSystem(dropletLayer, (short)(baseCount * 0.22f));
            PlayBurstOnSystem(glowLayer, (short)(baseCount * 0.1f));
        }

        private void PlayBurstOnSystem(ParticleSystem ps, short count)
        {
            if (ps == null)
                return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = ps.emission;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            // Prime the nozzle immediately without creating a visible volley. Most
            // particles arrive as an even flow across the complete spray time.
            int initialCount = Mathf.Clamp(Mathf.RoundToInt(count * 0.03f), 1, 4);
            emission.rateOverTime =
                (count - initialCount) / Mathf.Max(0.01f, SpraySettings.BurstDuration);
            ps.Play();
            ps.Emit(initialCount);
        }

        /// <summary>
        /// Update direction and position for all layers
        /// </summary>
        public void SetDirectionAndPosition(Vector2 direction, Vector3 position)
        {
            if (containerObj == null)
                return;

            containerObj.transform.position = position;

            // Unity cone emits along local +Z. Use LookRotation to point +Z toward the
            // spray direction. Vector3.up as up keeps the spray flat on the ground plane.
            Vector3 sprayDir3D = direction.normalized.ToWorld();
            if (sprayDir3D.sqrMagnitude > 0.001f)
                containerObj.transform.rotation = Quaternion.LookRotation(sprayDir3D, Vector3.up);
        }

        /// <summary>
        /// Update parameters based on current stats
        /// </summary>
        public void UpdateForStats(float range, float width)
        {
            UpdateLayerForStats(coreSpray, range, width * 0.75f, 0.35f);
            UpdateLayerForStats(mistLayer, range, width, 0.4f);
            UpdateLayerForStats(dropletLayer, range, width * 0.9f, 0.3f);
            UpdateLayerForStats(glowLayer, range, width * 0.5f, 0.25f);
        }

        private void UpdateLayerForStats(
            ParticleSystem ps,
            float range,
            float angle,
            float lifetimeBase
        )
        {
            if (ps == null)
                return;

            var main = ps.main;
            float speed = range / lifetimeBase;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.85f, speed * 1.15f);

            var shape = ps.shape;
            shape.angle = angle * 0.5f;
        }

        /// <summary>
        /// Stop all layers
        /// </summary>
        public void Stop()
        {
            coreSpray?.Stop();
            mistLayer?.Stop();
            dropletLayer?.Stop();
            glowLayer?.Stop();
        }

        /// <summary>
        /// Get particle speed for damage timing
        /// </summary>
        public float GetParticleSpeed()
        {
            if (coreSpray == null)
                return SpraySettings.BaseSprayRange / 0.35f;
            return coreSpray.main.startSpeed.constantMax;
        }
    }
}
