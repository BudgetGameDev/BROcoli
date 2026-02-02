using UnityEngine;

/// <summary>
/// Player stats management with fully programmatic initialization.
/// All fields are private and discovered/set at runtime - no serialized scene references.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    // Default stat values (matching original scene values)
    private const float DefaultHealth = 100f;
    private const float DefaultMaxHealth = 100f;
    private const float DefaultAttackSpeed = 0.6f;
    private const float DefaultDamage = 10f;
    private const float DefaultMovementSpeed = 4f;  // Original scene value was 4, not 10
    private const float DefaultMaxExperience = 30f;  // Level up after ~3 easy enemy kills
    private const float DefaultDetectionRadius = 12f;

    // Current stat values - private backing fields
    private float _currentHealth;
    private float _currentMaxHealth;
    private float _currentAttackSpeed;
    private float _currentDamage;
    private float _currentMovementSpeed;
    private float _currentExperience;
    private float _currentMaxExperience;
    private float _currentLevel;
    private float _currentDetectionRadius;
    private float _currentSprayRange;
    private float _currentSprayWidth;
    private float _currentSprayDamageMultiplier;

// UI references - discovered dynamically
    private Bar _healthBar;
    private Bar _experienceBar;
    private LevelUpScreen _levelUpScreen;

    // Public read-only properties
    public bool IsAlive => _currentHealth > 0f;
    public float CurrentHealth => _currentHealth;
    public float CurrentMaxHealth => _currentMaxHealth;
    public float CurrentAttackSpeed => _currentAttackSpeed;
    public float CurrentDamage => _currentDamage;
    public float CurrentMovementSpeed => _currentMovementSpeed;
    public float CurrentExperience => _currentExperience;
    public float CurrentMaxExperience => _currentMaxExperience;
    public float CurrentLevel => _currentLevel;
    public float CurrentDetectionRadius => _currentDetectionRadius;
    public float CurrentSprayRange => _currentSprayRange;
    public float CurrentSprayWidth => _currentSprayWidth;
    public float CurrentSprayDamageMultiplier => _currentSprayDamageMultiplier;

    private void Awake()
    {
        DiscoverUIComponents();
    }

    private void Start()
    {
        ResetStats();
    }

    /// <summary>
    /// Discover UI Bar components by GameObject name.
    /// </summary>
    private void DiscoverUIComponents()
    {
        // Find bars by name in scene
        var allBars = FindObjectsByType<Bar>(FindObjectsSortMode.None);
        
        foreach (var bar in allBars)
        {
            if (bar.gameObject.name == "HealthBar")
            {
                _healthBar = bar;
            }
            else if (bar.gameObject.name == "ExperienceBar")
            {
                _experienceBar = bar;
            }
        }

        if (_healthBar == null)
        {
            Debug.LogWarning("PlayerStats: Could not find HealthBar in scene");
        }
        if (_experienceBar == null)
        {
            Debug.LogWarning("PlayerStats: Could not find ExperienceBar in scene");
        }
    }

    /// <summary>
    /// Reset all stats to default values.
    /// </summary>
    public void ResetStats()
    {
        _currentHealth = DefaultHealth;
        _currentMaxHealth = DefaultMaxHealth;
        _currentAttackSpeed = DefaultAttackSpeed;
        _currentDamage = DefaultDamage;
        _currentMovementSpeed = DefaultMovementSpeed;
        _currentExperience = 0f;
        _currentMaxExperience = DefaultMaxExperience;
        _currentLevel = 1f;
        _currentDetectionRadius = DefaultDetectionRadius;
        _currentSprayRange = SpraySettings.BaseSprayRange;
        _currentSprayWidth = SpraySettings.BaseSprayAngle;
        _currentSprayDamageMultiplier = 1f;

        _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
        _experienceBar?.UpdateBar(_currentExperience, _currentMaxExperience);
    }

    /// <summary>
    /// Apply a boost to player stats.
    /// </summary>
    public void ApplyBoost(BoostBase boost)
    {
        switch (boost)
        {
            case HealthBoost healthBoost:
                AddHealth(healthBoost.Amount);
                break;
            case AttackSpeedBoost attackSpeedBoost:
                AddAttackSpeed(attackSpeedBoost.Amount);
                break;
            case DamageBoost damageBoost:
                AddDamage(damageBoost.Amount);
                break;
            case MovementSpeedBoost movementSpeedBoost:
                AddMovementSpeed(movementSpeedBoost.Amount);
                break;
            case ExperienceBoost experienceBoost:
                AddExperience(experienceBoost.Amount);
                break;
            case DetectionRadiusBoost detectionRadiusBoost:
                AddDetectionRadius(detectionRadiusBoost.Amount);
                break;
            case SprayRangeBoost sprayRangeBoost:
                AddSprayRange(sprayRangeBoost.Amount);
                break;
            case SprayWidthBoost sprayWidthBoost:
                AddSprayWidth(sprayWidthBoost.Amount);
                break;
            default:
                Debug.LogWarning("Unknown boost type applied.");
                break;
        }
    }

    /// <summary>
    /// Apply damage to the player.
    /// </summary>
    public void ApplyDamage(float damage)
    {
        AddHealth(-damage);
    }

    /// <summary>
    /// Add experience points.
    /// </summary>
    public void ApplyExperience(float experience)
    {
        AddExperience(experience);
    }

    private void LevelUp()
    {
        _currentLevel += 1f;

        // Base stat gains on level up (smaller now since player chooses upgrades)
        float healthGain = 10f;
        _currentHealth += healthGain;
        _currentMaxHealth += healthGain;
        _currentExperience -= _currentMaxExperience;
        _currentMaxExperience *= 2f;  // Double XP needed each level (30 -> 60 -> 120 -> 240...)

        _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
        _experienceBar?.UpdateBar(_currentExperience, _currentMaxExperience);

        // Show level up screen with upgrade choices
        if (_levelUpScreen == null)
        {
            _levelUpScreen = FindAnyObjectByType<LevelUpScreen>();
        }
        if (_levelUpScreen != null)
        {
            _levelUpScreen.Show((int)_currentLevel, this);
        }
    }

    private void AddHealth(float amount)
    {
        _currentHealth = Mathf.Min(_currentHealth + amount, _currentMaxHealth);
        _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
    }

    private void AddAttackSpeed(float amount)
    {
        _currentAttackSpeed *= amount;
    }

    private void AddDamage(float amount)
    {
        _currentDamage += amount;
    }

    private void AddMovementSpeed(float amount)
    {
        _currentMovementSpeed += amount;
    }

    private void AddExperience(float amount)
    {
        _currentExperience += amount;
        if (_currentExperience >= _currentMaxExperience)
        {
            LevelUp();
        }
        else
        {
            _experienceBar?.UpdateBar(_currentExperience, _currentMaxExperience);
        }
    }

    private void AddDetectionRadius(float amount)
    {
        _currentDetectionRadius += amount;
    }

    public void AddSprayRange(float amount)
    {
        _currentSprayRange += amount;
    }

    public void AddSprayWidth(float amount)
    {
        _currentSprayWidth = Mathf.Clamp(_currentSprayWidth + amount, 5f, 60f);
    }

    public void AddSprayDamageMultiplier(float amount)
    {
        _currentSprayDamageMultiplier += amount;
    }

    // Public methods for upgrade system
    public void AddMaxHealth(float amount)
    {
        _currentMaxHealth += amount;
        _currentHealth += amount; // Also heal by that amount
        _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
    }

    public void AddDamagePublic(float amount)
    {
        _currentDamage += amount;
    }

    public void AddSpeedPublic(float amount)
    {
        _currentMovementSpeed += amount;
    }

    public void AddAttackSpeedPublic(float amount)
    {
        _currentAttackSpeed *= (1f + amount);
    }

    public void AddDetectionRadiusPublic(float amount)
    {
        _currentDetectionRadius += amount;
    }
}
