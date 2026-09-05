using System;
using System.IO;
using System.Linq;
using BudgetGameDev.Shared.Rendering;
using UnityEditor;
using UnityEditor.Build;

namespace BudgetGameDev.Hub.Editor
{
    /// <summary>One rendering pipeline, scene set, and runtime front end per player.</summary>
    public static class BuildRenderingPolicy
    {
        public const string UniversalPlayerDefine = "BROCOLI_URP_PLAYER";
        public static RenderPipelineKind? PipelineOverride { get; set; }

        public static RenderPipelineKind PipelineFor(BuildTarget target)
        {
            // Web players always use Universal, even during a native HDRP build session.
            if (target == BuildTarget.WebGL)
                return RenderPipelineKind.Universal;
            if (PipelineOverride.HasValue)
                return PipelineOverride.Value;

            string[] args = Environment.GetCommandLineArgs();
            int index = Array.FindIndex(
                args,
                arg => string.Equals(arg, "-renderPipeline", StringComparison.OrdinalIgnoreCase)
            );
            return ParsePipeline(
                index < 0 ? null
                : index + 1 < args.Length ? args[index + 1]
                : ""
            );
        }

        public static RenderPipelineKind ParsePipeline(string value) =>
            value?.ToLowerInvariant() switch
            {
                null or "urp" => RenderPipelineKind.Universal,
                "hdrp" => RenderPipelineKind.HighDefinition,
                _ => throw new BuildFailedException("-renderPipeline must be urp or hdrp."),
            };

        public static string[] FilterScenes(string[] scenes, RenderPipelineKind pipeline)
        {
            string excludedSuffix =
                pipeline == RenderPipelineKind.Universal
                    ? RenderingSceneNames.HighDefinitionSuffix
                    : RenderingSceneNames.UniversalSuffix;
            return scenes
                .Where(path =>
                    !Path.GetFileNameWithoutExtension(path)
                        .EndsWith(excludedSuffix, StringComparison.OrdinalIgnoreCase)
                )
                .ToArray();
        }

        /// <summary>Must run before BuildPlayer captures scenes and compiles player scripts.</summary>
        public static BuildPlayerOptions PrepareOptions(BuildPlayerOptions options)
        {
            options = BuildContentPolicy.PrepareOptions(options);
            RenderPipelineKind pipeline = PipelineFor(options.target);
            options.scenes = FilterScenes(options.scenes ?? Array.Empty<string>(), pipeline);
            options.extraScriptingDefines = (options.extraScriptingDefines ?? Array.Empty<string>())
                .Where(define => define != UniversalPlayerDefine)
                .Concat(
                    pipeline == RenderPipelineKind.Universal
                        ? new[] { UniversalPlayerDefine }
                        : Array.Empty<string>()
                )
                .Distinct()
                .ToArray();
            return options;
        }

        public static string[] FilterAssemblies(string[] assemblies, RenderPipelineKind pipeline) =>
            pipeline == RenderPipelineKind.Universal
                ? assemblies
                    .Where(path =>
                        Path.GetFileNameWithoutExtension(path)
                            is not (
                                "Unity.RenderPipelines.HighDefinition.Runtime"
                                or "Unity.RenderPipelines.HighDefinition.Config.Runtime"
                                or "BudgetGameDev.Shared.Rendering.HighDefinition"
                            )
                    )
                    .ToArray()
                : assemblies;

        [InitializeOnLoadMethod]
        private static void RegisterBuildWindowHandler() =>
            BuildPlayerWindow.RegisterBuildPlayerHandler(options =>
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(PrepareOptions(options))
            );
    }

    /// <summary>Installed HDRP packages compile in the Editor, but must not ship in URP players.</summary>
    public sealed class BuildRenderingAssemblyFilter : IFilterBuildAssemblies
    {
        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies) =>
            BuildRenderingPolicy.FilterAssemblies(
                assemblies,
                BuildRenderingPolicy.PipelineFor(EditorUserBuildSettings.activeBuildTarget)
            );
    }
}
