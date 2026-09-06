using BudgetGameDev.Shared;
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
        private static void ExerciseLevelUpScreen(LevelUpScreen levelUp, PlayerStats stats)
        {
            ResetEventSystemForLevelUp(levelUp);
            SetHierarchyField(levelUp, "confirmButtonText", null);
            InvokeHierarchy(levelUp, "EnsureConfirmButton");
            InvokeHierarchy(levelUp, "SetupButtons");
            InvokeHierarchy(levelUp, "SetupSelectionVisuals");
            InvokeHierarchy(levelUp, "SetSelectedIndex", -1);
            InvokeHierarchy(levelUp, "SetSelectedIndex", 1);
            InvokeHierarchy(levelUp, "NavigateSelection", -1);
            InvokeHierarchy(levelUp, "UpdateSelectionVisuals");
            InvokeHierarchy(levelUp, "ChooseUpgrade", -1);
            InvokeHierarchy(levelUp, "ConfirmSelectedUpgrade");
            levelUp.GetOption(-1);
            levelUp.GetOption(100);

            levelUp.Show(3, stats);
            InvokeHierarchy(levelUp, "Update");
            LevelUpAutoResolver resolver = Object.FindAnyObjectByType<LevelUpAutoResolver>();
            if (resolver != null)
            {
                SetHierarchyField(resolver, "_screen", null);
                SetHierarchyField(resolver, "_stats", null);
                SetHierarchyField(resolver, "_cooldown", 0f);
                InvokeHierarchy(resolver, "Update");
            }
            ExerciseLevelUpGamepad(levelUp);
            InvokeHierarchy(levelUp, "ChooseUpgrade", 1);
            InvokeHierarchy(levelUp, "UpdateConfirmButton");
            InvokeHierarchy(levelUp, "ConfirmSelectedUpgrade");
            SetHierarchyField(levelUp, "lastNavTime", -10f);
            levelUp.ProcessNavigation(-1f, false, Time.unscaledTime);
            levelUp.ProcessNavigation(1f, false, Time.unscaledTime + 1f);
            levelUp.ProcessNavigation(0f, true, Time.unscaledTime + 2f);
            SetHierarchyField(levelUp, "hasPendingSelection", true);
            levelUp.ProcessNavigation(0f, true, Time.unscaledTime + 3f);
            SetHierarchyField(levelUp, "playerStats", null);
            InvokeHierarchy(levelUp, "ApplyUpgrade", 0);
            SetHierarchyField(levelUp, "confirmButtonText", null);
            InvokeHierarchy(levelUp, "UpdateConfirmButton");

            GameObject panel = GetHierarchyField<GameObject>(levelUp, "levelUpPanel");
            SetHierarchyField(levelUp, "levelUpPanel", null);
            LogAssert.Expect(LogType.Warning, "[LevelUpScreen] Panel not assigned");
            levelUp.Show(4, stats);
            SetHierarchyField(levelUp, "levelUpPanel", panel);
            levelUp.Hide();
        }

        private static void ExercisePauseGamepad(PauseMenu pause)
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                pause.Pause();
                SetHierarchyField(pause, "lastNavTime", -10f);
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadDown),
                    () => InvokeHierarchy(pause, "HandleControllerNavigation")
                );
                InvokeHierarchy(pause, "SelectMenuButton", 0);
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South),
                    () => InvokeHierarchy(pause, "HandleControllerNavigation")
                );
                if (pause.IsPaused())
                    pause.Resume();
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }
        }

        private static void ExerciseVirtualController()
        {
            VirtualController controller = Object.FindAnyObjectByType<VirtualController>();
            if (controller == null)
                return;

            SetHierarchyField(controller, "isMobileCached", true);
            SetHierarchyField(controller, "isMobileCacheSet", true);
            InvokeHierarchy(controller, "IsMobilePlatform");
            InvokeHierarchy(controller, "OnEnable");
            InvokeHierarchy(controller, "Update");
            InvokeHierarchy(controller, "SetupActionButton");
            InvokeHierarchy(controller, "HandleJoystickInput");
            InvokeHierarchy(controller, "IsTouchOnJoystick", Vector2.zero);
            InvokeHierarchy(controller, "UpdateJoystickPosition", Vector2.zero);
            InvokeHierarchy(controller, "ResetJoystick");
            InvokeHierarchy(controller, "UpdateLayoutForOrientation");
            InvokeHierarchy(controller, "ClampAnchorToSafeArea", null, Vector2.one * 0.5f);

            Button action = GetHierarchyField<Button>(controller, "actionButton");
            if (action != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(action.gameObject);
                InvokeHierarchy(controller, "OnActionButtonPressed");
            }
            InvokeHierarchy(controller, "OnDisable");
            InvokeHierarchy(controller, "OnEnable");
        }
    }
}
