using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseSavesEdges(ResponsiveMainMenuLayout layout)
        {
            SetHierarchyField(layout, "deleteArmed", true);
            InvokeHierarchy(layout, "SyncSavesHint");
            SetHierarchyField(layout, "deleteArmed", false);
            InvokeHierarchy(layout, "SyncSavesHint");

            int visible = GetHierarchyField<int>(layout, "visibleSaveCount");
            SetHierarchyField(layout, "selectedRow", -1);
            InvokeHierarchy(layout, "PlaySelectedRun");
            InvokeHierarchy(layout, "DeleteSelectedRun");
            InvokeHierarchy(layout, "FocusRow", visible);

            IList rows = (IList)GetHierarchyField<object>(layout, "saveRows");
            if (rows.Count > 0)
            {
                object row = rows[0];
                FieldInfo slot = row.GetType().GetField("Slot");
                SetHierarchyField(layout, "visibleSaveCount", 1);
                SetHierarchyField(layout, "selectedRow", 0);
                InvokeHierarchy(layout, "PlaySelectedRun", (System.Func<int, bool>)(_ => true));
                slot.SetValue(row, -1);
                SetHierarchyField(layout, "visibleSaveCount", 1);
                SetHierarchyField(layout, "selectedRow", 0);
                InvokeHierarchy(layout, "PlaySelectedRun");
                slot.SetValue(row, -1);
                SetHierarchyField(layout, "visibleSaveCount", 1);
                SetHierarchyField(layout, "selectedRow", 0);
                SetHierarchyField(layout, "deleteArmed", true);
                InvokeHierarchy(layout, "DeleteSelectedRun");
            }

            Button play = GetHierarchyField<Button>(layout, "playSaveButton");
            bool playInteractable = play.interactable;
            play.interactable = false;
            InvokeHierarchy(layout, "FocusAction", 0);
            play.interactable = playInteractable;

            RectTransform content = GetHierarchyField<RectTransform>(layout, "savesContent");
            RectTransform viewport = GetHierarchyField<RectTransform>(layout, "savesViewport");
            ScrollRect scroll = GetHierarchyField<ScrollRect>(layout, "savesScroll");
            SetHierarchyField(layout, "visibleSaveCount", 10);
            SetHierarchyField(layout, "saveRowHeight", 60f);
            SetHierarchyField(layout, "saveRowStride", 68f);
            SetHierarchyField(layout, "savesFocus", 0);
            content.sizeDelta = new Vector2(content.sizeDelta.x, 1000f);
            viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, 100f);
            scroll.verticalNormalizedPosition = 0f;
            InvokeHierarchy(layout, "EnsureSelectedRowVisible");
            SetHierarchyField(layout, "savesFocus", 9);
            scroll.verticalNormalizedPosition = 1f;
            InvokeHierarchy(layout, "EnsureSelectedRowVisible");

            var selectables = GetHierarchyField<List<Selectable>>(layout, "savesSelectables");
            var savedSelectables = new List<Selectable>(selectables);
            selectables.Clear();
            InvokeHierarchy(layout, "FocusSave", 0, false);
            selectables.AddRange(savedSelectables);

            Button back = GetHierarchyField<Button>(layout, "backSavesButton");
            InvokeHierarchy(layout, "FocusButton", back);
            SetHierarchyField(layout, "deleteArmed", true);
            InvokeHierarchy(layout, "FocusSave", 0, false);
            SetHierarchyField(layout, "deleteArmed", false);

            GameObject pointerObject = new(
                "Coverage Save Pointer",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            Button pointerButton = pointerObject.GetComponent<Button>();
            int enters = 0;
            InvokeHierarchy(
                layout,
                "RegisterSavesPointer",
                pointerButton,
                (System.Action)(() => enters++)
            );
            EventTrigger trigger = pointerObject.GetComponent<EventTrigger>();
            Assert.That(trigger, Is.Not.Null);
            trigger.triggers[0].callback.Invoke(new BaseEventData(EventSystem.current));
            Assert.That(enters, Is.EqualTo(1));
            SetHierarchyField(layout, "<SavesOpen>k__BackingField", false);
            trigger.triggers[0].callback.Invoke(new BaseEventData(EventSystem.current));
            Assert.That(enters, Is.EqualTo(1));
            SetHierarchyField(layout, "<SavesOpen>k__BackingField", true);
            Object.Destroy(pointerObject);

            SetHierarchyField(layout, "lastSavesNavTime", -10f);
            layout.ProcessSavesInput(false, 1f, 0f, false, Time.unscaledTime);
            layout.ProcessSavesInput(false, -1f, 0f, false, Time.unscaledTime + 1f);
            layout.ProcessSavesInput(false, 0f, 0f, false, Time.unscaledTime + 2f);

            int playIndex = selectables.IndexOf(play);
            if (playIndex >= 0 && play.interactable)
            {
                SetHierarchyField(layout, "savesFocus", playIndex);
                layout.ProcessSavesInput(false, 0f, 1f, false, Time.unscaledTime + 3f);
                layout.ProcessSavesInput(false, 0f, -1f, false, Time.unscaledTime + 4f);
            }

            ResetMenuInputGate();
            layout.ProcessSavesInput(true, 0f, 0f, false, Time.unscaledTime + 5f);
            Button[] mainButtons = GetHierarchyField<Button[]>(layout, "mainButtons");
            Button firstMainButton = mainButtons[0];
            mainButtons[0] = null;
            InvokeHierarchy(layout, "OpenSaves");
            mainButtons[0] = firstMainButton;
            selectables = GetHierarchyField<List<Selectable>>(layout, "savesSelectables");
            int backIndex = selectables.IndexOf(
                GetHierarchyField<Button>(layout, "backSavesButton")
            );
            SetHierarchyField(layout, "savesFocus", backIndex);
            ResetMenuInputGate();
            layout.ProcessSavesInput(false, 0f, 0f, true, Time.unscaledTime + 6f);
            InvokeHierarchy(layout, "OpenSaves");

            if (visible > 0)
            {
                SetHierarchyField(layout, "selectedRow", 0);
                InvokeHierarchy(layout, "DeleteSelectedRun");
                InvokeHierarchy(layout, "FocusSave", 1, false);
            }
        }

        private static void ResetMenuInputGate()
        {
            typeof(MenuInputGate)
                .GetMethod(
                    "ResetStaticState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                )
                .Invoke(null, null);
        }

        private static void ExerciseSettingsInput(ResponsiveMainMenuLayout layout)
        {
            InvokeHierarchy(layout, "OpenSettings");
            SetHierarchyField(layout, "lastSettingsNavTime", -10f);
            float now = Time.unscaledTime;
            layout.ProcessSettingsInput(false, 1f, 0f, false, now);
            layout.ProcessSettingsInput(false, -1f, 0f, false, now + 1f);
            SetHierarchyField(layout, "selectedSetting", 0);
            layout.ProcessSettingsInput(false, 0f, 1f, false, now + 2f);

            Selectable[] selectables = GetHierarchyField<Selectable[]>(
                layout,
                "settingsSelectables"
            );
            int buttonIndex = System.Array.FindIndex(selectables, item => item is Button);
            if (buttonIndex >= 0)
            {
                SetHierarchyField(layout, "selectedSetting", buttonIndex);
                ResetMenuInputGate();
                layout.ProcessSettingsInput(false, 0f, 0f, true, now + 3f);
            }
            if (!ResponsiveMainMenuLayout.SettingsOpen)
                InvokeHierarchy(layout, "OpenSettings");
            ResetMenuInputGate();
            layout.ProcessSettingsInput(true, 0f, 0f, false, now + 4f);
        }
    }
}
