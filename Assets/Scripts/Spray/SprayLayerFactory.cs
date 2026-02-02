using UnityEngine;

/// <summary>
/// Factory methods for creating spray particle system layers.
/// Provides shared setup utilities used by all layer types.
/// </summary>
public static class SprayLayerFactory
{
    public static ParticleSystem CreateCoreLayer(Transform parent, Texture2D texture)
    {
        return SprayLayerCore.Create(parent, texture);
    }
    
    public static ParticleSystem CreateMistLayer(Transform parent, Texture2D texture)
    {
        return SprayLayerMist.Create(parent, texture);
    }
    
    public static ParticleSystem CreateDropletLayer(Transform parent, Texture2D texture)
    {
        return SprayLayerDroplet.Create(parent, texture);
    }
    
    public static ParticleSystem CreateGlowLayer(Transform parent, Texture2D texture)
    {
        return SprayLayerGlow.Create(parent, texture);
    }
    
    /// <summary>
    /// Common setup for particle system game object
    /// </summary>
    public static ParticleSystem SetupLayerObject(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        var ps = obj.AddComponent<ParticleSystem>();
        // Stop the particle system before configuring - Unity requires this to set duration
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }
    
    /// <summary>
    /// Setup common main module properties
    /// </summary>
    public static void SetupMainModule(ParticleSystem.MainModule main, float lifetimeMin, float lifetimeMax,
        float speedMultMin, float speedMultMax, float sizeMin, float sizeMax, Color color, int maxParticles, float gravity)
    {
        main.duration = SpraySettings.BurstDuration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        float speed = SpraySettings.BaseSprayRange / ((lifetimeMin + lifetimeMax) * 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * speedMultMin, speed * speedMultMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.gravityModifier = gravity;
    }
    
    /// <summary>
    /// Setup emission module (burst mode, no continuous emission)
    /// </summary>
    public static void SetupEmission(ParticleSystem ps)
    {
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
    }
    
    /// <summary>
    /// Setup cone shape for spray emission
    /// </summary>
    public static void SetupConeShape(ParticleSystem ps, float angle, float radius, float radiusThickness = 1f)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = radius;
        shape.radiusThickness = radiusThickness;
    }
    
    /// <summary>
    /// Setup size over lifetime curve
    /// </summary>
    public static void SetupSizeOverLifetime(ParticleSystem ps, params (float time, float value)[] keys)
    {
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        foreach (var (time, value) in keys)
            sizeCurve.AddKey(time, value);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }
    
    /// <summary>
    /// Setup color over lifetime gradient
    /// </summary>
    public static void SetupColorOverLifetime(ParticleSystem ps, GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
    {
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);
        colorOverLifetime.color = gradient;
    }
    
    /// <summary>
    /// Setup noise module for turbulence
    /// </summary>
    public static void SetupNoise(ParticleSystem ps, float strength, float frequency, float scrollSpeed = 0f, bool damping = true)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        if (scrollSpeed > 0) noise.scrollSpeed = scrollSpeed;
        noise.damping = damping;
    }
    
    /// <summary>
    /// Setup billboard renderer
    /// </summary>
    public static void SetupBillboardRenderer(ParticleSystem ps, Texture2D texture, Material mat, int sortingOrderOffset = 0)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = SpraySettings.ParticleSortingOrder + sortingOrderOffset;
        mat.mainTexture = texture;
        renderer.material = mat;
    }
    
    /// <summary>
    /// Setup delayed spread - particles start tight then fan out after SpreadDelayPercent of lifetime
    /// </summary>
    public static void SetupDelayedSpread(ParticleSystem ps, float maxSpreadVelocity)
    {
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        
        // Radial velocity creates the fan-out effect
        AnimationCurve radialCurve = new AnimationCurve();
        radialCurve.AddKey(0f, 0f);                                    // No spread at start
        radialCurve.AddKey(SpraySettings.SpreadDelayPercent, 0f);      // Still tight at 33%
        radialCurve.AddKey(0.6f, maxSpreadVelocity * 0.7f);            // Start spreading
        radialCurve.AddKey(1f, maxSpreadVelocity);                     // Full spread at end
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(1f, radialCurve);
    }
    
    /// <summary>
    /// Setup collision module for particle-based hit detection.
    /// Particles will collide with enemies and trigger damage on impact.
    /// </summary>
    public static void SetupCollision(ParticleSystem ps)
    {
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision2D;
        collision.sendCollisionMessages = true;
        collision.collidesWith = LayerMask.GetMask("Enemy");
        collision.maxCollisionShapes = 20;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.radiusScale = 1.5f;  // Slightly larger collision radius for better hit detection
        collision.dampen = 0f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 0.5f;  // Particle loses 50% life on hit (can still hit others)
    }
}
