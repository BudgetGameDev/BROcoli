using System.Collections.Generic;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PlayerStats
    {
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
            float critFactor =
                1f + (_currentCritChance / 100f) * Mathf.Max(0f, _currentCritDamage - 1f);
            float sprayFactor = Mathf.Max(0.25f, _currentSprayDamageMultiplier);
            float dps = Mathf.Max(1f, CurrentDamage) * critFactor * sprayFactor / attackInterval;

            const float baselineCritFactor =
                1f + (DefaultCritChance / 100f) * (DefaultCritDamage - 1f);
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
}
