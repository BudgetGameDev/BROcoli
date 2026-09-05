using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class RenderingSceneLoaderTests
    {
        [TearDown]
        public void RestoreHooks() => RenderingSceneLoader.ResetHooks();

        [Test]
        public void UnknownPipelineHasNoRenderingScene()
        {
            RenderingSceneLoader.ResolvePipeline = () => RenderPipelineKind.Unknown;

            Assert.That(RenderingSceneLoader.LoadRenderingScene("Dungeon_Common"), Is.Null);
        }

        [Test]
        public void MissingRenderingSceneIsReportedAndNotLoaded()
        {
            RenderingSceneLoader.ResolvePipeline = () => RenderPipelineKind.Universal;
            RenderingSceneLoader.IsSceneInBuild = _ => false;
            bool loaded = false;
            RenderingSceneLoader.LoadAdditive = _ => loaded = true;
            LogAssert.Expect(
                LogType.Warning,
                "[Rendering] 'Dungeon_URP' is not in the build, so 'Dungeon_Common' renders without its Universal settings."
            );

            Assert.That(RenderingSceneLoader.LoadRenderingScene("Dungeon_Common"), Is.Null);
            Assert.That(loaded, Is.False);
        }

        [Test]
        public void AvailableRenderingSceneLoadsAdditively()
        {
            string loaded = null;
            RenderingSceneLoader.ResolvePipeline = () => RenderPipelineKind.HighDefinition;
            RenderingSceneLoader.IsSceneInBuild = _ => true;
            RenderingSceneLoader.LoadAdditive = name => loaded = name;

            Assert.That(
                RenderingSceneLoader.LoadRenderingScene("Dungeon_Common"),
                Is.EqualTo("Dungeon_HDRP")
            );
            Assert.That(loaded, Is.EqualTo("Dungeon_HDRP"));
        }

        [Test]
        public void DefaultBuildLookupRejectsAnUnknownSceneAndHooksCanBeRestored()
        {
            RenderingSceneLoader.ResetHooks();
            RenderingSceneLoader.ResolvePipeline = () => RenderPipelineKind.Universal;
            LogAssert.Expect(
                LogType.Warning,
                "[Rendering] 'DefinitelyMissing_URP' is not in the build, so 'DefinitelyMissing_Common' renders without its Universal settings."
            );

            Assert.That(
                RenderingSceneLoader.LoadRenderingScene("DefinitelyMissing_Common"),
                Is.Null
            );
        }

        [Test]
        public void LoaderLifecycleStoresAnEmptyResolutionWithoutThrowingOnDestroy()
        {
            RenderingSceneLoader.ResolvePipeline = () => RenderPipelineKind.Unknown;
            RenderingSceneLoader.UnloadRenderingScene(null);
            GameObject host = new("Rendering scene loader lifecycle");
            host.AddComponent<RenderingSceneLoader>();

            Object.DestroyImmediate(host);

            Assert.That(host == null, Is.True);
        }

        [Test]
        public void LoaderLifecycleUnloadsItsAdditiveRenderingScene()
        {
            const string BaseScenePath = "Assets/__CoverageBase.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), BaseScenePath);
            try
            {
                Scene rendering = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive
                );
                rendering.name = "Coverage_Rendering";
                GameObject host = new("Rendering scene loader unload lifecycle");
                var loader = host.AddComponent<RenderingSceneLoader>();
                typeof(RenderingSceneLoader)
                    .GetField(
                        "loadedRenderingScene",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .SetValue(loader, rendering.name);

                RenderingSceneLoader.UnloadRenderingScene(rendering.name);
                typeof(RenderingSceneLoader)
                    .GetField(
                        "loadedRenderingScene",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .SetValue(loader, null);
                Object.DestroyImmediate(host);

                Assert.That(host == null, Is.True);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(BaseScenePath);
            }
        }
    }
}
