using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Represents a single upgrade option that can be offered during level up.
    /// Supports normal upgrades and "troll" trade-off upgrades (+stat/-stat).
    /// </summary>
    [System.Serializable]
    public partial class UpgradeOption
    {
        public enum Rarity
        {
            Common,
            Uncommon,
            Rare,
            Epic,
            Legendary,
        }

        public enum StatType
        {
            MaxHealth,
            Damage,
            Speed,
            AttackSpeed,
            SprayRange,
            SprayWidth,
            DetectionRadius,
            CritChance,
            CritDamage,
            Dodge,
            Armor,
            HealthRegen,
            LifeSteal,
        }

        public StatType Type;
        public Rarity RarityLevel;
        public float Amount;
        public string DisplayName;
        public string Description;

        // Trade-off (troll) upgrade fields
        public bool IsTrollUpgrade;
        public StatType PenaltyType;
        public float PenaltyAmount;

        /// </summary>
        public static UpgradeOption GenerateRandom(int playerLevel)
        {
            var option = new UpgradeOption();

            // Weighted rarity roll - higher levels have slightly better chances
            // Epic/Legendary are now much rarer
            float roll = Random.value;
            float legendaryChance = Mathf.Min(0.005f + playerLevel * 0.001f, 0.025f); // Max 2.5%
            float epicChance = Mathf.Min(0.01f + playerLevel * 0.002f, 0.04f); // Max 4%
            float rareChance = Mathf.Min(0.08f + playerLevel * 0.008f, 0.18f); // Max 18%
            float uncommonChance = 0.35f;

            if (roll < legendaryChance)
                option.RarityLevel = Rarity.Legendary;
            else if (roll < legendaryChance + epicChance)
                option.RarityLevel = Rarity.Epic;
            else if (roll < legendaryChance + epicChance + rareChance)
                option.RarityLevel = Rarity.Rare;
            else if (roll < legendaryChance + epicChance + rareChance + uncommonChance)
                option.RarityLevel = Rarity.Uncommon;
            else
                option.RarityLevel = Rarity.Common;

            // Random stat type
            var statTypes = System.Enum.GetValues(typeof(StatType));
            option.Type = (StatType)statTypes.GetValue(Random.Range(0, statTypes.Length));

            // Amount based on rarity and stat type
            float rarityMult = RarityMultiplier(option.RarityLevel);

            // Set amount and description based on stat type
            switch (option.Type)
            {
                case StatType.MaxHealth:
                    option.Amount = Mathf.Round(15f * rarityMult);
                    option.DisplayName = "Max Health";
                    option.Description = $"+{option.Amount:F0} Max Health";
                    break;
                case StatType.Damage:
                    option.Amount = Mathf.Round(8f * rarityMult);
                    option.DisplayName = "Damage";
                    option.Description = $"+{option.Amount:F0} Damage";
                    break;
                case StatType.Speed:
                    option.Amount = 0.2f * rarityMult;
                    option.DisplayName = "Movement Speed";
                    option.Description = $"+{option.Amount:F1} Speed";
                    break;
                case StatType.AttackSpeed:
                    option.Amount = 0.05f * rarityMult;
                    option.DisplayName = "Attack Speed";
                    option.Description = $"+{option.Amount * 100:F0}% Attack Speed";
                    break;
                case StatType.SprayRange:
                    option.Amount = 0.1f * rarityMult;
                    option.DisplayName = "Spray Range";
                    option.Description = $"+{option.Amount:F1} Range";
                    break;
                case StatType.SprayWidth:
                    option.Amount = Mathf.Round(2f * rarityMult);
                    option.DisplayName = "Spray Width";
                    option.Description = $"+{option.Amount:F0}° Width";
                    break;
                case StatType.DetectionRadius:
                    option.Amount = 1f * rarityMult;
                    option.DisplayName = "Detection";
                    option.Description = $"+{option.Amount:F0} Detection Range";
                    break;
                case StatType.CritChance:
                    option.Amount = 3f * rarityMult;
                    option.DisplayName = "Crit Chance";
                    option.Description = $"+{option.Amount:F0}% Crit Chance";
                    break;
                case StatType.CritDamage:
                    option.Amount = 0.15f * rarityMult;
                    option.DisplayName = "Crit Damage";
                    option.Description = $"+{option.Amount * 100:F0}% Crit Damage";
                    break;
                case StatType.Dodge:
                    option.Amount = 2f * rarityMult;
                    option.DisplayName = "Dodge";
                    option.Description = $"+{option.Amount:F0}% Dodge Chance";
                    break;
                case StatType.Armor:
                    option.Amount = 3f * rarityMult;
                    option.DisplayName = "Armor";
                    option.Description = $"+{option.Amount:F0} Armor";
                    break;
                case StatType.HealthRegen:
                    option.Amount = 1f * rarityMult;
                    option.DisplayName = "Regen";
                    option.Description = $"+{option.Amount:F1} HP/sec";
                    break;
                case StatType.LifeSteal:
                    option.Amount = 2f * rarityMult;
                    option.DisplayName = "Life Steal";
                    option.Description = $"+{option.Amount:F0}% Life Steal";
                    break;
            }

            return option;
        }

        /// <summary>
        /// Generate a "troll" trade-off upgrade: big bonus to one stat, penalty to another.
        /// These are higher risk/reward and have distinctive colors.
        /// </summary>
        public static UpgradeOption GenerateTrollUpgrade(int playerLevel)
        {
            var option = new UpgradeOption();
            option.IsTrollUpgrade = true;

            // Troll upgrades are always Rare or better (they're special)
            float roll = Random.value;
            if (roll < 0.1f)
                option.RarityLevel = Rarity.Legendary;
            else if (roll < 0.3f)
                option.RarityLevel = Rarity.Epic;
            else
                option.RarityLevel = Rarity.Rare;

            // Bigger multipliers for troll upgrades (high risk, high reward)
            float rarityMult = TrollRarityMultiplier(option.RarityLevel);

            // Pick random stat types (bonus and penalty must be different)
            var statTypes = System.Enum.GetValues(typeof(StatType));
            option.Type = (StatType)statTypes.GetValue(Random.Range(0, statTypes.Length));

            do
            {
                option.PenaltyType = (StatType)
                    statTypes.GetValue(Random.Range(0, statTypes.Length));
            } while (option.PenaltyType == option.Type);

            // Set bonus amount
            SetStatAmount(option, rarityMult);

            // Set penalty amount (about 60-80% of what a normal upgrade would give)
            float penaltyMult = rarityMult * Random.Range(0.6f, 0.8f);
            option.PenaltyAmount = GetPenaltyAmount(option.PenaltyType, penaltyMult);

            // Build description with colored text
            string bonusDesc = GetStatDescription(option.Type, option.Amount, true);
            string penaltyDesc = GetStatDescription(
                option.PenaltyType,
                option.PenaltyAmount,
                false
            );

            option.DisplayName = $"{GetStatShortName(option.Type)} Trade";
            option.Description =
                $"<color=#4CFF4C>{bonusDesc}</color>\n<color=#FF4C4C>{penaltyDesc}</color>";

            return option;
        }
    }
}
