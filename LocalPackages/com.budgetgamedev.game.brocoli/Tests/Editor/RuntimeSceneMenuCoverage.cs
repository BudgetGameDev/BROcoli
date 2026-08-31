using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseMainMenuInput(ResponsiveMainMenuLayout layout)
        {
            RectTransform settingsPanel = GetHierarchyField<RectTransform>(layout, "settingsPanel");
            RectTransform creditsPanel = GetHierarchyField<RectTransform>(layout, "creditsPanel");
            SetHierarchyField(layout, "settingsPanel", null);
            InvokeHierarchy(layout, "LayoutSettingsPanel", 500f, 1f, 0f, false, false);
            SetHierarchyField(layout, "settingsPanel", settingsPanel);
            SetHierarchyField(layout, "creditsPanel", null);
            InvokeHierarchy(layout, "LayoutCreditsPanel", 500f, 1f, 0f, false, false);
            SetHierarchyField(layout, "creditsPanel", creditsPanel);
            RectTransform savesPanel = GetHierarchyField<RectTransform>(layout, "savesPanel");
            SetHierarchyField(layout, "savesPanel", null);
            InvokeHierarchy(layout, "LayoutSavesPanel", 500f, 1f, 0f, false, false);
            SetHierarchyField(layout, "savesPanel", savesPanel);
            MainMenu menu = layout.GetComponent<MainMenu>();
            Assert.That(menu, Is.Not.Null);
            GameObject installButton = GetHierarchyField<GameObject>(menu, "installAppButton");
            SetHierarchyField(menu, "installAppButton", null);
            menu.ConfigureInstallButton(false);
            SetHierarchyField(menu, "installAppButton", installButton);
            menu.ConfigureInstallButton(true);
            menu.ConfigureInstallButton(false);
            menu.ShowInstallPrompt();
            Assert.That(menu.LoadSave(-1), Is.False);
            Button[] buttons = GetHierarchyField<Button[]>(layout, "mainButtons");
            if (buttons == null)
            {
                InvokeHierarchy(layout, "CacheExistingMenuObjects");
                buttons = GetHierarchyField<Button[]>(layout, "mainButtons");
            }
            menu.SetNavigationOrder(null);
            menu.SetNavigationOrder(buttons);
            GameObject submitObject = new(
                "Coverage Main Menu Submit",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            Button submitButton = submitObject.GetComponent<Button>();
            int submits = 0;
            submitButton.onClick.AddListener(() => submits++);
            SetHierarchyField(menu, "menuButtons", new[] { submitButton });
            SetHierarchyField(menu, "selectedIndex", 0);
            menu.HandleSubmit(false);
            SetHierarchyField(menu, "menuButtons", new Button[] { null });
            menu.HandleSubmit(true);
            SetHierarchyField(menu, "menuButtons", new[] { submitButton });
            ResetMenuInputGate();
            menu.SubmitSelected();
            Assert.That(submits, Is.EqualTo(1));
            Object.Destroy(submitObject);
            SetHierarchyField(menu, "menuButtons", buttons);
            Button first = System.Array.Find(buttons, button => button != null);
            int firstIndex = System.Array.IndexOf(buttons, first);
            if (firstIndex >= 0)
            {
                buttons[firstIndex] = null;
                InvokeHierarchy(layout, "OpenCredits");
                InvokeHierarchy(layout, "CloseCredits");
                buttons[firstIndex] = first;
            }
            menu.SetupControllerNavigation(true, first);
            InvokeHierarchy(menu, "SuppressEventSystemNavigation");
            InvokeHierarchy(menu, "SuppressEventSystemNavigation");
            InvokeHierarchy(menu, "RestoreEventSystemNavigation");
            InvokeHierarchy(menu, "HandleNavigation", 0f);
            InvokeHierarchy(menu, "HandleNavigation", -1f);
            InvokeHierarchy(menu, "HandleNavigation", -1f);
            SetHierarchyField(menu, "nextNavigationTime", -1f);
            InvokeHierarchy(menu, "HandleNavigation", -1f);
            Button second = System.Array.Find(buttons, button => button != null && button != first);
            if (first != null && second != null)
            {
                InvokeHierarchy(menu, "CompareByScreenPosition", first, second);
                InvokeHierarchy(menu, "CompareByMenuOrder", first, second);
                InvokeHierarchy(menu, "RankOf", first);
            }

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadDown),
                    () => InvokeHierarchy(menu, "HandleMenuInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South),
                    () => InvokeHierarchy(menu, "HandleSubmit")
                );
                if (ResponsiveMainMenuLayout.ModalOpen)
                {
                    if (ResponsiveMainMenuLayout.SavesOpen)
                        InvokeHierarchy(layout, "CloseSaves");
                    else if (ResponsiveMainMenuLayout.SettingsOpen)
                        InvokeHierarchy(layout, "CloseSettings");
                    else if (ResponsiveMainMenuLayout.CreditsOpen)
                        InvokeHierarchy(layout, "CloseCredits");
                }

                InvokeHierarchy(layout, "OpenSettings");
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadDown),
                    () => InvokeHierarchy(layout, "Update")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadRight),
                    () => InvokeHierarchy(layout, "Update")
                );
                SetHierarchyField(layout, "selectedSetting", 3);
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South),
                    () => InvokeHierarchy(layout, "Update")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.East),
                    () => InvokeHierarchy(layout, "Update")
                );
                if (ResponsiveMainMenuLayout.SettingsOpen)
                    InvokeHierarchy(layout, "CloseSettings");

                InvokeHierarchy(layout, "OpenCredits");
                layout.ProcessCreditsInput(false, true, false, 0f, false);
                layout.ProcessCreditsInput(false, false, true, 0f, false);
                layout.ProcessCreditsInput(false, false, false, 1f, false);
                layout.ProcessCreditsInput(false, false, false, 0f, true);
                InvokeHierarchy(layout, "OpenCredits");
                layout.ProcessCreditsInput(true, false, false, 0f, false);
                InvokeHierarchy(layout, "OpenCredits");
                InvokeHierarchy(layout, "Update");
                Pulse(
                    gamepad,
                    new GamepadState { rightStick = Vector2.down },
                    () => InvokeHierarchy(layout, "Update")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South),
                    () => InvokeHierarchy(layout, "UpdateCreditsInput")
                );
                if (ResponsiveMainMenuLayout.CreditsOpen)
                    InvokeHierarchy(layout, "CloseCredits");

                InvokeHierarchy(layout, "OpenSaves");
                InvokeHierarchy(layout, "Update");
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadDown),
                    () => InvokeHierarchy(layout, "UpdateSavesInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadRight),
                    () => InvokeHierarchy(layout, "UpdateSavesInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState { leftStick = Vector2.up },
                    () => InvokeHierarchy(layout, "UpdateSavesInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.East),
                    () => InvokeHierarchy(layout, "UpdateSavesInput")
                );
                if (ResponsiveMainMenuLayout.SavesOpen)
                    InvokeHierarchy(layout, "CloseSaves");
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }

            typeof(MenuInputGate)
                .GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            Assert.That(MenuInputGate.TryConsumeSubmit(), Is.True);
            Assert.That(MenuInputGate.TryConsumeSubmit(), Is.False);
            Assert.That(MenuInputGate.TryConsumeCancel(), Is.True);
            Assert.That(MenuInputGate.TryConsumeCancel(), Is.False);
        }

        private static void Pulse(Gamepad gamepad, GamepadState state, System.Action action)
        {
            InputSystem.QueueStateEvent(gamepad, state);
            InputSystem.Update();
            action();
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            InputSystem.Update();
        }
    }
}
