using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player stats management with fully programmatic initialization.
/// All fields are private and discovered/set at runtime - no serialized scene references.
///
/// STAT EXPLANATIONS:
/// - Detection Radius: Range at which player auto-targets enemies for combat
/// - Crit Chance: % chance to deal critical hit damage (0-100)
/// - Crit Damage: Multiplier for critical hits (e.g., 1.5 = 150% damage)
/// - Dodge: % chance to completely avoid incoming damage (0-100)
/// - Armor: Flat damage reduction applied before taking damage
/// - Health Regen: HP restored per second
/// - Life Steal: % of damage dealt returned as health (0-100)
/// </summary>
public partial class PlayerStats : MonoBehaviour
{
    // Default stat values (matching original scene values)
    private const float DefaultHealth = 100f;
    private const float DefaultMaxHealth = 100f;
    private const float DefaultAttackSpeed = 0.6f;
    public const float DefaultBaseDamage = 8f;
    private const float DefaultMovementSpeed = 4f;
    private const float DefaultMaxExperience = 30f;
    private const float DefaultDetectionRadius = 12f;

    // Roguelike stat defaults
    private const float DefaultCritChance = 5f; // 5% base crit chance
    private const float DefaultCritDamage = 1.5f; // 150% crit damage
    private const float DefaultDodgeChance = 0f; // 0% dodge
    private const float DefaultArmor = 0f; // 0 flat damage reduction
    private const float DefaultHealthRegen = 0f; // 0 HP/sec
    private const float DefaultLifeSteal = 0f; // 0% life steal

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

    // Roguelike stats
    private float _currentCritChance;
    private float _currentCritDamage;
    private float _currentDodgeChance;
    private float _currentArmor;
    private float _currentHealthRegen;
    private float _currentLifeSteal;

    // Health regen timer
    private float _regenTimer;

    // Temporary boost tracking
    private struct ActiveBoost
    {
        public TemporaryBoostType type;
        public float amount;
        public float remainingTime;
    }

    private List<ActiveBoost> _activeBoosts = new List<ActiveBoost>();

    // Temporary boost bonuses (added on top of base stats)
    private float _tempMovementSpeedBonus;
    private float _tempDamageBonus;
    private float _tempAttackSpeedMultiplier;
    private float _tempHealthRegenBonus;
    private float _tempEnemyTimeScale = 1f;
    private float _debugBaseDamage = DefaultBaseDamage;

    public static float ActiveEnemyTimeScale { get; private set; } = 1f;
    public static Transform ActiveMagnetTarget { get; private set; }
    public static Transform ActivePlayerTarget { get; private set; }

    // UI references - discovered dynamically
    private Bar _healthBar;
    private Bar _experienceBar;
    private LevelUpScreen _levelUpScreen;
    private bool _levelUpChoicePending;

    // Public read-only properties (include temporary bonuses)
    public bool IsAlive => _currentHealth > 0f;
    public float CurrentHealth => _currentHealth;
    public float CurrentMaxHealth => _currentMaxHealth;
    public float CurrentAttackSpeed =>
        Mathf.Max(0.15f, _currentAttackSpeed * (1f - _tempAttackSpeedMultiplier)); // Lower = faster
    public float CurrentDamage =>
        _currentDamage + _tempDamageBonus + (_debugBaseDamage - DefaultBaseDamage);
    public float CurrentMovementSpeed => _currentMovementSpeed + _tempMovementSpeedBonus;
    public float CurrentExperience => _currentExperience;
    public float CurrentMaxExperience => _currentMaxExperience;
    public float CurrentLevel => _currentLevel;
    public float CurrentDetectionRadius => _currentDetectionRadius;
    public float CurrentSprayRange => _currentSprayRange;
    public float CurrentSprayWidth => _currentSprayWidth;
    public float CurrentSprayDamageMultiplier => _currentSprayDamageMultiplier;

