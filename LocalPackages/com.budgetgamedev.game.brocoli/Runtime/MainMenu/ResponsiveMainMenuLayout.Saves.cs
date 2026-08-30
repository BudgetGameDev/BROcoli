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

        /// <summary>
        /// The line under the heading carries whichever of the two things the player
        /// needs to know: how much room is left, or that Delete is waiting on a
        /// second press.
        /// </summary>
        private void SyncSavesHint()
        {
            if (deleteArmed)
            {
                savesHint.text = "PRESS DELETE AGAIN TO REMOVE THE PICKED RUN";
                return;
            }

            savesHint.text = newRunButton.interactable
                ? $"{visibleSaveCount} OF {BrocoliSaveSystem.MaxSaves} SLOTS USED"
                : "ALL SLOTS FULL  ·  DELETE A RUN TO START A NEW ONE";
        }

        private static void PaintButton(Button button, bool primaryAction)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = primaryAction ? Primary : SurfaceVariant;
            colors.highlightedColor = primaryAction ? PrimaryHover : Hex("#3A4B42");
            colors.pressedColor = primaryAction ? PrimaryPressed : Hex("#202C26");
            colors.selectedColor = primaryAction ? PrimaryHover : Hex("#3A4B42");
            button.colors = colors;
        }

        private void RebuildSavesSelectables()
        {
            savesSelectables.Clear();
            for (int index = 0; index < visibleSaveCount; index++)
                savesSelectables.Add(saveRows[index].Select);

            // Play and Delete share one line, so only Play is listed: Delete is the
            // same line's second column. A button that cannot be pressed is left out
            // rather than selected, so walking down never stops on a dead entry.
            if (playSaveButton.interactable)
                savesSelectables.Add(playSaveButton);
            if (newRunButton.interactable)
                savesSelectables.Add(newRunButton);
            if (newTouchRunButton.interactable)
                savesSelectables.Add(newTouchRunButton);
            savesSelectables.Add(backSavesButton);

            savesFocus = Mathf.Clamp(savesFocus, 0, savesSelectables.Count - 1);
            if (!OnActionLine)
                savesColumn = 0;
        }

        private bool OnActionLine =>
            savesFocus >= 0
            && savesFocus < savesSelectables.Count
            && ReferenceEquals(savesSelectables[savesFocus], playSaveButton);

        private void OpenSaves()
        {
            ProceduralUIAudio.PlaySelect();
            mainButtonsWereActive = new bool[mainButtons.Length];
            for (int index = 0; index < mainButtons.Length; index++)
            {
                if (mainButtons[index] == null)
                    continue;

                mainButtonsWereActive[index] = mainButtons[index].gameObject.activeSelf;
                mainButtons[index].gameObject.SetActive(false);
            }

            SavesOpen = true;
            deleteArmed = false;
            savesFocus = 0;
            savesColumn = 0;

            // The newest run is the one a player almost always wants, so it starts
            // picked and Play is one press away.
            selectedRow = 0;
            savesPanel.gameObject.SetActive(true);
            RefreshSaves();
            Canvas.ForceUpdateCanvases();
            savesScroll.verticalNormalizedPosition = 1f;
            FocusSave(0, false);
        }

        private void CloseSaves()
        {
            ProceduralUIAudio.PlaySelect();
            SavesOpen = false;
            deleteArmed = false;
            savesPanel.gameObject.SetActive(false);
            if (mainButtonsWereActive != null)
            {
                for (int index = 0; index < mainButtons.Length; index++)
                    if (mainButtons[index] != null)
                        mainButtons[index].gameObject.SetActive(mainButtonsWereActive[index]);
            }

            GetComponent<MainMenu>()?.SetupControllerNavigation(true, savesButton);
            ApplyResponsiveLayout(true);
        }

        private void PlaySelectedRun()
        {
            if (selectedRow < 0 || selectedRow >= visibleSaveCount)
                return;

            int slot = saveRows[selectedRow].Slot;
            MainMenu menu = GetComponent<MainMenu>();
            if (menu != null && menu.LoadSave(slot))
                return;

            // The slot emptied underneath us - another tab, a cleared browser
            // profile - so show what is really there instead of a dead row.
            RefreshSaves();
            FocusSave(savesFocus, false);
        }

        private void DeleteSelectedRun()
        {
            if (selectedRow < 0 || selectedRow >= visibleSaveCount)
                return;

            if (!deleteArmed)
            {
                // Hours of play and no undo: one press arms, the next deletes.
                ProceduralUIAudio.PlayHover();
                deleteArmed = true;
                PaintSaveSelection();
                return;
            }

            ProceduralUIAudio.PlaySelect();
            BrocoliSaveSystem.DeleteSave(saveRows[selectedRow].Slot);
            deleteArmed = false;

            // Stay where the list is: the run under the one just removed is picked.
            RefreshSaves();
            FocusSave(Mathf.Min(savesFocus, savesSelectables.Count - 1), false);
        }

        /// <summary>
        /// Picking a run is all a row press does. Starting it is Play's job, so a
        /// stray press on the list can never drop the player into a dungeon.
        /// </summary>
        private void FocusRow(int index)
        {
            if (index >= visibleSaveCount)
                return;

            savesColumn = 0;
            FocusSave(index);
        }

        private void FocusAction(int column)
        {
            if (!playSaveButton.interactable)
                return;

            savesColumn = column;
            FocusSave(savesSelectables.IndexOf(playSaveButton));
        }

        private void FocusButton(Button button)
        {
            int index = savesSelectables.IndexOf(button);
            if (index >= 0)
                FocusSave(index);
        }

        private void FocusSave(int index, bool playSound = true)
        {
            if (savesSelectables.Count == 0)
                return;

            int count = savesSelectables.Count;
            int next = ((index % count) + count) % count;
            bool moved = next != savesFocus;
            savesFocus = next;

            if (!OnActionLine)
                savesColumn = 0;

            // Landing on a row picks that run; leaving Delete puts its safety back on.
            if (savesFocus < visibleSaveCount)
                selectedRow = savesFocus;
            if (moved && deleteArmed)
                deleteArmed = false;

            PaintSaveSelection();
            EventSystem.current?.SetSelectedGameObject(CurrentSaveSelectable().gameObject);
            if (playSound)
                ProceduralUIAudio.PlayHover();

            EnsureSelectedRowVisible();
        }

        private Selectable CurrentSaveSelectable()
        {
            return OnActionLine && savesColumn == 1
                ? deleteSaveButton
                : savesSelectables[savesFocus];
        }

        private void RegisterSavesPointer(Button target, Action select)
        {
            EventTrigger trigger = target.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ =>
            {
                if (SavesOpen)
                    select();
            });
            trigger.triggers.Add(entry);
        }

        private void UpdateSavesInput()
        {
            bool cancel =
                Input.GetKeyDown(KeyCode.Escape)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                CloseSaves();
                return;
            }

            float vertical =
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1f
                : Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? -1f
                : 0f;
            float horizontal =
                Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1f
                : Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? -1f
                : 0f;
            if (Gamepad.current != null)
            {
                Vector2 axis = Gamepad.current.dpad.ReadValue();
                if (axis.sqrMagnitude < 0.25f)
                    axis = Gamepad.current.leftStick.ReadValue();
                if (Mathf.Abs(axis.y) > 0.5f)
                    vertical = Mathf.Sign(axis.y);
                if (Mathf.Abs(axis.x) > 0.5f)
                    horizontal = Mathf.Sign(axis.x);
            }

            if (Time.unscaledTime - lastSavesNavTime >= 0.18f)
            {
                if (Mathf.Abs(vertical) > 0.5f)
                {
                    lastSavesNavTime = Time.unscaledTime;
                    FocusSave(savesFocus + (vertical > 0f ? -1 : 1));
                }
                else if (Mathf.Abs(horizontal) > 0.5f && OnActionLine)
                {
                    lastSavesNavTime = Time.unscaledTime;
                    FocusAction(horizontal > 0f ? 1 : 0);
                }
            }

            bool submit =
                Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            if (
                submit
                && CurrentSaveSelectable() is Button button
                && button.interactable
                && MenuInputGate.TryConsumeSubmit()
            )
            {
                button.onClick.Invoke();
            }
        }
    }
}
