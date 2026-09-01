using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void UpdateHdrDetailsInput()
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
            ProcessHdrDetailsInput(cancel, vertical, horizontal, submit, Time.unscaledTime);
        }

        internal void ProcessHdrDetailsInput(
            bool cancel,
            float vertical,
            float horizontal,
            bool submit,
            float now
        )
        {
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                CloseHdrDetails();
                return;
            }

            if (now - lastHdrDetailsNavTime >= 0.18f)
            {
                int delta = 0;
                if (Mathf.Abs(horizontal) > 0.5f)
                    delta = horizontal > 0f ? 1 : -1;
                else if (Mathf.Abs(vertical) > 0.5f)
                    delta = vertical > 0f ? -2 : 2;
                if (delta != 0)
                {
                    lastHdrDetailsNavTime = now;
                    SelectHdrDetailsControl(selectedHdrDetailsControl + delta);
                }
            }

            if (!submit || !MenuInputGate.TryConsumeSubmit())
                return;
            if (
                hdrDetailsSelectables[selectedHdrDetailsControl] is Button button
                && button.interactable
            )
                button.onClick.Invoke();
        }
    }
}
