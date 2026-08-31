using System.Collections.Generic;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
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
    }
}
