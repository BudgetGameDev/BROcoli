using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BudgetGameDev.Hub.Editor
{
    /// <summary>Explicit scripts-only iteration against a compatible completed full build.</summary>
    public static class NativeBuildIteration
    {
        public static BuildPlayerOptions Prepare(BuildPlayerOptions options, bool scriptsOnly)
        {
            if (!scriptsOnly)
            {
                // A failed replacement build must not leave an older baseline eligible for reuse.
                string previousReceipt = ReceiptPath(options);
                if (File.Exists(previousReceipt))
                    File.Delete(previousReceipt);
                return options;
            }
            string receipt = ReceiptPath(options);
            if (
                !(
                    File.Exists(options.locationPathName)
                    || Directory.Exists(options.locationPathName)
                )
                || !File.Exists(receipt)
                || File.ReadAllText(receipt) != Signature(options)
            )
                throw new BuildFailedException(
                    "Scripts-only iteration requires a successful full build at this output "
                        + "with the same Unity version, pipeline, scenes, defines and build options."
                );
            options.options |= BuildOptions.BuildScriptsOnly;
            return options;
        }

        public static void RecordSuccess(BuildPlayerOptions options)
        {
            if ((options.options & BuildOptions.BuildScriptsOnly) == 0)
            {
                string path = ReceiptPath(options);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Signature(options));
            }
        }

        private static string ReceiptPath(BuildPlayerOptions options) =>
            "Library/BuildIteration/"
            + Hash128.Compute(Path.GetFullPath(options.locationPathName))
            + ".json";

        private static string Signature(BuildPlayerOptions options) =>
            JsonUtility.ToJson(
                new Receipt
                {
                    project = Path.GetFullPath("."),
                    unity = Application.unityVersion,
                    target = options.target.ToString(),
                    pipeline = BuildRenderingPolicy.PipelineFor(options.target).ToString(),
                    options = (int)(options.options & ~BuildOptions.BuildScriptsOnly),
                    scenes = options.scenes ?? Array.Empty<string>(),
                    defines = options.extraScriptingDefines ?? Array.Empty<string>(),
                    platformDefines = PlayerSettings.GetScriptingDefineSymbols(
                        NamedBuildTarget.FromBuildTargetGroup(
                            BuildPipeline.GetBuildTargetGroup(options.target)
                        )
                    ),
                }
            );

        [Serializable]
        private sealed class Receipt
        {
            public string project;
            public string unity;
            public string target;
            public string pipeline;
            public int options;
            public string[] scenes;
            public string[] defines;
            public string platformDefines;
        }
    }
}
