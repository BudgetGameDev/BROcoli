using System.Collections;
using UnityEngine;

/// <summary>
/// Handles damage reception, knockback triggering, and death/game-over logic.
/// Damage feedback (knockback, shake, vignette) scales with percentage of max health lost.
/// Damage from enemies is dealt exclusively by attack animations for proper sync.
/// </summary>
public class PlayerDamageHandler : MonoBehaviour
{
    // Keep hit feedback as a short nudge. The previous 5-12 velocity range
    // visibly launched the player and could compound with body contacts.
    private const float MinKnockbackForce = 1f;
    private const float MaxKnockbackForce = 2.25f;
    private const float DamageImmunityDuration = 0.3f; // Immunity frames after taking damage

    [Header("Death Sequence")]
    [SerializeField, Min(0.2f)] private float deathAnimationDuration = 0.9f;
    [SerializeField] private float deathSpinDegrees = 220f;
    [SerializeField, Range(0f, 0.25f)] private float finalDeathScale = 0.04f;

    private PlayerStats _playerStats;
    private PlayerMovement _playerMovement;
    private PlayerAudioHandler _audioHandler;
    private ShuffleWalkVisual _hopVisual;

    private bool _gameOver;
    private bool _deathAnimationPlaying;
    private float _lastDamageTime = -999f; // Time of last damage taken

    /// <summary>
    /// Whether the game is over (player died).
    /// </summary>
    public bool IsGameOver => _gameOver;
    public bool IsDeathAnimationPlaying => _deathAnimationPlaying;
    public float DeathAnimationDuration => deathAnimationDuration;

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
        if (_playerStats == null) return 0.5f;
        float maxHealth = _playerStats.CurrentMaxHealth;
        if (maxHealth <= 0f) return 0.5f;
        return Mathf.Clamp01(damage / maxHealth);
    }
    
    /// <summary>
    /// Trigger all damage feedback effects scaled by intensity.
    /// </summary>
    private void TriggerDamageFeedback(float damage, Vector2 knockbackDirection)
    {
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
        if (_gameOver) return false;
        
        // Check damage immunity window to prevent rapid multiple hits
        if (Time.time - _lastDamageTime < DamageImmunityDuration)
        {
            return false;
        }
        _lastDamageTime = Time.time;

        // Play damage sound
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
    public void HandleCollision(Collider2D other)
    {
        if (_gameOver) return;

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

    private void HandleProjectileCollision(Collider2D other)
    {
        _audioHandler?.PlayCollisionSound();

        EnemyBase enemy = other.GetComponent<EnemyBase>();
        float damage = enemy?.Damage ?? 0f;
        
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
        if (_playerStats == null) return;
        if (_gameOver) return;

        if (!_playerStats.IsAlive)
        {
            TriggerGameOver();
        }
    }

    /// <summary>
    /// Trigger game over state.
    /// </summary>
    public void TriggerGameOver()
    {
        if (_gameOver) return;

        Debug.Log("Game over");
        _gameOver = true;

        // Stop ambient audio
        _audioHandler?.StopAllAmbient();
        _audioHandler?.PlayGameOverSound();

        // Save and display the final run state without loading another scene.
        SaveFinalRunStats(out int finalScore, out int finalWave, out bool wasInfiniteMode);

        // Notify listeners
        OnGameOver?.Invoke();

        StopPlayerSimulation();
        StartCoroutine(PlayDeathSequence(finalScore, finalWave, wasInfiniteMode));
    }

    private void StopPlayerSimulation()
    {
        foreach (Collider2D playerCollider in GetComponents<Collider2D>())
            playerCollider.enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }
    }

    private IEnumerator PlayDeathSequence(int score, int wave, bool infiniteMode)
    {
        _deathAnimationPlaying = true;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.localRotation;
        float duration = Mathf.Max(0.2f, deathAnimationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.LerpUnclamped(
                startScale,
                startScale * finalDeathScale,
                eased);
            transform.localRotation = startRotation *
                Quaternion.Euler(0f, 0f, deathSpinDegrees * eased);
            yield return null;
        }

        transform.localScale = startScale * finalDeathScale;
        transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, deathSpinDegrees);
        _deathAnimationPlaying = false;
        GameOverOverlay.Show(score, wave, infiniteMode);
    }

    private void SaveFinalRunStats(out int score, out int wave, out bool infiniteMode)
    {
        GameStates gameStates = FindAnyObjectByType<GameStates>();
        WaveGenerator waveGenerator = FindAnyObjectByType<WaveGenerator>();
        score = gameStates != null ? gameStates.score : 0;
        wave = waveGenerator != null ? waveGenerator.CurrentWaveNumber : 1;
        infiniteMode = waveGenerator != null && waveGenerator.IsInfiniteMode;

        PlayerPrefs.SetInt("LastScore", score);
        PlayerPrefs.SetInt("LastWave", wave);
        PlayerPrefs.SetInt("WasInfiniteMode", infiniteMode ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"Saved final run: score {score}, wave {wave}, infinite {infiniteMode}");
    }
}
