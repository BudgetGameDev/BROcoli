using System.Collections.Generic;
using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class PauseMenu
    {
        private bool navigationInitialized;

        internal static bool DetectMobilePlatform(
            bool targetDetected,
            DeviceType deviceType,
            DeviceType simulatedDeviceType,
            bool simulatedMobile
        ) =>
            targetDetected
            || deviceType == DeviceType.Handheld
            || simulatedDeviceType == DeviceType.Handheld
            || simulatedMobile;

        /// <summary>
        /// The frame after which a toggle press is believed again. Regaining focus makes
        /// the input system deliver whatever was held while the window was away, and in the
        /// editor that means alt-tabbing back lands on the pause menu every time. One frame
        /// of deafness is enough, because a real press arrives while the window has focus.
        /// </summary>
        private int toggleDeafUntilFrame;

        internal void OnApplicationFocus(bool focused)
        {
            // The editor keeps running while unfocused, so alt-tabbing away is not a pause;
            // only the input that arrives with the focus change has to be dropped.
            if (focused)
                toggleDeafUntilFrame = Time.frameCount + 1;
        }

        internal void ProcessToggleInput(bool escapePressed, bool startPressed)
        {
            if (Time.frameCount <= toggleDeafUntilFrame)
                return;
            if (escapePressed || startPressed)
                TogglePause();
        }

        internal static float ResolveVerticalInput(
            bool keyboardUp,
            bool keyboardDown,
            float dpadY,
            float stickY
        )
        {
            if (Mathf.Abs(dpadY) > 0.5f)
                return Mathf.Sign(dpadY);
            if (Mathf.Abs(stickY) > 0.5f)
                return Mathf.Sign(stickY);
            if (keyboardUp)
                return 1f;
            return keyboardDown ? -1f : 0f;
        }

        internal void ProcessControllerNavigation(float vertical, bool submit, bool back)
        {
            if (menuButtons == null || menuButtons.Length == 0)
                return;
            if (Time.unscaledTime - lastNavTime < NavRepeatDelay)
                return;

            if (Mathf.Abs(vertical) > 0.1f)
            {
                lastNavTime = Time.unscaledTime;
                int direction = vertical > 0 ? -1 : 1;
                int newIndex = Mathf.Clamp(
                    selectedButtonIndex + direction,
                    0,
                    menuButtons.Length - 1
                );
                if (newIndex != selectedButtonIndex)
                    SelectMenuButton(newIndex);
            }

            if (submit && selectedButtonIndex >= 0 && selectedButtonIndex < menuButtons.Length)
            {
                Button button = menuButtons[selectedButtonIndex];
                if (button != null && button.interactable)
                    button.onClick.Invoke();
            }

            if (back)
                Resume();
        }

        private void SetupMenuNavigation()
        {
            if (pauseMenuUI == null)
                return;
            if (navigationInitialized)
            {
                ResetMenuNavigation();
                return;
            }

            Button[] allButtons = pauseMenuUI.GetComponentsInChildren<Button>(true);
            var buttonList = new List<Button>();
            foreach (Button button in allButtons)
            {
                if (button != null && button.interactable && button.gameObject.activeInHierarchy)
                    buttonList.Add(button);
            }

            menuButtons = buttonList.ToArray();
            buttonOutlines = new Outline[menuButtons.Length];
            originalScales = new Vector3[menuButtons.Length];

            for (int i = 0; i < menuButtons.Length; i++)
            {
                Button button = menuButtons[i];
                originalScales[i] = button.transform.localScale;
                Outline outline = button.GetComponent<Outline>();
                if (outline == null)
                    outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = MenuTheme.SelectionOutline;
                outline.effectDistance = MenuTheme.SelectionThickness;
                outline.enabled = false;
                buttonOutlines[i] = outline;

                int index = i;
                EventTrigger trigger = button.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = button.gameObject.AddComponent<EventTrigger>();
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((data) => SelectMenuButton(index));
                trigger.triggers.Add(enterEntry);
            }

            navigationInitialized = menuButtons.Length > 0;
        }

        private void ResetMenuNavigation()
        {
            if (menuButtons == null)
                return;

            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null)
                    continue;
                if (originalScales != null && i < originalScales.Length)
                    menuButtons[i].transform.localScale = originalScales[i];
                if (
                    buttonOutlines != null
                    && i < buttonOutlines.Length
                    && buttonOutlines[i] != null
                )
                    buttonOutlines[i].enabled = false;
            }

            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (
                selected != null
                && pauseMenuUI != null
                && selected.transform.IsChildOf(pauseMenuUI.transform)
            )
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
