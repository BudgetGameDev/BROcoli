using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sanitizer spray weapon for the player.
/// Uses a particle system for visual effect and deals splash damage based on particle hits.
/// Range and width are dynamically read from PlayerStats and can be upgraded.
/// </summary>
public class SanitizerSpray : MonoBehaviour
{
    [Header("Spray Settings (Base values - overridden by PlayerStats)")]
    [SerializeField] private float baseSprayRange = 1.8f;
    [SerializeField] private float baseSprayAngle = 60f; // Cone angle in degrees
    [SerializeField] private float baseDamagePerParticle = 0.5f; // Damage per particle hit
    [SerializeField] private float damageTickRate = 0.1f; // How often damage is calculated from particle hits
    
    [Header("Burst Settings")]
    [SerializeField] private float burstDuration = 0.3f; // How long each spray burst lasts
    [SerializeField] private float burstCooldown = 0.1f; // Minimum time between bursts
    private float lastBurstTime = -10f;
    private float currentBurstEndTime = 0f;
    
    [Header("References")]
    [SerializeField] private ParticleSystem sprayParticles;
    [SerializeField] private ProceduralSprayAudio sprayAudio;
    [SerializeField] private Transform handTransform; // The hand holding the spray
    [SerializeField] private SpriteRenderer handSprite;
    [SerializeField] private SpriteRenderer sprayCanSprite;
    
    [Header("Visual Settings")]
    [SerializeField] private float handOffset = 0.6f; // Offset from player center (far enough to see spray)
    [SerializeField] private float visualZOffset = -0.5f; // Z position for visual depth (negative = in front)
    [SerializeField] private float isometricYOffset = 0.3f; // Extra Y offset when spraying upward (for isometric camera)
    [SerializeField] private float cameraAngle = 60f; // Camera tilt angle in degrees (60 = typical isometric)
    [SerializeField] private Color sprayColor = new Color(0.7f, 0.9f, 1f, 0.6f); // Light blue sanitizer
    [SerializeField] private bool flipHandWithDirection = true;
    
    [Header("Hand Animation")]
    [SerializeField] private float handRotationSpeed = 720f; // Degrees per second for hand rotation
    [SerializeField] private float aimDelayBeforeSpray = 0.08f; // Delay to allow hand to aim before spraying
    [SerializeField] private bool showHandAlways = true; // Keep hand visible at all times
    
    [Header("Particle Settings")]
    [SerializeField] private float particleSpeedMultiplier = 2.5f;  // Multiplied by range to get speed
    [SerializeField] private float particleLifetimeBase = 0.5f;    // Base lifetime, adjusted for range
    [SerializeField] private int emissionRate = 45;
    
    // Dynamic stats from PlayerStats
    private float currentRange;
    private float currentWidth;
    
    // Track if we're in a burst (for damage processing)
    private bool isInBurst = false;
    
    // Hand animation state
    private float currentHandAngle = 0f;
    private float targetHandAngle = 0f;
    private Vector2 targetDirection = Vector2.right;
    private bool isAiming = false;
    private float aimStartTime = 0f;
    private Vector2 pendingSprayDirection;
    private bool hasPendingSpray = false;
    
    // Public property for PlayerController to check range
    public float SprayRange => currentRange;
    
    private bool isSpraying = false;
    private float nextDamageTick = 0f;
    private Vector2 sprayDirection = Vector2.right;
    private PlayerStats playerStats;
    private Transform playerTransform;
    
    // Particle collision tracking for splash damage
    private Dictionary<EnemyBase, int> particleHitCounts = new Dictionary<EnemyBase, int>();
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();
    
    // Cache for enemies in range
    private Collider2D[] hitBuffer = new Collider2D[20];

    void Awake()
    {
        // Create particle system if not assigned
        if (sprayParticles == null)
        {
            CreateSprayParticleSystem();
        }
        
        // Create hand visuals if not assigned
        if (handTransform == null)
        {
            CreateHandVisuals();
        }
        
        // Get audio component
        if (sprayAudio == null)
        {
            sprayAudio = GetComponent<ProceduralSprayAudio>();
            if (sprayAudio == null)
            {
                sprayAudio = gameObject.AddComponent<ProceduralSprayAudio>();
            }
        }
        
        playerTransform = transform.parent;
        if (playerTransform != null)
        {
            // Try to find PlayerStats - might be on parent or a child of parent
            playerStats = playerTransform.GetComponent<PlayerStats>();
            if (playerStats == null)
            {
                playerStats = playerTransform.GetComponentInChildren<PlayerStats>();
            }
            if (playerStats == null)
            {
                playerStats = FindFirstObjectByType<PlayerStats>();
            }
        }
        
        // Initialize dynamic stats
        UpdateStatsFromPlayer();
    }

