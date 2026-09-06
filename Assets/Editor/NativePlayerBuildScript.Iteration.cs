#if UNITY_EDITOR
using BudgetGameDev.Hub.Editor;
using BudgetGameDev.Shared.Rendering;
using UnityEditor;

public sealed partial class NativePlayerBuildScript
{
    private static bool forceScriptsOnly;

    [MenuItem("Tools/Build/Iteration/Windows HDRP Full Development Player")]
    public static void BuildHdrpIterationFull() => BuildHdrpIteration(false);

    [MenuItem("Tools/Build/Iteration/Windows HDRP Scripts Only")]
    public static void BuildHdrpIterationScriptsOnly() => BuildHdrpIteration(true);

    private static void BuildHdrpIteration(bool scriptsOnly)
    {
        var previous = BuildRenderingPolicy.PipelineOverride;
        bool previousScriptsOnly = forceScriptsOnly;
        try
        {
            BuildRenderingPolicy.PipelineOverride = RenderPipelineKind.HighDefinition;
            forceScriptsOnly = scriptsOnly;
            BuildPlayer(
                BuildTarget.StandaloneWindows64,
                "build/iteration/windows-hdrp/BROcoli.exe",
                "Windows HDRP Iteration",
                PrepareBuild(),
                true
            );
        }
        finally
        {
            forceScriptsOnly = previousScriptsOnly;
            BuildRenderingPolicy.PipelineOverride = previous;
        }
    }
}
#endif
