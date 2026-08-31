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
                    ? ComposeWasd(
                        keyboard.aKey.isPressed,
                        keyboard.dKey.isPressed,
                        keyboard.sKey.isPressed,
                        keyboard.wKey.isPressed
                    )
                    : Vector2.zero;

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
    }
}
