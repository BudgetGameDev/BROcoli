#if UNITY_EDITOR
using System;
using System.Linq;
using BudgetGameDev.Hub.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class NativePlayerBuildScript
{
    private static void BuildSingle(BuildTarget target, string defaultOutput, string label)
    {
        string output = ReadArgument("-buildOutput") ?? defaultOutput;
        BuildPlayer(target, output, label, PrepareBuild(), HasArgument("-development"));
    }

    private static string[] PrepareBuild()
    {
        BudgetGameDev.Hub.Editor.HubBuildScenes.Sync(false);

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
        BuildReport report;
        try
        {
            ConfigureTarget(target);
            report = BuildPipeline.BuildPlayer(
                BuildRenderingPolicy.PrepareOptions(
                    new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = output,
                        target = target,
                        options = development ? BuildOptions.Development : BuildOptions.None,
                    }
                )
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

    private static void ConfigureQualityLevels(BuildTarget target, RenderPipelineAsset pipeline)
    {
        // Start from the authored tiers on repeated callbacks and when BuildAll switches targets.
        var settings = QualitySettings.GetQualitySettings();
        EditorJsonUtility.FromJsonOverwrite(authoredQualitySettings, settings);
        string platform = NamedBuildTarget
            .FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target))
            .TargetName;
        int[] included = QualitySettings.GetActiveQualityLevelsForPlatform(platform);
        int[] compatible = included
            .Where(index =>
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(index);
                return asset == null || asset.GetType() == pipeline.GetType();
            })
            .ToArray();
        if (compatible.Length == 0)
            throw new BuildFailedException(
                $"No {pipeline.GetType().Name} quality levels enabled for {target}."
            );

        foreach (int index in included.Except(compatible))
            if (!QualitySettings.TryExcludePlatformAt(platform, index, out Exception error))
                throw new BuildFailedException(error);

        // An excluded HDRP default must not survive in a URP player. Keep an authored
        // compatible default; otherwise use the highest remaining enabled tier.
        var serialized = new SerializedObject(settings);
        var defaults = serialized.FindProperty("m_PerPlatformDefaultQuality");
        for (int index = 0; index < defaults.arraySize; index++)
        {
            var entry = defaults.GetArrayElementAtIndex(index);
            if (entry.FindPropertyRelative("first").stringValue != platform)
                continue;
            var quality = entry.FindPropertyRelative("second");
            if (!compatible.Contains(quality.intValue))
                quality.intValue = compatible.Last();
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }
}
#endif