    // Roguelike stat properties
    public float CurrentCritChance => _currentCritChance;
    public float CurrentCritDamage => _currentCritDamage;
    public float CurrentDodgeChance => _currentDodgeChance;
    public float CurrentArmor => _currentArmor;
    public float CurrentHealthRegen => _currentHealthRegen;
    public float CurrentLifeSteal => _currentLifeSteal;

    private void Awake()
    {
        RegisterPickupTarget();
        WarnOnDuplicateStats();
        DiscoverUIComponents();
        DiabloHud.EnsurePresent();
    }

    private void OnEnable()
    {
        RegisterPickupTarget();
    }

    private void RegisterPickupTarget()
    {
        Transform root = transform.root;
        if (root != null && root.CompareTag("Player"))
            ActivePlayerTarget = root;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGlobalEffectState()
    {
        ActiveEnemyTimeScale = 1f;
        ActiveMagnetTarget = null;
        ActivePlayerTarget = null;
    }

    private void Start()
    {
        ResetStats();
    }

    private void Update()
    {
        // Health regeneration (base + temporary bonus)
        float totalRegen = _currentHealthRegen + _tempHealthRegenBonus;
        if (totalRegen > 0f && _currentHealth < _currentMaxHealth && _currentHealth > 0f)
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= 1f)
            {
                _regenTimer -= 1f;
                float healAmount = totalRegen;
                _currentHealth = Mathf.Min(_currentHealth + healAmount, _currentMaxHealth);
                _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
            }
        }

