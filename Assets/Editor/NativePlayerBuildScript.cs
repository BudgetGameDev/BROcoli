#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Manual Windows, macOS, and Linux desktop player builds.</summary>
public sealed class NativePlayerBuildScript : IPreprocessBuildWithReport
{
    private const string DefaultBuildRoot = "build/native/players";

    private static readonly string[] KnownTargets = { "windows", "macos", "linux" };

    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        ConfigureTarget(report.summary.platform);
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
        BuildReport report = BuildPipeline.BuildPlayer(
            new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None,
            }
        );

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
#endif
