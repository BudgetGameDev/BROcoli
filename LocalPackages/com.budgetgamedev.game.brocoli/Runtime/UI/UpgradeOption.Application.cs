using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class UpgradeOption
    {
        internal static void SetStatAmount(UpgradeOption option, float rarityMult)
        {
            switch (option.Type)
            {
                case StatType.MaxHealth:
                    option.Amount = Mathf.Round(15f * rarityMult);
                    break;
                case StatType.Damage:
                    option.Amount = Mathf.Round(8f * rarityMult);
                    break;
                case StatType.Speed:
                    option.Amount = 0.2f * rarityMult;
                    break;
                case StatType.AttackSpeed:
                    option.Amount = 0.05f * rarityMult;
                    break;
                case StatType.SprayRange:
                    option.Amount = 0.1f * rarityMult;
                    break;
                case StatType.SprayWidth:
                    option.Amount = Mathf.Round(2f * rarityMult);
                    break;
                case StatType.DetectionRadius:
                    option.Amount = 1f * rarityMult;
                    break;
                case StatType.CritChance:
                    option.Amount = 3f * rarityMult;
                    break;
                case StatType.CritDamage:
                    option.Amount = 0.15f * rarityMult;
                    break;
                case StatType.Dodge:
                    option.Amount = 2f * rarityMult;
                    break;
                case StatType.Armor:
                    option.Amount = 3f * rarityMult;
                    break;
                case StatType.HealthRegen:
                    option.Amount = 1f * rarityMult;
                    break;
                case StatType.LifeSteal:
                    option.Amount = 2f * rarityMult;
                    break;
            }
        }

        internal static float GetPenaltyAmount(StatType type, float mult)
        {
            return type switch
            {
                StatType.MaxHealth => Mathf.Round(15f * mult),
                StatType.Damage => Mathf.Round(8f * mult),
                StatType.Speed => 0.2f * mult,
                StatType.AttackSpeed => 0.05f * mult,
                StatType.SprayRange => 0.1f * mult,
                StatType.SprayWidth => Mathf.Round(2f * mult),
                StatType.DetectionRadius => 1f * mult,
                StatType.CritChance => 3f * mult,
                StatType.CritDamage => 0.15f * mult,
                StatType.Dodge => 2f * mult,
                StatType.Armor => 3f * mult,
                StatType.HealthRegen => 1f * mult,
                StatType.LifeSteal => 2f * mult,
                _ => 1f * mult,
            };
        }

        internal static string GetStatDescription(StatType type, float amount, bool isBonus)
        {
            string sign = isBonus ? "+" : "-";
            return type switch
            {
                StatType.MaxHealth => $"{sign}{amount:F0} Max HP",
                StatType.Damage => $"{sign}{amount:F0} Damage",
                StatType.Speed => $"{sign}{amount:F1} Speed",
                StatType.AttackSpeed => $"{sign}{amount * 100:F0}% Atk Spd",
                StatType.SprayRange => $"{sign}{amount:F1} Range",
                StatType.SprayWidth => $"{sign}{amount:F0}° Width",
                StatType.DetectionRadius => $"{sign}{amount:F0} Detection",
                StatType.CritChance => $"{sign}{amount:F0}% Crit",
                StatType.CritDamage => $"{sign}{amount * 100:F0}% Crit DMG",
                StatType.Dodge => $"{sign}{amount:F0}% Dodge",
                StatType.Armor => $"{sign}{amount:F0} Armor",
                StatType.HealthRegen => $"{sign}{amount:F1} Regen",
                StatType.LifeSteal => $"{sign}{amount:F0}% Lifesteal",
                _ => $"{sign}{amount:F0}",
            };
        }

        internal static string GetStatShortName(StatType type)
        {
            return type switch
            {
                StatType.MaxHealth => "HP",
                StatType.Damage => "DMG",
                StatType.Speed => "SPD",
                StatType.AttackSpeed => "ATK",
                StatType.SprayRange => "RNG",
                StatType.SprayWidth => "WID",
                StatType.DetectionRadius => "DET",
                StatType.CritChance => "CRIT",
                StatType.CritDamage => "CDMG",
                StatType.Dodge => "DDG",
                StatType.Armor => "ARM",
                StatType.HealthRegen => "REG",
                StatType.LifeSteal => "LSTL",
                _ => "???",
            };
        }

        /// <summary>
        /// Apply this upgrade to player stats.
        /// </summary>
        public void ApplyTo(PlayerStats stats)
        {
            // Apply bonus
            ApplyStatChange(stats, Type, Amount, true);

            // Apply penalty if this is a troll upgrade
            if (IsTrollUpgrade)
            {
                ApplyStatChange(stats, PenaltyType, PenaltyAmount, false);
            }
        }

        internal void ApplyStatChange(PlayerStats stats, StatType type, float amount, bool isBonus)
        {
            // For penalties, we subtract instead of add
            float finalAmount = isBonus ? amount : -amount;

            switch (type)
            {
                case StatType.MaxHealth:
                    stats.AddMaxHealth(finalAmount);
                    break;
                case StatType.Damage:
                    stats.AddDamagePublic(finalAmount);
                    break;
                case StatType.Speed:
                    stats.AddSpeedPublic(finalAmount);
                    break;
                case StatType.AttackSpeed:
                    // Attack speed is a multiplier, handle differently
                    if (isBonus)
                        stats.AddAttackSpeedPublic(amount);
                    else
                        stats.AddAttackSpeedPublic(-amount * 0.5f); // Penalty is less harsh
                    break;
                case StatType.SprayRange:
                    stats.AddSprayRange(finalAmount);
                    break;
                case StatType.SprayWidth:
                    stats.AddSprayWidth(finalAmount);
                    break;
                case StatType.DetectionRadius:
                    stats.AddDetectionRadiusPublic(finalAmount);
                    break;
                case StatType.CritChance:
                    stats.AddCritChance(finalAmount);
                    break;
                case StatType.CritDamage:
                    stats.AddCritDamage(finalAmount);
                    break;
                case StatType.Dodge:
                    stats.AddDodgeChance(finalAmount);
                    break;
                case StatType.Armor:
                    stats.AddArmor(finalAmount);
                    break;
                case StatType.HealthRegen:
                    stats.AddHealthRegen(finalAmount);
                    break;
                case StatType.LifeSteal:
                    stats.AddLifeSteal(finalAmount);
                    break;
            }
        }
    }
}
