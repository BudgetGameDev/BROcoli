using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace BudgetGameDev.Shared.Rendering.Editor
{
    internal static class StreamlineSetup
    {
        internal const string FrameworkDefine = "ENABLE_UPSCALER_FRAMEWORK";

        [InitializeOnLoadMethod]
        private static void Install() => EditorApplication.delayCall += EnableFramework;

        [MenuItem("Tools/Rendering/Enable Streamline Upscaler Framework")]
        private static void EnableFramework()
        {
            if (
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)
                != BuildTargetGroup.Standalone
            )
                return;
            if (
                Type.GetType(
                    "UnityEngine.Rendering.Universal.SharedStreamlineHooks, Unity.RenderPipelines.Universal.Runtime"
                ) == null
            )
                return;
            // The framework changes serialized pipeline fields, so Editor and player must share it.
            var target = NamedBuildTarget.Standalone;
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';');
            if (symbols.Contains(FrameworkDefine))
                return;
            PlayerSettings.SetScriptingDefineSymbols(
                target,
                string.Join(
                    ";",
                    symbols.Where(value => !string.IsNullOrEmpty(value)).Append(FrameworkDefine)
                )
            );
            AssetDatabase.SaveAssets();
        }
    }
}
