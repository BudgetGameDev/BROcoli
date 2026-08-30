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

        private const string LastPlayedKey = "Hub.LastPlayedGameId";

        /// <summary>The running game, or null while the launcher is in front.</summary>
        public static GameDefinition Active { get; private set; }

        /// <summary>
        /// Clears session state at startup. Statics survive "Enter Play Mode
        /// without domain reload", so without this a second play session would
        /// begin believing a game from the previous one is still running.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState() => Active = null;

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

            if (!LauncherStartup.IsSceneInBuild(scene))
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
            return true;
        }

        /// <summary>Leaves the running game and shows the launcher again.</summary>
        public static void ReturnToLauncher()
        {
            Active = null;
            GameAudioSettings.Configure(null, null);
            Time.timeScale = 1f;
            SceneManager.LoadScene(LauncherSceneName);
        }
    }
}
