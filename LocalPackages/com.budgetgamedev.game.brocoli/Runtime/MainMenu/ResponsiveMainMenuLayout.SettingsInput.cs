using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void Update()
        {
            if (SystemReadinessSession.IsOpen)
                return;
            if (SavesOpen)
            {
                UpdateSavesInput();
                return;
            }

            if (CreditsOpen)
            {
                UpdateCreditsInput();
                return;
            }

            if (!SettingsOpen)
                return;
            if (HdrCalibrationOpen)
            {
                UpdateHdrCalibrationInput();
                return;
            }
            if (HdrDetailsOpen)
            {
                UpdateHdrDetailsInput();
                return;
            }
            if (nvidiaPage?.IsOpen == true)
                return;
            UpdateSettingsInput();
        }

        private void UpdateSettingsInput()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool cancel =
                keyboard?.escapeKey.wasPressedThisFrame == true
                || gamepad?.buttonEast.wasPressedThisFrame == true;
            float vertical =
                keyboard?.upArrowKey.isPressed == true || keyboard?.wKey.isPressed == true ? 1f
                : keyboard?.downArrowKey.isPressed == true || keyboard?.sKey.isPressed == true ? -1f
                : 0f;
            float horizontal =
                keyboard?.rightArrowKey.isPressed == true || keyboard?.dKey.isPressed == true ? 1f
                : keyboard?.leftArrowKey.isPressed == true || keyboard?.aKey.isPressed == true ? -1f
                : 0f;
            if (gamepad != null)
            {
                Vector2 axis = gamepad.dpad.ReadValue();
                if (axis.sqrMagnitude < 0.25f)
                    axis = gamepad.leftStick.ReadValue();
                Vector2 navigation = ResolveGamepadAxis(axis, vertical, horizontal);
                vertical = navigation.y;
                horizontal = navigation.x;
            }
            bool submit =
                keyboard?.enterKey.wasPressedThisFrame == true
                || keyboard?.numpadEnterKey.wasPressedThisFrame == true
                || keyboard?.spaceKey.wasPressedThisFrame == true
                || gamepad?.buttonSouth.wasPressedThisFrame == true;
            ProcessSettingsInput(cancel, vertical, horizontal, submit, Time.unscaledTime);
        }

        internal void ProcessSettingsInput(
            bool cancel,
            float vertical,
            float horizontal,
            bool submit,
            float now
        )
        {
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                CloseSettings();
                return;
            }

            if (now - lastSettingsNavTime >= 0.18f)
            {
                if (Mathf.Abs(vertical) > 0.5f)
                {
                    lastSettingsNavTime = now;
                    SelectSetting(selectedSetting + (vertical > 0f ? -1 : 1));
                }
                else if (Mathf.Abs(horizontal) > 0.5f)
                {
                    if (selectedSetting < volumeSliders.Length)
                    {
                        lastSettingsNavTime = now;
                        volumeSliders[selectedSetting].value += Mathf.Sign(horizontal) * 0.05f;
                    }
                    else if (settingsSelectables[selectedSetting] == hdrToggleButton)
                    {
                        lastSettingsNavTime = now;
                        GameDisplaySettings.ToggleHdr();
                    }
                }
            }

            if (
                submit
                && settingsSelectables[selectedSetting] is Button button
                && MenuInputGate.TryConsumeSubmit()
            )
                button.onClick.Invoke();
        }

        private void RegisterPointerSelection()
        {
            for (int i = 0; i < settingsSelectables.Length; i++)
            {
                int index = i;
                EventTrigger trigger = settingsSelectables[i]
                    .gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selectedSetting = index);
                trigger.triggers.Add(entry);
            }
        }

        private void SelectSetting(int index, bool playSound = true)
        {
            selectedSetting = (index + settingsSelectables.Length) % settingsSelectables.Length;
            EventSystem.current?.SetSelectedGameObject(
                settingsSelectables[selectedSetting].gameObject
            );
            if (playSound)
                ProceduralUIAudio.PlayHover();
        }
    }
}
