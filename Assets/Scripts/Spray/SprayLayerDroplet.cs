using UnityEngine;

/// <summary>
/// Creates the scattered droplet layer - small fast individual droplets.
/// </summary>
public static class SprayLayerDroplet
{
    public static ParticleSystem Create(Transform parent, Texture2D texture)
    {
        var ps = SprayLayerFactory.SetupLayerObject(parent, "DropletLayer");
        
        // Main - individual droplets that scatter after spreading
        var main = ps.main;
        SprayLayerFactory.SetupMainModule(main,
            lifetimeMin: 0.25f, lifetimeMax: 0.45f,
            speedMultMin: 0.95f, speedMultMax: 1.1f,
            sizeMin: 0.015f, sizeMax: 0.04f,
            color: new Color(1f, 1f, 1f, 0.9f),
            maxParticles: 200, gravity: 0.04f);
        
        SprayLayerFactory.SetupEmission(ps);
        SprayLayerFactory.SetupConeShape(ps, angle: 0.5f, radius: 0.005f);  // Nearly straight line
        
        // Color - bright droplets that fade as they scatter
        SprayLayerFactory.SetupColorOverLifetime(ps,
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.4f),
                new GradientColorKey(new Color(0.9f, 0.95f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.33f),
                new GradientAlphaKey(0.4f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        
        SprayLayerFactory.SetupNoise(ps, strength: 0.18f, frequency: 4f);
        SprayLayerFactory.SetupDelayedSpread(ps, maxSpreadVelocity: 2.2f);  // Scatter after 33%
        SprayLayerFactory.SetupBillboardRenderer(ps, texture, SprayMaterialCreator.GetSprayDropletMaterial(), 1);
        
        return ps;
    }
}
