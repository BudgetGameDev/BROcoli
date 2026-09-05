using System;
using System.Linq;
using System.Reflection;
using BudgetGameDev.Hub.Editor;
using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Hub.Tests
{
    public sealed class BuildRenderingPolicyTests
    {
        [TestCase(null, RenderPipelineKind.Universal)]
        [TestCase("URP", RenderPipelineKind.Universal)]
        [TestCase("hdrp", RenderPipelineKind.HighDefinition)]
        public void ParsesPipelineSelection(string value, RenderPipelineKind expected) =>
            Assert.That(BuildRenderingPolicy.ParsePipeline(value), Is.EqualTo(expected));

        [TestCase("")]
        [TestCase("unknown")]
        public void RejectsInvalidPipeline(string value) =>
            Assert.Throws<BuildFailedException>(() => BuildRenderingPolicy.ParsePipeline(value));

        [TestCase(RenderPipelineKind.Universal, "URP")]
        [TestCase(RenderPipelineKind.HighDefinition, "HDRP")]
        public void KeepsLauncherAndCommonScenesWithOnlySelectedRenderingScenes(
            RenderPipelineKind pipeline,
            string suffix
        )
        {
            string[] scenes =
            {
                "Launcher.unity",
                "Dungeon_Common.unity",
                "Dungeon_URP.unity",
                "Dungeon_HDRP.unity",
                "HDRP_Menu.unity",
            };
            Assert.That(
                BuildRenderingPolicy.FilterScenes(scenes, pipeline),
                Is.EqualTo(
                    new[]
                    {
                        "Launcher.unity",
                        "Dungeon_Common.unity",
                        $"Dungeon_{suffix}.unity",
                        "HDRP_Menu.unity",
                    }
                )
            );
        }

        [TestCase(RenderPipelineKind.Universal)]
        [TestCase(RenderPipelineKind.HighDefinition)]
        public void PlayerDefinesExcludeHdrpOnlyForUniversal(RenderPipelineKind pipeline)
        {
            var previous = BuildRenderingPolicy.PipelineOverride;
            try
            {
                BuildRenderingPolicy.PipelineOverride = pipeline;
                var options = BuildRenderingPolicy.PrepareOptions(
                    new BuildPlayerOptions
                    {
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.Development,
                        scenes = new[]
                        {
                            "Launcher.unity",
                            "Dungeon_URP.unity",
                            "Dungeon_HDRP.unity",
                        },
                        extraScriptingDefines = new[]
                        {
                            "KEEP_ME",
                            BuildRenderingPolicy.UniversalPlayerDefine,
                        },
                    }
                );
                Assert.That(options.extraScriptingDefines, Does.Contain("KEEP_ME"));
                Assert.That(
                    options.extraScriptingDefines.Contains(
                        BuildRenderingPolicy.UniversalPlayerDefine
                    ),
                    Is.EqualTo(pipeline == RenderPipelineKind.Universal)
                );
                Assert.That(options.scenes.Length, Is.EqualTo(2));
            }
            finally
            {
                BuildRenderingPolicy.PipelineOverride = previous;
            }
        }

        [Test]
        public void UniversalPlayerOmitsHdrpAssembliesButPreservesSharedAndUniversal()
        {
            string[] assemblies =
            {
                "Library/Unity.RenderPipelines.HighDefinition.Runtime.dll",
                "Library/Unity.RenderPipelines.HighDefinition.Config.Runtime.dll",
                "Library/BudgetGameDev.Shared.Rendering.HighDefinition.dll",
                "Library/Unity.RenderPipelines.Core.Runtime.dll",
                "Library/Unity.RenderPipelines.Universal.Runtime.dll",
                "Library/BudgetGameDev.Shared.dll",
            };
            Assert.That(
                BuildRenderingPolicy.FilterAssemblies(assemblies, RenderPipelineKind.Universal),
                Is.EqualTo(assemblies.Skip(3).ToArray())
            );
            Assert.That(
                BuildRenderingPolicy.FilterAssemblies(
                    assemblies,
                    RenderPipelineKind.HighDefinition
                ),
                Is.EqualTo(assemblies)
            );
        }

        [Test]
        public void WebGlRemainsUniversalDuringHdrpSelection()
        {
            var previous = BuildRenderingPolicy.PipelineOverride;
            try
            {
                BuildRenderingPolicy.PipelineOverride = RenderPipelineKind.HighDefinition;
                Assert.That(
                    BuildRenderingPolicy.PipelineFor(BuildTarget.WebGL),
                    Is.EqualTo(RenderPipelineKind.Universal)
                );
            }
            finally
            {
                BuildRenderingPolicy.PipelineOverride = previous;
            }
        }

        [TestCase(BuildTarget.StandaloneWindows64, RenderPipelineKind.Universal)]
        [TestCase(BuildTarget.StandaloneOSX, RenderPipelineKind.Universal)]
        [TestCase(BuildTarget.StandaloneLinux64, RenderPipelineKind.Universal)]
        [TestCase(BuildTarget.WebGL, RenderPipelineKind.Universal)]
        [TestCase(BuildTarget.StandaloneWindows64, RenderPipelineKind.HighDefinition)]
        public void BuildKeepsCompatibleTiersAndRestoresSettings(
            BuildTarget target,
            RenderPipelineKind pipeline
        )
        {
            // The project build script is in the predefined Editor assembly, which an asmdef
            // cannot reference. Exercise its actual callbacks through reflection.
            Type builder = AppDomain
                .CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("NativePlayerBuildScript"))
                .First(type => type != null);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo configure = builder.GetMethod("ConfigureDefaultPipeline", flags);
            MethodInfo restore = builder.GetMethod("RestoreDefaultPipeline", flags);
            var previous = BuildRenderingPolicy.PipelineOverride;
            RenderPipelineAsset defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            string originalQuality = EditorJsonUtility.ToJson(QualitySettings.GetQualitySettings());
            try
            {
                BuildRenderingPolicy.PipelineOverride = pipeline;
                string assetType =
                    pipeline == RenderPipelineKind.Universal
                        ? "UniversalRenderPipelineAsset"
                        : "HDRenderPipelineAsset";
                // Repeated preprocessing must not overwrite the restoration snapshot.
                configure.Invoke(null, new object[] { target });
                configure.Invoke(null, new object[] { target });
                Assert.That(
                    GraphicsSettings.defaultRenderPipeline.GetType().Name,
                    Is.EqualTo(assetType)
                );
                string platform = NamedBuildTarget
                    .FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target))
                    .TargetName;
                int[] levels = QualitySettings.GetActiveQualityLevelsForPlatform(platform);
                Assert.That(levels, Is.Not.Empty);
                if (pipeline == RenderPipelineKind.HighDefinition)
                {
                    Assert.That(levels, Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
                    Assert.That(QualitySettings.names[9], Is.EqualTo("HDRP RT Ultra"));
                    Assert.That(
                        QualitySettings.GetRenderPipelineAssetAt(9),
                        Is.SameAs(GraphicsSettings.defaultRenderPipeline)
                    );
                }
                foreach (int level in levels)
                {
                    var asset = QualitySettings.GetRenderPipelineAssetAt(level);
                    Assert.That(asset == null || asset.GetType().Name == assetType, Is.True);
                }
                var serialized = new SerializedObject(QualitySettings.GetQualitySettings());
                var defaults = serialized.FindProperty("m_PerPlatformDefaultQuality");
                for (int index = 0; index < defaults.arraySize; index++)
                {
                    var entry = defaults.GetArrayElementAtIndex(index);
                    if (entry.FindPropertyRelative("first").stringValue == platform)
                    {
                        if (pipeline == RenderPipelineKind.HighDefinition)
                            Assert.That(
                                entry.FindPropertyRelative("second").intValue,
                                Is.EqualTo(9)
                            );
                        Assert.That(
                            levels,
                            Does.Contain(entry.FindPropertyRelative("second").intValue)
                        );
                    }
                }
            }
            finally
            {
                restore.Invoke(null, null);
                BuildRenderingPolicy.PipelineOverride = previous;
            }
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(defaultPipeline));
            Assert.That(
                EditorJsonUtility.ToJson(QualitySettings.GetQualitySettings()),
                Is.EqualTo(originalQuality)
            );
        }
    }
}
