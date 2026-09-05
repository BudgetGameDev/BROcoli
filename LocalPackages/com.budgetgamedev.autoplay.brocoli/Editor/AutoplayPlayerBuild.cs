using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    /// <summary>
    /// Builds the desktop player the autoplay harness drives, always for the host
    /// this is running on. The harness launches the player as a real process, so it
    /// has to be a player the host can execute -- which is why there is no target
    /// option here.
    /// </summary>
    public static class AutoplayPlayerBuild
    {
        internal static string OutputRoot =>
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("BROCOLI_AUTOPLAY_BUILD_ROOT")
            )
                ? "Build/BROcoli-autoplay"
                : Environment.GetEnvironmentVariable("BROCOLI_AUTOPLAY_BUILD_ROOT");

        /// <summary>The build target for the running editor's own platform.</summary>
        internal static BuildTarget HostTarget =>
            Application.platform switch
            {
                RuntimePlatform.OSXEditor => BuildTarget.StandaloneOSX,
                RuntimePlatform.WindowsEditor => BuildTarget.StandaloneWindows64,
                _ => BuildTarget.StandaloneLinux64,
            };

        /// <summary>Where the player is written, extension included.</summary>
        internal static string PlayerPath(BuildTarget target) =>
            target switch
            {
                BuildTarget.StandaloneOSX => $"{OutputRoot}/BROcoli-autoplay.app",
                BuildTarget.StandaloneWindows64 => $"{OutputRoot}/BROcoli-autoplay.exe",
                _ => $"{OutputRoot}/BROcoli-autoplay.x86_64",
            };

        /// <summary>
        /// The file to actually start. A macOS player is a bundle directory, and the
        /// binary inside it is named after the product rather than the bundle, so it
        /// is discovered rather than assumed.
        /// </summary>
        internal static string ExecutablePath(string playerPath)
        {
            if (!playerPath.EndsWith(".app", StringComparison.Ordinal))
                return playerPath;

            string binaries = Path.Combine(playerPath, "Contents", "MacOS");
            if (!Directory.Exists(binaries))
                return null;
            return Directory.EnumerateFiles(binaries).FirstOrDefault();
        }

        [MenuItem("Tools/Autoplay/Build Player")]
        public static void BuildForHost() => Build(HostTarget);

        /// <summary>Builds the player, returning the path to run or null on failure.</summary>
        internal static string Build(BuildTarget target)
        {
            string[] scenes = PrepareScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Autoplay] No enabled scenes in Build Settings; cannot build.");
                return null;
            }

            string playerPath = PlayerPath(target);
            Debug.Log($"[Autoplay] Building {target} player with {scenes.Length} scene(s)...");
            BuildReport report = BuildPipeline.BuildPlayer(
                BudgetGameDev.Hub.Editor.BuildRenderingPolicy.PrepareOptions(
                    new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = playerPath,
                        target = target,
                        options = BuildOptions.Development,
                        extraScriptingDefines = new[] { "GAME_AUTOPLAY" },
                    }
                )
            );

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"[Autoplay] Build {summary.result} with {summary.totalErrors} error(s)."
                );
                return null;
            }

            string managed =
                target == BuildTarget.StandaloneOSX
                    ? Path.Combine(playerPath, "Contents", "Resources", "Data", "Managed")
                    : Path.Combine(
                        Path.GetDirectoryName(playerPath),
                        Path.GetFileNameWithoutExtension(playerPath) + "_Data",
                        "Managed"
                    );
            if (
                Directory.Exists(managed)
                && !File.Exists(Path.Combine(managed, "BudgetGameDev.Autoplay.Brocoli.dll"))
            )
                throw new InvalidOperationException(
                    "Autoplay adapter was stripped from the development player; refusing to launch a non-automated game."
                );

            Debug.Log($"[Autoplay] Build succeeded ({summary.totalSize} bytes) -> {playerPath}");
            return playerPath;
        }

        /// <summary>
        /// Build Settings is derived from the installed game registry and refreshed here.
        /// A build preprocessor hook would run after BuildPipeline has already read
        /// the scene list, which is too late to matter.
        /// </summary>
        private static string[] PrepareScenes()
        {
            BudgetGameDev.Hub.Editor.HubBuildScenes.Sync(false);
            return EditorBuildSettings
                .scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }
    }
}
