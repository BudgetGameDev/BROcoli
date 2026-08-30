using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public partial class LevelUpScreen
{
    private void HandleControllerNavigation()
    {
        // Rate limit navigation
        if (Time.unscaledTime - lastNavTime < NavRepeatDelay)
            return;

        float horizontal = 0f;

        // Keyboard arrows
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            horizontal = -1f;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            horizontal = 1f;

        // Gamepad
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            Vector2 stick = Gamepad.current.leftStick.ReadValue();

            if (Mathf.Abs(dpad.x) > 0.5f)
                horizontal = Mathf.Sign(dpad.x);
            else if (Mathf.Abs(stick.x) > 0.5f)
                horizontal = Mathf.Sign(stick.x);
        }

        // Navigate
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            lastNavTime = Time.unscaledTime;
            int newIndex = selectedIndex + (int)Mathf.Sign(horizontal);
            newIndex = Mathf.Clamp(newIndex, 0, 2);
            SetSelectedIndex(newIndex);
        }

        // Submit with Enter/Space/Gamepad A
        bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            submit = true;
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
