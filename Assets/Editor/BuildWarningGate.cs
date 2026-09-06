#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Rejects player-build warnings except for known Unity toolchain diagnostics.</summary>
public sealed class BuildWarningGate : IPostprocessBuildWithReport
{
    public int callbackOrder => int.MaxValue;

    public void OnPostprocessBuild(BuildReport report)
    {
        string[] warnings = UnexpectedWarnings(report);
        if (warnings.Length == 0)
            return;

        throw new BuildFailedException(
            $"Build emitted {warnings.Length} unexpected warning(s); warnings are errors.\n"
                + string.Join("\n", warnings)
        );
    }

    public static string[] UnexpectedWarnings(BuildReport report) =>
        report
            .steps.SelectMany(step => step.messages)
            .Where(message => message.type == LogType.Warning)
            .Select(message => message.content)
            .Where(content => !IsKnownToolchainWarning(content))
            .Distinct()
            .ToArray();

    internal static bool IsKnownToolchainWarning(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        // Unity revalidates this bundled HDRP diagnostic graph when switching to
        // URP. It is not a player shader; keep the exception exact so game shader
        // validation warnings still fail the release.
        if (
            content
            == "Shader Graph at Packages/com.unity.render-pipelines.high-definition/Runtime/Tools/"
                + "ColorChecker/ColorCheckerShader.shadergraph has 2 warning(s), the first is: "
                + "Validation: Exposure Node is not allowed by Universal implementation"
        )
            return true;

        if (
            content.StartsWith(
                "Duplicate assembly 'System.Runtime.CompilerServices.Unsafe.dll' with different versions detected",
                StringComparison.Ordinal
            )
        )
        {
            return content.Contains("Packages/com.unity.", StringComparison.Ordinal);
        }

        if (
            content.StartsWith(
                "Assembly system.componentmodel.composition has duplicate hint path",
                StringComparison.Ordinal
            )
            || content.StartsWith(
                "Assembly system.runtime.interopservices.windowsruntime has duplicate hint path",
                StringComparison.Ordinal
            )
        )
        {
            return true;
        }

        return content.StartsWith("[Unity.TextMeshPro] - ", StringComparison.Ordinal)
            && content.EndsWith(
                "Method was given it's own cpp file because it is large and costly to compile",
                StringComparison.Ordinal
            );
    }
}
#endif
