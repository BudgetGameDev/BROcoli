using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsivePauseMenuLayout
    {
        public void HandleSettingsInput(Keyboard keyboard, Gamepad gamepad)
        {
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
                Vector2 navigation = ResolvePauseGamepadAxis(axis, vertical, horizontal);
                vertical = navigation.y;
                horizontal = navigation.x;
            }
            bool submit =
                keyboard?.enterKey.wasPressedThisFrame == true
                || keyboard?.numpadEnterKey.wasPressedThisFrame == true
                || keyboard?.spaceKey.wasPressedThisFrame == true
                || gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (HdrCalibrationOpen)
                ProcessPauseHdrCalibrationInput(
                    cancel,
                    vertical,
                    horizontal,
                    submit,
                    Time.unscaledTime
                );
            else if (HdrDetailsOpen)
                ProcessPauseHdrDetailsInput(
                    cancel,
                    vertical,
                    horizontal,
                    submit,
                    Time.unscaledTime
                );
            else
                ProcessPauseSettingsInput(cancel, vertical, horizontal, submit, Time.unscaledTime);
        }

        internal static Vector2 ResolvePauseGamepadAxis(
            Vector2 axis,
            float vertical,
            float horizontal
        ) => axis.sqrMagnitude >= 0.25f ? axis : new Vector2(horizontal, vertical);

        internal void ProcessPauseSettingsInput(
            bool cancel,
            float vertical,
            float horizontal,
            bool submit,
            float now
        )
        {
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                HideSettings();
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
                        SyncSettings();
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

        internal void ProcessPauseHdrDetailsInput(
            bool cancel,
            float vertical,
            float horizontal,
            bool submit,
            float now
        )
        {
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                HideHdrDetails();
                return;
            }
            if (now - lastHdrDetailNavTime >= 0.18f)
            {
                int delta =
                    Mathf.Abs(horizontal) > 0.5f ? (horizontal > 0f ? 1 : -1)
                    : Mathf.Abs(vertical) > 0.5f ? (vertical > 0f ? -2 : 2)
                    : 0;
                if (delta != 0)
                {
                    lastHdrDetailNavTime = now;
                    SelectHdrDetail(selectedHdrDetail + delta);
                }
            }
            if (!submit || !MenuInputGate.TryConsumeSubmit())
                return;
            if (hdrDetailSelectables[selectedHdrDetail] is Button button && button.interactable)
                button.onClick.Invoke();
        }

        internal void ProcessPauseHdrCalibrationInput(
            bool cancel,
            float vertical,
            float horizontal,
            bool submit,
            float now
        )
        {
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                EndHdrCalibration(false);
                return;
            }
            if (now - lastHdrCalibrationNavTime >= 0.18f)
            {
                if (Mathf.Abs(vertical) > 0.5f)
                {
                    lastHdrCalibrationNavTime = now;
                    SelectPauseHdrCalibrationControl(
                        selectedHdrCalibrationControl + (vertical > 0f ? -1 : 1)
                    );
                }
                else if (
                    Mathf.Abs(horizontal) > 0.5f
                    && hdrCalibrationSelectables[selectedHdrCalibrationControl]
                        == hdrCalibrationSlider
                )
                {
                    lastHdrCalibrationNavTime = now;
                    hdrCalibrationSlider.value +=
                        Mathf.Sign(horizontal)
                        * (
                            hdrCalibrationStep == PauseHdrCalibrationStep.PeakBrightness
                                ? 25f
                                : 0.02f
                        );
                }
            }
            if (!submit || !MenuInputGate.TryConsumeSubmit())
                return;
            Selectable selected = hdrCalibrationSelectables[selectedHdrCalibrationControl];
            if (selected is Button button)
                button.onClick.Invoke();
            else if (selected == hdrCalibrationSlider)
                NextPauseHdrCalibrationStep();
        }

        private static void Select(Selectable selectable) =>
            EventSystem.current?.SetSelectedGameObject(selectable?.gameObject);
    }
}
