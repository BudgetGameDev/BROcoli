using UnityEngine;

/// <summary>
/// Handles player input from keyboard and virtual controller.
/// Provides raw and smoothed input values for other components.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    private const float InputSmoothSpeed = 15f;
    
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
    public bool HasInput => _rawInput.sqrMagnitude > 0.01f;

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
        // Get keyboard input
        Vector2 keyboardInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        
        // Normalize if exceeds unit circle (diagonal movement)
        if (keyboardInput.sqrMagnitude > 1f)
        {
            keyboardInput = keyboardInput.normalized;
        }

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

        // Prioritize keyboard over virtual controller
        Vector2 targetInput;
        if (keyboardInput.sqrMagnitude > 0.01f)
        {
            targetInput = keyboardInput;
        }
        else if (virtualInput.sqrMagnitude > 0.01f)
        {
            targetInput = virtualInput;
        }
        else
        {
            targetInput = Vector2.zero;
        }

        _rawInput = targetInput;

        // Update smoothed input
        _smoothedInput = Vector2.Lerp(_smoothedInput, _rawInput, InputSmoothSpeed * Time.deltaTime);

        // Track last non-zero input for facing direction
        if (_rawInput.sqrMagnitude > 0.01f)
        {
            _lastNonZeroInput = _rawInput.normalized;
        }
    }

    /// <summary>
    /// Resets all input state to zero.
    /// </summary>
    public void ResetInput()
    {
        _rawInput = Vector2.zero;
        _smoothedInput = Vector2.zero;
    }
}
