using System;
using System.Collections.Generic;
using System.Globalization;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class AutoplayConfig
    {
        public string ReactionProfile = "stress";
        public float ObservationIntervalSeconds;
        public float ReactionDelaySeconds;

        internal void ApplyReactionProfile(string profile)
        {
            if (profile != "stress" && profile != "reference")
                throw new ArgumentException(
                    "Reaction profile must be stress or reference.",
                    nameof(profile)
                );
            ReactionProfile = profile;
            // A reproducible design hypothesis, not a measured model of human performance.
            ObservationIntervalSeconds = profile == "reference" ? .1f : 0f;
            ReactionDelaySeconds = profile == "reference" ? .2f : 0f;
        }

        private void ApplyReactionOptions(List<string> arguments, Func<string, string> environment)
        {
            string profile = ReactionProfile;
            foreach (string argument in arguments)
                if (argument.StartsWith("--reaction-profile=", StringComparison.Ordinal))
                    profile = argument.Substring("--reaction-profile=".Length);
            profile = environment("BROCOLI_REACTION_PROFILE") ?? profile;
            ApplyReactionProfile(profile);
            foreach (string argument in arguments)
            {
                if (argument.StartsWith("--observation-interval=", StringComparison.Ordinal))
                    ObservationIntervalSeconds = ReactionSeconds(
                        argument.Substring("--observation-interval=".Length)
                    );
                if (argument.StartsWith("--reaction-delay=", StringComparison.Ordinal))
                    ReactionDelaySeconds = ReactionSeconds(
                        argument.Substring("--reaction-delay=".Length)
                    );
            }
            string observation = environment("BROCOLI_OBSERVATION_INTERVAL");
            string delay = environment("BROCOLI_REACTION_DELAY");
            if (!string.IsNullOrEmpty(observation))
                ObservationIntervalSeconds = ReactionSeconds(observation);
            if (!string.IsNullOrEmpty(delay))
                ReactionDelaySeconds = ReactionSeconds(delay);
        }

        private static float ReactionSeconds(string raw)
        {
            if (
                !float.TryParse(
                    raw,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value
                )
                || float.IsNaN(value)
                || float.IsInfinity(value)
                || value < 0f
                || value > 2f
            )
                throw new ArgumentException(
                    "Reaction timing must be finite seconds between 0 and 2."
                );
            return value;
        }
    }
}
