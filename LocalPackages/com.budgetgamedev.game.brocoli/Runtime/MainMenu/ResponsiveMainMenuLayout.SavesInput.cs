using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void UpdateSavesInput()
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
            ProcessSavesInput(cancel, vertical, horizontal, submit, Time.unscaledTime);
        }

        internal void ProcessSavesInput(
            bool cancel,
            float vertical,
            float horizontal,
            bool submit,
            float now
        )
        {
            if (cancel && MenuInputGate.TryConsumeCancel())
            {
                CloseSaves();
                return;
            }

            if (now - lastSavesNavTime >= 0.18f)
            {
                if (Mathf.Abs(vertical) > 0.5f)
                {
                    lastSavesNavTime = now;
                    FocusSave(savesFocus + (vertical > 0f ? -1 : 1));
                }
                else if (Mathf.Abs(horizontal) > 0.5f && OnActionLine)
                {
                    lastSavesNavTime = now;
                    FocusAction(horizontal > 0f ? 1 : 0);
                }
            }

            if (
                submit
                && CurrentSaveSelectable() is Button button
                && button.interactable
                && MenuInputGate.TryConsumeSubmit()
            )
            {
                button.onClick.Invoke();
            }
        }

        internal static Vector2 ResolveGamepadAxis(
            Vector2 axis,
            float keyboardVertical,
            float keyboardHorizontal
        )
        {
            float vertical = Mathf.Abs(axis.y) > 0.5f ? Mathf.Sign(axis.y) : keyboardVertical;
            float horizontal = Mathf.Abs(axis.x) > 0.5f ? Mathf.Sign(axis.x) : keyboardHorizontal;
            return new Vector2(horizontal, vertical);
        }
    }
}
