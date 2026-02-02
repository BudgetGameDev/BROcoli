using UnityEngine;
using TMPro;

/// <summary>
/// Displays current player stats in the pause menu with color coding.
/// White = base stat, Green = positive bonus, Red = negative penalty
/// </summary>
public class PlayerStatsDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statsText;
    
    private PlayerStats playerStats;
    
    // Track stat changes from base values
    private float baseMaxHealth = 100f;
    private float baseDamage = 10f;
    private float baseSpeed = 4f;
    private float baseAttackSpeed = 0.6f;
    private float baseDetection = 12f;
    private float baseCritChance = 5f;
    private float baseCritDamage = 1.5f;
    private float baseDodge = 0f;
    private float baseArmor = 0f;
    private float baseRegen = 0f;
    private float baseLifeSteal = 0f;
    
    void OnEnable()
    {
        RefreshStats();
    }
    
    public void RefreshStats()
    {
        if (playerStats == null)
        {
            playerStats = FindAnyObjectByType<PlayerStats>();
        }
        
        if (playerStats == null || statsText == null)
        {
            // Try to find stats text by name
            if (statsText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.gameObject.name.ToLower().Contains("stats"))
                    {
                        statsText = t;
                        break;
                    }
                }
            }
            
            if (playerStats == null)
            {
                if (statsText != null) statsText.text = "No player stats found";
                return;
            }
        }
        
        // Build stats display with color coding
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=24><b>PLAYER STATS</b></size>");
        sb.AppendLine();
        
        // Core stats
        sb.AppendLine(FormatStat("Level", playerStats.CurrentLevel, 1f, false, true));
        sb.AppendLine(FormatStat("Health", playerStats.CurrentHealth, baseMaxHealth, false, true, $"/{playerStats.CurrentMaxHealth:F0}"));
        sb.AppendLine(FormatStat("Max HP", playerStats.CurrentMaxHealth, baseMaxHealth));
        sb.AppendLine(FormatStat("Damage", playerStats.CurrentDamage, baseDamage));
        sb.AppendLine(FormatStat("Speed", playerStats.CurrentMovementSpeed, baseSpeed, false, false, "", 1));
        sb.AppendLine(FormatStat("Atk Speed", playerStats.CurrentAttackSpeed, baseAttackSpeed, true)); // Lower is better
        sb.AppendLine(FormatStat("Detection", playerStats.CurrentDetectionRadius, baseDetection));
        
        sb.AppendLine();
        sb.AppendLine("<size=20><b>COMBAT</b></size>");
        sb.AppendLine(FormatStat("Crit %", playerStats.CurrentCritChance, baseCritChance, false, false, "%"));
        sb.AppendLine(FormatStat("Crit DMG", playerStats.CurrentCritDamage * 100f, baseCritDamage * 100f, false, false, "%"));
        sb.AppendLine(FormatStat("Dodge", playerStats.CurrentDodgeChance, baseDodge, false, false, "%"));
        sb.AppendLine(FormatStat("Armor", playerStats.CurrentArmor, baseArmor));
        sb.AppendLine(FormatStat("Regen", playerStats.CurrentHealthRegen, baseRegen, false, false, "/s", 1));
        sb.AppendLine(FormatStat("Lifesteal", playerStats.CurrentLifeSteal, baseLifeSteal, false, false, "%"));
        
        statsText.text = sb.ToString();
    }
    
    private string FormatStat(string name, float value, float baseValue, bool lowerIsBetter = false, bool noColor = false, string suffix = "", int decimals = 0)
    {
        string valueStr = decimals > 0 ? value.ToString($"F{decimals}") : value.ToString("F0");
        valueStr += suffix;
        
        if (noColor)
        {
            return $"{name}: <color=white>{valueStr}</color>";
        }
        
        float diff = value - baseValue;
        if (lowerIsBetter) diff = -diff; // Invert for stats where lower is better
        
        string color;
        if (Mathf.Abs(diff) < 0.01f)
        {
            color = "white";
        }
        else if (diff > 0)
        {
            color = "#4CFF4C"; // Green
        }
        else
        {
            color = "#FF4C4C"; // Red
        }
        
        return $"{name}: <color={color}>{valueStr}</color>";
    }
}
