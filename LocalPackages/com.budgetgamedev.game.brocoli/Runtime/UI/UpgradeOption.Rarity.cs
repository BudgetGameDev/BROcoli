using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// How a rarity is presented to the player, and how hard it scales an upgrade.
    /// Split from UpgradeOption.cs to keep that file within the source-size gate.
    /// </summary>
    public partial class UpgradeOption
    {
        // Rarity colors
        public static readonly Color CommonColor = new Color(0.7f, 0.7f, 0.7f); // Gray
        public static readonly Color UncommonColor = new Color(0.3f, 0.9f, 0.3f); // Green
        public static readonly Color RareColor = new Color(0.3f, 0.5f, 1f); // Blue
        public static readonly Color EpicColor = new Color(0.7f, 0.3f, 0.9f); // Purple
        public static readonly Color LegendaryColor = new Color(1f, 0.8f, 0.2f); // Gold

        public Color GetRarityColor()
        {
            return RarityLevel switch
            {
                Rarity.Common => CommonColor,
                Rarity.Uncommon => UncommonColor,
                Rarity.Rare => RareColor,
                Rarity.Epic => EpicColor,
                Rarity.Legendary => LegendaryColor,
                _ => CommonColor,
            };
        }

        public string GetRarityName()
        {
            return RarityLevel.ToString().ToUpper();
        }

        /// <summary>How much a normal upgrade is scaled by, for a given rarity.</summary>
        internal static float RarityMultiplier(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common => 1f,
                Rarity.Uncommon => 1.5f,
                Rarity.Rare => 2.5f,
                Rarity.Epic => 4f,
                Rarity.Legendary => 6f,
                _ => 1f,
            };
        }

        /// <summary>Trade-off upgrades scale harder, and never roll below Rare.</summary>
        internal static float TrollRarityMultiplier(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Rare => 3f,
                Rarity.Epic => 5f,
                Rarity.Legendary => 8f,
                _ => 3f,
            };
        }

        /// <summary>
        /// Generate a random upgrade option with weighted rarity.
    }
}
