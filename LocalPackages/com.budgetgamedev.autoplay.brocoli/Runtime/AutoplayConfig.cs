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
    public sealed partial class AutoplayConfig
    {
        public bool Enabled;
        public bool CaptureEnabled = true;
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

        /// <summary>
        /// Drive the player's own journey: make a run from the menu, quit to the
        /// menu and resume it, do it again with a second character, and then die.
        /// Off by default because it is the one thing a run does that writes real
        /// save slots, which only the journey knows how to hand back.
        /// </summary>
        public bool ExerciseSaveJourney;

        /// <summary>
        /// Interval frames the run may write. The tier's interval is coarsened to
        /// spread this many pictures over the whole session rather than the run
        /// being truncated, so a budget never costs a run its later frames.
        /// </summary>
        public int MaxFrames = FrameCapture.DefaultMaxFrames;

        /// <summary>
        /// Event triggers to photograph, as <c>event[#occurrence|*][+delay]</c>. This
        /// is what lets an agent ask a batch run a specific question -- show me the
        /// first experience orb that drops -- instead of reading every frame.
        /// </summary>
        public readonly List<string> CaptureOn = new();

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
            config.ApplyReactionOptions(supplied, environment);
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
                case "--no-capture":
                    CaptureEnabled = false;
                    return false;
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
                case "--journey":
                    ExerciseSaveJourney = true;
                    return false;
                case "--no-journey":
                    ExerciseSaveJourney = false;
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
            else if (argument.StartsWith("--max-frames=", StringComparison.Ordinal))
                TryInt(argument.Substring(13), ref MaxFrames);
            else if (argument.StartsWith("--minlevel=", StringComparison.Ordinal))
                TryInt(argument.Substring(11), ref MinLevel);
            else if (argument.StartsWith("--out=", StringComparison.Ordinal))
                OutDir = argument.Substring(6);
            else if (argument.StartsWith("--scenario=", StringComparison.Ordinal))
                Scenario = argument.Substring(11);
            else if (argument.StartsWith("--sha=", StringComparison.Ordinal))
                Sha = argument.Substring(6);
            else if (argument.StartsWith("--capture-on=", StringComparison.Ordinal))
                AddCaptureTriggers(argument.Substring(13));
        }

        private void ApplyEnvironment(Func<string, string> environment)
        {
            TryInt(environment("BROCOLI_SEED"), ref Seed);
            TryFloat(environment("BROCOLI_DURATION"), ref Duration);
            TryFloat(environment("BROCOLI_INTERVAL"), ref Interval);
            TryFloat(environment("BROCOLI_TIMESTEP"), ref Timestep);
            TryInt(environment("BROCOLI_MAX_FRAMES"), ref MaxFrames);
            TryText(environment("BROCOLI_OUT"), ref OutDir);
            TryText(environment("BROCOLI_SCENARIO"), ref Scenario);

            string triggers = environment("BROCOLI_CAPTURE_ON");
            if (!string.IsNullOrEmpty(triggers))
            {
                CaptureOn.Clear();
                AddCaptureTriggers(triggers);
            }
        }

        /// <summary>Adds a comma-separated list, so the option can repeat or bundle.</summary>
        private void AddCaptureTriggers(string value)
        {
            foreach (string spec in value.Split(','))
                if (!string.IsNullOrWhiteSpace(spec))
                    CaptureOn.Add(spec.Trim());
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
            + $"interval={Interval}s maxFrames={MaxFrames} timestep={Timestep:0.####} "
            + $"deterministic={Deterministic} captureEnabled={CaptureEnabled} "
            + $"scenario={Scenario} menus={DriveMenus} features={ExerciseFeatures} "
            + $"journey={ExerciseSaveJourney} "
            + $"reaction={ReactionProfile} observation={ObservationIntervalSeconds}s delay={ReactionDelaySeconds}s "
            + $"captureOn={(CaptureOn.Count > 0 ? string.Join(";", CaptureOn) : "none")} "
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
