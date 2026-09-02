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
/// Points each clone's Git configuration at the Unity installation running it: the Smart Merge
/// driver for Unity's YAML assets, and the line ending and fetch settings a shared checkout needs.
/// These are machine specific, so they are configured here rather than committed.
///
/// It deliberately does not move the branch. Churn between machines comes from files whose
/// committed contents differ from what the Editor writes, and the fix for that is to commit what
/// the Editor writes; fetching or rebasing on launch does not address it, and relocating the
/// checkout as a side effect of opening the Editor is a poor trade for work in progress.
/// </summary>
[InitializeOnLoad]
internal static class UnityGitWorkspaceBootstrap
{
    private const string SessionKey = "BudgetGameDev.UnityGitWorkspaceBootstrap.Ran.v1";
    private const int LocalCommandTimeoutMilliseconds = 10000;

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

    [MenuItem("Tools/Project/Configure Git Workspace")]
    private static void RunFromMenu()
    {
        Run();
    }

    private static void RunOncePerEditorLaunch()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        // Set this before invoking Git. An incoming script change can trigger a domain reload,
        // and the operation must not start again in the same Editor process.
        SessionState.SetBool(SessionKey, true);
        Run();
    }

    private static void Run()
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
