using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// The stat mutators upgrades and boosts call into. Each one adjusts a single
    /// current stat and refreshes whatever UI or dependent system tracks it.
    /// </summary>
    public partial class PlayerStats
    {
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
            // CurrentAttackSpeed is an interval, so a positive speed upgrade must
            // shorten it; a negative trade-off lengthens it.
            _currentAttackSpeed *= Mathf.Max(0.1f, 1f - amount);
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
    }
}
