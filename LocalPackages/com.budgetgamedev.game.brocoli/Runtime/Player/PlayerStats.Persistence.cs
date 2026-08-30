using System.Collections;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PlayerStats
    {
        internal BrocoliPlayerSave CaptureRunState()
        {
            var save = new BrocoliPlayerSave
            {
                health = _currentHealth,
                maxHealth = _currentMaxHealth,
                attackSpeed = _currentAttackSpeed,
                damage = _currentDamage,
                movementSpeed = _currentMovementSpeed,
                experience = _currentExperience,
                maxExperience = _currentMaxExperience,
                level = _currentLevel,
                detectionRadius = _currentDetectionRadius,
                sprayRange = _currentSprayRange,
                sprayWidth = _currentSprayWidth,
                sprayDamageMultiplier = _currentSprayDamageMultiplier,
                critChance = _currentCritChance,
                critDamage = _currentCritDamage,
                dodgeChance = _currentDodgeChance,
                armor = _currentArmor,
                healthRegen = _currentHealthRegen,
                lifeSteal = _currentLifeSteal,
                levelUpChoicePending = _levelUpChoicePending,
            };

            foreach (ActiveBoost boost in _activeBoosts)
            {
                if (boost.remainingTime <= 0f)
                    continue;

                save.temporaryBoosts.Add(
                    new BrocoliTemporaryBoostSave
                    {
                        type = boost.type,
                        amount = boost.amount,
                        remainingTime = boost.remainingTime,
                    }
                );
            }

            return save;
        }

        internal void RestoreRunState(BrocoliPlayerSave save)
        {
            if (save == null)
                return;

            _currentHealth = save.health;
            _currentMaxHealth = save.maxHealth;
            _currentAttackSpeed = save.attackSpeed;
            _currentDamage = save.damage;
            _currentMovementSpeed = save.movementSpeed;
            _currentExperience = save.experience;
            _currentMaxExperience = save.maxExperience;
            _currentLevel = save.level;
            _currentDetectionRadius = save.detectionRadius;
            _currentSprayRange = save.sprayRange;
            _currentSprayWidth = save.sprayWidth;
            _currentSprayDamageMultiplier = save.sprayDamageMultiplier;
            _currentCritChance = save.critChance;
            _currentCritDamage = save.critDamage;
            _currentDodgeChance = save.dodgeChance;
            _currentArmor = save.armor;
            _currentHealthRegen = save.healthRegen;
            _currentLifeSteal = save.lifeSteal;
            _regenTimer = 0f;

            _activeBoosts.Clear();
            if (save.temporaryBoosts != null)
            {
                foreach (BrocoliTemporaryBoostSave boost in save.temporaryBoosts)
                {
                    if (boost == null || boost.remainingTime <= 0f)
                        continue;

                    _activeBoosts.Add(
                        new ActiveBoost
                        {
                            type = boost.type,
                            amount = boost.amount,
                            remainingTime = boost.remainingTime,
                        }
                    );
                }
            }
            RecalculateTemporaryBonuses();

            _levelUpChoicePending = false;
            _healthBar?.UpdateBar(_currentHealth, _currentMaxHealth);
            _experienceBar?.UpdateBar(_currentExperience, _currentMaxExperience);

            if (save.levelUpChoicePending)
                StartCoroutine(RestorePendingLevelUpChoice());
        }

        private IEnumerator RestorePendingLevelUpChoice()
        {
            while (FindAnyObjectByType<GamePreloader>() != null)
                yield return null;
            yield return null;

            _levelUpScreen ??= FindAnyObjectByType<LevelUpScreen>();
            if (_levelUpScreen == null)
                yield break;

            _levelUpScreen.Show(Mathf.RoundToInt(_currentLevel), this);
            _levelUpChoicePending = _levelUpScreen.IsShowing();
        }
    }
}
