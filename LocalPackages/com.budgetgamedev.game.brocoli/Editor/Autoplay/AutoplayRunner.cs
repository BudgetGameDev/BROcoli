using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    /// <summary>
    /// Runs an end-to-end autoplay session: builds the host player, launches it,
    /// waits for it to finish, and reports what it did. This is the whole harness --
    /// it replaces the launcher scripts it grew out of, so one implementation serves
    /// macOS, Linux, and Windows and there is no shell dialect to keep in step.
    ///
    /// From a terminal, with the editor closed:
    ///
    ///   unity run . -- -executeMethod
    ///     BudgetGameDev.Games.Brocoli.Editor.AutoplayRunner.Run -tier coverage
    ///
    /// The process exit code is the run's verdict, so it drops straight into any
    /// automation. From an open editor, use Tools > Autoplay.
    /// </summary>
    public static partial class AutoplayRunner
    {
        public static void Run()
        {
            AutoplayRunRequest request = AutoplayRunRequest.FromCommandLine();
            int code = Execute(request);
            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(code);
        }

        [MenuItem("Tools/Autoplay/Run Smoke Tier")]
        public static void RunSmoke() => LaunchDetached("smoke");

        [MenuItem("Tools/Autoplay/Run Medium Tier")]
        public static void RunMedium() => LaunchDetached("medium");

        [MenuItem("Tools/Autoplay/Run Feature Coverage Tier")]
        public static void RunCoverage() => LaunchDetached("coverage");

        [MenuItem("Tools/Autoplay/Run Marathon Tier")]
        public static void RunMarathon() => LaunchDetached("marathon");

        /// <summary>
        /// Menu runs start the player and return. Waiting would block the editor's
        /// main thread for the length of the run, which for the marathon tier means
        /// an editor that looks hung for several minutes.
        /// </summary>
        private static void LaunchDetached(string tier)
        {
            var request = new AutoplayRunRequest { Tier = tier };
            string executable = ResolvePlayer(request);
            if (executable == null)
                return;

            Directory.CreateDirectory(request.OutDir);
            StartPlayer(executable, request);
            Debug.Log($"[Autoplay] Started the {tier} tier. Results: {request.OutDir}");
        }

        private static int Execute(AutoplayRunRequest request)
        {
            string executable = ResolvePlayer(request);
            if (executable == null)
                return 2;

            Directory.CreateDirectory(request.OutDir);
            Debug.Log(
                $"[Autoplay] Running tier '{request.Tier}' seed {request.Seed} -> {request.OutDir}"
            );

            using Process player = StartPlayer(executable, request);
            if (!player.WaitForExit(request.TimeoutSeconds * 1000))
            {
                player.Kill();
                Debug.LogError(
                    $"[Autoplay] The player did not finish within {request.TimeoutSeconds}s."
                );
                return 2;
            }

            ReportRun(request, player.ExitCode);
            return player.ExitCode;
        }

        /// <summary>Builds when asked or when no player is there yet.</summary>
        private static string ResolvePlayer(AutoplayRunRequest request)
        {
            string playerPath = AutoplayPlayerBuild.PlayerPath(AutoplayPlayerBuild.HostTarget);
            bool present = File.Exists(playerPath) || Directory.Exists(playerPath);
            if (request.Build || !present)
            {
                playerPath = AutoplayPlayerBuild.Build(AutoplayPlayerBuild.HostTarget);
                if (playerPath == null)
                    return null;
            }

            string executable = AutoplayPlayerBuild.ExecutablePath(playerPath);
            if (executable == null)
                Debug.LogError($"[Autoplay] No executable found inside {playerPath}.");
            return executable;
        }

        private static Process StartPlayer(string executable, AutoplayRunRequest request)
        {
            var start = new ProcessStartInfo(Path.GetFullPath(executable))
            {
                UseShellExecute = false,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };
            foreach (string argument in PlayerArguments(request))
                start.ArgumentList.Add(argument);
            return Process.Start(start);
        }

        internal static IEnumerable<string> PlayerArguments(AutoplayRunRequest request)
        {
            yield return "--autoplay";
            yield return $"--tier={request.Tier}";
            yield return $"--seed={request.Seed.ToString(CultureInfo.InvariantCulture)}";
            yield return $"--out={request.OutDir}";
            yield return $"--sha={request.Sha}";
            foreach (string extra in request.Overrides)
                yield return extra;
            yield return "-logFile";
            yield return Path.Combine(request.OutDir, "player.log");
        }
    }
}
