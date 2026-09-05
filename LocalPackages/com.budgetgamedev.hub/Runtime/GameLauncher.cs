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

        internal void Start()
        {
            GameCatalog.Invalidate();
            BuildInterface();
            Populate();
            RestoreSelection();
            SuppressEventSystemNavigation();
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
