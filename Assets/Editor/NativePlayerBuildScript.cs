#if UNITY_EDITOR
using System;
using System.Linq;
using BudgetGameDev.Hub.Editor;
using BudgetGameDev.Shared.Rendering;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Manual Windows, macOS, and Linux desktop player builds.</summary>
public sealed partial class NativePlayerBuildScript
    : IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
{
    private const string DefaultBuildRoot = "build/native/players";

    private const string HighDefinitionPipelinePath =
        "Assets/Settings/Rendering/HDRP/BROcoli HDRP RT Ultra.asset";
    private const string UniversalPipelinePath = "Assets/3dRenderer.asset";

    private static readonly string[] KnownTargets = { "windows", "macos", "linux" };

    private static RenderPipelineAsset authoredDefaultPipeline;
    private static bool defaultPipelineHeld;
    private static string authoredQualitySettings;
    private static BuildTarget? configuredTarget;
    private static RenderPipelineAsset configuredPipeline;

    /// <summary>
    /// First, ahead of every other build callback. High Definition's own
    /// <c>HDRPPreprocessBuild</c> sits at <c>int.MinValue + 100</c> and refuses a target whose
    /// quality levels and Graphics Settings name different pipelines, so the pipeline this
    /// target ships with has to be chosen before it looks.
    /// </summary>
    public int callbackOrder => int.MinValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        ConfigureTarget(report.summary.platform);
    }

    /// <summary>Puts the authored pipeline back, so a build leaves the project as it found it.</summary>
    public void OnPostprocessBuild(BuildReport report)
    {
        RestoreDefaultPipeline();
    }

    [MenuItem("Tools/Build/Native/All Desktop Players")]
    public static void BuildAll()
    {
        string root = ReadArgument("-buildOutput") ?? DefaultBuildRoot;
        bool development = HasArgument("-development");
        string[] selected = ReadSelectedTargets();
        string[] scenes = PrepareBuild();

        if (Array.IndexOf(selected, "windows") >= 0)
        {
            BuildPlayer(
                BuildTarget.StandaloneWindows64,
                CombinePath(root, $"windows/{BuildContentPolicy.ProductName}.exe"),
                "Windows HDR10",
                scenes,
                development
            );
        }
        if (Array.IndexOf(selected, "macos") >= 0)
        {
            BuildPlayer(
                BuildTarget.StandaloneOSX,
                CombinePath(root, $"macos/{BuildContentPolicy.ProductName}.app"),
                "macOS Metal HDR",
                scenes,
                development
            );
        }
        if (Array.IndexOf(selected, "linux") >= 0)
        {
            BuildPlayer(
                BuildTarget.StandaloneLinux64,
                CombinePath(root, $"linux/{BuildContentPolicy.ProductName}.x86_64"),
                "Linux",
                scenes,
                development
            );
        }
    }

    /// <summary>Reads the -buildTargets selection, defaulting to every player.</summary>
    internal static string[] ParseSelectedTargets(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return KnownTargets;

        string[] selected = argument
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        if (selected.Length == 0)
            throw new BuildFailedException("-buildTargets selected no native players.");

        string unknown = selected.FirstOrDefault(name => Array.IndexOf(KnownTargets, name) < 0);
        if (unknown != null)
        {
            throw new BuildFailedException(
                $"Unknown native build target '{unknown}'. "
                    + $"Expected one of {string.Join(", ", KnownTargets)}."
            );
        }

        return selected;
    }

    private static string[] ReadSelectedTargets() =>
        ParseSelectedTargets(ReadArgument("-buildTargets"));

    [MenuItem("Tools/Build/Native/Windows URP HDR10 Player")]
    public static void BuildWindows() =>
        BuildSingle(
            BuildTarget.StandaloneWindows64,
            "build/native/players/windows/BROcoli.exe",
            "Windows HDR10"
        );

    [MenuItem("Tools/Build/Native/Windows HDRP HDR10 Player")]
    public static void BuildWindowsHighDefinition()
    {
        var previous = BuildRenderingPolicy.PipelineOverride;
        try
        {
            BuildRenderingPolicy.PipelineOverride = RenderPipelineKind.HighDefinition;
            BuildWindows();
        }
        finally
        {
            BuildRenderingPolicy.PipelineOverride = previous;
        }
    }

    [MenuItem("Tools/Build/Native/macOS Metal HDR Player")]
    public static void BuildMacOS() =>
        BuildSingle(
            BuildTarget.StandaloneOSX,
            "build/native/players/macos/BROcoli.app",
            "macOS Metal HDR"
        );

    [MenuItem("Tools/Build/Native/Linux Player")]
    public static void BuildLinux() =>
        BuildSingle(
            BuildTarget.StandaloneLinux64,
            "build/native/players/linux/BROcoli.x86_64",
            "Linux"
        );

    internal static void ConfigureTarget(BuildTarget target)
    {
        ConfigureSplashScreen();
        ConfigureDefaultPipeline(target);

        switch (target)
        {
            case BuildTarget.StandaloneWindows64:
                ConfigureWindows();
                break;
            case BuildTarget.StandaloneOSX:
                ConfigureMacOS();
                break;
            case BuildTarget.StandaloneLinux64:
                ConfigureLinux();
                break;
        }
    }

    private static void ConfigureSplashScreen()
    {
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
    }

    internal static string PipelineAssetPathFor(BuildTarget target) =>
        BuildRenderingPolicy.PipelineFor(target) == RenderPipelineKind.HighDefinition
            ? HighDefinitionPipelinePath
            : UniversalPipelinePath;

    /// <summary>Temporarily select only the pipeline and quality tiers this player uses.</summary>
    internal static void ConfigureDefaultPipeline(BuildTarget target)
    {
        string path = PipelineAssetPathFor(target);
        RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
        if (pipeline == null)
        {
            throw new BuildFailedException(
                $"The {target} player renders through {path}, which is missing."
            );
        }

        // Unity calls this from the entry point, build player processor and preprocessor.
        // Reapplying identical quality settings reimports pipeline-dependent shader graphs.
        if (defaultPipelineHeld && configuredTarget == target && configuredPipeline == pipeline)
            return;

        if (!defaultPipelineHeld)
        {
            authoredDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            authoredQualitySettings = EditorJsonUtility.ToJson(
                QualitySettings.GetQualitySettings()
            );
            defaultPipelineHeld = true;
            // A build that fails reaches no post-process callback, and one started from the
            // Build Settings window reaches none of this class's own entry points either, so
            // the project would be left pointing at a pipeline it is not authored with. The
            // Editor ticks again once the build is over, whichever way it went.
            EditorApplication.update += RestoreWhenBuildEnds;
        }

        ConfigureQualityLevels(target, pipeline);
        SetDefaultPipeline(pipeline);
        configuredTarget = target;
        configuredPipeline = pipeline;
    }

    private static void RestoreWhenBuildEnds()
    {
        if (BuildPipeline.isBuildingPlayer)
            return;

        EditorApplication.update -= RestoreWhenBuildEnds;
        RestoreDefaultPipeline();
    }

    private static void RestoreDefaultPipeline()
    {
        if (!defaultPipelineHeld)
            return;

        EditorApplication.update -= RestoreWhenBuildEnds;
        EditorJsonUtility.FromJsonOverwrite(
            authoredQualitySettings,
            QualitySettings.GetQualitySettings()
        );
        EditorUtility.SetDirty(QualitySettings.GetQualitySettings());
        authoredQualitySettings = null;
        SetDefaultPipeline(authoredDefaultPipeline);
        authoredDefaultPipeline = null;
        defaultPipelineHeld = false;
        configuredTarget = null;
        configuredPipeline = null;
    }

    /// <summary>
    /// Writes the graphics default through to disk. The build reads the serialized settings, not
    /// the object this holds, so a change left in memory is a change the player never sees: it
    /// is what makes a Windows build fail on the pipeline mix even with this callback in place.
    /// </summary>
    private static void SetDefaultPipeline(RenderPipelineAsset pipeline)
    {
        GraphicsSettings.defaultRenderPipeline = pipeline;
        AssetDatabase.SaveAssets();
    }
}

/// <summary>
/// Chooses the target's pipeline before anything else in the build looks at it.
///
/// A build player processor runs ahead of every <c>IPreprocessBuildWithReport</c> callback, and
/// that is the only place early enough: High Definition reads the quality levels and Graphics
/// Settings from its own probe volume processor, which is one of these, so a callback -- at any
/// order -- arrives after it has already decided the project mixes pipelines and refused.
/// </summary>
public sealed class NativePlayerPipelineSelector : BuildPlayerProcessor
{
    public override int callbackOrder => int.MinValue;

    public override void PrepareForBuild(BuildPlayerContext context)
    {
        NativePlayerBuildScript.ConfigureDefaultPipeline(context.BuildPlayerOptions.target);
    }
}
#endif
