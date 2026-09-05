using NUnit.Framework;
using UnityEditor;

namespace BudgetGameDev.Shared.Rendering.HighDefinition.Tests
{
    public sealed partial class LightingPipelineParityTests
    {
        private static void IgnoreWhenHdrpIsUnsupported()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                Assert.Ignore(
                    "HDRP rendering is unavailable while WebGL is the active build target."
                );
        }
    }
}
