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
        private EventSystem suppressedEventSystem;

        /// <summary>
        /// One sampled frame of the devices the launcher reads.
        /// </summary>
        /// <remarks>
        /// Sampling the devices and deciding what the sample means are kept apart,
        /// the way <see cref="LauncherStartup.Resolve"/> takes build membership as a
        /// parameter rather than reading it. Everything worth getting wrong -- which
        /// way the highlight moves, when a held direction repeats, whether a press
        /// starts a game -- then follows from plain values.
        /// </remarks>
        internal readonly struct NavigationInput
        {
            public NavigationInput(bool up, bool down, float stick, bool submit)
            {
                Up = up;
                Down = down;
                Stick = stick;
                Submit = submit;
            }

            /// <summary>Keyboard up, from the arrow key or W.</summary>
            public bool Up { get; }

            /// <summary>Keyboard down, from the arrow key or S.</summary>
            public bool Down { get; }

            /// <summary>Controller vertical axis, from the d-pad or the left stick.</summary>
            public float Stick { get; }

            /// <summary>A confirm press that started this frame.</summary>
            public bool Submit { get; }
        }

        internal void Update() =>
            Apply(ReadDevices(Keyboard.current, Gamepad.current), Time.unscaledTime);

        /// <summary>
        /// Samples the devices, holding no decisions of its own. A controller press
        /// only exists inside a running frame, so this reads and nothing more.
        /// </summary>
        internal static NavigationInput ReadDevices(Keyboard keyboard, Gamepad gamepad)
        {
            Vector2 dpad = gamepad == null ? Vector2.zero : gamepad.dpad.ReadValue();
            Vector2 stick = gamepad == null ? Vector2.zero : gamepad.leftStick.ReadValue();
            bool submit =
                (
                    keyboard != null
                    && (
                        keyboard.enterKey.wasPressedThisFrame
                        || keyboard.numpadEnterKey.wasPressedThisFrame
                        || keyboard.spaceKey.wasPressedThisFrame
                    )
                ) || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            return new NavigationInput(
                keyboard != null && (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed),
                keyboard != null && (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed),
                Mathf.Abs(dpad.y) > 0.5f ? dpad.y : stick.y,
                submit
            );
        }

        /// <summary>Acts on one sampled frame.</summary>
        internal void Apply(NavigationInput input, float now)
        {
            HandleNavigation(NavigationAxis(input), now);

            if (input.Submit)
                LaunchSelected();
        }

        /// <summary>
        /// Up or down as one number. A controller past its halfway point overrides
        /// the keyboard, so a stick held while a key is tapped still wins.
        /// </summary>
        internal static float NavigationAxis(NavigationInput input)
        {
            float vertical = 0f;
            if (input.Up)
                vertical = 1f;
            else if (input.Down)
                vertical = -1f;

            if (Mathf.Abs(input.Stick) > 0.5f)
                vertical = Mathf.Sign(input.Stick);

            return vertical;
        }

        private void SuppressEventSystemNavigation() =>
            SuppressEventSystemNavigation(EventSystem.current);

        /// <summary>
        /// The launcher reads navigation directly so one confirm press cannot also
        /// be submitted by the UI input module to a button selected by the mouse.
        /// Pointer clicks continue to work while navigation events are disabled.
        /// </summary>
        internal void SuppressEventSystemNavigation(EventSystem eventSystem)
        {
            if (eventSystem == null || !eventSystem.sendNavigationEvents)
                return;

            eventSystem.sendNavigationEvents = false;
            suppressedEventSystem = eventSystem;
        }

        /// <summary>
        /// Hands navigation back to the exact event system that was silenced, rather
        /// than to whichever one happens to be current when the launcher goes away.
        /// </summary>
        internal void OnDestroy()
        {
            if (suppressedEventSystem != null)
                suppressedEventSystem.sendNavigationEvents = true;

            suppressedEventSystem = null;
        }

        /// <summary>
        /// Moves once on a fresh press, then repeats after a delay while the
        /// direction is held. <paramref name="now"/> is a parameter so the repeat
        /// timing can be checked without waiting on the clock.
        /// </summary>
        internal void HandleNavigation(float vertical, float now)
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
                nextNavigationTime = now + NavigationRepeatDelay;
            }
            else if (now < nextNavigationTime)
            {
                return;
            }
            else
            {
                nextNavigationTime = now + NavigationRepeatInterval;
            }

            MoveSelection(direction);
        }

        /// <summary>Moves once in display order, wrapping past either end.</summary>
        internal void MoveSelection(int direction)
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

            float correction = ScrollCorrection(rowBounds, viewport.rect);
            if (Mathf.Approximately(correction, 0f))
                return;

            gameListScroll.StopMovement();
            content.anchoredPosition += Vector2.up * correction;
        }

        /// <summary>
        /// How far the content has to move for a row to sit inside the viewport:
        /// zero when it already fits, otherwise the shortest shift that brings the
        /// nearer edge back in.
        /// </summary>
        internal static float ScrollCorrection(Bounds rowBounds, Rect viewport)
        {
            if (rowBounds.max.y > viewport.yMax)
                return viewport.yMax - rowBounds.max.y;

            if (rowBounds.min.y < viewport.yMin)
                return viewport.yMin - rowBounds.min.y;

            return 0f;
        }
    }
}
