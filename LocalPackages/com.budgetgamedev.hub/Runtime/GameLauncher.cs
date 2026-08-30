using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Hub
{
    /// <summary>
    /// The launcher screen: a scrollable list of every registered game, and a
    /// Select button that hands control to the highlighted one.
    /// </summary>
    /// <remarks>
    /// Deliberately brand-neutral. It shows no title art, no game-specific colour,
    /// and nothing that would read as one particular game's front end — each game
    /// brands itself in its own main menu, which is what Select opens. The UI is
    /// built in code so the scene stays a bootstrap and the layout can adapt to
    /// however many games happen to be installed.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed partial class GameLauncher : MonoBehaviour
    {
        private readonly List<GameEntry> entries = new();

        private Button selectButton;
        private Text emptyLabel;
        private RectTransform listContent;
        private int selectedIndex = -1;

        /// <summary>One row in the list, kept so selection can restyle it.</summary>
        private sealed class GameEntry
        {
            public GameDefinition Game;
            public Button Button;
            public Image Background;
            public Text Label;
        }

        /// <summary>
        /// Cleared at startup so a configured game boots once per run. Statics
        /// survive "Enter Play Mode without domain reload".
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAutoBoot() => autoBootUsed = false;

        private static bool autoBootUsed;

        private void Start()
        {
            GameCatalog.Invalidate();
            if (TryBootConfiguredGame())
                return;

            BuildInterface();
            Populate();
            RestoreSelection();
        }

        /// <summary>
        /// Opens the configured startup scene, if there is a usable one, and only
        /// the first time the launcher opens in this run.
        /// </summary>
        /// <remarks>
        /// The once-per-run limit is what keeps the picker reachable: a player who
        /// leaves a booted game through "all games" would otherwise be thrown
        /// straight back into it and could never see the list.
        /// </remarks>
        private bool TryBootConfiguredGame()
        {
            if (autoBootUsed)
                return false;

            autoBootUsed = true;

            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                LauncherConfig.Load().StartupScene,
                GameCatalog.All,
                LauncherStartup.IsSceneInBuild
            );
            if (plan.ShowsPicker)
                return false;

            if (plan.Game != null)
                return GameSession.Launch(plan.Game, plan.SceneName);

            // A configured scene that no registered game claims still opens; it
            // just gets no per-game setup, because there is none to apply.
            Debug.Log($"[Launcher] Opening configured startup scene '{plan.SceneName}'.");
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(plan.SceneName);
            return true;
        }

        private void Populate()
        {
            IReadOnlyList<GameDefinition> games = GameCatalog.All;
            for (int index = 0; index < games.Count; index++)
                entries.Add(CreateEntry(games[index], index));

            bool any = entries.Count > 0;
            emptyLabel.gameObject.SetActive(!any);
            selectButton.gameObject.SetActive(any);
        }

        /// <summary>
        /// Preselects the game played last, so returning to the launcher lands on
        /// the row the player most likely wants again.
        /// </summary>
        private void RestoreSelection()
        {
            if (entries.Count == 0)
                return;

            string lastPlayed = GameSession.LastPlayedId;
            int index = entries.FindIndex(entry =>
                string.Equals(entry.Game.Id, lastPlayed, System.StringComparison.OrdinalIgnoreCase)
            );
            Select(index >= 0 ? index : 0);
        }

        private void Select(int index)
        {
            selectedIndex = index;
            for (int i = 0; i < entries.Count; i++)
                ApplyRowStyle(entries[i], i == index);

            selectButton.interactable = index >= 0 && entries[index].Game.IsPlayable;
        }

        /// <summary>Called by the Select button.</summary>
        public void LaunchSelected()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
                return;

            GameSession.Launch(entries[selectedIndex].Game);
        }
    }
}
