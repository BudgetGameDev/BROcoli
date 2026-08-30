using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Creates the outer mist/fog layer - soft surrounding particles.
    /// </summary>
    public static class SprayLayerMist
    {
        public static ParticleSystem Create(Transform parent, Texture2D texture)
        {
            var ps = SprayLayerFactory.SetupLayerObject(parent, "MistLayer");

            // Main - soft background, starts tight with beam
            var main = ps.main;
            SprayLayerFactory.SetupMainModule(
                main,
                lifetimeMin: 0.35f,
                lifetimeMax: 0.55f,
                speedMultMin: 0.85f,
                speedMultMax: 1.0f,
                sizeMin: 0.06f,
                sizeMax: 0.14f,
                color: new Color(0.82f, 0.92f, 1f, 0.75f),
                maxParticles: 300,
                gravity: -0.005f
            );

            SprayLayerFactory.SetupEmission(ps);
            SprayLayerFactory.SetupConeShape(ps, angle: 1f, radius: 0f);

            // Mist blooms from the same pinpoint nozzle origin.
            SprayLayerFactory.SetupSizeOverLifetime(
                ps,
                (0f, 0.1f),
                (0.1f, 0.55f),
                (0.55f, 1f),
                (0.8f, 0.7f),
                (1f, 0.2f)
            );

            // Color - soft fade after spreading
            SprayLayerFactory.SetupColorOverLifetime(
                ps,
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.98f, 0.99f, 1f), 0f),
                    new GradientColorKey(new Color(0.95f, 0.97f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.9f, 0.95f, 1f), 1f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.65f, 0f),
                    new GradientAlphaKey(0.45f, 0.4f),
                    new GradientAlphaKey(0.15f, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                }
            );

            SprayLayerFactory.SetupNoise(ps, strength: 0.12f, frequency: 2f, scrollSpeed: 0.2f);
            SprayLayerFactory.SetupBillboardRenderer(
                ps,
                texture,
                SprayMaterialCreator.GetSprayMistMaterial(),
                -1
            );
            SprayLayerFactory.SetupCollision(ps);

            return ps;
        }
    }
}
