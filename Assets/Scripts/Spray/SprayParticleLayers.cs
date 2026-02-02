using UnityEngine;

/// <summary>
/// Creates and manages multiple particle system layers for a realistic 3D spray effect.
/// Combines core spray, mist, droplets, and glow for PBR-like appearance.
/// </summary>
public class SprayParticleLayers
{
    // Particle system layers
    private ParticleSystem coreSpray;      // Dense center spray
    private ParticleSystem mistLayer;       // Outer fog/mist
    private ParticleSystem dropletLayer;    // Individual droplets
    private ParticleSystem glowLayer;       // Bright highlights
    
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
        softCircleTex = SprayMaterialCreator.CreateSoftCircleTexture(64);
        dropletTex = SprayMaterialCreator.CreateDropletTexture(32);
        
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
        PlayBurstOnSystem(coreSpray, (short)(baseCount * 1.0f));
        PlayBurstOnSystem(mistLayer, (short)(baseCount * 0.4f));
        PlayBurstOnSystem(dropletLayer, (short)(baseCount * 0.6f));
        PlayBurstOnSystem(glowLayer, (short)(baseCount * 0.15f));
    }
    
    private void PlayBurstOnSystem(ParticleSystem ps, short count)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, count) });
        ps.Play();
    }
    
    /// <summary>
    /// Update direction and position for all layers
    /// </summary>
    public void SetDirectionAndPosition(Vector2 direction, Vector3 position)
    {
        if (containerObj == null) return;
        
        containerObj.transform.position = new Vector3(position.x, position.y, -0.5f);
        
        // Unity cone emits along local +Z. For 2D on XY plane, use LookRotation to point +Z
        // toward the spray direction. Vector3.back as up keeps the spray flat on the XY plane.
        Vector3 sprayDir3D = new Vector3(direction.x, direction.y, 0f).normalized;
        if (sprayDir3D.sqrMagnitude > 0.001f)
            containerObj.transform.rotation = Quaternion.LookRotation(sprayDir3D, Vector3.back);
    }
    
    /// <summary>
    /// Update parameters based on current stats
    /// </summary>
    public void UpdateForStats(float range, float width)
    {
        UpdateLayerForStats(coreSpray, range, width * 0.6f, 0.35f);
        UpdateLayerForStats(mistLayer, range, width * 0.9f, 0.4f);
        UpdateLayerForStats(dropletLayer, range, width * 0.75f, 0.3f);
        UpdateLayerForStats(glowLayer, range, width * 0.4f, 0.25f);
    }
    
    private void UpdateLayerForStats(ParticleSystem ps, float range, float angle, float lifetimeBase)
    {
        if (ps == null) return;
        
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
        if (coreSpray == null) return SpraySettings.BaseSprayRange / 0.35f;
        return coreSpray.main.startSpeed.constantMax;
    }
}
