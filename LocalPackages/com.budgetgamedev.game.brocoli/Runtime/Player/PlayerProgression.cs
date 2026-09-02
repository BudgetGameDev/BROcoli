using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// The run's experience curve, kept in one place so pacing is tuned by editing
    /// numbers rather than by hunting through the level-up path.
    ///
    /// The cost of a level grows geometrically, but the growth factor itself decays
    /// toward a floor: the first levels arrive within a kill or two, the middle of a
    /// run gives a level every minute or so, and the late game stretches without ever
    /// walling. A fixed doubling walls hard -- level twelve costs as much as levels
    /// one through eleven together -- so a session either ends before the wall or
    /// spends everything after it watching a bar that does not move, while the rooms
    /// it is fighting through keep getting deeper.
    /// </summary>
    public static class PlayerProgression
    {
        // Measured against what a run actually earns. A balance run brings in
        // roughly a thousand experience a minute once the dungeon opens up, which
        // these three numbers are set against: the opening levels land in a handful
        // of kills, and a quarter-hour session finishes somewhere around level
        // fifteen rather than eighteen or five.

        /// <summary>Experience the first level costs.</summary>
        public const float BaseExperience = 40f;

        /// <summary>Growth from the first level to the second, before any decay.</summary>
        public const float GrowthStart = 1.7f;

        /// <summary>The growth every later level converges on.</summary>
        public const float GrowthFloor = 1.19f;

        /// <summary>How much of the gap to the floor each level closes.</summary>
        public const float GrowthDecay = 0.75f;

        /// <summary>The growth applied between <paramref name="level"/> and the next.</summary>
        public static float GrowthAt(int level)
        {
            float steps = Mathf.Max(0, level - 1);
            return GrowthFloor + (GrowthStart - GrowthFloor) * Mathf.Pow(GrowthDecay, steps);
        }

        /// <summary>
        /// Experience required to leave <paramref name="level"/>. Level one costs
        /// <see cref="BaseExperience"/>, which is what a fresh run starts holding.
        /// </summary>
        public static float ExperienceForLevel(int level)
        {
            float required = BaseExperience;
            for (int at = 1; at < Mathf.Max(1, level); at++)
                required *= GrowthAt(at);
            return Mathf.Round(required);
        }

        /// <summary>
        /// Experience a fresh run spends in total to reach <paramref name="level"/>.
        /// This is the number that says whether a curve walls: read it against what a
        /// run actually earns per minute.
        /// </summary>
        public static float ExperienceToReach(int level)
        {
            float total = 0f;
            for (int at = 1; at < Mathf.Max(1, level); at++)
                total += ExperienceForLevel(at);
            return total;
        }
    }
}
