using UnityEngine;

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
        SprayLayerFactory.SetupMainModule(main,
            lifetimeMin: 0.3f, lifetimeMax: 0.5f,
            speedMultMin: 0.9f, speedMultMax: 1.1f,
            sizeMin: 0.02f, sizeMax: 0.05f,
            color: new Color(1f, 1f, 1f, 0.95f),
            maxParticles: 400, gravity: 0.02f);
        
        SprayLayerFactory.SetupEmission(ps);
        SprayLayerFactory.SetupConeShape(ps, angle: 0.5f, radius: 0.005f);  // Nearly straight line
        
        // Size - consistent at start, then shrink as it fizzles
        SprayLayerFactory.SetupSizeOverLifetime(ps,
            (0f, 0.7f), (0.33f, 0.9f), (0.5f, 1f), (0.8f, 0.6f), (1f, 0.15f));
        
        // Color - bright and solid at start, fade after spreading
        SprayLayerFactory.SetupColorOverLifetime(ps,
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.35f),
                new GradientColorKey(new Color(0.92f, 0.96f, 1f), 0.7f),
                new GradientColorKey(new Color(0.85f, 0.92f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.95f, 0.33f),
                new GradientAlphaKey(0.5f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        
        SprayLayerFactory.SetupNoise(ps, strength: 0.08f, frequency: 3f, scrollSpeed: 0.3f);
        SprayLayerFactory.SetupDelayedSpread(ps, maxSpreadVelocity: 2.5f);  // Strong fan out after 33%
        SprayLayerFactory.SetupBillboardRenderer(ps, texture, SprayMaterialCreator.GetSprayCoreMaterial(), 0);
        
        // Enable collision for particle-based hit detection
        SprayLayerFactory.SetupCollision(ps);
        
        // Add collision handler component for damage dealing
        ps.gameObject.AddComponent<SprayParticleCollisionHandler>();
        
        return ps;
    }
}
