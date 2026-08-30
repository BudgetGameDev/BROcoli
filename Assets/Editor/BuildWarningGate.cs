#if UNITY_EDITOR
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Rejects every player build that records a warning in its BuildReport.</summary>
public sealed class BuildWarningGate : IPostprocessBuildWithReport
{
    public int callbackOrder => int.MaxValue;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.totalWarnings == 0)
            return;

        throw new BuildFailedException(
            $"Build emitted {report.summary.totalWarnings} warning(s); warnings are errors."
        );
    }
}
#endif
