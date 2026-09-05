using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExercisePauseMenu(PauseMenu pause)
        {
            Assert.That(
                PauseMenu.DetectMobilePlatform(
                    false,
                    DeviceType.Desktop,
                    DeviceType.Desktop,
                    false
                ),
                Is.False
            );
            Assert.That(
                PauseMenu.DetectMobilePlatform(
                    false,
                    DeviceType.Handheld,
                    DeviceType.Desktop,
                    false
                ),
                Is.True
            );
            Assert.That(
                PauseMenu.DetectMobilePlatform(
                    false,
                    DeviceType.Desktop,
                    DeviceType.Handheld,
                    false
                ),
                Is.True
            );
            Assert.That(
                PauseMenu.DetectMobilePlatform(false, DeviceType.Desktop, DeviceType.Desktop, true),
                Is.True
            );
            InvokeHierarchy(pause, "EnsureEventSystemActive");
            InvokeHierarchy(pause, "SetupButtons");
            InvokeHierarchy(pause, "SetupMenuNavigation");
            InvokeHierarchy(pause, "SetupMenuNavigation");
            InvokeHierarchy(pause, "SelectMenuButton", -1);
            InvokeHierarchy(pause, "SelectMenuButton", 1);
            InvokeHierarchy(pause, "UpdateSelectionVisuals");
            SetHierarchyField(pause, "lastNavTime", Time.unscaledTime);
            InvokeHierarchy(pause, "HandleControllerNavigation");
            SetHierarchyField(pause, "lastNavTime", -10f);
            InvokeHierarchy(pause, "HandleControllerNavigation");
            InvokeHierarchy(pause, "RefreshCanvasGraphics");
            InvokeHierarchy(pause, "ResetMenuNavigation");
            pause.TogglePause();
            pause.TogglePause();
            ExercisePauseSettings(pause);
            ExercisePauseGamepad(pause);

            GameObject panel = pause.pauseMenuUI;
            pause.pauseMenuUI = null;
            LogAssert.Expect(LogType.Error, "[PauseMenu] pauseMenuUI is null!");
            pause.Pause();
            LogAssert.Expect(LogType.Error, "[PauseMenu] pauseMenuUI is null!");
            pause.Resume();
            pause.pauseMenuUI = panel;
            ExercisePauseEdgeCases(pause);
        }

        private static void ExercisePauseSettings(PauseMenu pause)
        {
            pause.Pause();
            InvokeHierarchy(pause, "OpenSettings");
            ResponsivePauseMenuLayout layout =
                pause.pauseMenuUI.GetComponent<ResponsivePauseMenuLayout>();
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.SettingsOpen, Is.True);
            Assert.That(pause.IsPaused(), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            ExercisePauseHdrInput(layout);
            InvokeHierarchy(pause, "Update");
            InvokeHierarchy(layout, "ShowHdrDetails");
            Assert.That(layout.HdrDetailsOpen, Is.True);
            InvokeHierarchy(layout, "OpenHdrCalibration");
            Assert.That(layout.HdrCalibrationOpen, Is.True);
            InvokeHierarchy(layout, "EndHdrCalibration", false);
            Assert.That(layout.HdrCalibrationOpen, Is.False);
            Assert.That(layout.HdrDetailsOpen, Is.True);
            InvokeHierarchy(layout, "HideHdrDetails");
            layout.HideSettings();
            Assert.That(layout.SettingsOpen, Is.False);
            Assert.That(pause.IsPaused(), Is.True);
            InvokeHierarchy(pause, "OpenSettings");
            Assert.That(layout.SettingsOpen, Is.True);
            pause.Resume();
            Assert.That(layout.SettingsOpen, Is.False);
        }

        private static void ExercisePauseEdgeCases(PauseMenu pause)
        {
            GameObject originalPanel = pause.pauseMenuUI;
            GameObject originalPauseButton = pause.pauseButton;
            Button originalResume = pause.resumeButton;
            Button originalSettings = pause.settingsButton;
            Button originalMainMenu = pause.mainMenuButton;
            GameObject panel = new("Coverage Pause Panel", typeof(RectTransform));
            panel.SetActive(false);
            Button resume = new GameObject(
                "Resume",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            ).GetComponent<Button>();
            resume.transform.SetParent(panel.transform, false);
            Button mainMenu = new GameObject(
                "Main Menu",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            ).GetComponent<Button>();
            mainMenu.transform.SetParent(panel.transform, false);
            ResponsivePauseMenuLayout coverageLayout =
                panel.AddComponent<ResponsivePauseMenuLayout>();
            InvokeHierarchy(coverageLayout, "Awake");
            InvokeHierarchy(coverageLayout, "OnRectTransformDimensionsChange");
            InvokeHierarchy(coverageLayout, "LateUpdate");
            InvokeHierarchy(coverageLayout, "LateUpdate");
            pause.pauseMenuUI = panel;
            SetHierarchyField(pause, "responsiveLayout", null);
            InvokeHierarchy(pause, "OpenSettings");
            pause.resumeButton = null;
            pause.settingsButton = null;
            pause.mainMenuButton = null;
            InvokeHierarchy(pause, "SetupButtons");
            Assert.That(pause.settingsButton, Is.Not.Null);

            GameObject emptyPanel = new("Coverage Empty Pause Panel", typeof(RectTransform));
            pause.pauseMenuUI = emptyPanel;
            pause.resumeButton = null;
            pause.settingsButton = null;
            pause.mainMenuButton = null;
            LogAssert.Expect(LogType.Error, "[PauseMenu] Settings button not found!");
            LogAssert.Expect(LogType.Error, "[PauseMenu] Resume button not found!");
            LogAssert.Expect(LogType.Error, "[PauseMenu] MainMenu button not found!");
            InvokeHierarchy(pause, "SetupButtons");

            GameObject eventObject = new("Coverage Disabled Event System");
            eventObject.SetActive(false);
            EventSystem coverageEventSystem = eventObject.AddComponent<EventSystem>();
            SetHierarchyField(pause, "eventSystem", coverageEventSystem);
            InvokeHierarchy(pause, "EnsureEventSystemActive");
            BaseInputModule module = eventObject.GetComponent<BaseInputModule>();
            module.enabled = false;
            InvokeHierarchy(pause, "EnsureEventSystemActive");

            pause.pauseMenuUI = panel;
            pause.resumeButton = resume;
            pause.settingsButton = panel
                .transform.Find("SafeArea/PauseCard/SettingsButton")
                ?.GetComponent<Button>();
            pause.mainMenuButton = mainMenu;
            SetHierarchyField(pause, "navigationInitialized", false);
            InvokeHierarchy(pause, "SetupMenuNavigation");
            Button[] buttons = { resume, mainMenu };
            SetHierarchyField(pause, "menuButtons", buttons);
            SetHierarchyField(pause, "selectedButtonIndex", 0);
            SetHierarchyField(pause, "menuButtons", new Button[] { null, resume });
            SetHierarchyField(pause, "navigationInitialized", false);
            InvokeHierarchy(pause, "SetupMenuNavigation");
            InvokeHierarchy(pause, "ResetMenuNavigation");
            InvokeHierarchy(pause, "UpdateSelectionVisuals");
            SetHierarchyField(pause, "menuButtons", buttons);
            SetHierarchyField(pause, "lastNavTime", -10f);
            Assert.That(PauseMenu.ResolveVerticalInput(true, false, 0f, 0f), Is.EqualTo(1f));
            Assert.That(PauseMenu.ResolveVerticalInput(false, true, 0f, 0f), Is.EqualTo(-1f));
            Assert.That(PauseMenu.ResolveVerticalInput(false, false, -1f, 0f), Is.EqualTo(-1f));
            Assert.That(PauseMenu.ResolveVerticalInput(false, false, 0f, 1f), Is.EqualTo(1f));
            Assert.That(PauseMenu.ResolveVerticalInput(false, false, 0f, 0f), Is.Zero);
            pause.ProcessToggleInput(false, false);
            pause.ProcessControllerNavigation(0f, false, false);
            SetHierarchyField(pause, "lastNavTime", -10f);
            pause.ProcessControllerNavigation(-1f, false, false);
            SetHierarchyField(pause, "lastNavTime", -10f);
            pause.ProcessControllerNavigation(1f, false, false);
            pause.ProcessToggleInput(false, true);
            if (pause.IsPaused())
                pause.Resume();
            pause.Pause();
            SetHierarchyField(pause, "lastNavTime", -10f);
            pause.ProcessControllerNavigation(0f, true, false);
            pause.Pause();
            SetHierarchyField(pause, "lastNavTime", -10f);
            pause.ProcessControllerNavigation(0f, false, true);

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadDown),
                    () => InvokeHierarchy(pause, "HandleControllerNavigation")
                );
                SetHierarchyField(pause, "lastNavTime", -10f);
                Pulse(
                    gamepad,
                    new GamepadState { leftStick = Vector2.up },
                    () => InvokeHierarchy(pause, "HandleControllerNavigation")
                );
                SetHierarchyField(pause, "lastNavTime", -10f);
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South),
                    () => InvokeHierarchy(pause, "HandleControllerNavigation")
                );
                pause.Pause();
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.Start),
                    () => InvokeHierarchy(pause, "Update")
                );
                pause.Pause();
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.East),
                    () => InvokeHierarchy(pause, "HandleControllerNavigation")
                );
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }

            SetHierarchyField(pause, "buttonOutlines", null);
            InvokeHierarchy(pause, "UpdateSelectionVisuals");
            SetHierarchyField(pause, "mainCanvas", null);
            InvokeHierarchy(pause, "RefreshCanvasGraphics");
            if (pause.IsPaused())
                pause.Resume();
            Assert.That(((IPauseController)pause).IsPaused, Is.False);

            EventSystem replacement = ExercisePauseEventSystemEdges(pause);

            pause.pauseMenuUI = originalPanel;
            pause.pauseButton = originalPauseButton;
            pause.resumeButton = originalResume;
            pause.settingsButton = originalSettings;
            pause.mainMenuButton = originalMainMenu;
            SetHierarchyField(pause, "eventSystem", replacement);
            Object.Destroy(panel);
            Object.Destroy(emptyPanel);
            Object.Destroy(eventObject);
        }
    }
}