    void Start()
    {
        // Ensure particles are stopped initially
        if (sprayParticles != null)
        {
            sprayParticles.Stop();
        }
        
        // Show hand based on setting
        SetHandVisible(showHandAlways);
        
        // Initialize hand angle
        currentHandAngle = 0f;
        targetHandAngle = 0f;
    }
    
    /// <summary>
    /// Update spray stats from PlayerStats (call this when stats change)
    /// </summary>
    public void UpdateStatsFromPlayer()
    {
        if (playerStats != null)
        {
            currentRange = playerStats.CurrentSprayRange;
            currentWidth = playerStats.CurrentSprayWidth;
        }
        else
        {
            currentRange = baseSprayRange;
            currentWidth = baseSprayAngle;
        }
        
        // Update particle system to match new stats
        UpdateParticleSystemForStats();
    }
    
    /// <summary>
    /// Update particle system parameters to match current range and width
    /// </summary>
    private void UpdateParticleSystemForStats()
    {
        if (sprayParticles == null) return;
        
        // Calculate particle speed and lifetime to reach exactly the current range
        // Travel distance = speed * lifetime, solve for speed given fixed lifetime ratio
        float targetDistance = currentRange * 1.25f; // Overshoot slightly so particles reach enemies
        float particleSpeed = targetDistance / particleLifetimeBase;
        
        var main = sprayParticles.main;
        main.startSpeed = particleSpeed;
        main.startLifetime = particleLifetimeBase;
        main.gravityModifier = 0f;  // Ensure no gravity - spray should travel in direction aimed
        
        // Update cone angle but preserve rotation
        var shape = sprayParticles.shape;
        shape.angle = currentWidth * 0.5f;
        // Ensure shape rotation is correct (emit along X axis)
        shape.rotation = new Vector3(0, 0, -90);
    }

    void Update()
    {
        // Periodically refresh stats from player (in case of upgrades)
        if (Time.frameCount % 30 == 0) // Every 30 frames
        {
            UpdateStatsFromPlayer();
        }
        
        // Animate hand rotation smoothly
        AnimateHandRotation();
        
        // Check if we have a pending spray and aim delay has passed
        if (hasPendingSpray && Time.time >= aimStartTime + aimDelayBeforeSpray)
        {
            // Check if hand is close enough to target angle
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(currentHandAngle, targetHandAngle));
            if (angleDiff < 15f) // Within 15 degrees is good enough
            {
                ExecutePendingSpray();
            }
        }
        
        // Check if burst has ended
        if (isInBurst && Time.time >= currentBurstEndTime)
        {
            isInBurst = false;
        }
        
