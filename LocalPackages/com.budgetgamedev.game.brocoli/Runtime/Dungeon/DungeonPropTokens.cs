/// <summary>
/// The props a room theme is allowed to ask for by name.
///
/// Almost everything about a prop is measured off the asset, but which room a
/// prop belongs in cannot be: no measurement says a chair goes beside a table
/// or that shields hang in an armoury. That last piece stays authored, and this
/// is where it is written down.
///
/// Naming a prop in <see cref="All"/> is what makes the gate cover it.
/// DungeonPropCatalogTests walks this list and fails when a token matches no
/// registered prefab, so a prop that is renamed, replaced, or deleted breaks
/// the build instead of quietly leaving rooms half-dressed - which is exactly
/// how shrines and vaults lost their pillars for several releases.
/// </summary>
namespace BudgetGameDev.Games.Brocoli
{
    public static class DungeonPropTokens
    {
        public const string Barrel = "Barrel";
        public const string Chair = "Chair";
        public const string Coin = "Coin";
        public const string Key = "Key";
        public const string Pot = "Pot";
        public const string Potion = "Potion";
        public const string Rocks = "Rocks";
        public const string ShieldRectangle = "ShieldRectangle";
        public const string ShieldRound = "ShieldRound";
        public const string Stones = "Stones";
        public const string Table = "Table";
        public const string Trap = "Trap";
        public const string WeaponSpear = "WeaponSpear";
        public const string WeaponSword = "WeaponSword";
        public const string WoodStructure = "WoodStructure";
        public const string WoodSupport = "WoodSupport";

        /// <summary>Every token a theme may ask for, for the gate to check.</summary>
        public static readonly string[] All =
        {
            Barrel,
            Chair,
            Coin,
            Key,
            Pot,
            Potion,
            Rocks,
            ShieldRectangle,
            ShieldRound,
            Stones,
            Table,
            Trap,
            WeaponSpear,
            WeaponSword,
            WoodStructure,
            WoodSupport,
        };
    }
}
