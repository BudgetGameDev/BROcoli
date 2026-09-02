using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Keeps each clone's Git configuration compatible with Unity and safely synchronizes dev once
/// per interactive Editor launch. Machine-specific Git settings stay out of the repository.
/// </summary>
[InitializeOnLoad]
internal static class UnityGitWorkspaceBootstrap
{
    private const string SessionKey = "BudgetGameDev.UnityGitWorkspaceBootstrap.Ran.v1";
    private const string DevelopmentBranch = "dev";
    private const string Remote = "origin";
    private const int LocalCommandTimeoutMilliseconds = 10000;
    private const int NetworkCommandTimeoutMilliseconds = 15000;

    private readonly struct GitResult
    {
        internal GitResult(int exitCode, string standardOutput, string standardError, bool timedOut)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
            TimedOut = timedOut;
        }

        internal int ExitCode { get; }
        internal string StandardOutput { get; }
        internal string StandardError { get; }
        internal bool TimedOut { get; }

        internal bool Succeeded => !TimedOut && ExitCode == 0;
    }

    private readonly struct GitSetting
    {
        internal GitSetting(string key, string value)
        {
            Key = key;
            Value = value;
        }

        internal string Key { get; }
        internal string Value { get; }
    }

    static UnityGitWorkspaceBootstrap()
    {
        if (!Application.isBatchMode)
            EditorApplication.delayCall += RunOncePerEditorLaunch;
    }

    [MenuItem("Tools/Project/Run Startup Git Sync")]
    private static void RunFromMenu()
    {
        Run(logSuccess: true);
    }

    private static void RunOncePerEditorLaunch()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        // Set this before invoking Git. An incoming script change can trigger a domain reload,
        // and the operation must not start again in the same Editor process.
        SessionState.SetBool(SessionKey, true);
        Run(logSuccess: false);
    }

    private static void Run(bool logSuccess)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string gitEntry = Path.Combine(projectRoot, ".git");
        if (!Directory.Exists(gitEntry) && !File.Exists(gitEntry))
            return;

        try
        {
            string mergeTool = FindUnityYamlMerge(
                EditorApplication.applicationPath,
                EditorApplication.applicationContentsPath
            );
            ConfigureClone(projectRoot, mergeTool);
            SynchronizeDevelopmentBranch(projectRoot, logSuccess);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Unity Git] Startup configuration failed: {exception.Message}");
        }
    }

    private static void ConfigureClone(string projectRoot, string mergeTool)
    {
        string normalizedMergeTool = mergeTool.Replace('\\', '/');
        var settings = new[]
        {
            new GitSetting("core.autocrlf", "false"),
            new GitSetting("core.safecrlf", "true"),
            new GitSetting("core.precomposeunicode", "true"),
            new GitSetting("fetch.prune", "true"),
            new GitSetting("pull.ff", "only"),
            new GitSetting("merge.conflictStyle", "zdiff3"),
            new GitSetting("merge.unityyamlmerge.name", "Unity Smart Merge"),
            new GitSetting(
                "merge.unityyamlmerge.driver",
                $"\"{normalizedMergeTool}\" merge -p %O %B %A %A"
            ),
            new GitSetting("merge.unityyamlmerge.recursive", "binary"),
        };

        foreach (GitSetting setting in settings)
        {
            GitResult current = RunGit(
                projectRoot,
                LocalCommandTimeoutMilliseconds,
                "config",
                "--local",
                "--get",
                setting.Key
            );
            if (current.Succeeded && current.StandardOutput.TrimEnd() == setting.Value)
                continue;

            RequireGit(
                projectRoot,
                LocalCommandTimeoutMilliseconds,
                "config",
                "--local",
                "--replace-all",
                setting.Key,
                setting.Value
            );
        }
    }

    private static void SynchronizeDevelopmentBranch(string projectRoot, bool logSuccess)
    {
        GitResult branch = RunGit(
            projectRoot,
            LocalCommandTimeoutMilliseconds,
            "branch",
            "--show-current"
        );
        if (!branch.Succeeded || branch.StandardOutput.Trim() != DevelopmentBranch)
            return;

        GitResult status = RequireGit(
            projectRoot,
            LocalCommandTimeoutMilliseconds,
            "status",
            "--porcelain",
            "--untracked-files=all"
        );
        if (!string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            Debug.Log(
                "[Unity Git] Automatic dev sync skipped because the working tree has local changes."
            );
            return;
        }

        GitResult fetch = RunGit(
            projectRoot,
            NetworkCommandTimeoutMilliseconds,
            "fetch",
            "--prune",
            Remote,
            DevelopmentBranch
        );
        if (!fetch.Succeeded)
        {
            Debug.LogWarning($"[Unity Git] Could not fetch origin/dev: {DescribeFailure(fetch)}");
            return;
        }

        GitResult comparison = RequireGit(
            projectRoot,
            LocalCommandTimeoutMilliseconds,
            "rev-list",
            "--left-right",
            "--count",
            $"HEAD...{Remote}/{DevelopmentBranch}"
        );
        int[] counts = comparison
            .StandardOutput.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();
        if (counts.Length != 2)
            throw new InvalidOperationException("Git returned an invalid ahead/behind count.");

        int ahead = counts[0];
        int behind = counts[1];
        if (behind == 0)
        {
            if (logSuccess)
                Debug.Log($"[Unity Git] {DevelopmentBranch} is current.");
            return;
        }

        bool refreshAssets = false;
        AssetDatabase.DisallowAutoRefresh();
        try
        {
            if (ahead == 0)
            {
                RequireGit(
                    projectRoot,
                    LocalCommandTimeoutMilliseconds,
                    "merge",
                    "--ff-only",
                    $"{Remote}/{DevelopmentBranch}"
                );
                refreshAssets = true;
                Debug.Log($"[Unity Git] Fast-forwarded {DevelopmentBranch} by {behind} commit(s).");
                return;
            }

            GitResult rebase = RunGit(
                projectRoot,
                NetworkCommandTimeoutMilliseconds,
                "rebase",
                $"{Remote}/{DevelopmentBranch}"
            );
            if (rebase.Succeeded)
            {
                refreshAssets = true;
                Debug.Log(
                    $"[Unity Git] Rebased {ahead} local {DevelopmentBranch} commit(s) over "
                        + $"{behind} incoming commit(s)."
                );
                return;
            }

            GitResult abort = RunGit(
                projectRoot,
                LocalCommandTimeoutMilliseconds,
                "rebase",
                "--abort"
            );
            if (!abort.Succeeded)
            {
                throw new InvalidOperationException(
                    "Automatic rebase conflicted and Git could not restore the original checkout: "
                        + DescribeFailure(abort)
                );
            }

            refreshAssets = true;
            Debug.LogWarning(
                "[Unity Git] Incoming dev changes overlap local commits. The automatic rebase was "
                    + "aborted and the original checkout was restored; resolve this Git conflict "
                    + "before switching machines."
            );
        }
        finally
        {
            AssetDatabase.AllowAutoRefresh();
            if (refreshAssets)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static string FindUnityYamlMerge(
        string editorApplicationPath,
        string editorContentsPath
    )
    {
        string editorDirectory = Path.GetDirectoryName(editorApplicationPath);
        if (string.IsNullOrEmpty(editorDirectory))
            throw new FileNotFoundException("Could not determine the Unity Editor directory.");

        var candidates = new List<string>
        {
            Path.Combine(editorDirectory, "Data", "Tools", "UnityYAMLMerge.exe"),
            Path.Combine(editorDirectory, "Data", "Tools", "UnityYAMLMerge"),
            Path.Combine(editorDirectory, "Tools", "UnityYAMLMerge"),
        };

        // The contents path is the portable way in: Data on Windows and Linux, and the bundle's
        // Contents on macOS, where applicationPath names the .app itself and the tool lives
        // inside it. Unity 6 ships it under Helpers there rather than Tools.
        if (!string.IsNullOrEmpty(editorContentsPath))
        {
            foreach (string folder in new[] { "Tools", "Helpers", "Resources" })
            {
                candidates.Add(Path.Combine(editorContentsPath, folder, "UnityYAMLMerge.exe"));
                candidates.Add(Path.Combine(editorContentsPath, folder, "UnityYAMLMerge"));
            }
        }

        DirectoryInfo parent = Directory.GetParent(editorDirectory);
        if (parent != null)
            candidates.Add(Path.Combine(parent.FullName, "Tools", "UnityYAMLMerge"));

        string mergeTool = candidates.FirstOrDefault(File.Exists);
        if (mergeTool == null)
        {
            throw new FileNotFoundException(
                "UnityYAMLMerge was not found beside the running Unity Editor.",
                candidates[0]
            );
        }

        return Path.GetFullPath(mergeTool);
    }

    private static GitResult RequireGit(
        string projectRoot,
        int timeoutMilliseconds,
        params string[] arguments
    )
    {
        GitResult result = RunGit(projectRoot, timeoutMilliseconds, arguments);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"git {string.Join(" ", arguments)} failed: {DescribeFailure(result)}"
            );
        }

        return result;
    }

    private static GitResult RunGit(
        string projectRoot,
        int timeoutMilliseconds,
        params string[] arguments
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.EnvironmentVariables["GCM_INTERACTIVE"] = "Never";

        using (var process = new Process { StartInfo = startInfo })
        {
            try
            {
                if (!process.Start())
                    return new GitResult(-1, string.Empty, "Git did not start.", false);
            }
            catch (Exception exception)
            {
                return new GitResult(-1, string.Empty, exception.Message, false);
            }

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException) { }

                return new GitResult(
                    -1,
                    standardOutput.GetAwaiter().GetResult(),
                    standardError.GetAwaiter().GetResult(),
                    true
                );
            }

            process.WaitForExit();
            return new GitResult(
                process.ExitCode,
                standardOutput.GetAwaiter().GetResult(),
                standardError.GetAwaiter().GetResult(),
                false
            );
        }
    }

    private static string DescribeFailure(GitResult result)
    {
        if (result.TimedOut)
            return "the operation timed out";

        string detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        detail = detail.Trim();
        return string.IsNullOrEmpty(detail) ? $"exit code {result.ExitCode}" : detail;
    }
}
