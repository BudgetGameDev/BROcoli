using System.Collections.Generic;
using System.Linq;
using BudgetGameDev.Hub;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BudgetGameDev.Hub.Editor
{
    /// <summary>
    /// Keeps Build Settings agreeing with the installed games.
    /// </summary>
    /// <remarks>
    /// Scene lists are otherwise edited by hand, which breaks the moment a game
    /// package is added to or dropped from the manifest: a stale entry points at a
    /// scene that no longer exists, and a missing one makes a listed game fail to
    /// launch. Deriving the list from the registry removes that whole class of
    /// mistake, and running it before every build means CI cannot ship a stale one.
    /// </remarks>
    public sealed class HubBuildScenes : IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;

        public void OnPreprocessBuild(BuildReport report) => Sync(false);

        [MenuItem("Budget GameDev/Sync Build Scenes")]
        private static void SyncFromMenu() => Sync(true);

        /// <summary>
        /// Rebuilds the scene list: the launcher first, so a player build boots into
        /// it, then every scene each registered game declares.
        /// </summary>
        public static void Sync(bool log)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            var seen = new HashSet<string>();

            string launcher = FindLauncherScene();
            if (launcher == null)
                Debug.LogError(
                    $"[Hub] Launcher scene '{GameSession.LauncherSceneName}' is missing."
                );
            else if (seen.Add(launcher))
                scenes.Add(new EditorBuildSettingsScene(launcher, true));

            foreach (GameDefinition game in LoadDefinitions())
            foreach (string path in ScenePathsOf(game))
                if (seen.Add(path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));

            EditorBuildSettings.scenes = scenes.ToArray();
            if (log)
                Debug.Log(
                    $"[Hub] Build scenes synced ({scenes.Count}):\n  "
                        + string.Join("\n  ", scenes.Select(scene => scene.path))
                );
        }

        /// <summary>
        /// Reads definitions through the AssetDatabase rather than the runtime
        /// catalog, so the list is correct even before entering play mode.
        /// </summary>
        private static IEnumerable<GameDefinition> LoadDefinitions() =>
            AssetDatabase
                .FindAssets($"t:{nameof(GameDefinition)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameDefinition>)
                .Where(game => game != null)
                .OrderBy(game => game.SortOrder)
                .ThenBy(game => game.DisplayName);

        private static IEnumerable<string> ScenePathsOf(GameDefinition game)
        {
            if (game.MainMenuScene != null)
                yield return AssetDatabase.GetAssetPath(game.MainMenuScene);

            foreach (SceneAsset scene in game.AdditionalScenes)
                if (scene != null)
                    yield return AssetDatabase.GetAssetPath(scene);
        }

        private static string FindLauncherScene() =>
            AssetDatabase
                .FindAssets($"t:SceneAsset {GameSession.LauncherSceneName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path =>
                    System.IO.Path.GetFileNameWithoutExtension(path)
                    == GameSession.LauncherSceneName
                );
    }
}
