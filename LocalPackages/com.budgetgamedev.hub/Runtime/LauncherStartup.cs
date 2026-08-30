using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Hub
{
    /// <summary>
    /// Decides whether the launcher opens its picker or boots straight into a
    /// configured scene.
    /// </summary>
    public static class LauncherStartup
    {
        /// <summary>What the launcher should do when it opens.</summary>
        public readonly struct Plan
        {
            /// <summary>Scene to open, or null to show the picker.</summary>
            public string SceneName { get; }

            /// <summary>
            /// The game owning <see cref="SceneName"/>, when one declares it. Its
            /// per-game configuration is applied before the scene loads, so a
            /// booted game is set up exactly as if it had been picked by hand.
            /// </summary>
            public GameDefinition Game { get; }

            public bool ShowsPicker => SceneName == null;

            private Plan(string sceneName, GameDefinition game)
            {
                SceneName = sceneName;
                Game = game;
            }

            public static Plan Picker => new(null, null);

            public static Plan Boot(string sceneName, GameDefinition game) => new(sceneName, game);
        }

        /// <summary>
        /// Resolves the configured startup scene against what is actually in the
        /// build.
        /// </summary>
        /// <remarks>
        /// Anything unusable falls back to the picker rather than failing: a config
        /// naming a scene that was renamed, or that belongs to a game package no
        /// longer in the manifest, must not leave the player staring at a build
        /// that cannot open. The bad value is reported, not obeyed.
        /// </remarks>
        /// <param name="startupScene">Configured scene name; may be null or empty.</param>
        /// <param name="games">Registered games, used to find the scene's owner.</param>
        /// <param name="isInBuild">Whether a scene name can actually be loaded.</param>
        public static Plan Resolve(
            string startupScene,
            IReadOnlyList<GameDefinition> games,
            Func<string, bool> isInBuild
        )
        {
            if (string.IsNullOrWhiteSpace(startupScene))
                return Plan.Picker;

            string scene = startupScene.Trim();
            if (isInBuild == null || !isInBuild(scene))
            {
                Debug.LogWarning(
                    $"[Launcher] Startup scene '{scene}' is not in the build; "
                        + "showing the game list instead."
                );
                return Plan.Picker;
            }

            return Plan.Boot(scene, OwnerOf(scene, games));
        }

        /// <summary>
        /// Whether a scene name appears in the build settings.
        /// </summary>
        /// <remarks>
        /// Enumerating the build list rather than calling
        /// <c>Application.CanStreamedLevelBeLoaded</c>, which answers "can this
        /// load right now" and so reports false for every scene outside play mode.
        /// Build-list membership is both the question actually being asked and
        /// checkable from editor tooling and tests.
        /// </remarks>
        public static bool IsSceneInBuild(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            string wanted = sceneName.Trim();
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index);
                if (
                    string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(path),
                        wanted,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The game that declares this scene, or null when none does. A scene with
        /// no owner still boots; it just gets no per-game setup.
        /// </summary>
        private static GameDefinition OwnerOf(string scene, IReadOnlyList<GameDefinition> games) =>
            games?.FirstOrDefault(game =>
                game != null
                && game.SceneNames != null
                && game.SceneNames.Any(name =>
                    string.Equals(name, scene, StringComparison.OrdinalIgnoreCase)
                )
            );
    }
}
