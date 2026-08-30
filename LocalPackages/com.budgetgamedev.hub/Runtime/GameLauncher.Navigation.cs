using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BudgetGameDev.Hub
{
    /// <summary>Keyboard and controller navigation for the game picker.</summary>
    public sealed partial class GameLauncher
    {
        private const float NavigationRepeatDelay = 0.35f;
        private const float NavigationRepeatInterval = 0.14f;

        private int navigationDirection;
        private float nextNavigationTime;
        private bool suppressedNavigationEvents;

        private void Update()
        {
            HandleNavigation(ReadNavigationAxis());

            if (SubmitWasPressed())
                LaunchSelected();
        }

        /// <summary>
        /// The launcher reads navigation directly so one confirm press cannot also
        /// be submitted by the UI input module to a button selected by the mouse.
        /// Pointer clicks continue to work while navigation events are disabled.
        /// </summary>
        private void SuppressEventSystemNavigation()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || !eventSystem.sendNavigationEvents)
                return;

            eventSystem.sendNavigationEvents = false;
            suppressedNavigationEvents = true;
        }

        private void OnDestroy()
        {
            if (suppressedNavigationEvents && EventSystem.current != null)
                EventSystem.current.sendNavigationEvents = true;
        }

        /// <summary>Up/down from the keyboard, d-pad, or controller left stick.</summary>
        private static float ReadNavigationAxis()
        {
            float vertical = 0f;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
                    vertical = 1f;
                else if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
                    vertical = -1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 axis = gamepad.dpad.ReadValue();
                if (Mathf.Abs(axis.y) <= 0.5f)
                    axis = gamepad.leftStick.ReadValue();
                if (Mathf.Abs(axis.y) > 0.5f)
                    vertical = Mathf.Sign(axis.y);
            }

            return vertical;
        }

        private void HandleNavigation(float vertical)
        {
            if (Mathf.Abs(vertical) < 0.5f)
            {
                navigationDirection = 0;
                return;
            }

            int direction = vertical > 0f ? -1 : 1;
            if (direction != navigationDirection)
            {
                navigationDirection = direction;
                nextNavigationTime = Time.unscaledTime + NavigationRepeatDelay;
            }
            else if (Time.unscaledTime < nextNavigationTime)
            {
                return;
            }
            else
            {
                nextNavigationTime = Time.unscaledTime + NavigationRepeatInterval;
            }

            MoveSelection(direction);
        }

        /// <summary>Moves once in display order, wrapping past either end.</summary>
        private void MoveSelection(int direction)
        {
            if (entries.Count == 0 || direction == 0)
                return;

            int candidate = selectedIndex;
            for (int attempt = 0; attempt < entries.Count; attempt++)
            {
                candidate = (candidate + direction + entries.Count) % entries.Count;
                if (!entries[candidate].Game.IsPlayable)
                    continue;

                Select(candidate);
                return;
            }
        }

        private static bool SubmitWasPressed()
        {
            Keyboard keyboard = Keyboard.current;
            bool keyboardSubmit =
                keyboard != null
                && (
                    keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame
                );

            Gamepad gamepad = Gamepad.current;
            return keyboardSubmit || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        }

        /// <summary>Keeps controller selection visible in a list longer than the viewport.</summary>
        private void KeepSelectedRowVisible()
        {
            if (
                gameListScroll == null
                || selectedIndex < 0
                || selectedIndex >= entries.Count
                || entries[selectedIndex].Button == null
            )
                return;

            Canvas.ForceUpdateCanvases();

            RectTransform viewport = gameListScroll.viewport;
            RectTransform content = gameListScroll.content;
            Bounds rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewport,
                entries[selectedIndex].Button.transform
            );

            float correction = 0f;
            if (rowBounds.max.y > viewport.rect.yMax)
                correction = viewport.rect.yMax - rowBounds.max.y;
            else if (rowBounds.min.y < viewport.rect.yMin)
                correction = viewport.rect.yMin - rowBounds.min.y;

            if (Mathf.Approximately(correction, 0f))
                return;

            gameListScroll.StopMovement();
            content.anchoredPosition += Vector2.up * correction;
        }
    }
}
