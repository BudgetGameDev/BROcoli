using System;
using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
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

        private void PlaySelectedRun() =>
            PlaySelectedRun(slot =>
            {
                MainMenu menu = GetComponent<MainMenu>();
                return menu != null && menu.LoadSave(slot);
            });

        internal void PlaySelectedRun(System.Func<int, bool> loadSave)
        {
            if (selectedRow < 0 || selectedRow >= visibleSaveCount)
                return;

            int slot = saveRows[selectedRow].Slot;
            if (loadSave(slot))
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
    }
}
