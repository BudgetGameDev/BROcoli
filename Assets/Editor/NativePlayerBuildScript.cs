#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Manual Windows, macOS, and Linux desktop player builds.</summary>
public sealed class NativePlayerBuildScript : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string DefaultBuildRoot = "build/native/players";

    /// <summary>
    /// The pipeline the Windows player renders through: the top tier, which is also what
    /// `Standalone` in the quality settings' per-platform defaults selects. The other three
    /// tiers name their own asset and are reached by changing quality level.
    /// </summary>
    private const string HighDefinitionPipelinePath =
        "Assets/Settings/Rendering/HDRP/BROcoli HDRP RT Ultra.asset";

    /// <summary>The pipeline every other player renders through, the web build included.</summary>
    private const string UniversalPipelinePath = "Assets/3dRenderer.asset";

    private static readonly string[] KnownTargets = { "windows", "macos", "linux" };

    private static RenderPipelineAsset authoredDefaultPipeline;
    private static bool defaultPipelineHeld;

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
                CombinePath(root, "windows/BROcoli.exe"),
                "Windows HDR10",
                scenes,
                development
            );
        }
        if (Array.IndexOf(selected, "macos") >= 0)
        {
            BuildPlayer(
                BuildTarget.StandaloneOSX,
                CombinePath(root, "macos/BROcoli.app"),
                "macOS Metal HDR",
                scenes,
                development
            );
        }
        if (Array.IndexOf(selected, "linux") >= 0)
        {
            BuildPlayer(
                BuildTarget.StandaloneLinux64,
                CombinePath(root, "linux/BROcoli.x86_64"),
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

    [MenuItem("Tools/Build/Native/Windows HDR10 Player")]
    public static void BuildWindows() =>
        BuildSingle(
            BuildTarget.StandaloneWindows64,
            "build/native/players/windows/BROcoli.exe",
            "Windows HDR10"
        );

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

    /// <summary>The pipeline <paramref name="target"/> ships with: Windows renders through High
    /// Definition, every other player through Universal.</summary>
    internal static string PipelineAssetPathFor(BuildTarget target) =>
        target == BuildTarget.StandaloneWindows64 ? HighDefinitionPipelinePath
        : UniversalPipelinePath;

    /// <summary>
    /// Points Graphics Settings at the pipeline this target ships with, so a player carries one
    /// pipeline rather than two.
    ///
    /// The four Windows tiers name their own pipeline asset; the six levels from Very Low to
    /// Ultra name none, so they follow this default. High Definition refuses to build a target
    /// whose quality levels and Graphics Settings disagree, which is why leaving the default on
    /// Universal fails the Windows player before it compiles a shader rather than shipping a
    /// mixed one.
    ///
    /// The authored value is remembered and put back once the player is written.
    /// </summary>
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

        if (!defaultPipelineHeld)
        {
            authoredDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            defaultPipelineHeld = true;
            // A build that fails reaches no post-process callback, and one started from the
            // Build Settings window reaches none of this class's own entry points either, so
            // the project would be left pointing at a pipeline it is not authored with. The
            // Editor ticks again once the build is over, whichever way it went.
            EditorApplication.update += RestoreWhenBuildEnds;
        }

        SetDefaultPipeline(pipeline);
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
        SetDefaultPipeline(authoredDefaultPipeline);
        authoredDefaultPipeline = null;
        defaultPipelineHeld = false;
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

    private static void BuildSingle(BuildTarget target, string defaultOutput, string label)
    {
        string output = ReadArgument("-buildOutput") ?? defaultOutput;
        BuildPlayer(target, output, label, PrepareBuild(), HasArgument("-development"));
    }

    private static string[] PrepareBuild()
    {
        BudgetGameDev.Hub.Editor.HubBuildScenes.Sync(false);
        BudgetGameDev.Hub.Editor.LauncherConfigSync.Sync();

        string[] scenes = EditorBuildSettings
            .scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new BuildFailedException("No enabled scenes are configured for native builds.");

        return scenes;
    }

    private static void BuildPlayer(
        BuildTarget target,
        string output,
        string label,
        string[] scenes,
        bool development
    )
    {
        ConfigureTarget(target);
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = target,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                }
            );
        }
        finally
        {
            // A build that fails never reaches the post-process callback, and the pipeline it
            // was pointed at is not what the project is authored with.
            RestoreDefaultPipeline();
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"{label} build {report.summary.result} with "
                    + $"{report.summary.totalErrors} error(s)."
            );
        }

        Debug.Log(
            $"[{label} Build] Succeeded ({report.summary.totalSize} bytes, "
                + $"{report.summary.totalWarnings} warning(s), "
                + $"{report.summary.totalErrors} error(s)) -> {output}"
        );
    }

    private static void ConfigureWindows()
    {
        ConfigureHdrOutput();
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.useFlipModelSwapchain = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(
            BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 }
        );
    }

    private static void ConfigureMacOS()
    {
        ConfigureHdrOutput();
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
        PlayerSettings.SetGraphicsAPIs(
            BuildTarget.StandaloneOSX,
            new[] { GraphicsDeviceType.Metal }
        );
    }

    private static void ConfigureLinux()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
        PlayerSettings.SetGraphicsAPIs(
            BuildTarget.StandaloneLinux64,
            new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore }
        );
    }

    private static void ConfigureHdrOutput()
    {
        PlayerSettings.allowHDRDisplaySupport = true;
        PlayerSettings.useHDRDisplay = true;
        PlayerSettings.hdrBitDepth = HDRDisplayBitDepth.BitDepth10;
    }

    private static string CombinePath(string root, string relative) =>
        $"{root.TrimEnd('/', '\\')}/{relative}";

    private static bool HasArgument(string name) =>
        Environment
            .GetCommandLineArgs()
            .Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

    private static string ReadArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }

        return null;
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