        // Update temporary boosts
        UpdateTemporaryBoosts();
    }

    /// <summary>
    /// Update and expire temporary boosts
    /// </summary>
    private void UpdateTemporaryBoosts()
    {
        if (_activeBoosts.Count == 0)
            return;

        bool needsRecalculate = false;

        for (int i = _activeBoosts.Count - 1; i >= 0; i--)
        {
            var boost = _activeBoosts[i];
            boost.remainingTime -= Time.deltaTime;

            if (boost.remainingTime <= 0f)
            {
                _activeBoosts.RemoveAt(i);
                needsRecalculate = true;
                Debug.Log($"Temporary boost expired: {boost.type}");
            }
            else
            {
                _activeBoosts[i] = boost;
            }
        }

        if (needsRecalculate)
        {
            RecalculateTemporaryBonuses();
        }
    }

    /// <summary>
    /// Recalculate all temporary bonuses from active boosts
    /// </summary>
    private void RecalculateTemporaryBonuses()
    {
        _tempMovementSpeedBonus = 0f;
        _tempDamageBonus = 0f;
        _tempAttackSpeedMultiplier = 0f;
        _tempHealthRegenBonus = 0f;
        _tempEnemyTimeScale = 1f;
        bool magnetActive = false;

        foreach (var boost in _activeBoosts)
        {
            switch (boost.type)
            {
                case TemporaryBoostType.MovementSpeed:
                    _tempMovementSpeedBonus += boost.amount;
                    break;
                case TemporaryBoostType.Damage:
                    _tempDamageBonus += boost.amount;
                    break;
                case TemporaryBoostType.AttackSpeed:
                    _tempAttackSpeedMultiplier += boost.amount;
                    break;
                case TemporaryBoostType.HealthRegen:
                    _tempHealthRegenBonus += boost.amount;
                    break;
                case TemporaryBoostType.TimeSlow:
                    _tempEnemyTimeScale = Mathf.Min(
                        _tempEnemyTimeScale,
                        Mathf.Clamp(boost.amount, 0.1f, 1f)
                    );
                    break;
                case TemporaryBoostType.Magnet:
                    magnetActive = true;
                    break;
            }
        }

        ActiveEnemyTimeScale = _tempEnemyTimeScale;
        ActiveMagnetTarget = magnetActive ? transform : null;
    }

    /// <summary>
    /// Apply a temporary boost that expires after duration seconds
    /// </summary>
    public void ApplyTemporaryBoost(TemporaryBoostType type, float amount, float duration)
    {
        // Repeated pickups refresh their effect instead of stacking it.
        for (int i = 0; i < _activeBoosts.Count; i++)
        {
            ActiveBoost active = _activeBoosts[i];
            if (active.type != type)
                continue;

            active.amount =
                type == TemporaryBoostType.TimeSlow
                    ? Mathf.Min(active.amount, amount)
                    : Mathf.Max(active.amount, amount);
            active.remainingTime = Mathf.Max(active.remainingTime, duration);
            _activeBoosts[i] = active;
            RecalculateTemporaryBonuses();
            return;
        }

        _activeBoosts.Add(
            new ActiveBoost
            {
                type = type,
                amount = amount,
                remainingTime = duration,
            }
        );

        // Immediately recalculate bonuses
        RecalculateTemporaryBonuses();

        Debug.Log($"Applied temporary boost: {type} +{amount} for {duration}s");
    }

    /// <summary>
    /// Check if player has an active boost of the given type
    /// </summary>
    public bool HasActiveBoost(TemporaryBoostType type)
    {
        foreach (var boost in _activeBoosts)
        {
            if (boost.type == type)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if player currently has magnet effect active
    /// </summary>
    public bool HasMagnetActive => HasActiveBoost(TemporaryBoostType.Magnet);

    /// <summary>
    /// Get the magnet radius (amount stored in boost)
    /// </summary>
    public float MagnetRadius
    {
        get
        {
            foreach (var boost in _activeBoosts)
            {
                if (boost.type == TemporaryBoostType.Magnet)
                    return boost.amount;
            }
            return 0f;
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
        _currentDamage = DefaultBaseDamage;
        _currentMovementSpeed = DefaultMovementSpeed;
        _currentExperience = 0f;
        _currentMaxExperience = DefaultMaxExperience;
        _currentLevel = 1f;
        _levelUpChoicePending = false;
        _currentDetectionRadius = DefaultDetectionRadius;
        _currentSprayRange = SpraySettings.BaseSprayRange;
        _currentSprayWidth = SpraySettings.BaseSprayAngle;
        _currentSprayDamageMultiplier = 1f;

        // Reset roguelike stats
        _currentCritChance = DefaultCritChance;
        _currentCritDamage = DefaultCritDamage;
        _currentDodgeChance = DefaultDodgeChance;
        _currentArmor = DefaultArmor;
        _currentHealthRegen = DefaultHealthRegen;
        _currentLifeSteal = DefaultLifeSteal;
        _regenTimer = 0f;

        // Clear temporary boosts
        _activeBoosts.Clear();
        _tempMovementSpeedBonus = 0f;
        _tempDamageBonus = 0f;
        _tempAttackSpeedMultiplier = 0f;
        _tempHealthRegenBonus = 0f;
        _tempEnemyTimeScale = 1f;
        ActiveEnemyTimeScale = 1f;
        ActiveMagnetTarget = null;

        _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
        _experienceBar?.UpdateBar(_currentExperience, _currentMaxExperience);
    }

    private void OnDisable()
    {
        ActiveEnemyTimeScale = 1f;
        if (ActiveMagnetTarget == transform)
            ActiveMagnetTarget = null;
        if (gameObject.CompareTag("Player") && ActivePlayerTarget == transform)
            ActivePlayerTarget = null;
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
    /// Apply damage to the player. Respects dodge and armor.
    /// </summary>
    public void ApplyDamage(float damage)
    {
        // Check dodge
        if (_currentDodgeChance > 0f && Random.value * 100f < _currentDodgeChance)
        {
            // Dodged! No damage taken
            return;
        }

        // Apply armor reduction
        float reducedDamage = Mathf.Max(0f, damage - _currentArmor);
        AddHealth(-reducedDamage);
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

        // The paid requirement was removed before entering this method, so
        // overflow remains available for the next level instead of being lost.
        _currentMaxExperience *= 2f; // Double XP needed each level (30 -> 60 -> 120 -> 240...)

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
            _levelUpChoicePending = _levelUpScreen.IsShowing();
        }
    }

    /// <summary>Continues resolving banked XP after the current upgrade was chosen.</summary>
    public void CompleteLevelUpChoice()
    {
        _levelUpChoicePending = false;
        ResolveLevelUps();
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
        if (amount <= 0f)
            return;

        _currentExperience += amount;
        ResolveLevelUps();
    }

    private void ResolveLevelUps()
    {
        while (!_levelUpChoicePending && _currentExperience >= _currentMaxExperience)
        {
            _currentExperience -= _currentMaxExperience;
            LevelUp();
        }

        _experienceBar?.UpdateBar(_currentExperience, _currentMaxExperience);
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

    /// <summary>
    /// Overrides only the original base damage for editor/debug tuning. Earned
    /// upgrades and temporary damage bonuses remain additive.
    /// </summary>
    public void SetDebugBaseDamage(float damage)
    {
        _debugBaseDamage = Mathf.Max(0f, damage);
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

    // Roguelike stat modifiers
    public void AddCritChance(float amount)
    {
        _currentCritChance = Mathf.Clamp(_currentCritChance + amount, 0f, 100f);
    }

    public void AddCritDamage(float amount)
    {
        _currentCritDamage += amount;
    }

    public void AddDodgeChance(float amount)
    {
        _currentDodgeChance = Mathf.Clamp(_currentDodgeChance + amount, 0f, 75f); // Cap at 75%
    }

    public void AddArmor(float amount)
    {
        _currentArmor += amount;
    }

    public void AddHealthRegen(float amount)
    {
        _currentHealthRegen += amount;
    }

    public void AddLifeSteal(float amount)
    {
        _currentLifeSteal = Mathf.Clamp(_currentLifeSteal + amount, 0f, 100f);
    }

    /// <summary>
    /// Calculate final damage output with crit chance.
    /// Call this when dealing damage to enemies.
    /// </summary>
    public float CalculateDamageOutput(float baseDamage, out bool wasCrit)
    {
        wasCrit = Random.value * 100f < _currentCritChance;
        if (wasCrit)
        {
            return baseDamage * _currentCritDamage;
        }
        return baseDamage;
    }

    /// <summary>
    /// The player's combat power relative to a fresh run (1.0 at spawn).
    /// Offense is expected DPS (damage, attack speed, crit, spray multiplier);
    /// defense is effective HP (max health, armor, dodge, regen, life steal).
    /// The dungeon reads this when a room spawns its enemies, so groups stay
    /// correctly challenging as the build comes online.
    /// </summary>
    public float ComputePowerScore()
    {
        float attackInterval = Mathf.Max(0.15f, CurrentAttackSpeed);
        float critFactor = 1f + (_currentCritChance / 100f) * Mathf.Max(0f, _currentCritDamage - 1f);
        float sprayFactor = Mathf.Max(0.25f, _currentSprayDamageMultiplier);
        float dps = Mathf.Max(1f, CurrentDamage) * critFactor * sprayFactor / attackInterval;

        const float baselineCritFactor = 1f + (DefaultCritChance / 100f) * (DefaultCritDamage - 1f);
        const float baselineDps = DefaultBaseDamage * baselineCritFactor / DefaultAttackSpeed;
        float offense = dps / baselineDps;

        // Regen and life steal behave like extra health over a stretch of
        // fighting; armor and dodge stretch how far each hit point goes.
        float sustain = _currentHealthRegen + dps * (_currentLifeSteal / 100f) * 0.5f;
        float effectiveHealth =
            (_currentMaxHealth + sustain * 8f)
            * (1f + _currentArmor / 20f)
            / Mathf.Max(0.25f, 1f - _currentDodgeChance / 100f);
        float defense = effectiveHealth / DefaultMaxHealth;

        return Mathf.Sqrt(Mathf.Max(0.1f, offense) * Mathf.Max(0.1f, defense));
    }

    /// <summary>
    /// Apply life steal healing based on damage dealt.
    /// Call this after dealing damage to enemies.
    /// </summary>
    public void ApplyLifeSteal(float damageDealt)
    {
        if (_currentLifeSteal > 0f)
        {
            float healAmount = damageDealt * (_currentLifeSteal / 100f);
            _currentHealth = Mathf.Min(_currentHealth + healAmount, _currentMaxHealth);
            _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
        }
    }
}
