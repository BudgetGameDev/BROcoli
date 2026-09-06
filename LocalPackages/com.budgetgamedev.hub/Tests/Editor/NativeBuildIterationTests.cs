using System;
using System.IO;
using BudgetGameDev.Hub.Editor;
using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BudgetGameDev.Hub.Tests
{
    public sealed class NativeBuildIterationTests
    {
        [Test]
        public void ScriptsOnlyRequiresMatchingFullBuildAndPreservesDevelopmentMode()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string executable = Path.Combine(directory, "BROcoli.exe");
            string receipt = "Library/BuildIteration/" + Hash128.Compute(executable) + ".json";
            var previous = BuildRenderingPolicy.PipelineOverride;
            try
            {
                BuildRenderingPolicy.PipelineOverride = RenderPipelineKind.HighDefinition;
                var options = new BuildPlayerOptions
                {
                    target = BuildTarget.StandaloneWindows64,
                    locationPathName = executable,
                    options = BuildOptions.Development,
                    scenes = new[] { "Dungeon_HDRP.unity" },
                    extraScriptingDefines = new[] { "KEEP_ME" },
                };
                Assert.That(
                    NativeBuildIteration.Prepare(options, false).options,
                    Is.EqualTo(BuildOptions.Development)
                );
                Assert.Throws<BuildFailedException>(() =>
                    NativeBuildIteration.Prepare(options, true)
                );
                File.WriteAllText(executable, "test player");
                Assert.Throws<BuildFailedException>(() =>
                    NativeBuildIteration.Prepare(options, true)
                );
                NativeBuildIteration.RecordSuccess(options);
                var scripts = NativeBuildIteration.Prepare(options, true);
                Assert.That(
                    scripts.options,
                    Is.EqualTo(BuildOptions.Development | BuildOptions.BuildScriptsOnly)
                );
                BuildRenderingPolicy.PipelineOverride = RenderPipelineKind.Universal;
                Assert.Throws<BuildFailedException>(() =>
                    NativeBuildIteration.Prepare(options, true)
                );
                BuildRenderingPolicy.PipelineOverride = RenderPipelineKind.HighDefinition;
                options.scenes = new[] { "Changed_HDRP.unity" };
                Assert.Throws<BuildFailedException>(() =>
                    NativeBuildIteration.Prepare(options, true)
                );
                options.scenes = new[] { "Dungeon_HDRP.unity" };
                options.options = BuildOptions.None;
                Assert.Throws<BuildFailedException>(() =>
                    NativeBuildIteration.Prepare(options, true)
                );
                options.options = BuildOptions.Development;
                options.extraScriptingDefines = new[] { "DIFFERENT" };
                Assert.Throws<BuildFailedException>(() =>
                    NativeBuildIteration.Prepare(options, true)
                );
                options.extraScriptingDefines = new[] { "KEEP_ME" };
                NativeBuildIteration.Prepare(options, false);
                Assert.Throws<BuildFailedException>(() =>
                    NativeBuildIteration.Prepare(options, true)
                );
            }
            finally
            {
                BuildRenderingPolicy.PipelineOverride = previous;
                File.Delete(receipt);
                Directory.Delete(directory, true);
            }
        }
    }
}
