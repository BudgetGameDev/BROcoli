using System.Collections.Generic;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    public static partial class DungeonPropPbrBaker
    {
        private const string Wood = "DungeonPropWood";
        private const string Steel = "DungeonPropSteel";
        private const string Gold = "DungeonPropGold";
        private const string Stone = "DungeonPropStone";
        private const string Clay = "DungeonPropClay";

        public const string MiniDungeonKit = "KenneyMiniDungeon";
        public const string ModularDungeonKit = "KenneyModularDungeonKit";

        /// <summary>Which material each palette family of a prop becomes.</summary>
        public readonly struct Recipe
        {
            public readonly string Warm;
            public readonly string Cool;
            public readonly string Dark;
            public readonly string Gold;
            public readonly bool Cylindrical;
            public readonly string Kit;

            /// <summary>
            /// The kit mesh behind each child renderer, by child name. A bake
            /// overwrites the prefab's mesh, so the kit mesh has to be named
            /// here rather than read back off the prop.
            /// </summary>
            public readonly IReadOnlyDictionary<string, string> Meshes;

            public Recipe(
                string warm,
                string cool,
                string dark,
                string gold,
                IReadOnlyDictionary<string, string> meshes,
                bool cylindrical = false,
                string kit = MiniDungeonKit
            )
            {
                Warm = warm;
                Cool = cool;
                Dark = dark;
                Gold = gold;
                Meshes = meshes;
                Cylindrical = cylindrical;
                Kit = kit;
            }

            public string For(PaletteFamily family) =>
                family switch
                {
                    PaletteFamily.Warm => Warm,
                    PaletteFamily.Cool => Cool,
                    PaletteFamily.Dark => Dark,
                    _ => Gold,
                };
        }

        private static Dictionary<string, string> Model(string mesh) => new() { ["Model"] = mesh };

        /// <summary>
        /// Which material each prop's palette families become. Props left out
        /// keep the material they ship with: the potion is glass and coloured
        /// liquid, which the flat palette reads better than any tiling map.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Recipe> Recipes = new Dictionary<
            string,
            Recipe
        >
        {
            ["DungeonBarrel"] = new Recipe(
                Wood,
                Steel,
                Steel,
                Gold,
                Model("barrel"),
                cylindrical: true
            ),
            ["DungeonChair"] = new Recipe(Wood, Wood, Wood, Wood, Model("chair")),
            ["DungeonTable"] = new Recipe(Wood, Wood, Wood, Wood, Model("table")),
            ["DungeonWoodStructure"] = new Recipe(Wood, Wood, Wood, Wood, Model("wood-structure")),
            ["DungeonWoodSupport"] = new Recipe(Wood, Wood, Wood, Wood, Model("wood-support")),
            ["DungeonChest"] = new Recipe(
                Wood,
                Steel,
                Steel,
                Gold,
                new Dictionary<string, string> { ["Model"] = "chest", ["lid"] = "lid" }
            ),
            ["DungeonChestGolden"] = new Recipe(
                Gold,
                Gold,
                Steel,
                Gold,
                new Dictionary<string, string> { ["Model"] = "chest", ["lid"] = "lid" }
            ),
            ["DungeonCoin"] = new Recipe(Gold, Gold, Gold, Gold, Model("coin")),
            ["DungeonKey"] = new Recipe(Gold, Gold, Gold, Gold, Model("key")),
            // A pot is fired clay all the way round; the palette's cool rim is
            // shading on the lip, not a steel band.
            ["DungeonPot"] = new Recipe(Clay, Clay, Clay, Clay, Model("pot"), cylindrical: true),
            ["DungeonRocks"] = new Recipe(Stone, Stone, Stone, Stone, Model("rocks")),
            ["DungeonStones"] = new Recipe(Stone, Stone, Stone, Stone, Model("stones")),
            ["DungeonShieldRound"] = new Recipe(Wood, Steel, Steel, Gold, Model("shield-round")),
            ["DungeonShieldRectangle"] = new Recipe(
                Wood,
                Steel,
                Steel,
                Gold,
                Model("shield-rectangle")
            ),
            ["DungeonWeaponSword"] = new Recipe(Wood, Steel, Steel, Gold, Model("weapon-sword")),
            ["DungeonWeaponSpear"] = new Recipe(Wood, Steel, Steel, Gold, Model("weapon-spear")),
            ["DungeonTrap"] = new Recipe(
                Steel,
                Steel,
                Steel,
                Steel,
                new Dictionary<string, string> { ["Model"] = "trap", ["spikes"] = "spikes" }
            ),
            ["DungeonGateOpen"] = new Recipe(
                Stone,
                Steel,
                Steel,
                Gold,
                Model("gate"),
                kit: ModularDungeonKit
            ),
        };
    }
}