        // Process damage during spray or burst
        if (isSpraying || isInBurst)
        {
            UpdateSprayDirection();
            ProcessParticleHitDamage();
        }
    }
    
    /// <summary>
    /// Called by Unity when particles collide with triggers
    /// </summary>
    void OnParticleTrigger()
    {
        if (sprayParticles == null) return;
        
        // Get particles that entered triggers
        List<ParticleSystem.Particle> enter = new List<ParticleSystem.Particle>();
        int numEnter = sprayParticles.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
        
        for (int i = 0; i < numEnter; i++)
        {
            // The particle position can help us find which enemy was hit
            Vector3 particlePos = enter[i].position;
            
            // Find enemy at this position
            Collider2D hit = Physics2D.OverlapPoint(particlePos);
            if (hit != null && hit.CompareTag("Enemy"))
            {
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    RegisterParticleHit(enemy);
                }
            }
        }
    }
    
    /// <summary>
    /// Register a particle hit on an enemy for splash damage calculation
    /// </summary>
    private void RegisterParticleHit(EnemyBase enemy)
    {
        if (!particleHitCounts.ContainsKey(enemy))
        {
            particleHitCounts[enemy] = 0;
        }
        particleHitCounts[enemy]++;
    }
    
    /// <summary>
    /// Process accumulated particle hits and deal splash damage
    /// </summary>
    private void ProcessParticleHitDamage()
    {
        if (Time.time < nextDamageTick) return;
        nextDamageTick = Time.time + damageTickRate;
        
        // Also do cone-based detection as backup (particles may not always trigger)
        DetectEnemiesInCone();
        
        // Apply damage based on hit counts
        float damageMultiplier = playerStats != null ? playerStats.CurrentSprayDamageMultiplier : 1f;
        // Use 35% of current damage per tick, multiplied by hit count for splash effect
        float baseDamage = playerStats != null ? playerStats.CurrentDamage * 0.35f : baseDamagePerParticle * 3f;
        
        foreach (var kvp in particleHitCounts)
        {
            EnemyBase enemy = kvp.Key;
            int hitCount = kvp.Value;
            
            if (enemy != null && hitCount > 0)
            {
                // More particles = more damage (splash damage)
                float damage = baseDamage * hitCount * damageMultiplier;
                enemy.TakeDamage(damage, sprayDirection);
            }
        }
        
        // Clear hit counts for next tick
        particleHitCounts.Clear();
    }
    
    /// <summary>
    /// Detect enemies in spray cone and register hits (backup for particle collision)
    /// </summary>
    private void DetectEnemiesInCone()
    {
        Vector2 origin = playerTransform != null ? (Vector2)playerTransform.position : (Vector2)transform.position;
        
        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, currentRange, hitBuffer);
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;
            
            // Check if enemy is within cone angle
            Vector2 toEnemy = ((Vector2)hit.transform.position - origin).normalized;
            float angleToEnemy = Vector2.Angle(sprayDirection, toEnemy);
            
            if (angleToEnemy <= currentWidth * 0.5f)
            {
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    // Register multiple hits based on how centered they are in the cone
                    // More centered = more "particles" hitting = more damage
                    float centeredness = 1f - (angleToEnemy / (currentWidth * 0.5f));
                    int simulatedHits = Mathf.Max(1, Mathf.RoundToInt(3 * centeredness)); // 1-3 hits based on position
                    
                    for (int j = 0; j < simulatedHits; j++)
                    {
                        RegisterParticleHit(enemy);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Start spraying in the given direction
    /// </summary>
    public void StartSpray(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            sprayDirection = direction.normalized;
        }
        
        if (!isSpraying)
        {
            isSpraying = true;
            
            // Start particle effect
            if (sprayParticles != null)
            {
                sprayParticles.Play();
            }
            
            // Start audio
            if (sprayAudio != null)
            {
                sprayAudio.StartSpray();
            }
            
            // Show hand
            SetHandVisible(true);
        }
        
        UpdateSprayDirection();
    }

    /// <summary>
    /// Stop spraying
    /// </summary>
    public void StopSpray()
    {
        if (isSpraying)
        {
            isSpraying = false;
            
            // Stop particle effect
            if (sprayParticles != null)
            {
                sprayParticles.Stop();
            }
            
            // Stop audio
            if (sprayAudio != null)
            {
                sprayAudio.StopSpray();
            }
            
            // Hide hand after a short delay
            Invoke(nameof(HideHand), 0.2f);
        }
    }

    /// <summary>
    /// Fire a single spray burst (for attack-based gameplay)
    /// Returns true if the burst was fired, false if on cooldown
    /// </summary>
    public bool FireSprayBurst(Vector2 direction, float duration = 0.25f)
    {
        // Check cooldown
        if (Time.time < lastBurstTime + burstCooldown)
        {
            return false;
        }
        
        // Check if we're still in an active burst
        if (Time.time < currentBurstEndTime)
        {
            return false;
        }
        
        // Check if we already have a pending spray
        if (hasPendingSpray)
        {
            return false;
        }
        
        if (direction.sqrMagnitude > 0.01f)
        {
            pendingSprayDirection = direction.normalized;
        }
        else
        {
            pendingSprayDirection = sprayDirection;
        }
        
        // Start aiming towards target
        targetDirection = pendingSprayDirection;
        targetHandAngle = Mathf.Atan2(pendingSprayDirection.y, pendingSprayDirection.x) * Mathf.Rad2Deg;
        isAiming = true;
        aimStartTime = Time.time;
        hasPendingSpray = true;
        
        // Show hand and start aiming animation
        SetHandVisible(true);
        
        return true;
    }
    
    /// <summary>
    /// Execute the pending spray after aim delay
    /// </summary>
    private void ExecutePendingSpray()
    {
        if (!hasPendingSpray) return;
        
        hasPendingSpray = false;
        isAiming = false;
        sprayDirection = pendingSprayDirection;
        
        lastBurstTime = Time.time;
        currentBurstEndTime = Time.time + burstDuration;
        isInBurst = true;
        
        UpdateSprayDirection();
        
        // Play burst particles
        if (sprayParticles != null)
        {
            // Stop any existing particles first
            sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            var emission = sprayParticles.emission;
            emission.SetBursts(new ParticleSystem.Burst[] 
            { 
                new ParticleSystem.Burst(0f, (short)(emissionRate * burstDuration * 1.5f)) 
            });
            sprayParticles.Play();
        }
        
        // Play burst audio
        if (sprayAudio != null)
        {
            sprayAudio.PlaySprayBurst();
        }
        
        // Keep hand visible, hide after burst
        if (!showHandAlways)
        {
            Invoke(nameof(HideHand), burstDuration + 0.1f);
        }
        
        // Queue damage tick immediately for burst
        nextDamageTick = 0f;
    }

    public bool IsSpraying => isSpraying;
    public bool IsOnCooldown => Time.time < currentBurstEndTime;

    private void UpdateSprayDirection()
    {
        // Rotate the spray system to face the direction (works for all angles including up/down)
        float angle = Mathf.Atan2(sprayDirection.y, sprayDirection.x) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0, 0, angle);
        
        // Apply isometric camera compensation to particle velocity
        // When aiming "up" on screen (positive Y in world), particles need Z velocity
        // to visually travel towards enemies in isometric view
        ApplyIsometricVelocityCompensation();
        
        // Calculate position offsets for isometric camera visibility
        // When spraying upward (positive Y), we need to offset the spray to stay visible
        // In Diablo-style isometric, "up" on screen is towards the back, so we push spray forward and up
        float yOffset = 0f;
        float zOffset = visualZOffset;
        
        // When spraying upward, move the spray container up and further in front
        // This keeps it visible above the player sprite in isometric view
        if (sprayDirection.y > 0.3f)
        {
            // Interpolate offset based on how much we're aiming upward
            float upwardAmount = Mathf.InverseLerp(0.3f, 1f, sprayDirection.y);
            yOffset = isometricYOffset * upwardAmount;
            zOffset = visualZOffset - (0.3f * upwardAmount); // Push even more forward when aiming up
        }
        
        // Set position with calculated offsets
        transform.localPosition = new Vector3(0, yOffset, zOffset);
        
        // Update hand position and rotation
        if (handTransform != null)
        {
            handTransform.localPosition = new Vector3(handOffset, 0, 0);
            
            // Flip hand sprite based on direction (flip when pointing left)
            if (flipHandWithDirection && handSprite != null)
            {
                // Flip vertically when pointing left so hand stays oriented correctly
                bool pointingLeft = sprayDirection.x < -0.1f;
                handSprite.flipY = pointingLeft;
                if (sprayCanSprite != null) sprayCanSprite.flipY = pointingLeft;
            }
        }
    }

    /// <summary>
    /// Apply velocity compensation so particles visually travel towards enemies in isometric camera view.
    /// When aiming "up" on screen (positive Y), particles need negative Z velocity to rise towards camera.
    /// Only applies to vertical aiming - horizontal spray is unaffected.
    /// </summary>
    private void ApplyIsometricVelocityCompensation()
    {
        if (sprayParticles == null) return;
        
        var velocityOverLifetime = sprayParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        
        // Calculate Z velocity needed to compensate for camera angle
        // Only apply when aiming vertically (up or down)
        // For horizontal aiming, no Z compensation needed
        float verticalAmount = Mathf.Abs(sprayDirection.y);
        
        if (verticalAmount > 0.2f)
        {
            // For a 60-degree camera, when aiming up (Y+), particles need to move towards camera (Z-)
            float angleRad = cameraAngle * Mathf.Deg2Rad;
            float zCompensation = -sprayDirection.y * Mathf.Tan(angleRad) * (currentRange / particleLifetimeBase) * 0.5f;
            
            // Apply the Z velocity - negative Z moves towards camera (appears to go "up" on screen)
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(zCompensation);
        }
        else
        {
            // No Z compensation for mostly horizontal aiming
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0);
        }
        
        // Don't override X/Y - let the particle system's natural direction work
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0);
    }

    private void SetHandVisible(bool visible)
    {
        if (handSprite != null) handSprite.enabled = visible;
        if (sprayCanSprite != null) sprayCanSprite.enabled = visible;
    }

    private void HideHand()
    {
        // Only hide if not set to always show and not spraying
        if (!showHandAlways && !isSpraying && !isInBurst && !hasPendingSpray)
        {
            SetHandVisible(false);
        }
    }
    
    /// <summary>
    /// Smoothly animate the hand rotation towards the target direction
    /// </summary>
    private void AnimateHandRotation()
    {
        // Calculate target angle from target direction (use pending direction if aiming)
        Vector2 aimDir = hasPendingSpray ? pendingSprayDirection : (isInBurst ? sprayDirection : targetDirection);
        targetHandAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        
        // Smoothly rotate towards target
        float angleDiff = Mathf.DeltaAngle(currentHandAngle, targetHandAngle);
        float maxRotation = handRotationSpeed * Time.deltaTime;
        
        if (Mathf.Abs(angleDiff) <= maxRotation)
        {
            currentHandAngle = targetHandAngle;
        }
        else
        {
            currentHandAngle += Mathf.Sign(angleDiff) * maxRotation;
        }
        
        // Normalize angle
        if (currentHandAngle > 180f) currentHandAngle -= 360f;
        if (currentHandAngle < -180f) currentHandAngle += 360f;
        
        // Apply rotation to the spray system (which contains the hand)
        transform.localRotation = Quaternion.Euler(0, 0, currentHandAngle);
        
        // Calculate position offsets for isometric camera visibility
        float yOffset = 0f;
        float zOffset = visualZOffset;
        
        // When aiming upward, adjust position
        if (aimDir.y > 0.3f)
        {
            float upwardAmount = Mathf.InverseLerp(0.3f, 1f, aimDir.y);
            yOffset = isometricYOffset * upwardAmount;
            zOffset = visualZOffset - (0.3f * upwardAmount);
        }
        
        transform.localPosition = new Vector3(0, yOffset, zOffset);
        
        // Update hand flip based on direction
        if (handTransform != null)
        {
            handTransform.localPosition = new Vector3(handOffset, 0, 0);
            
            if (flipHandWithDirection && handSprite != null)
            {
                bool pointingLeft = aimDir.x < -0.1f;
                handSprite.flipY = pointingLeft;
                if (sprayCanSprite != null) sprayCanSprite.flipY = pointingLeft;
            }
        }
    }

    private void CreateSprayParticleSystem()
    {
        GameObject particleObj = new GameObject("SprayParticles");
        particleObj.transform.SetParent(transform);
        particleObj.transform.localPosition = Vector3.zero;
        particleObj.transform.localRotation = Quaternion.identity;
        
        sprayParticles = particleObj.AddComponent<ParticleSystem>();
        
        // Calculate initial particle speed based on range
        float targetDistance = currentRange * 1.25f;
        float particleSpeed = targetDistance / particleLifetimeBase;
        
        // Configure main module
        var main = sprayParticles.main;
        main.duration = burstDuration;
        main.loop = false;  // Don't loop - we use single bursts
        main.startLifetime = particleLifetimeBase;
        main.startSpeed = particleSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = sprayColor;
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.gravityModifier = 0f;  // No gravity - spray should travel in direction aimed
        
        // Configure emission - disable continuous, only use bursts
        var emission = sprayParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;  // No continuous emission
        // Burst will be set when firing
        
        // Configure shape - cone shape for spray (uses currentWidth)
        var shape = sprayParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = currentWidth * 0.5f;
        shape.radius = 0.1f;
        shape.radiusThickness = 1f;
        shape.position = new Vector3(handOffset + 0.2f, 0, 0); // Start from spray can nozzle
        shape.rotation = new Vector3(0, 0, -90); // Rotate cone to emit along local X axis (right) instead of Y (up)
        
        // Configure size over lifetime - particles spread out
        var sizeOverLifetime = sprayParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.3f, 1f);
        sizeCurve.AddKey(1f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // Configure color over lifetime - fade out
        var colorOverLifetime = sprayParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(sprayColor, 0f), 
                new GradientColorKey(sprayColor, 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0.8f, 0f), 
                new GradientAlphaKey(0.6f, 0.5f), 
                new GradientAlphaKey(0f, 1f) 
            }
        );
        colorOverLifetime.color = gradient;
        
        // Configure velocity over lifetime - slow down
        var velocityOverLifetime = sprayParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
        
        // Configure noise for organic spray look
        var noise = sprayParticles.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 2f;
        noise.scrollSpeed = 0.5f;
        
        // Add renderer settings
        var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 10; // Above player
        
        // Use default particle material
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = sprayColor;
    }

    private void CreateHandVisuals()
    {
        // Create hand container
        GameObject handObj = new GameObject("SprayHand");
        handObj.transform.SetParent(transform);
        handObj.transform.localPosition = new Vector3(handOffset, 0, 0);
        handObj.transform.localRotation = Quaternion.identity;
        handTransform = handObj.transform;
        
        // Create hand sprite (simple rectangle for now - can be replaced with actual sprite)
        GameObject handSpriteObj = new GameObject("HandSprite");
        handSpriteObj.transform.SetParent(handTransform);
        handSpriteObj.transform.localPosition = new Vector3(-0.1f, 0, 0);
        handSpriteObj.transform.localScale = new Vector3(0.15f, 0.12f, 1f);
        
        handSprite = handSpriteObj.AddComponent<SpriteRenderer>();
        handSprite.sprite = CreateSimpleSprite(Color.white); // Placeholder - should be skin colored
        handSprite.color = new Color(0.96f, 0.87f, 0.78f); // Skin tone
        handSprite.sortingOrder = 12; // In front of player
        
        // Create spray can sprite
        GameObject canSpriteObj = new GameObject("SprayCanSprite");
        canSpriteObj.transform.SetParent(handTransform);
        canSpriteObj.transform.localPosition = new Vector3(0.08f, 0, 0);
        canSpriteObj.transform.localScale = new Vector3(0.2f, 0.12f, 1f);
        
        sprayCanSprite = canSpriteObj.AddComponent<SpriteRenderer>();
        sprayCanSprite.sprite = CreateSimpleSprite(Color.white);
        sprayCanSprite.color = new Color(0.2f, 0.6f, 0.9f); // Blue sanitizer can
        sprayCanSprite.sortingOrder = 13; // In front of hand
        
        // Create nozzle
        GameObject nozzleObj = new GameObject("Nozzle");
        nozzleObj.transform.SetParent(handTransform);
        nozzleObj.transform.localPosition = new Vector3(0.22f, 0.02f, 0);
        nozzleObj.transform.localScale = new Vector3(0.06f, 0.04f, 1f);
        
        SpriteRenderer nozzleSprite = nozzleObj.AddComponent<SpriteRenderer>();
        nozzleSprite.sprite = CreateSimpleSprite(Color.white);
        nozzleSprite.color = new Color(0.3f, 0.3f, 0.3f); // Dark gray nozzle
        nozzleSprite.sortingOrder = 14; // In front of can
    }

    private Sprite CreateSimpleSprite(Color color)
    {
        // Create a simple 4x4 white texture
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    void OnDrawGizmosSelected()
    {
        // Draw spray range and cone
        Vector3 origin = transform.position;
        if (playerTransform != null) origin = playerTransform.position;
        
        float drawRange = Application.isPlaying ? currentRange : baseSprayRange;
        float drawWidth = Application.isPlaying ? currentWidth : baseSprayAngle;
        
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(origin, drawRange);
        
        // Draw cone
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.5f);
        Vector3 dir = transform.right;
        Vector3 left = Quaternion.Euler(0, 0, drawWidth * 0.5f) * dir;
        Vector3 right = Quaternion.Euler(0, 0, -drawWidth * 0.5f) * dir;
        
        Gizmos.DrawLine(origin, origin + left * drawRange);
        Gizmos.DrawLine(origin, origin + right * drawRange);
    }
}
