using UnityEngine;

/// <summary>
/// Creates sparse bright highlight particles for the spray glow effect.
/// </summary>
public static class SprayLayerGlow
{
    public static ParticleSystem Create(Transform parent, Texture2D texture)
    {
        var ps = SprayLayerFactory.SetupLayerObject(parent, "GlowLayer");
        
        // Main - bright highlight for the core beam
        var main = ps.main;
        SprayLayerFactory.SetupMainModule(main,
            lifetimeMin: 0.2f, lifetimeMax: 0.35f,
            speedMultMin: 0.95f, speedMultMax: 1.05f,
            sizeMin: 0.025f, sizeMax: 0.06f,
            color: new Color(1f, 1f, 1f, 0.85f),
            maxParticles: 60, gravity: 0.01f);
        
        SprayLayerFactory.SetupEmission(ps);
        SprayLayerFactory.SetupConeShape(ps, angle: 0.3f, radius: 0.003f);  // Very tight beam
        
        // Size - consistent at start, shrink after spread
        SprayLayerFactory.SetupSizeOverLifetime(ps,
            (0f, 0.8f), (0.33f, 1f), (0.55f, 0.7f), (1f, 0.1f));
        
        // Color - very bright core, fade quickly after spread
        SprayLayerFactory.SetupColorOverLifetime(ps,
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.33f),
                new GradientColorKey(new Color(0.95f, 0.98f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.85f, 0.33f),
                new GradientAlphaKey(0.25f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        
        SprayLayerFactory.SetupDelayedSpread(ps, maxSpreadVelocity: 1.2f);  // Subtle spread
        SprayLayerFactory.SetupBillboardRenderer(ps, texture, SprayMaterialCreator.GetSprayGlowMaterial(), 2);
        
        return ps;
    }
}
