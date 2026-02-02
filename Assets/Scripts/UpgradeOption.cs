using UnityEngine;

/// <summary>
/// Represents a single upgrade option that can be offered during level up.
/// </summary>
[System.Serializable]
public class UpgradeOption
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
    public enum StatType { MaxHealth, Damage, Speed, AttackSpeed, SprayRange, SprayWidth, DetectionRadius }

    public StatType Type;
    public Rarity RarityLevel;
    public float Amount;
    public string DisplayName;
    public string Description;

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
                option.Description = $"+{option.Amount:F0} Detection";
                break;
        }

        return option;
    }

    /// <summary>
    /// Apply this upgrade to player stats.
    /// </summary>
    public void ApplyTo(PlayerStats stats)
    {
        switch (Type)
        {
            case StatType.MaxHealth:
                stats.AddMaxHealth(Amount);
                break;
            case StatType.Damage:
                stats.AddDamagePublic(Amount);
                break;
            case StatType.Speed:
                stats.AddSpeedPublic(Amount);
                break;
            case StatType.AttackSpeed:
                stats.AddAttackSpeedPublic(Amount);
                break;
            case StatType.SprayRange:
                stats.AddSprayRange(Amount);
                break;
            case StatType.SprayWidth:
                stats.AddSprayWidth(Amount);
                break;
            case StatType.DetectionRadius:
                stats.AddDetectionRadiusPublic(Amount);
                break;
        }
    }
}
