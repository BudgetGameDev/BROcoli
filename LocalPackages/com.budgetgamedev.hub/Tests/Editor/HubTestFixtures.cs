using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// Builds registry entries in memory, so a test can describe a set of installed
    /// games that no package actually ships.
    /// </summary>
    /// <remarks>
    /// Values go into the backing fields directly. Going through SerializedObject
    /// would fire OnValidate, which rebuilds sceneNames from the editor-only
    /// SceneAsset references and would wipe exactly the values under test.
    /// </remarks>
    public sealed class HubTestGames
    {
        private readonly List<GameDefinition> games = new();

        /// <summary>Every game built so far, in the order it was built.</summary>
        public GameDefinition[] All => games.ToArray();

        /// <summary>
        /// A game whose first scene is its main menu. With no scenes at all the
        /// entry is listed but unplayable, which is the other case worth building.
        /// </summary>
        public GameDefinition Add(string id, params string[] scenes) =>
            Named(id, string.Empty, scenes);

        public GameDefinition Named(string id, string displayName, params string[] scenes)
        {
            var game = ScriptableObject.CreateInstance<GameDefinition>();
            game.name = id;
            Set(game, "id", id);
            Set(game, "displayName", displayName);
            Set(game, "mainMenuSceneName", scenes.Length > 0 ? scenes[0] : string.Empty);
            Set(game, "sceneNames", scenes);
            games.Add(game);
            return game;
        }

        public static void Set(GameDefinition game, string field, object value) =>
            typeof(GameDefinition)
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(game, value);

        public void DestroyAll()
        {
            foreach (GameDefinition game in games)
                Object.DestroyImmediate(game);

            games.Clear();
        }
    }

    /// <summary>
    /// The scenes this build can actually load. GameSession refuses anything else,
    /// so tests that need a real load target ask here rather than naming a scene a
    /// package could later rename or drop.
    /// </summary>
    internal static class HubTestScenes
    {
        /// <summary>The first scene in the build, or null when the build has none.</summary>
        public static string First() => At(0);

        /// <summary>The scene at a build index, or null when the build is shorter.</summary>
        public static string At(int index)
        {
            if (index < 0 || index >= SceneManager.sceneCountInBuildSettings)
                return null;

            string path = SceneUtility.GetScenePathByBuildIndex(index);
            return string.IsNullOrEmpty(path)
                ? null
                : System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }
}
