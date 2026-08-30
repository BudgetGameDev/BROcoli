using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Creates the dense core spray layer - main visible spray particles.
    /// </summary>
    public static class SprayLayerCore
    {
        public static ParticleSystem Create(Transform parent, Texture2D texture)
        {
            var ps = SprayLayerFactory.SetupLayerObject(parent, "CoreSpray");

            // Main - dense beam, tight stream at start
            var main = ps.main;
            SprayLayerFactory.SetupMainModule(
                main,
                lifetimeMin: 0.3f,
                lifetimeMax: 0.5f,
                speedMultMin: 0.9f,
                speedMultMax: 1.1f,
                sizeMin: 0.035f,
                sizeMax: 0.075f,
                color: new Color(0.9f, 0.96f, 1f, 0.75f),
                maxParticles: 400,
                gravity: 0.02f
            );

            SprayLayerFactory.SetupEmission(ps);
            // Start at the nozzle itself. Runtime stat tuning supplies the cone angle
            // before emission so the plume fans out immediately instead of forming a barrel.
            SprayLayerFactory.SetupConeShape(ps, angle: 0.5f, radius: 0f);

            // Bloom just beyond the nozzle, then shrink as the plume dissipates.
            SprayLayerFactory.SetupSizeOverLifetime(
                ps,
                (0f, 0.2f),
                (0.08f, 0.75f),
                (0.45f, 1f),
                (0.8f, 0.6f),
                (1f, 0.15f)
            );

            // Color - bright and solid at start, fade after spreading
            SprayLayerFactory.SetupColorOverLifetime(
                ps,
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.35f),
                    new GradientColorKey(new Color(0.92f, 0.96f, 1f), 0.7f),
                    new GradientColorKey(new Color(0.85f, 0.92f, 1f), 1f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.75f, 0f),
                    new GradientAlphaKey(0.7f, 0.33f),
                    new GradientAlphaKey(0.4f, 0.65f),
                    new GradientAlphaKey(0f, 1f),
                }
            );

            SprayLayerFactory.SetupNoise(ps, strength: 0.08f, frequency: 3f, scrollSpeed: 0.3f);
            SprayLayerFactory.SetupBillboardRenderer(
                ps,
                texture,
                SprayMaterialCreator.GetSprayCoreMaterial(),
                0
            );
            // Enable collision for particle-based hit detection
            SprayLayerFactory.SetupCollision(ps, sendCollisionMessages: true);

            // Add collision handler component for damage dealing
            ps.gameObject.AddComponent<SprayParticleCollisionHandler>();

            return ps;
        }
    }
}
