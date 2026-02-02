using UnityEngine;

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
        SprayLayerFactory.SetupMainModule(main,
            lifetimeMin: 0.35f, lifetimeMax: 0.55f,
            speedMultMin: 0.85f, speedMultMax: 1.0f,
            sizeMin: 0.03f, sizeMax: 0.07f,
            color: new Color(0.95f, 0.97f, 1f, 0.6f),
            maxParticles: 150, gravity: -0.005f);
        
        SprayLayerFactory.SetupEmission(ps);
        SprayLayerFactory.SetupConeShape(ps, angle: 1f, radius: 0.008f, radiusThickness: 0.8f);  // Nearly straight
        
        // Size - small at start, grow after spread, then shrink
        SprayLayerFactory.SetupSizeOverLifetime(ps,
            (0f, 0.4f), (0.33f, 0.6f), (0.55f, 1f), (0.8f, 0.7f), (1f, 0.2f));
        
        // Color - soft fade after spreading
        SprayLayerFactory.SetupColorOverLifetime(ps,
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.98f, 0.99f, 1f), 0f),
                new GradientColorKey(new Color(0.95f, 0.97f, 1f), 0.5f),
                new GradientColorKey(new Color(0.9f, 0.95f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.5f, 0.4f),
                new GradientAlphaKey(0.15f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        
        SprayLayerFactory.SetupNoise(ps, strength: 0.12f, frequency: 2f, scrollSpeed: 0.2f);
        SprayLayerFactory.SetupDelayedSpread(ps, maxSpreadVelocity: 3.0f);  // Wider fan out
        SprayLayerFactory.SetupBillboardRenderer(ps, texture, SprayMaterialCreator.GetSprayMistMaterial(), -1);
        
        return ps;
    }
}
