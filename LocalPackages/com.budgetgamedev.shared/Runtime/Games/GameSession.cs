using System;
using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Hub
{
    /// <summary>
    /// Which game is running, and the two transitions between the launcher and a
    /// game. Games call <see cref="ReturnToLauncher"/> from their own menus; they
    /// never need to know the launcher scene's name.
    /// </summary>
    public static class GameSession
    {
        /// <summary>Scene name of the launcher shipped by this package.</summary>
        public const string LauncherSceneName = "GameLauncher";

        internal const string LastPlayedKey = "Hub.LastPlayedGameId";

        /// <summary>
        /// How a scene is actually opened. A scene load only exists in play mode, so
        /// this is the seam edit-mode tests replace to observe the decision without
        /// one, the way the scene resolver takes build membership
        /// as a parameter. Nothing in the shipped game assigns it.
        /// </summary>
        internal static Action<string> SceneLoader = SceneManager.LoadScene;

        /// <summary>The running game, or null while the launcher is in front.</summary>
        public static GameDefinition Active { get; private set; }

        /// <summary>
        /// Clears session state at startup. Statics survive "Enter Play Mode
        /// without domain reload", so without this a second play session would
        /// begin believing a game from the previous one is still running.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetSessionState()
        {
            Active = null;
            SceneLoader = SceneManager.LoadScene;
        }

        /// <summary>Id the launcher should preselect: whatever was played last.</summary>
        public static string LastPlayedId
        {
            get => PlayerPrefs.GetString(LastPlayedKey, string.Empty);
            private set
            {
                PlayerPrefs.SetString(LastPlayedKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Hands control to a game by loading its own main menu, after applying the
        /// per-game configuration that shared systems read.
        /// </summary>
        public static bool Launch(GameDefinition game) => Launch(game, null);

        /// <summary>
        /// Hands control to a game at one of its own scenes rather than its main
        /// menu. A configured startup scene uses this, so a build that boots
        /// straight into gameplay still gets the same per-game setup that picking
        /// the game by hand would have applied.
        /// </summary>
        /// <param name="sceneName">Scene to open; null or empty means the main menu.</param>
        public static bool Launch(GameDefinition game, string sceneName)
        {
            if (game == null)
            {
                Debug.LogError("[GameSession] Cannot launch a null game.");
                return false;
            }

            string scene = string.IsNullOrWhiteSpace(sceneName)
                ? game.MainMenuSceneName
                : sceneName.Trim();

            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogError($"[GameSession] '{game.name}' has no main menu scene.");
                return false;
            }

            if (!IsSceneInBuild(scene))
            {
                Debug.LogError(
                    $"[GameSession] Scene '{scene}' is not in the build. "
                        + "Run Budget GameDev > Sync Build Scenes."
                );
                return false;
            }

            Active = game;
            LastPlayedId = game.Id;
            GameAudioSettings.Configure(game.MixerResourcePath, game.MainMenuSceneName);
            OpenScene(scene);
            return true;
        }

        public static bool LauncherAvailable => IsSceneInBuild(LauncherSceneName);

        public static bool IsSceneInBuild(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
                if (
                    string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(
                            SceneUtility.GetScenePathByBuildIndex(index)
                        ),
                        sceneName.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ConfigureStandaloneGame()
        {
            if (LauncherAvailable)
                return;
            GameCatalog.Invalidate();
            if (GameCatalog.All.Count != 1)
                return;
            Active = GameCatalog.All[0];
            GameAudioSettings.Configure(Active.MixerResourcePath, Active.MainMenuSceneName);
        }

        /// <summary>Leaves the running game and shows the launcher again.</summary>
        public static void ReturnToLauncher()
        {
            if (!LauncherAvailable)
                return;
            Active = null;
            GameAudioSettings.Configure(null, null);
            OpenScene(LauncherSceneName);
        }

        /// <summary>
        /// Opens a scene at normal speed. Every transition resets the time scale,
        /// because a game paused when it was left would otherwise hand a frozen
        /// clock to whatever opens next.
        /// </summary>
        internal static void OpenScene(string sceneName)
        {
            Time.timeScale = 1f;
            SceneLoader(sceneName);
        }
    }
}
