using UnityEngine;

/// <summary>
/// Sanitizer spray weapon for the player.
/// Uses a particle system for visual effect and deals splash damage based on particle hits.
/// Range and width are dynamically read from PlayerStats and can be upgraded.
/// 
/// This is the main controller that coordinates:
/// - SprayParticleController: Particle effects and velocity
/// - SprayDamageHandler: Damage calculation and enemy detection
/// - SprayHandVisuals: Hand animation and visual positioning
/// - SpraySettings: All configuration constants
/// </summary>
public class SanitizerSpray : MonoBehaviour
{
    // References - can be assigned in scene
    [SerializeField] private ParticleSystem sprayParticles;
    [SerializeField] private ProceduralSprayAudio sprayAudio;
    [SerializeField] private Transform handTransform;
    [SerializeField] private SpriteRenderer handSprite;
    [SerializeField] private SpriteRenderer sprayCanSprite;
    
    // Dynamic stats from PlayerStats
    private float currentRange;
    private float currentWidth;
    
    // Spray state
    private bool isSpraying = false;
    private bool isInBurst = false;
    private float lastBurstTime = -10f;
    private float currentBurstEndTime = 0f;
    
    // Aiming state
    private bool hasPendingSpray = false;
    private float aimStartTime = 0f;
    
    // References
    private PlayerStats playerStats;
    private Transform playerTransform;
    
    // Components
    private SprayParticleController particleController;
    private SprayDamageHandler damageHandler;
    private SprayHandVisuals handVisuals;

    // Public properties
    public float SprayRange => currentRange;
    public float SprayWidth => currentWidth;
    public bool IsSpraying => isSpraying;
    public bool IsOnCooldown => Time.time < currentBurstEndTime;
    
    /// <summary>
    /// Get the particle travel speed for movement prediction calculations
    /// </summary>
    public float GetParticleSpeed()
    {
        return particleController?.GetParticleSpeed() 
            ?? (currentRange / SpraySettings.ParticleLifetimeBase);
    }

    void Awake()
    {
        InitializeComponents();
        FindReferences();
        UpdateStatsFromPlayer();
    }

    private void InitializeComponents()
    {
        // Initialize particle controller
        particleController = new SprayParticleController(transform);
        if (sprayParticles != null)
            particleController.SetParticleSystem(sprayParticles);
        else
            particleController.CreateParticleSystem();
        
        // Get the created particle system reference
        sprayParticles = particleController.Particles;
        
        // Initialize hand visuals
        handVisuals = new SprayHandVisuals(transform);
        if (handTransform != null)
            handVisuals.SetReferences(handTransform, handSprite, sprayCanSprite);
        else
            handVisuals.CreateHandVisuals();
    }

    private void FindReferences()
    {
        // Get audio component
        if (sprayAudio == null)
        {
            sprayAudio = GetComponent<ProceduralSprayAudio>();
            if (sprayAudio == null)
                sprayAudio = gameObject.AddComponent<ProceduralSprayAudio>();
        }
        
        playerTransform = transform.parent;
        if (playerTransform != null)
        {
            playerStats = playerTransform.GetComponent<PlayerStats>();
            if (playerStats == null)
                playerStats = playerTransform.GetComponentInChildren<PlayerStats>();
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();
        }
        
        // Initialize damage handler with references
        damageHandler = new SprayDamageHandler(playerStats, playerTransform);
    }

    void Start()
    {
        particleController?.Stop();
        handVisuals?.SetVisible(SpraySettings.ShowHandAlways);
    }

    public void UpdateStatsFromPlayer()
    {
        if (playerStats != null)
        {
            currentRange = playerStats.CurrentSprayRange;
            currentWidth = playerStats.CurrentSprayWidth;
        }
        else
        {
            currentRange = SpraySettings.BaseSprayRange;
            currentWidth = SpraySettings.BaseSprayAngle;
        }
        
        particleController?.UpdateForStats(currentRange, currentWidth);
    }

    void Update()
    {
        // Periodically refresh stats
        if (Time.frameCount % 30 == 0)
            UpdateStatsFromPlayer();
        
        // Keep hand's range in sync
        handVisuals?.SetRange(currentRange);
        
        // Hand ALWAYS tracks target (no freezing)
        handVisuals?.Update();
        
        // Handle pending spray
        if (hasPendingSpray)
        {
            HandlePendingSpray();
        }
        
        // Check if burst ended
        if (isInBurst && Time.time >= currentBurstEndTime)
        {
            isInBurst = false;
            handVisuals?.ClearTarget();
        }
        
        // During spray: use hand's CurrentDirection for EVERYTHING
        if (isSpraying || isInBurst)
        {
            Vector2 dir = handVisuals?.CurrentDirection ?? Vector2.right;
            Vector3 nozzle = handVisuals?.GetNozzleWorldPosition() ?? transform.position;
            particleController?.SetSprayDirectionAndPosition(dir, nozzle, currentRange, currentWidth);
            
            // Use cone-based damage detection (instant, no delay)
            // This detects enemies in the spray cone and deals damage immediately
            damageHandler?.ProcessDamage(dir, currentRange, currentWidth, (Vector2)nozzle);
        }
    }
    
