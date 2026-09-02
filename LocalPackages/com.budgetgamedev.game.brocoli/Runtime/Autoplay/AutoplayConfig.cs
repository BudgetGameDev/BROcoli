using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Autoplay configuration, parsed from command-line arguments and environment
    /// variables. A <c>--tier=</c> preset supplies the shape of the run; explicit
    /// options then override individual fields, and environment variables override
    /// both so a shell can steer a run it did not build the arguments for.
    /// </summary>
    public sealed class AutoplayConfig
    {
        public bool Enabled;
        public int Seed = 12345;
        public float Duration = 60f; // game-seconds to simulate
        public float Interval = 0.5f; // game-seconds between samples/captures
        public string OutDir;
        public bool Deterministic = true;
        public float Timestep = 1f / 60f; // captureDeltaTime: game-seconds per rendered frame
        public string Scenario = "survive"; // smoke | survive | progress | coverage
        public int MinLevel = 2; // pass threshold for the "progress" scenario
        public string Sha = ""; // git SHA, for the run manifest
        public string Tier = ""; // the named preset this run started from
        public bool DriveMenus; // enter through the main menu instead of the dungeon
        public bool ExerciseFeatures = true; // drive the optional systems, not just combat

        public static AutoplayConfig FromCommandLine() =>
            FromArguments(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable);

        internal static AutoplayConfig FromArguments(
            IEnumerable<string> arguments,
            Func<string, string> environment
        )
        {
            var config = new AutoplayConfig();
            var supplied = new List<string>(arguments);

            // The tier lands first so that an explicit option always wins over the
            // preset it appears alongside, whatever order the two were written in.
            AutoplayTiers.Apply(config, ResolveTier(supplied, environment));

            bool enabled = EnvFlag(environment("BROCOLI_AUTOPLAY"));
            foreach (string argument in supplied)
                enabled |= config.ApplyArgument(argument);

            config.Enabled = enabled;
            config.ApplyEnvironment(environment);
            config.OutDir = ResolveOutDir(config.OutDir);
            return config;
        }

        private static string ResolveTier(
            IEnumerable<string> arguments,
            Func<string, string> environment
        )
        {
            string tier = environment("BROCOLI_TIER");
            foreach (string argument in arguments)
                if (argument.StartsWith("--tier=", StringComparison.Ordinal))
                    tier = argument.Substring(7);
            return tier;
        }

        /// <summary>Applies one option, reporting whether it requested autoplay.</summary>
        private bool ApplyArgument(string argument)
        {
            switch (argument)
            {
                case "--autoplay":
                    return true;
                case "--deterministic":
                    Deterministic = true;
                    return false;
                case "--no-deterministic":
                    Deterministic = false;
                    return false;
                case "--menus":
                    DriveMenus = true;
                    return false;
                case "--no-menus":
                    DriveMenus = false;
                    return false;
                case "--features":
                    ExerciseFeatures = true;
                    return false;
                case "--no-features":
                    ExerciseFeatures = false;
                    return false;
                default:
                    ApplyValueArgument(argument);
                    return false;
            }
        }

        private void ApplyValueArgument(string argument)
        {
            if (argument.StartsWith("--seed=", StringComparison.Ordinal))
                TryInt(argument.Substring(7), ref Seed);
            else if (argument.StartsWith("--duration=", StringComparison.Ordinal))
                TryFloat(argument.Substring(11), ref Duration);
            else if (argument.StartsWith("--interval=", StringComparison.Ordinal))
                TryFloat(argument.Substring(11), ref Interval);
            else if (argument.StartsWith("--timestep=", StringComparison.Ordinal))
                TryFloat(argument.Substring(11), ref Timestep);
            else if (argument.StartsWith("--minlevel=", StringComparison.Ordinal))
                TryInt(argument.Substring(11), ref MinLevel);
            else if (argument.StartsWith("--out=", StringComparison.Ordinal))
                OutDir = argument.Substring(6);
            else if (argument.StartsWith("--scenario=", StringComparison.Ordinal))
                Scenario = argument.Substring(11);
            else if (argument.StartsWith("--sha=", StringComparison.Ordinal))
                Sha = argument.Substring(6);
        }

        private void ApplyEnvironment(Func<string, string> environment)
        {
            TryInt(environment("BROCOLI_SEED"), ref Seed);
            TryFloat(environment("BROCOLI_DURATION"), ref Duration);
            TryFloat(environment("BROCOLI_INTERVAL"), ref Interval);
            TryFloat(environment("BROCOLI_TIMESTEP"), ref Timestep);
            TryText(environment("BROCOLI_OUT"), ref OutDir);
            TryText(environment("BROCOLI_SCENARIO"), ref Scenario);
        }

        private static string ResolveOutDir(string requested) =>
            string.IsNullOrEmpty(requested)
                ? Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "AutoplayRuns",
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                )
                : requested;

        public override string ToString() =>
            $"tier={(Tier.Length > 0 ? Tier : "custom")} seed={Seed} duration={Duration}s "
            + $"interval={Interval}s timestep={Timestep:0.####} deterministic={Deterministic} "
            + $"scenario={Scenario} menus={DriveMenus} features={ExerciseFeatures} "
            + $"sha={Sha} out={OutDir}";

        private static bool EnvFlag(string value) =>
            value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        private static void TryText(string raw, ref string target)
        {
            if (!string.IsNullOrEmpty(raw))
                target = raw;
        }

        private static void TryInt(string raw, ref int target)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                target = v;
        }

        private static void TryFloat(string raw, ref float target)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                target = v;
        }
    }
}
