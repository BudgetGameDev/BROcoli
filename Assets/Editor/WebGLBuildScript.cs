#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Reproducible WebGL build entry point for local diagnostics and CI.
/// </summary>
public static class WebGLBuildScript
{
    private const string DefaultOutputPath = "build/WebGL";
    private const string DevelopmentOutputPath = "build/WebGLDebug";

    public static void Build()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        string outputPath = ReadArgument(arguments, "-buildOutput") ?? DefaultOutputPath;
        bool development = arguments.Contains("-development", StringComparer.OrdinalIgnoreCase);
        BuildPlayer(outputPath, development);
    }

    public static void BuildDevelopment()
    {
        BuildPlayer(DevelopmentOutputPath, true);
    }

    private static void BuildPlayer(string outputPath, bool development)
    {
        // Build Settings is derived from the game registry, so refresh it before
        // reading the scene list: the IPreprocessBuildWithReport hook fires after
        // BuildPlayer already captured these paths, which would be too late.
        BudgetGameDev.Hub.Editor.HubBuildScenes.Sync(false);

        // Same reasoning for the launcher config: the player reads a generated
        // copy of the root LauncherConfig.txt, and Unity does not watch files
        // outside Assets/, so regenerate it here rather than trusting that some
        // earlier editor event already did.
        BudgetGameDev.Hub.Editor.LauncherConfigSync.Sync();

        string[] scenes = EditorBuildSettings
            .scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes are configured for the WebGL build.");
        }

        BuildOptions buildOptions = BuildOptions.None;
        if (development)
        {
            // WebGL supports development diagnostics but not an attachable script debugger.
            buildOptions |= BuildOptions.Development;
        }

        Debug.Log(
            $"[WebGLBuild] Building {outputPath} (development={development}, scenes={scenes.Length})"
        );

        BuildReport report = BuildPipeline.BuildPlayer(
            new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = buildOptions,
            }
        );

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"WebGL build {report.summary.result} with {report.summary.totalErrors} errors."
            );
        }

        Debug.Log(
            $"[WebGLBuild] Succeeded ({report.summary.totalSize} bytes, warnings={report.summary.totalWarnings})"
        );
    }

    private static string ReadArgument(string[] arguments, string name)
    {
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }
}
#endif
