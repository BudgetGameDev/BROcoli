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
        public static bool Launch(GameDefinition game)
        {
            if (game == null || !game.IsPlayable)
            {
                Debug.LogError($"[GameSession] '{game?.name ?? "null"}' has no main menu scene.");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(game.MainMenuSceneName))
            {
                Debug.LogError(
                    $"[GameSession] Scene '{game.MainMenuSceneName}' is not in the build. "
                        + "Run Budget GameDev > Sync Build Scenes."
                );
                return false;
            }

            Active = game;
            LastPlayedId = game.Id;
            GameAudioSettings.Configure(game.MixerResourcePath, game.MainMenuSceneName);
            Time.timeScale = 1f;
            SceneManager.LoadScene(game.MainMenuSceneName);
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
