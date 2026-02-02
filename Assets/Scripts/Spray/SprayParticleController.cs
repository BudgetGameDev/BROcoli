using UnityEngine;

/// <summary>
/// Handles particle system creation, configuration, and runtime updates for the spray weapon.
/// Uses multi-layer particle system for realistic 3D PBR-style spray effect.
/// Manages velocity compensation for isometric view and spray direction updates.
/// </summary>
public class SprayParticleController
{
    private ParticleSystem sprayParticles;
    private Transform parentTransform;
    private Vector2 currentDirection = Vector2.right;
    
    // Multi-layer particle system for realistic spray
    private SprayParticleLayers particleLayers;
    private bool useLayeredParticles = true;
    
    public ParticleSystem Particles => useLayeredParticles ? particleLayers?.CoreSpray : sprayParticles;
    
    /// <summary>
    /// Get the current particle speed (for damage timing calculations)
    /// </summary>
    public float GetParticleSpeed()
    {
        if (useLayeredParticles && particleLayers != null)
            return particleLayers.GetParticleSpeed();
        if (sprayParticles == null) return SpraySettings.BaseSprayRange / SpraySettings.ParticleLifetimeBase;
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
        particleObj.transform.localPosition = new Vector3(0, 0, -0.5f);
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
        main.startSpeed = new ParticleSystem.MinMaxCurve(targetSpeed * 0.9f, targetSpeed * 1.1f);
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
            new GradientColorKey[] { 
                new GradientColorKey(SpraySettings.SprayColor, 0f), 
                new GradientColorKey(SpraySettings.SprayColor, 0.8f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0.9f, 0f), 
                new GradientAlphaKey(0.8f, 0.7f),
                new GradientAlphaKey(0.3f, 1f)
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
        
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = SpraySettings.SprayColor;
    }

    /// <summary>
    /// Update particle system parameters for current range and width stats
    /// </summary>
    public void UpdateForStats(float currentRange, float currentWidth)
    {
        // Update layered particles if using them
        if (useLayeredParticles && particleLayers != null)
        {
            particleLayers.UpdateForStats(currentRange, currentWidth);
        }
        
        // Also update legacy system if it exists
        if (sprayParticles == null) return;
        
        var main = sprayParticles.main;
        // Calculate speed so particles travel full range during their lifetime
        float targetSpeed = currentRange / SpraySettings.ParticleLifetimeBase;
        main.startSpeed = new ParticleSystem.MinMaxCurve(targetSpeed * 0.9f, targetSpeed * 1.1f);
        main.startLifetime = SpraySettings.ParticleLifetimeBase;
        main.gravityModifier = 0f;
        
        // Update cone angle based on spray width
        var shape = sprayParticles.shape;
        shape.angle = currentWidth * 0.5f; // Half angle for cone
    }

    /// <summary>
    /// Set the spray direction and origin position.
    /// Direction is 2D only (XY plane), particles stay at z=-0.5.
    /// Unity's Cone shape emits along local +Z axis by default.
    /// </summary>
    public void SetSprayDirectionAndPosition(Vector2 direction, Vector3 nozzleWorldPos, float currentRange, float currentWidth)
    {
        currentDirection = direction.normalized;
        
        // Update layered particles
        if (useLayeredParticles && particleLayers != null)
        {
            particleLayers.SetDirectionAndPosition(direction, nozzleWorldPos);
            particleLayers.UpdateForStats(currentRange, currentWidth);
        }
        
        // Also update legacy system if active
        if (sprayParticles == null || !sprayParticles.gameObject.activeInHierarchy) return;
        
        // Update speed based on current range
        var main = sprayParticles.main;
        float targetSpeed = currentRange / SpraySettings.ParticleLifetimeBase;
        main.startSpeed = new ParticleSystem.MinMaxCurve(targetSpeed * 0.9f, targetSpeed * 1.1f);
        
        // Update cone angle
        var shape = sprayParticles.shape;
        shape.angle = currentWidth * 0.5f;
        
        // Position the particle system at the nozzle world position
        sprayParticles.transform.position = new Vector3(nozzleWorldPos.x, nozzleWorldPos.y, -0.5f);
        
        // Unity cone emits along local +Z. For 2D on XY plane, use LookRotation to point +Z
        // toward the spray direction. Vector3.back as up keeps the spray flat on the XY plane.
        Vector3 sprayDir3D = new Vector3(direction.x, direction.y, 0f).normalized;
        if (sprayDir3D.sqrMagnitude > 0.001f)
            sprayParticles.transform.rotation = Quaternion.LookRotation(sprayDir3D, Vector3.back);
        
        // Reset shape position/rotation - transform handles everything
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
    }

    /// <summary>
    /// Apply velocity and direction - rotates particle transform to spray in the given world direction.
    /// Unity's Cone shape emits along local +Z axis.
    /// </summary>
    public void ApplyVelocityCompensation(Vector2 sprayDirection, float currentRange, float currentWidth)
    {
        if (sprayParticles == null) return;
        
        currentDirection = sprayDirection.normalized;
        
        // Update speed based on current range
        var main = sprayParticles.main;
        float targetSpeed = currentRange / SpraySettings.ParticleLifetimeBase;
        main.startSpeed = new ParticleSystem.MinMaxCurve(targetSpeed * 0.9f, targetSpeed * 1.1f);
        
        // Update cone angle
        var shape = sprayParticles.shape;
        shape.angle = currentWidth * 0.5f;
        
        // Unity's Cone shape emits particles along the local +Z axis.
        // For 2D gameplay on the XY plane, rotate the transform so +Z points in the spray direction.
        Vector3 sprayDir3D = new Vector3(sprayDirection.x, sprayDirection.y, 0f);
        sprayParticles.transform.rotation = Quaternion.LookRotation(sprayDir3D, Vector3.back);
        
        // Reset shape rotation - transform handles direction
        shape.rotation = Vector3.zero;
    }

    /// <summary>
    /// Update shape position for nozzle offset based on current direction
    /// </summary>
    public void UpdateNozzlePosition()
    {
        if (sprayParticles == null || !sprayParticles.gameObject.activeInHierarchy) return;
        
        // Position the spawn point at the nozzle location relative to player
        // The nozzle moves with the hand which rotates around the player
        var shape = sprayParticles.shape;
        Vector3 nozzleOffset = new Vector3(
            currentDirection.x * SpraySettings.NozzleOffset,
            currentDirection.y * SpraySettings.NozzleOffset,
            0
        );
        shape.position = nozzleOffset;
    }

    /// <summary>
    /// Play a spray burst with specified particle count
    /// </summary>
    public void PlayBurst()
    {
        // Play on layered system
        if (useLayeredParticles && particleLayers != null)
        {
            int baseCount = (int)(SpraySettings.EmissionRate * SpraySettings.BurstDuration * 1.5f);
            particleLayers.PlayBurst(baseCount);
        }
        
        // Also play on legacy if active
        if (sprayParticles != null && sprayParticles.gameObject.activeInHierarchy)
        {
            sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            var emission = sprayParticles.emission;
            short burstCount = (short)(SpraySettings.EmissionRate * SpraySettings.BurstDuration * 1.5f);
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, burstCount) });
            
            sprayParticles.Play();
        }
    }

    /// <summary>
    /// Start continuous spray emission
    /// </summary>
    public void Play()
    {
        // Layered system uses burst mode
        if (useLayeredParticles && particleLayers != null)
        {
            int baseCount = (int)(SpraySettings.EmissionRate * SpraySettings.BurstDuration);
            particleLayers.PlayBurst(baseCount);
        }
        sprayParticles?.Play();
    }

    /// <summary>
    /// Stop particle emission
    /// </summary>
    public void Stop()
    {
        particleLayers?.Stop();
        sprayParticles?.Stop();
    }

    /// <summary>
    /// Check if particle system exists
    /// </summary>
    public bool HasParticles => (useLayeredParticles && particleLayers != null && particleLayers.HasParticles) 
        || sprayParticles != null;
}
