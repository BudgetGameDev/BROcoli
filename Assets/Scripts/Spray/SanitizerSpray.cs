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
    private Vector2 pendingSprayDirection;
    private Vector2 sprayDirection = Vector2.right;
    
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
        
        // Get aim direction for animation
        Vector2 aimDir = hasPendingSpray ? pendingSprayDirection 
            : (isInBurst ? sprayDirection : sprayDirection);
        handVisuals?.AnimateRotation(aimDir);
        
        // Check if pending spray should fire
        if (hasPendingSpray && Time.time >= aimStartTime + SpraySettings.AimDelayBeforeSpray)
        {
            if (handVisuals != null && handVisuals.IsNearTarget)
                ExecutePendingSpray();
        }
        
        // Check if burst ended
        if (isInBurst && Time.time >= currentBurstEndTime)
            isInBurst = false;
        
        // Process damage during spray or burst
        if (isSpraying || isInBurst)
        {
            UpdateSprayDirection();
            damageHandler?.ProcessDamage(sprayDirection, currentRange, currentWidth);
        }
    }

    void OnParticleTrigger()
    {
        damageHandler?.ProcessParticleTrigger(sprayParticles);
    }

    public void StartSpray(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
            sprayDirection = direction.normalized;
        
        if (!isSpraying)
        {
            isSpraying = true;
            particleController?.Play();
            sprayAudio?.StartSpray();
            handVisuals?.SetVisible(true);
        }
        
        UpdateSprayDirection();
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

    public bool FireSprayBurst(Vector2 direction, float duration = 0.25f)
    {
        if (Time.time < lastBurstTime + SpraySettings.BurstCooldown)
            return false;
        if (Time.time < currentBurstEndTime)
            return false;
        if (hasPendingSpray)
            return false;
        
        pendingSprayDirection = direction.sqrMagnitude > 0.01f 
            ? direction.normalized : sprayDirection;
        
        handVisuals?.SetTargetDirection(pendingSprayDirection);
        aimStartTime = Time.time;
        hasPendingSpray = true;
        handVisuals?.SetVisible(true);
        
        return true;
    }

    private void ExecutePendingSpray()
    {
        if (!hasPendingSpray) return;
        
        hasPendingSpray = false;
        sprayDirection = pendingSprayDirection;
        
        lastBurstTime = Time.time;
        currentBurstEndTime = Time.time + SpraySettings.BurstDuration;
        isInBurst = true;
        
        UpdateSprayDirection();
        particleController?.PlayBurst();
        sprayAudio?.PlaySprayBurst();
        
        if (!SpraySettings.ShowHandAlways)
            Invoke(nameof(HideHand), SpraySettings.BurstDuration + 0.1f);
        
        damageHandler?.ResetDamageTick();
    }

    private void UpdateSprayDirection()
    {
        handVisuals?.UpdateDirection(sprayDirection);
        
        // Get the actual direction and nozzle position from hand visuals
        if (handVisuals != null && particleController != null)
        {
            Vector2 actualDirection = handVisuals.CurrentDirection;
            Vector3 nozzlePos = handVisuals.GetNozzleWorldPosition();
            particleController.SetSprayDirectionAndPosition(actualDirection, nozzlePos, currentRange, currentWidth);
            
            // Update damage handler with particle speed for travel time calculation
            damageHandler?.SetParticleSpeed(particleController.GetParticleSpeed());
        }
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
