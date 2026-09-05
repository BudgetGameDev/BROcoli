using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// Brings in the rendering scene belonging to the active pipeline, and takes it away
    /// again when its level unloads.
    ///
    /// Put one of these in each <c>_Common</c> scene. It resolves the pipeline at load time,
    /// so the same common scene serves the web build and the Windows one without either
    /// carrying the other's volumes.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class RenderingSceneLoader : MonoBehaviour
    {
        /// <summary>
        /// How the loader reaches the scene manager. Injected so the resolution and the
        /// bookkeeping can be tested without a build settings list.
        /// </summary>
        internal static Func<string, bool> IsSceneInBuild = IsSceneInBuildDefault;
        internal static Action<string> LoadAdditive = name =>
            SceneManager.LoadScene(name, LoadSceneMode.Additive);
        internal static Func<RenderPipelineKind> ResolvePipeline = () =>
            RenderPipelineProbe.Current;

        private string loadedRenderingScene;

        private void Awake()
        {
            loadedRenderingScene = LoadRenderingScene(gameObject.scene.name);
        }

        private void OnDestroy()
        {
            UnloadRenderingScene(loadedRenderingScene);
            loadedRenderingScene = null;
        }

        internal static void UnloadRenderingScene(string loadedScene)
        {
            if (string.IsNullOrEmpty(loadedScene))
                return;

            Scene scene = SceneManager.GetSceneByName(loadedScene);
            if (scene.isLoaded && SceneManager.sceneCount > 1)
                SceneManager.UnloadSceneAsync(scene);
        }

        /// <summary>
        /// Loads the rendering scene for <paramref name="commonSceneName"/> and returns its
        /// name, or null when there is nothing to load.
        ///
        /// A missing rendering scene is reported rather than thrown: the pipeline still
        /// renders, using whatever the common scene and the pipeline's own default volume
        /// profile provide, which is a degraded picture rather than a dead build.
        /// </summary>
        internal static string LoadRenderingScene(string commonSceneName)
        {
            RenderPipelineKind pipeline = ResolvePipeline();
            string renderingScene = RenderingSceneNames.RenderingSceneFor(
                commonSceneName,
                pipeline
            );
            if (string.IsNullOrEmpty(renderingScene))
                return null;

            if (!IsSceneInBuild(renderingScene))
            {
                Debug.LogWarning(
                    $"[Rendering] '{renderingScene}' is not in the build, so "
                        + $"'{commonSceneName}' renders without its {pipeline} settings."
                );
                return null;
            }

            LoadAdditive(renderingScene);
            return renderingScene;
        }

        private static bool IsSceneInBuildDefault(string sceneName)
        {
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index);
                if (
                    string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(path),
                        sceneName,
                        StringComparison.Ordinal
                    )
                )
                    return true;
            }

            return false;
        }

        /// <summary>Restores the injected hooks, so one test cannot leak into the next.</summary>
        internal static void ResetHooks()
        {
            IsSceneInBuild = IsSceneInBuildDefault;
            LoadAdditive = name => SceneManager.LoadScene(name, LoadSceneMode.Additive);
            ResolvePipeline = () => RenderPipelineProbe.Current;
        }
    }
}
