using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PlayerStats
    {
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
            float defense = effectiveHealth / DefaultMaxHealth * Mobility();

            return Mathf.Sqrt(Mathf.Max(0.1f, offense) * Mathf.Max(0.1f, defense));
        }

        /// <summary>
        /// What outrunning things is worth, as a multiplier on effective health.
        /// Moving faster is the other half of not being hit, and leaving it out let a
        /// build stack speed until nothing in the dungeon could touch it while still
        /// reading to the dungeon as a fresh player -- so every room it walked into
        /// was built for one. A balance sweep caught it as runs that reached level ten
        /// having grown 1.3x by this score next to runs that grew 5.3x.
        ///
        /// The exponent keeps it honest. Speed helps a great deal at first and less
        /// and less after that, because a dungeon room is only so wide, and it is
        /// clamped so neither a slowed build nor a runaway one dominates the score.
        /// </summary>
        private float Mobility() =>
            Mathf.Pow(Mathf.Clamp(_currentMovementSpeed / DefaultMovementSpeed, 0.5f, 2.5f), 0.7f);

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
