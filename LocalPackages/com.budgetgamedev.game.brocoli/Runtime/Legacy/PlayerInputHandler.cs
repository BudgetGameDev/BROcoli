using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Handles player input from keyboard and virtual controller.
    /// Provides raw and smoothed input values for other components.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        private const float InputSmoothSpeed = 15f;
        private const float InputEpsilonSquared = 0.000001f;

        private Vector2 _rawInput;
        private Vector2 _smoothedInput;
        private Vector2 _lastNonZeroInput;
        private VirtualController _virtualController;
        private LastInputPriorityAxis _horizontalWasd;
        private LastInputPriorityAxis _verticalWasd;

        /// <summary>
        /// The unprocessed input direction. Magnitude is 0-1 for analog, exactly 1 for keyboard.
        /// </summary>
        public Vector2 RawInput => _rawInput;

        /// <summary>
        /// Input smoothed over time for gradual transitions.
        /// </summary>
        public Vector2 SmoothedInput => _smoothedInput;

        /// <summary>
        /// The last non-zero input direction (useful for facing direction when stopped).
        /// </summary>
        public Vector2 LastNonZeroInput => _lastNonZeroInput;

        /// <summary>
        /// Whether the player is currently providing any input.
        /// </summary>
        public bool HasInput => _rawInput.sqrMagnitude > InputEpsilonSquared;

        private void Awake()
        {
            // Cache virtual controller reference - may be null on desktop
            _virtualController = FindFirstObjectByType<VirtualController>();
        }

        // NOTE: Input is updated explicitly by PlayerController in FixedUpdate
        // to maintain the same timing as the original code. Do NOT add Update() here.

        /// <summary>
        /// Collects input from keyboard and virtual controller, prioritizing keyboard.
        /// Call this manually if you need input updated at a specific time.
        /// </summary>
        public void UpdateInput()
        {
            // Autoplay/E2E: a bot may drive the player. Inert during normal play
            // (BotDriver.Active is false).
            if (BotDriver.Active)
            {
                ApplyResolvedInput(BotDriver.Move, 0.01f);
                return;
            }

            // Read WASD explicitly. Unity's legacy Horizontal/Vertical axes also
            // contain the arrow keys, which are reserved for overlay navigation.
            Keyboard keyboard = Keyboard.current;
            Vector2 keyboardInput =
                keyboard != null
                    ? ResolveWasd(
                        keyboard.aKey.isPressed,
                        keyboard.dKey.isPressed,
                        keyboard.sKey.isPressed,
                        keyboard.wKey.isPressed
                    )
                    : ResolveWasd(false, false, false, false);

            Gamepad gamepad = Gamepad.current;
            Vector2 gamepadInput =
                gamepad != null ? ClampMovementInput(gamepad.leftStick.ReadValue()) : Vector2.zero;

            // Get virtual controller input (mobile)
            Vector2 virtualInput = Vector2.zero;
            if (_virtualController == null)
            {
                // Try to find it again in case it was instantiated later
                _virtualController = VirtualController.Instance;
            }

            if (_virtualController != null)
            {
                virtualInput = _virtualController.JoystickInput;
            }

            Vector2 targetInput = ResolveMovementInput(keyboardInput, gamepadInput, virtualInput);

            // Keyboard, gamepad, and virtual sticks are screen-relative: pressing
            // up must walk toward the top of the screen. The camera is yawed 45
            // degrees over the world grid, so screen input turns by the same yaw
            // to become the world-space direction everything downstream expects.
            // The bot branch above stays unrotated - it already steers in world
            // space toward world targets.
            targetInput = targetInput.RotatedByYaw(CameraController.WorldYawDegrees);

            ApplyResolvedInput(targetInput, InputEpsilonSquared);
        }

        internal void ApplyResolvedInput(Vector2 targetInput, float facingThreshold)
        {
            _rawInput = targetInput;
            _smoothedInput = Vector2.Lerp(
                _smoothedInput,
                _rawInput,
                InputSmoothSpeed * Time.deltaTime
            );

            if (_rawInput.sqrMagnitude > facingThreshold)
                _lastNonZeroInput = _rawInput.normalized;
        }

        /// <summary>
        /// Resets all input state to zero.
        /// </summary>
        public void ResetInput()
        {
            _rawInput = Vector2.zero;
            _smoothedInput = Vector2.zero;
            _horizontalWasd.ResetHeldState();
            _verticalWasd.ResetHeldState();
        }

        internal Vector2 ResolveWasd(bool left, bool right, bool down, bool up)
        {
            // Tapping the opposing key only turns the player while the other
            // axis is steering: A held with a D tap keeps walking left, but W
            // and A held with a D tap swings the diagonal over to W and D.
            return ClampMovementInput(
                new Vector2(
                    _horizontalWasd.Resolve(left, right, down || up),
                    _verticalWasd.Resolve(down, up, left || right)
                )
            );
        }

        internal static Vector2 ComposeWasd(bool left, bool right, bool down, bool up)
        {
            return ClampMovementInput(
                new Vector2((right ? 1f : 0f) - (left ? 1f : 0f), (up ? 1f : 0f) - (down ? 1f : 0f))
            );
        }

        internal static Vector2 ResolveMovementInput(
            Vector2 keyboardInput,
            Vector2 gamepadInput,
            Vector2 virtualInput
        )
        {
            if (keyboardInput.sqrMagnitude > InputEpsilonSquared)
                return ClampMovementInput(keyboardInput);
            if (gamepadInput.sqrMagnitude > InputEpsilonSquared)
                return ClampMovementInput(gamepadInput);
            return ClampMovementInput(virtualInput);
        }

        private static Vector2 ClampMovementInput(Vector2 input) =>
            input.sqrMagnitude > 1f ? input.normalized : input;

        /// <summary>
        /// Resolves an opposing key pair (SOCD) to the direction used last.
        /// While both are held the axis keeps the direction it last moved, so
        /// tapping the opposite key never reverses the axis on its own. A tap
        /// only takes over when <paramref name="turnaroundAllowed"/> says the
        /// other axis is steering, which turns a diagonal 90 degrees instead of
        /// spinning it around. An axis that has never moved defaults to its
        /// negative key: A beats D, S beats W.
        /// </summary>
        internal struct LastInputPriorityAxis
        {
            private bool _negativeWasHeld;
            private bool _positiveWasHeld;
            private float _lastDirection;

            internal float Resolve(bool negativeHeld, bool positiveHeld, bool turnaroundAllowed)
            {
                bool negativePressed = negativeHeld && !_negativeWasHeld;
                bool positivePressed = positiveHeld && !_positiveWasHeld;
                _negativeWasHeld = negativeHeld;
                _positiveWasHeld = positiveHeld;

                if (negativeHeld && positiveHeld)
                {
                    if (turnaroundAllowed && negativePressed != positivePressed)
                        _lastDirection = positivePressed ? 1f : -1f;
                    else if (_lastDirection == 0f)
                        _lastDirection = -1f;

                    return _lastDirection;
                }

                if (positiveHeld)
                    return _lastDirection = 1f;
                if (negativeHeld)
                    return _lastDirection = -1f;

                // Release leaves _lastDirection alone: it is what a later press
                // of both keys at once falls back to.
                return 0f;
            }

            internal void ResetHeldState()
            {
                _negativeWasHeld = false;
                _positiveWasHeld = false;
            }
        }
    }
}
