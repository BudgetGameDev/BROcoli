using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void RegisterHdrCalibrationPointerSelection()
        {
            for (int index = 0; index < hdrCalibrationSelectables.Length; index++)
            {
                int capturedIndex = index;
                EventTrigger trigger = hdrCalibrationSelectables[index]
                    .gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selectedHdrCalibrationControl = capturedIndex);
                trigger.triggers.Add(entry);
            }
        }

        private void SelectHdrCalibrationControl(int index, bool playSound = true)
        {
            selectedHdrCalibrationControl =
                (index + hdrCalibrationSelectables.Length) % hdrCalibrationSelectables.Length;
            EventSystem.current?.SetSelectedGameObject(
                hdrCalibrationSelectables[selectedHdrCalibrationControl].gameObject
            );
            if (playSound)
                ProceduralUIAudio.PlayHover();
        }

        private void UpdateHdrCalibrationInput()
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
            ProcessHdrCalibrationInput(cancel, vertical, horizontal, submit, Time.unscaledTime);
        }

        internal void ProcessHdrCalibrationInput(
            bool cancel,
            float vertical,
            float horizontal,
            bool submit,
            float now
        )
        {
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                CloseHdrCalibration(false);
                return;
            }

            if (now - lastHdrCalibrationNavTime >= 0.18f)
            {
                if (Mathf.Abs(vertical) > 0.5f)
                {
                    lastHdrCalibrationNavTime = now;
                    SelectHdrCalibrationControl(
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
                    float direction = Mathf.Sign(horizontal);
                    switch (hdrCalibrationStep)
                    {
                        case HdrCalibrationStep.PeakBrightness:
                            hdrCalibrationSlider.value += direction * 25f;
                            break;
                        default:
                            hdrCalibrationSlider.value += direction * 0.02f;
                            break;
                    }
                }
            }

            if (!submit || !MenuInputGate.TryConsumeSubmit())
                return;

            Selectable selected = hdrCalibrationSelectables[selectedHdrCalibrationControl];
            if (selected is Button button)
                button.onClick.Invoke();
            else if (selected == hdrCalibrationSlider)
                NextHdrCalibrationStep();
        }
    }
}
