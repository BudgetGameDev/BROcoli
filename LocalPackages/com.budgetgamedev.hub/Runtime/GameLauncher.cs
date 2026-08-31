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
        private ScrollRect gameListScroll;
        private int selectedIndex = -1;

        /// <summary>One row in the list, kept so selection can restyle it.</summary>
        internal sealed class GameEntry
        {
            public GameDefinition Game;
            public Button Button;
            public Image Background;
            public Text Label;
        }

        /// <summary>The built rows, in display order. Read by the edit-mode tests.</summary>
        internal IReadOnlyList<GameEntry> Entries => entries;

        /// <summary>Highlighted row, or -1 while nothing is selected.</summary>
        internal int SelectedIndex => selectedIndex;

        internal Button SelectButton => selectButton;

        internal Text EmptyLabel => emptyLabel;

        internal ScrollRect ListScroll => gameListScroll;

        /// <summary>
        /// Cleared at startup so a configured game boots once per run. Statics
        /// survive "Enter Play Mode without domain reload".
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAutoBoot() => autoBootUsed = false;

        private static bool autoBootUsed;

        internal void Start()
        {
            GameCatalog.Invalidate();
            CompleteStart(TryBootConfiguredGame());
        }

        internal void CompleteStart(bool gameBooted)
        {
            if (gameBooted)
                return;

            BuildInterface();
            Populate();
            RestoreSelection();
            SuppressEventSystemNavigation();
        }

        private bool TryBootConfiguredGame() =>
            TryBootConfiguredGame(LauncherConfig.Load().StartupScene);

        /// <summary>
        /// Opens the configured startup scene, if there is a usable one, and only
        /// the first time the launcher opens in this run.
        /// </summary>
        /// <remarks>
        /// The once-per-run limit is what keeps the picker reachable: a player who
        /// leaves a booted game through "all games" would otherwise be thrown
        /// straight back into it and could never see the list.
        ///
        /// The scene name is a parameter rather than read here, so the decision can
        /// be checked against a known value the way <see cref="LauncherStartup"/>
        /// takes build membership as a parameter.
        /// </remarks>
        internal static bool TryBootConfiguredGame(string startupScene)
        {
            if (autoBootUsed)
                return false;

            autoBootUsed = true;

            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                startupScene,
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
            GameSession.OpenScene(plan.SceneName);
            return true;
        }

        internal void Populate()
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
        internal void RestoreSelection()
        {
            if (entries.Count == 0)
                return;

            string lastPlayed = GameSession.LastPlayedId;
            int index = entries.FindIndex(entry =>
                entry.Game.IsPlayable
                && string.Equals(
                    entry.Game.Id,
                    lastPlayed,
                    System.StringComparison.OrdinalIgnoreCase
                )
            );
            if (index < 0)
                index = entries.FindIndex(entry => entry.Game.IsPlayable);

            Select(index >= 0 ? index : 0);
        }

        internal void Select(int index)
        {
            selectedIndex = index;
            for (int i = 0; i < entries.Count; i++)
                ApplyRowStyle(entries[i], i == index);

            selectButton.interactable = index >= 0 && entries[index].Game.IsPlayable;
            KeepSelectedRowVisible();
        }

        /// <summary>Called by the Select button.</summary>
        public void LaunchSelected()
        {
            if (
                selectedIndex < 0
                || selectedIndex >= entries.Count
                || !entries[selectedIndex].Game.IsPlayable
            )
                return;

            GameSession.Launch(entries[selectedIndex].Game);
        }
    }
}
