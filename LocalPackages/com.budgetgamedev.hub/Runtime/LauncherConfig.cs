using System;
using UnityEngine;

namespace BudgetGameDev.Hub
{
    /// <summary>
    /// Optional, git-committed settings that let a build skip the picker and open
    /// straight into one game.
    /// </summary>
    /// <remarks>
    /// Committed rather than a per-machine preference so a single-game build, or a
    /// branch that always boots the game being worked on, is reproducible for
    /// everyone who checks it out. Every setting is optional: an absent, empty, or
    /// malformed file leaves the launcher behaving exactly as it does with no
    /// config at all, which is what keeps this safe to ship enabled by default.
    ///
    /// The format is line-based rather than JSON so the file can carry the
    /// documentation for its own settings, and so a setting can be commented out
    /// and back in without editing surrounding syntax.
    /// </remarks>
    public sealed class LauncherConfig
    {
        /// <summary>Resources path the launcher reads, without extension.</summary>
        public const string ResourceName = "LauncherConfig";

        private const string StartupSceneKey = "startupScene";

        /// <summary>
        /// Scene to open instead of the picker, or empty to show the picker. Only
        /// honoured when the scene is in the build; see
        /// <see cref="LauncherStartup.Resolve"/>.
        /// </summary>
        public string StartupScene { get; private set; } = string.Empty;

        /// <summary>
        /// Reads the committed config, or returns an empty one. Never throws: a
        /// broken config must not be able to stop the launcher from opening.
        /// </summary>
        public static LauncherConfig Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            return asset == null ? new LauncherConfig() : Parse(asset.text);
        }

        /// <summary>
        /// Parses "key = value" lines, ignoring blanks and '#' comments. Unknown
        /// keys are reported rather than ignored silently, so a misspelled setting
        /// is visible instead of looking like it had no effect.
        /// </summary>
        public static LauncherConfig Parse(string text)
        {
            var config = new LauncherConfig();
            if (string.IsNullOrEmpty(text))
                return config;

            int lineNumber = 0;
            foreach (string rawLine in text.Split('\n'))
            {
                lineNumber++;
                string line = StripComment(rawLine);
                if (line.Length == 0)
                    continue;

                int separator = line.IndexOf('=');
                if (separator < 0)
                {
                    Warn(lineNumber, $"expected 'key = value', got '{line}'");
                    continue;
                }

                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();

                if (string.Equals(key, StartupSceneKey, StringComparison.OrdinalIgnoreCase))
                    config.StartupScene = value;
                else
                    Warn(lineNumber, $"unknown setting '{key}'");
            }

            return config;
        }

        private static string StripComment(string line)
        {
            int comment = line.IndexOf('#');
            return (comment >= 0 ? line[..comment] : line).Trim();
        }

        private static void Warn(int lineNumber, string message) =>
            Debug.LogWarning($"[Launcher] {ResourceName} line {lineNumber}: {message}");
    }
}
