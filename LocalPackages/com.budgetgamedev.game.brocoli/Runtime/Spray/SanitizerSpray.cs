using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
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
    public partial class SanitizerSpray : MonoBehaviour
    {
        // References - can be assigned in scene
        [SerializeField]
        private ParticleSystem sprayParticles;

        [SerializeField]
        private ProceduralSprayAudio sprayAudio;

        [Header("Weapon Knockback")]
        [Tooltip("Multiplies the shared damage-relative enemy knockback roll.")]
        [SerializeField, Min(0f)]
        private float weaponKnockbackMultiplier = 0.9f;

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
            // Old scene/prefab references must not opt the weapon back into the
            // retired single-layer effect. Keep one layered controller per weapon.
            if (particleController != null)
                return;
            if (sprayParticles != null)
            {
                sprayParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var legacy in sprayParticles.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var emission = legacy.emission;
                    emission.enabled = false;
                    var legacyRenderer = legacy.GetComponent<ParticleSystemRenderer>();
                    if (legacyRenderer != null)
                        legacyRenderer.enabled = false;
                }
            }
            particleController = new SprayParticleController(transform);
            particleController.CreateParticleSystem();

            // Get the created particle system reference
            sprayParticles = particleController.Particles;

            // Initialize hand visuals
            handVisuals = new SprayHandVisuals(transform);
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
                    playerStats = PlayerStats.Resolve();
            }

            // Initialize damage handler with references
            damageHandler = new SprayDamageHandler(
                playerStats,
                playerTransform,
                weaponKnockbackMultiplier
            );
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
            if (Time.frameCount % 30 == 0)
                UpdateStatsFromPlayer();

            damageHandler?.SetWeaponKnockbackMultiplier(weaponKnockbackMultiplier);

            handVisuals?.SetRange(currentRange);

            handVisuals?.Update();

            if (hasPendingSpray)
            {
                HandlePendingSpray();
            }

            if (isInBurst && Time.time >= currentBurstEndTime)
            {
                damageHandler?.ResolveConeKnockback();
                isInBurst = false;
                handVisuals?.ClearTarget();
            }

            if (isSpraying || isInBurst)
            {
                Vector2 dir = handVisuals?.CurrentDirection ?? Vector2.right;
                Vector3 nozzle = handVisuals?.GetNozzleWorldPosition() ?? transform.position;
                particleController?.SetSprayDirectionAndPosition(
                    dir,
                    nozzle,
                    currentRange,
                    currentWidth
                );

                // Use cone-based damage detection (instant, no delay)
                // This detects enemies in the spray cone and deals damage immediately
                damageHandler?.ProcessDamage(dir, currentRange, currentWidth, nozzle);
            }
        }
    }
}
