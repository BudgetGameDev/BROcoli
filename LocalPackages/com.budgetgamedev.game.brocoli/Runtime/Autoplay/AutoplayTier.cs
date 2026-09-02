using System;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// One named run shape. These presets are the whole reason the harness needs no
    /// launcher scripts: a tier is data the player itself understands, so
    /// <c>--tier=marathon</c> means the same thing on macOS, Linux, and Windows.
    /// </summary>
    internal readonly struct AutoplayTier
    {
        internal readonly string Name;
        internal readonly string Description;
        internal readonly float Duration;
        internal readonly float Interval;
        internal readonly float Timestep;
        internal readonly string Scenario;
        internal readonly bool Deterministic;
        internal readonly bool DriveMenus;
        internal readonly bool ExerciseFeatures;
        internal readonly bool ExerciseSaveJourney;

        internal AutoplayTier(
            string name,
            string description,
            float duration,
            float interval,
            float timestep,
            string scenario,
            bool deterministic,
            bool driveMenus,
            bool exerciseFeatures,
            bool exerciseSaveJourney = false
        )
        {
            Name = name;
            Description = description;
            Duration = duration;
            Interval = interval;
            Timestep = timestep;
            Scenario = scenario;
            Deterministic = deterministic;
            DriveMenus = driveMenus;
            ExerciseFeatures = exerciseFeatures;
            ExerciseSaveJourney = exerciseSaveJourney;
        }
    }

    /// <summary>The tier catalogue, shared by the player and the editor-side runner.</summary>
    internal static class AutoplayTiers
    {
        internal const string Default = "medium";

        private const float Frame = 1f / 60f;

        /// <summary>
        /// Ordered cheapest first. Durations are game-seconds: the fake-time
        /// fast-forward compresses them into a fraction of that in wall-clock time.
        /// </summary>
        internal static readonly AutoplayTier[] All =
        {
            new(
                "smoke",
                "5 game-seconds, frequent captures: does the game start and render",
                5f,
                0.25f,
                Frame,
                "smoke",
                true,
                false,
                false
            ),
            new(
                "medium",
                "1 game-minute of ordinary play with the full feature sweep",
                60f,
                0.5f,
                Frame,
                "survive",
                true,
                false,
                true
            ),
            new(
                "fast",
                "5 game-minutes compressed hard by a coarse update step",
                300f,
                2f,
                0.033f,
                "survive",
                true,
                false,
                true
            ),
            new(
                "long",
                "5 game-minutes at the ordinary update step, sparser captures",
                300f,
                1f,
                Frame,
                "survive",
                true,
                false,
                true
            ),
            new(
                "marathon",
                "3 game-hours: does a long session stay stable and playable",
                10800f,
                60f,
                0.04f,
                "survive",
                true,
                false,
                true
            ),
            new(
                "coverage",
                "20 game-minutes from the main menu, asserting every feature was used",
                1200f,
                1f,
                // Measured: a coarser step here saturates the projectile pool and
                // the run dies to warnings rather than to anything about the game.
                0.025f,
                "coverage",
                true,
                true,
                true
            ),
            new(
                "balance",
                "15 game-minutes of uninterrupted play, graded on progression and difficulty",
                900f,
                1f,
                Frame,
                "balance",
                true,
                false,
                // The feature sweep pauses the game to poke menus. That is fine when
                // the question is coverage and wrong when it is pacing: the seconds a
                // level took would include the seconds spent in the inventory.
                false
            ),
            new(
                "journey",
                "4 game-minutes of the player's own journey: two runs made, resumed, and died in",
                240f,
                1f,
                Frame,
                AutoplayFeatures.JourneyScenario,
                true,
                true,
                // The coverage sweep and the journey both drive the pause menu, and
                // two of them taking turns at it is how a run ends up resumed into
                // an inventory. The journey owns the menus for the length of this
                // tier; `coverage` is where the overlays are graded.
                false,
                true
            ),
            new(
                "tune",
                "10 real-time minutes for live lighting tuning; no fast-forward",
                600f,
                1f,
                Frame,
                "smoke",
                false,
                false,
                false
            ),
        };

        internal static bool TryFind(string name, out AutoplayTier tier)
        {
            foreach (AutoplayTier candidate in All)
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    tier = candidate;
                    return true;
                }
            }

            tier = default;
            return false;
        }

        /// <summary>Applies a preset by name, leaving the config untouched when unknown.</summary>
        internal static void Apply(AutoplayConfig config, string name)
        {
            if (string.IsNullOrEmpty(name) || !TryFind(name, out AutoplayTier tier))
                return;

            config.Tier = tier.Name;
            config.Duration = tier.Duration;
            config.Interval = tier.Interval;
            config.Timestep = tier.Timestep;
            config.Scenario = tier.Scenario;
            config.Deterministic = tier.Deterministic;
            config.DriveMenus = tier.DriveMenus;
            config.ExerciseFeatures = tier.ExerciseFeatures;
            config.ExerciseSaveJourney = tier.ExerciseSaveJourney;
        }

        internal static string Names()
        {
            var names = new string[All.Length];
            for (int index = 0; index < All.Length; index++)
                names[index] = All[index].Name;
            return string.Join(", ", names);
        }
    }
}
