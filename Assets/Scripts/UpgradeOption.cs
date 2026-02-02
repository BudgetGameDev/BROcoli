using UnityEngine;

/// <summary>
/// Represents a single upgrade option that can be offered during level up.
/// Supports normal upgrades and "troll" trade-off upgrades (+stat/-stat).
/// </summary>
[System.Serializable]
public class UpgradeOption
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
    public enum StatType { 
        MaxHealth, Damage, Speed, AttackSpeed, SprayRange, SprayWidth, DetectionRadius,
        CritChance, CritDamage, Dodge, Armor, HealthRegen, LifeSteal
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

    // Rarity colors
    public static readonly Color CommonColor = new Color(0.7f, 0.7f, 0.7f);      // Gray
    public static readonly Color UncommonColor = new Color(0.3f, 0.9f, 0.3f);    // Green
    public static readonly Color RareColor = new Color(0.3f, 0.5f, 1f);          // Blue
    public static readonly Color EpicColor = new Color(0.7f, 0.3f, 0.9f);        // Purple
    public static readonly Color LegendaryColor = new Color(1f, 0.8f, 0.2f);     // Gold

    public Color GetRarityColor()
    {
        return RarityLevel switch
        {
            Rarity.Common => CommonColor,
            Rarity.Uncommon => UncommonColor,
            Rarity.Rare => RareColor,
            Rarity.Epic => EpicColor,
            Rarity.Legendary => LegendaryColor,
            _ => CommonColor
        };
    }

    public string GetRarityName()
    {
        return RarityLevel.ToString().ToUpper();
    }

    /// <summary>
    /// Generate a random upgrade option with weighted rarity.
    /// </summary>
    public static UpgradeOption GenerateRandom(int playerLevel)
    {
        var option = new UpgradeOption();
        
        // Weighted rarity roll - higher levels have better chances
        float roll = Random.value;
        float legendaryChance = Mathf.Min(0.02f + playerLevel * 0.005f, 0.10f);
        float epicChance = Mathf.Min(0.05f + playerLevel * 0.01f, 0.15f);
        float rareChance = Mathf.Min(0.15f + playerLevel * 0.015f, 0.25f);
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
        float rarityMult = option.RarityLevel switch
        {
            Rarity.Common => 1f,
            Rarity.Uncommon => 1.5f,
            Rarity.Rare => 2.5f,
            Rarity.Epic => 4f,
            Rarity.Legendary => 6f,
            _ => 1f
        };

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
        float rarityMult = option.RarityLevel switch
        {
            Rarity.Rare => 3f,
            Rarity.Epic => 5f,
            Rarity.Legendary => 8f,
            _ => 3f
        };
        
        // Pick random stat types (bonus and penalty must be different)
        var statTypes = System.Enum.GetValues(typeof(StatType));
        option.Type = (StatType)statTypes.GetValue(Random.Range(0, statTypes.Length));
        
        do {
            option.PenaltyType = (StatType)statTypes.GetValue(Random.Range(0, statTypes.Length));
        } while (option.PenaltyType == option.Type);
        
        // Set bonus amount
        SetStatAmount(option, rarityMult);
        
        // Set penalty amount (about 60-80% of what a normal upgrade would give)
        float penaltyMult = rarityMult * Random.Range(0.6f, 0.8f);
        option.PenaltyAmount = GetPenaltyAmount(option.PenaltyType, penaltyMult);
        
        // Build description with colored text
        string bonusDesc = GetStatDescription(option.Type, option.Amount, true);
        string penaltyDesc = GetStatDescription(option.PenaltyType, option.PenaltyAmount, false);
        
        option.DisplayName = $"{GetStatShortName(option.Type)} Trade";
        option.Description = $"<color=#4CFF4C>{bonusDesc}</color>\n<color=#FF4C4C>{penaltyDesc}</color>";
        
        return option;
    }
    
    private static void SetStatAmount(UpgradeOption option, float rarityMult)
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
    
    private static float GetPenaltyAmount(StatType type, float mult)
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
            _ => 1f * mult
        };
    }
    
    private static string GetStatDescription(StatType type, float amount, bool isBonus)
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
            _ => $"{sign}{amount:F0}"
        };
    }
    
    private static string GetStatShortName(StatType type)
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
            _ => "???"
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
    
    private void ApplyStatChange(PlayerStats stats, StatType type, float amount, bool isBonus)
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
