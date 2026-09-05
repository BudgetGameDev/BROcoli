using System.Collections;
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
        [UnityTearDown]
        public IEnumerator RestoreHooks()
        {
            RenderingSceneLoader.ResetHooks();
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

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

        [UnityTest]
        public IEnumerator LoaderLifecycleUnloadsItsAdditiveRenderingScene()
        {
            // SceneManager's loading/unloading API is a runtime operation. Running its
            // lifecycle in Play Mode avoids saving/replacing unrelated Editor scenes.
            yield return new EnterPlayMode();
            Scene original = SceneManager.GetActiveScene();
            Scene rendering = SceneManager.CreateScene("Coverage_Rendering");
            SceneManager.SetActiveScene(original);
            GameObject host = new("Rendering scene loader unload lifecycle");
            var loader = host.AddComponent<RenderingSceneLoader>();
            typeof(RenderingSceneLoader)
                .GetField(
                    "loadedRenderingScene",
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )
                .SetValue(loader, rendering.name);

            Object.DestroyImmediate(host);
            for (int frame = 0; frame < 30 && rendering.isLoaded; frame++)
                yield return null;
            Assert.That(host == null, Is.True);
            Assert.That(
                rendering.isLoaded,
                Is.False,
                "Destroying the loader must unload its additive rendering scene."
            );
            Assert.That(original.isLoaded, Is.True, "The common scene must remain loaded.");
        }
    }
}
