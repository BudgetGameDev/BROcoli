using System.Collections;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Handles damage reception, knockback triggering, and death/game-over logic.
    /// Damage feedback (knockback, shake, vignette) scales with percentage of max health lost.
    /// Damage from enemies is dealt exclusively by attack animations for proper sync.
    /// </summary>
    public partial class PlayerDamageHandler : MonoBehaviour
    {
        // Keep hit feedback as a short nudge. The previous 5-12 velocity range
        // visibly launched the player and could compound with body contacts.
        private const float MinKnockbackForce = 1f;
        private const float MaxKnockbackForce = 2.25f;
        private const float DamageImmunityDuration = 0.3f; // Immunity frames after taking damage

        [Header("Death Sequence")]
        [SerializeField, Min(0.2f)]
        private float deathFallDuration = 0.7f;

        [SerializeField, Min(0f)]
        private float deathSettleDuration = 0.35f;

        private PlayerStats _playerStats;
        private PlayerMovement _playerMovement;
        private PlayerAudioHandler _audioHandler;
        private ShuffleWalkVisual _hopVisual;
        private PlayerDeathVisual _deathVisual;

        private bool _gameOver;
        private bool _deathAnimationPlaying;
        private float _lastDamageTime = -999f; // Time of last damage taken

        /// <summary>
        /// Whether the game is over (player died).
        /// </summary>
        public bool IsGameOver => _gameOver;
        public bool IsDeathAnimationPlaying => _deathAnimationPlaying;
        public float DeathAnimationDuration => deathFallDuration + deathSettleDuration;

        /// <summary>
        /// Event fired when game over occurs.
        /// </summary>
        public event System.Action OnGameOver;

        private void Awake()
        {
            _playerStats = GetComponentInChildren<PlayerStats>();
            _playerMovement = GetComponent<PlayerMovement>();
            _audioHandler = GetComponent<PlayerAudioHandler>();
            _hopVisual = GetComponentInChildren<ShuffleWalkVisual>();
            _deathVisual = GetComponent<PlayerDeathVisual>();
            if (_deathVisual == null)
                _deathVisual = gameObject.AddComponent<PlayerDeathVisual>();

            if (_playerStats == null)
            {
                Debug.LogError("PlayerDamageHandler: No PlayerStats found - damage will not work!");
            }

            // Ensure feedback systems exist
            EnsureFeedbackSystems();
        }

        private void Start()
        {
            _gameOver = false;
        }

        private void EnsureFeedbackSystems()
        {
            // Add CameraShake to main camera if not present
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<CameraShake>() == null)
            {
                mainCam.gameObject.AddComponent<CameraShake>();
            }

            // Add DamageVignette if not present in scene
            if (FindAnyObjectByType<DamageVignette>() == null)
            {
                GameObject vignetteGO = new GameObject("DamageVignetteManager");
                vignetteGO.AddComponent<DamageVignette>();
            }
        }

        /// <summary>
        /// Calculate damage intensity as percentage of max health (0-1).
        /// </summary>
        private float CalculateDamageIntensity(float damage)
        {
            if (_playerStats == null)
                return 0.5f;
            float maxHealth = _playerStats.CurrentMaxHealth;
            if (maxHealth <= 0f)
                return 0.5f;
            return Mathf.Clamp01(damage / maxHealth);
        }

        /// <summary>
        /// Trigger all damage feedback effects scaled by intensity.
        /// </summary>
        private void TriggerDamageFeedback(float damage, Vector2 knockbackDirection)
        {
            AutoplayFeatureLog.Record(AutoplayFeatures.DamageTaken);
            float intensity = CalculateDamageIntensity(damage);

            // Scaled knockback - additive impulse, player keeps control
            if (knockbackDirection != Vector2.zero && _playerMovement != null)
            {
                float force = Mathf.Lerp(MinKnockbackForce, MaxKnockbackForce, intensity);
                _playerMovement.ApplyKnockbackImpulse(knockbackDirection.normalized, force);
            }

            // Apply stumble to slow player down - clears on next landing
            _hopVisual?.ApplyStumble(intensity);

            // Camera shake
            CameraShake.Shake(intensity * 0.8f);

            // Damage vignette pulse
            DamageVignette.Pulse(intensity);
        }

        /// <summary>
        /// Apply melee damage to the player without knockback.
        /// </summary>
        public bool TakeMeleeDamage(float damage)
        {
            return TakeMeleeDamage(damage, Vector2.zero);
        }

        /// <summary>
        /// Apply melee damage to the player with knockback.
        /// Called by enemy attack animations when strike lands.
        /// </summary>
        public bool TakeMeleeDamage(float damage, Vector2 knockbackDirection)
        {
            if (_gameOver)
                return false;

            // Check damage immunity window to prevent rapid multiple hits
            if (Time.time - _lastDamageTime < DamageImmunityDuration)
            {
                return false;
            }
            _lastDamageTime = Time.time;

            // A lethal hit gets only the dedicated defeat cue, not the inherited
            // damage sound immediately before it.
            if (!WillDamageBeFatal(damage))
                _audioHandler?.PlayDamageSound();

            // Apply damage to stats
            _playerStats?.ApplyDamage(damage);

            // Trigger scaled feedback effects
            TriggerDamageFeedback(damage, knockbackDirection);

            CheckForDeath();
            return true;
        }

        /// <summary>
        /// Handle collision with enemy or projectile.
        /// Note: Enemy collision does NOT deal damage - damage comes from enemy attack animations.
        /// </summary>
        public void HandleCollision(Collider other)
        {
            if (_gameOver)
                return;

            switch (other.tag)
            {
                case "Enemy":
                    // Enemy collision does NOT deal damage
                    // Damage is dealt by enemy attack animations (EnemyScript.PerformMeleeAttack)
                    // This ensures damage is synced with the visual strike
                    break;
                case "Projectile":
                    HandleProjectileCollision(other);
                    break;
            }
        }

        private void HandleProjectileCollision(Collider other)
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            float damage = enemy?.Damage ?? 0f;

            if (!WillDamageBeFatal(damage))
                _audioHandler?.PlayCollisionSound();

            // Apply damage and feedback for projectiles
            _playerStats?.ApplyDamage(damage);
            TriggerDamageFeedback(damage, Vector2.zero);
            CheckForDeath();
        }

        /// <summary>
        /// Check if player has died and trigger game over if so.
        /// </summary>
        public void CheckForDeath()
        {
            if (_playerStats == null)
                return;
            if (_gameOver)
                return;

            if (!_playerStats.IsAlive)
            {
                TriggerGameOver();
            }
        }

        private bool WillDamageBeFatal(float damage)
        {
            return _playerStats != null && damage >= _playerStats.CurrentHealth;
        }
    }
}
