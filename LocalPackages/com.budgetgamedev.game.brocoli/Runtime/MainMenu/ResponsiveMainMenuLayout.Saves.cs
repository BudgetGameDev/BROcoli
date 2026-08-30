using System;
using System.Collections.Generic;
using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// The save manager: every run the player is part-way through, newest first,
    /// with what it reached and when it was last played. A run is picked out of the
    /// list first and then played or deleted, so neither happens on the press that
    /// was only meant to look at it.
    /// </summary>
    public sealed partial class ResponsiveMainMenuLayout
    {
        private const string DeleteLabel = "DELETE";
        private const string ConfirmDeleteLabel = "SURE?";

        // Three states a row can be in, and none of them may be mistaken for another:
        // sitting there, under the pointer, and picked.
        private static readonly Color RowIdle = Hex("#212D26");
        private static readonly Color RowHover = Hex("#35473D");
        private static readonly Color RowPressed = Hex("#1A241E");

        private sealed class SaveRow
        {
            public RectTransform Root;
            public Button Select;
            public TMP_Text Headline;
            public TMP_Text Detail;
            public Outline Outline;

            /// <summary>Which save slot this row is showing; -1 while unused.</summary>
            public int Slot = -1;
        }

        private readonly List<SaveRow> saveRows = new();

        /// <summary>What the panel walks through top to bottom, one entry per line.</summary>
        private readonly List<Selectable> savesSelectables = new();

        private Button savesButton;
        private RectTransform savesPanel;
        private RectTransform savesViewport;
        private RectTransform savesContent;
        private ScrollRect savesScroll;
        private TMP_Text savesTitle;
        private TMP_Text savesHint;
        private TMP_Text savesEmpty;
        private Button playSaveButton;
        private Button deleteSaveButton;
        private TMP_Text deleteSaveLabel;
        private Button newRunButton;
        private Button newTouchRunButton;
        private Button backSavesButton;
        private Button[] savesActionButtons;

        private int visibleSaveCount;

        /// <summary>The line the player is on: a row, or one of the actions below.</summary>
        private int savesFocus;

        /// <summary>0 is Play, 1 is Delete, on the line the two share.</summary>
        private int savesColumn;

        /// <summary>The row Play and Delete act on; -1 when there is nothing saved.</summary>
        private int selectedRow = -1;

        /// <summary>Delete is armed and a second press will go through with it.</summary>
        private bool deleteArmed;
        private float lastSavesNavTime;

        public static bool SavesOpen { get; private set; }

        /// <summary>
        /// Builds the panel and adopts the scene's two play buttons into it: starting
        /// a run is one of the things the save manager does, not a separate route
        /// that could hand out an eleventh slot.
        /// </summary>
        private void BuildSavesPresentation(Button newRun, Button newTouchRun)
        {
            savesButton = CreateButton("SavesButton", card, "SAVES");
            savesButton.onClick.AddListener(OpenSaves);

            savesPanel = CreateRect("SavesPanel", card);
            savesTitle = CreateText("SavesTitle", savesPanel, "SAVED RUNS", 22f, OnSurface);
            savesHint = CreateText("SavesHint", savesPanel, string.Empty, 14f, OnSurfaceMuted);

            savesViewport = CreatePanel("SavesViewport", savesPanel, SurfaceVariant);
            savesViewport.GetComponent<Image>().raycastTarget = true;
            savesViewport.gameObject.AddComponent<RectMask2D>();
            savesScroll = savesViewport.gameObject.AddComponent<ScrollRect>();
            savesScroll.viewport = savesViewport;
            savesScroll.horizontal = false;
            savesScroll.vertical = true;
            savesScroll.movementType = ScrollRect.MovementType.Clamped;
            savesScroll.scrollSensitivity = 42f;

            savesContent = CreateRect("SavesContent", savesViewport);
            savesScroll.content = savesContent;

            savesEmpty = CreateText(
                "SavesEmpty",
                savesViewport,
                "NO SAVED RUNS YET",
                16f,
                OnSurfaceMuted
            );
            savesEmpty.raycastTarget = false;

            for (int index = 0; index < BrocoliSaveSystem.MaxSaves; index++)
                saveRows.Add(CreateSaveRow(index));

            playSaveButton = CreateButton("PlaySaveButton", savesPanel, "PLAY");
            playSaveButton.onClick.AddListener(PlaySelectedRun);
            RegisterSavesPointer(playSaveButton, () => FocusAction(0));

            deleteSaveButton = CreateButton("DeleteSaveButton", savesPanel, DeleteLabel);
            deleteSaveButton.onClick.AddListener(DeleteSelectedRun);
            deleteSaveLabel = deleteSaveButton.GetComponentInChildren<TMP_Text>(true);
            RegisterSavesPointer(deleteSaveButton, () => FocusAction(1));

            newRunButton = newRun;
            newTouchRunButton = newTouchRun;
            AdoptNewRunButton(newRunButton);
            AdoptNewRunButton(newTouchRunButton);

            backSavesButton = CreateButton("BackFromSavesButton", savesPanel, "BACK");
            backSavesButton.onClick.AddListener(CloseSaves);
            RegisterSavesPointer(backSavesButton, () => FocusButton(backSavesButton));

            savesActionButtons = new[]
            {
                playSaveButton,
                deleteSaveButton,
                newRunButton,
                newTouchRunButton,
                backSavesButton,
            };
            savesPanel.gameObject.SetActive(false);
        }

        private void AdoptNewRunButton(Button button)
        {
            if (button == null)
                return;

            button.transform.SetParent(savesPanel, false);
            RegisterSavesPointer(button, () => FocusButton(button));
        }

        private SaveRow CreateSaveRow(int index)
        {
            RectTransform root = CreateRect($"SaveRow{index}", savesContent);
            Button select = CreateButton($"SaveRow{index}Button", root, string.Empty);
            StyleButton(select, false, materialFont);

            TMP_Text headline = select.GetComponentInChildren<TMP_Text>(true);
            headline.alignment = TextAlignmentOptions.BottomLeft;
            headline.margin = new Vector4(18f, 0f, 18f, 0f);
            headline.characterSpacing = 1f;

            TMP_Text detail = CreateText(
                $"SaveDetail{index}",
                select.GetComponent<RectTransform>(),
                string.Empty,
                14f,
                OnSurfaceMuted
            );
            detail.alignment = TextAlignmentOptions.TopLeft;
            detail.margin = new Vector4(18f, 0f, 18f, 0f);
            detail.fontStyle = FontStyles.Normal;
            detail.raycastTarget = false;

            Outline outline = select.gameObject.AddComponent<Outline>();
            outline.effectColor = SelectionOutline;
            outline.effectDistance = SelectionThickness;
            outline.enabled = false;

            var row = new SaveRow
            {
                Root = root,
                Select = select,
                Headline = headline,
                Detail = detail,
                Outline = outline,
            };

            int captured = index;

            // Only a press picks a run. Hovering used to, which left the pick
            // chasing the pointer and nothing on screen meaning "this is the one".
            select.onClick.AddListener(() => FocusRow(captured));

            root.gameObject.SetActive(false);
            return row;
        }

        /// <summary>Reads the saves back off disk and repaints every row from them.</summary>
        private void RefreshSaves()
        {
            List<BrocoliRunSave> saves = BrocoliSaveSystem.LoadAll();
            DateTime now = DateTime.UtcNow;
            visibleSaveCount = Mathf.Min(saves.Count, saveRows.Count);

            for (int index = 0; index < saveRows.Count; index++)
            {
                SaveRow row = saveRows[index];
                bool used = index < visibleSaveCount;
                row.Root.gameObject.SetActive(used);
                if (!used)
                {
                    row.Slot = -1;
                    continue;
                }

                BrocoliRunSave save = saves[index];
                row.Slot = save.slot;
                row.Headline.text = BrocoliSaveSummary.Headline(save);
                row.Detail.text = BrocoliSaveSummary.Detail(save, now);
            }

            selectedRow =
                visibleSaveCount == 0 ? -1 : Mathf.Clamp(selectedRow, 0, visibleSaveCount - 1);
            savesEmpty.gameObject.SetActive(visibleSaveCount == 0);

            bool canCreate = saves.Count < BrocoliSaveSystem.MaxSaves;
            newRunButton.interactable = canCreate;
            newTouchRunButton.interactable = canCreate;

            PaintSaveSelection();
            RebuildSavesSelectables();
            ApplyResponsiveLayout(true);
        }

        /// <summary>
        /// Marks the run the actions will act on. It keeps its highlight while the
        /// player walks down to Play or Delete, which is the whole point of picking
        /// it first.
        /// </summary>
        private void PaintSaveSelection()
        {
            for (int index = 0; index < visibleSaveCount; index++)
                PaintRow(saveRows[index], index == selectedRow);

            bool hasSelection = selectedRow >= 0;
            playSaveButton.interactable = hasSelection;
            deleteSaveButton.interactable = hasSelection;
            PaintButton(playSaveButton, hasSelection);
            deleteSaveLabel.text = deleteArmed ? ConfirmDeleteLabel : DeleteLabel;
            SyncSavesHint();
        }

        /// <summary>
        /// Draws a row in one of its three states. The picked run takes the green
        /// fill and a bright outline; the pointer only ever lifts a row a shade,
        /// so hovering one run while another is picked cannot be misread.
        /// </summary>
        private static void PaintRow(SaveRow row, bool picked)
        {
            ColorBlock colors = row.Select.colors;
            colors.normalColor = picked ? Primary : RowIdle;
            colors.highlightedColor = picked ? PrimaryHover : RowHover;
            colors.pressedColor = picked ? PrimaryPressed : RowPressed;
            colors.selectedColor = picked ? Primary : RowIdle;
            row.Select.colors = colors;

            row.Outline.enabled = picked;
            row.Detail.color = picked ? Hex("#E7F5E9") : OnSurfaceMuted;
        }
    }
}
