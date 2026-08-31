using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class LevelUpScreen
    {
        private void HandleControllerNavigation()
        {
            Keyboard keyboard = Keyboard.current;
            Vector2 dpad = Gamepad.current?.dpad.ReadValue() ?? Vector2.zero;
            Vector2 stick = Gamepad.current?.leftStick.ReadValue() ?? Vector2.zero;
            float horizontal = ResolveHorizontalInput(
                keyboard?.leftArrowKey.isPressed == true || keyboard?.aKey.isPressed == true,
                keyboard?.rightArrowKey.isPressed == true || keyboard?.dKey.isPressed == true,
                dpad,
                stick
            );

            bool submit =
                keyboard?.enterKey.wasPressedThisFrame == true
                || keyboard?.spaceKey.wasPressedThisFrame == true
                || Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
            ProcessNavigation(horizontal, submit, Time.unscaledTime);
        }

        internal static float ResolveHorizontalInput(
            bool left,
            bool right,
            Vector2 dpad,
            Vector2 stick
        )
        {
            if (Mathf.Abs(dpad.x) > 0.5f)
                return Mathf.Sign(dpad.x);
            if (Mathf.Abs(stick.x) > 0.5f)
                return Mathf.Sign(stick.x);
            if (left)
                return -1f;
            return right ? 1f : 0f;
        }

        internal void ProcessNavigation(float horizontal, bool submit, float now)
        {
            if (now - lastNavTime < NavRepeatDelay)
                return;
            if (Mathf.Abs(horizontal) > 0.1f)
            {
                lastNavTime = now;
                int newIndex = selectedIndex + (int)Mathf.Sign(horizontal);
                newIndex = Mathf.Clamp(newIndex, 0, 2);
                SetSelectedIndex(newIndex);
            }

            if (submit)
            {
                if (hasPendingSelection)
                    ConfirmSelectedUpgrade();
                else
                    ChooseUpgrade(selectedIndex);
            }
        }

        private void UpdateConfirmButton()
        {
            if (confirmButton == null)
                return;

            confirmButton.interactable = hasPendingSelection && isShowing;
            if (confirmButtonText == null)
                return;

            if (!hasPendingSelection || currentOptions[selectedIndex] == null)
            {
                confirmButtonText.text = "SELECT AN UPGRADE";
                return;
            }

            confirmButtonText.text =
                $"CONFIRM {currentOptions[selectedIndex].DisplayName.ToUpperInvariant()}";
        }

        private void SetSelectedIndex(int index)
        {
            if (index < 0 || index > 2)
                return;

            // Play hover sound if index changed
            if (index != selectedIndex)
            {
                ProceduralUIAudio.PlayHover();
            }

            selectedIndex = index;

            // Update EventSystem selection
            if (choiceButtons[selectedIndex] != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(choiceButtons[selectedIndex].gameObject);
            }

            if (hasPendingSelection)
                UpdateConfirmButton();
        }

        private void UpdateSelectionVisuals()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null)
                    continue;

                bool isSelected = (i == selectedIndex);

                // Update outline
                if (buttonOutlines[i] != null)
                {
                    buttonOutlines[i].enabled = isSelected;
                }

                // Animate scale
                RectTransform rt = choiceButtons[i].GetComponent<RectTransform>();
                if (rt != null && i < originalScales.Length)
                {
                    float targetScale = isSelected ? selectedScale : normalScale;
                    Vector3 target = originalScales[i] * targetScale;
                    rt.localScale = Vector3.Lerp(
                        rt.localScale,
                        target,
                        Time.unscaledDeltaTime * scaleAnimSpeed
                    );
                }
            }
        }

        private void NavigateSelection(int direction)
        {
            selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, 2);
            SetSelectedIndex(selectedIndex);
        }

        private void EnsureEventSystemActive()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();

            if (eventSystem == null)
            {
                var allES = FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );
                if (allES.Length > 0)
                {
                    eventSystem = allES[0];
                }
            }

            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem_LevelUp");
                eventSystem = esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
            else if (!eventSystem.gameObject.activeInHierarchy)
            {
                eventSystem.gameObject.SetActive(true);
            }
        }
    }
}
