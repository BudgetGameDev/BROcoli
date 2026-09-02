using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Debug = UnityEngine.Debug;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    /// <summary>
    /// One requested run, assembled from the editor's command line. Anything the
    /// player already understands is forwarded verbatim rather than re-modelled, so
    /// adding an option to the player does not mean touching the runner.
    /// </summary>
    internal sealed class AutoplayRunRequest
    {
        /// <summary>Real seconds to wait for the player before giving up on it.</summary>
        internal const int DefaultTimeoutSeconds = 7200;

        internal string Tier = AutoplayTiers.Default;
        internal int Seed = 12345;
        internal string Sha = "";
        internal bool Build;

        /// <summary>
        /// Keeps the run's interval frames on disk instead of discarding them once
        /// the run has been read back and reported.
        /// </summary>
        internal bool KeepFrames;
        internal int TimeoutSeconds = DefaultTimeoutSeconds;
        internal readonly List<string> Overrides = new();

        private string outDir;

        /// <summary>Absolute, because the player runs with its own working directory.</summary>
        internal string OutDir
        {
            get =>
                outDir ??= Path.GetFullPath(
                    Path.Combine(
                        "AutoplayRuns",
                        DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                    )
                );
            set => outDir = Path.GetFullPath(value);
        }

        internal static AutoplayRunRequest FromCommandLine() =>
            FromArguments(Environment.GetCommandLineArgs(), ReadGitSha);

        internal static AutoplayRunRequest FromArguments(string[] arguments, Func<string> readSha)
        {
            var request = new AutoplayRunRequest { Sha = readSha() };
            for (int index = 0; index < arguments.Length; index++)
                index += request.ApplyArgument(arguments[index], Following(arguments, index));

            if (!AutoplayTiers.TryFind(request.Tier, out _))
            {
                Debug.LogError(
                    $"[Autoplay] Unknown tier '{request.Tier}'. Known tiers: {AutoplayTiers.Names()}."
                );
                request.Tier = AutoplayTiers.Default;
            }

            return request;
        }

        private static string Following(string[] arguments, int index) =>
            index + 1 < arguments.Length ? arguments[index + 1] : null;

        /// <summary>Applies one option, returning how many extra arguments it consumed.</summary>
        private int ApplyArgument(string argument, string value)
        {
            switch (argument)
            {
                case "-build":
                    Build = true;
                    return 0;
                case "-keep-frames":
                    KeepFrames = true;
                    return 0;
                case "-menus":
                    Overrides.Add("--menus");
                    return 0;
                case "-noMenus":
                    Overrides.Add("--no-menus");
                    return 0;
                case "-features":
                    Overrides.Add("--features");
                    return 0;
                case "-noFeatures":
                    Overrides.Add("--no-features");
                    return 0;
                case "-journey":
                    Overrides.Add("--journey");
                    return 0;
                case "-noJourney":
                    Overrides.Add("--no-journey");
                    return 0;
                default:
                    return ApplyValueArgument(argument, value);
            }
        }

        private int ApplyValueArgument(string argument, string value)
        {
            if (value == null)
                return 0;

            switch (argument)
            {
                case "-tier":
                    Tier = value;
                    return 1;
                case "-seed":
                    Seed = ParseInt(value, Seed);
                    return 1;
                case "-out":
                    OutDir = value;
                    return 1;
                case "-timeout":
                    TimeoutSeconds = ParseInt(value, TimeoutSeconds);
                    return 1;
                case "-duration":
                case "-interval":
                case "-timestep":
                case "-max-frames":
                case "-scenario":
                case "-minlevel":
                case "-tuning":
                case "-capture-on":
                    Overrides.Add($"--{argument.Substring(1)}={value}");
                    return 1;
                default:
                    return 0;
            }
        }

        private static int ParseInt(string raw, int fallback) =>
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;

        /// <summary>
        /// Stamps the run with the commit it tested. A missing or broken git is not
        /// worth failing a run over, so it degrades to an empty stamp.
        /// </summary>
        internal static string ReadGitSha()
        {
            try
            {
                var start = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using Process git = Process.Start(start);
                string sha = git.StandardOutput.ReadToEnd().Trim();
                git.WaitForExit();
                return git.ExitCode == 0 ? sha : "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
