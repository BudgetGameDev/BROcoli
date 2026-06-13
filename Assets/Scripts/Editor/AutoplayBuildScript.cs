#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds a StandaloneOSX player for autoplay/E2E runs.
///
/// CLI (requires the editor to be CLOSED — Unity allows one instance per project):
///   Unity -batchmode -quit -projectPath . -executeMethod AutoplayBuildScript.BuildAutoplayPlayer
///
/// Menu (use when the editor is already open):
///   Tools > Autoplay > Build Player
/// </summary>
public static class AutoplayBuildScript
{
    private const string OutputPath = "Build/BROcoli-autoplay.app";

    [MenuItem("Tools/Autoplay/Build Player")]
    public static void BuildAutoplayPlayer()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[Autoplay] No enabled scenes in Build Settings; cannot build.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development,
        };

        Debug.Log($"[Autoplay] Building {OutputPath} with {scenes.Length} scene(s)...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Autoplay] Build succeeded ({summary.totalSize} bytes) -> {OutputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[Autoplay] Build {summary.result} with {summary.totalErrors} error(s).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
#endif