    /// <summary>
    /// Handle pending spray: validate target and fire when ready.
    /// </summary>
    private void HandlePendingSpray()
    {
        float waitTime = Time.time - aimStartTime;
        
        // Cancel if target died/disabled or out of range
        if (handVisuals != null && (!handVisuals.HasTarget || !handVisuals.IsTargetInRange))
        {
            CancelPendingSpray();
            return;
        }
        
        bool minTimePassed = waitTime >= SpraySettings.AimDelayBeforeSpray;
        bool aimed = handVisuals?.IsAimedAtTarget ?? true;
        bool tookTooLong = waitTime >= SpraySettings.MaxAimTime;
        
        // Fire when hand is aimed at target (or timeout) - range already validated
        if (minTimePassed && (aimed || tookTooLong))
        {
            ExecutePendingSpray();
        }
    }

    void OnParticleTrigger()
    {
        damageHandler?.ProcessParticleTrigger(sprayParticles);
    }

    public void StartSpray(Vector2 direction)
    {
        // For continuous spray, we'd need a different approach
        // This is mainly used for burst mode now
        if (!isSpraying)
        {
            isSpraying = true;
            particleController?.Play();
            sprayAudio?.StartSpray();
            handVisuals?.SetVisible(true);
        }
        // Particle position updated in Update() via UpdateParticlePosition()
    }

    public void StopSpray()
    {
        if (isSpraying)
        {
            isSpraying = false;
            particleController?.Stop();
            sprayAudio?.StopSpray();
            Invoke(nameof(HideHand), 0.2f);
        }
    }

    /// <summary>
    /// Fire a spray burst at a specific target. Hand will track and aim.
    /// </summary>
    public bool FireSprayBurstAtTarget(Transform target)
    {
        if (target == null) return false;
        if (Time.time < lastBurstTime + SpraySettings.BurstCooldown) return false;
        if (Time.time < currentBurstEndTime) return false;
        if (hasPendingSpray) return false;
        
        // Range check before starting to aim - use collider bounds center for consistency
        if (playerTransform != null)
        {
            Collider2D col = target.GetComponent<Collider2D>();
            Vector2 targetPos = (col != null && col.enabled) ? (Vector2)col.bounds.center : (Vector2)target.position;
            float dist = Vector2.Distance(playerTransform.position, targetPos);
            if (dist > currentRange || dist < SpraySettings.MinTargetDistance)
                return false;
        }
        
        // Tell hand to track this target - it does ALL the aiming
        handVisuals?.SetTarget(target);
        
        aimStartTime = Time.time;
        hasPendingSpray = true;
        handVisuals?.SetVisible(true);
        
        return true;
    }
    
    /// <summary>
    /// Legacy direction-based burst - fires immediately in hand's current direction.
    /// </summary>
    public bool FireSprayBurst(Vector2 direction, float duration = 0.25f)
    {
        if (Time.time < lastBurstTime + SpraySettings.BurstCooldown)
            return false;
        if (Time.time < currentBurstEndTime)
            return false;
        if (hasPendingSpray)
            return false;
        
        // No target tracking - fire immediately
        aimStartTime = Time.time;
        hasPendingSpray = true;
        handVisuals?.SetVisible(true);
        
        return true;
    }
    
    /// <summary>
    /// Cancel a pending spray without firing.
    /// </summary>
    private void CancelPendingSpray()
    {
        hasPendingSpray = false;
        handVisuals?.ClearTarget();
        
        if (!SpraySettings.ShowHandAlways)
            Invoke(nameof(HideHand), 0.1f);
    }

    private void ExecutePendingSpray()
    {
        if (!hasPendingSpray) return;
        
        hasPendingSpray = false;
        // Hand keeps tracking during burst - no freeze
        
        lastBurstTime = Time.time;
        currentBurstEndTime = Time.time + SpraySettings.BurstDuration;
        isInBurst = true;
        
        particleController?.PlayBurst();
        sprayAudio?.PlaySprayBurst();
        
        if (!SpraySettings.ShowHandAlways)
            Invoke(nameof(HideHand), SpraySettings.BurstDuration + 0.1f);
        
        damageHandler?.ResetDamageTick();
    }

    private void HideHand()
    {
        if (!SpraySettings.ShowHandAlways && !isSpraying && !isInBurst && !hasPendingSpray)
            handVisuals?.SetVisible(false);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = playerTransform != null ? playerTransform.position : transform.position;
        float drawRange = Application.isPlaying ? currentRange : SpraySettings.BaseSprayRange;
        float drawWidth = Application.isPlaying ? currentWidth : SpraySettings.BaseSprayAngle;
        
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(origin, drawRange);
        
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.5f);
        Vector3 dir = transform.right;
        Vector3 left = Quaternion.Euler(0, 0, drawWidth * 0.5f) * dir;
        Vector3 right = Quaternion.Euler(0, 0, -drawWidth * 0.5f) * dir;
        
        Gizmos.DrawLine(origin, origin + left * drawRange);
        Gizmos.DrawLine(origin, origin + right * drawRange);
    }
}
