using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BudgetGameDev.Shared
{
    public partial class VirtualController
    {
        private void Update()
        {
            // Only process joystick input on mobile
            if (!IsMobilePlatform())
                return;

            // Check for orientation changes using screen dimensions (more reliable than Screen.orientation)
            // Only check every 0.1 seconds to avoid constant recalculations
            if (Time.unscaledTime - lastOrientationCheck > 0.1f)
            {
                lastOrientationCheck = Time.unscaledTime;
                bool isPortrait = Screen.height > Screen.width;

                bool safeAreaChanged = Screen.safeArea != lastSafeArea;
                if (isPortrait != wasPortrait || safeAreaChanged)
                {
                    wasPortrait = isPortrait;
                    lastSafeArea = Screen.safeArea;
                    Debug.Log(
                        $"[VirtualController] Orientation changed - isPortrait: {isPortrait}, screen: {Screen.width}x{Screen.height}"
                    );
                    UpdateLayoutForOrientation();
                }
            }

            HandleJoystickInput();
        }

        private void SetupActionButton()
        {
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(OnActionButtonPressed);
            }
        }

        private void OnActionButtonPressed()
        {
            // Simulate Enter/Space key press for menu selection
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                // Don't submit to the action button itself (prevents infinite recursion)
                if (eventSystem.currentSelectedGameObject == actionButton.gameObject)
                    return;

                ExecuteEvents.Execute(
                    eventSystem.currentSelectedGameObject,
                    new BaseEventData(eventSystem),
                    ExecuteEvents.submitHandler
                );
            }
        }

        private void HandleJoystickInput()
        {
            if (joystickBackground == null || joystickHandle == null)
                return;

            // Use EnhancedTouch API (works reliably on iOS and Android)
            var activeTouches = Touch.activeTouches;

            for (int i = 0; i < activeTouches.Count; i++)
            {
                var touch = activeTouches[i];
                ProcessTouch(touch.phase, touch.finger.index, touch.screenPosition);
            }

            // Desktop players can explicitly enable the virtual controller too.
#if UNITY_EDITOR || UNITY_STANDALONE
            var mouse = Mouse.current;
            if (activeTouches.Count == 0 && mouse != null)
            {
                ProcessMouse(
                    mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.isPressed,
                    mouse.leftButton.wasReleasedThisFrame,
                    mouse.position.ReadValue()
                );
            }
#endif
        }

        internal void ProcessTouch(TouchPhase phase, int fingerId, Vector2 screenPosition)
        {
            if (phase == TouchPhase.Began)
            {
                if (IsTouchOnJoystick(screenPosition) && !isDragging)
                {
                    isDragging = true;
                    dragFingerId = fingerId;
                    UpdateJoystickPosition(screenPosition);
                    Debug.Log(
                        $"[VirtualController] Touch began on joystick, finger: {dragFingerId}"
                    );
                }
                return;
            }

            if (fingerId != dragFingerId)
                return;
            if (phase == TouchPhase.Moved || phase == TouchPhase.Stationary)
                UpdateJoystickPosition(screenPosition);
            else if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                ResetJoystick();
        }

        internal void ProcessMouse(bool pressed, bool held, bool released, Vector2 screenPosition)
        {
            if (pressed && IsTouchOnJoystick(screenPosition))
                isDragging = true;
            if (isDragging && held)
                UpdateJoystickPosition(screenPosition);
            if (released)
                ResetJoystick();
        }

        private bool IsTouchOnJoystick(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBackground,
                screenPosition,
                canvas.worldCamera,
                out Vector2 localPoint
            );

            float radius = joystickBackground.sizeDelta.x * 0.5f;
            return localPoint.magnitude <= radius * 1.5f; // Slightly larger touch area
        }

        private void UpdateJoystickPosition(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBackground,
                screenPosition,
                canvas.worldCamera,
                out Vector2 localPoint
            );

            // Clamp to joystick range
            Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, joystickRange);
            joystickHandle.anchoredPosition = clampedPoint;

            joystickInput = VirtualJoystickMath.AnalogInput(
                clampedPoint,
                joystickRange,
                deadZone,
                analogResponseExponent
            );
        }

        private void ResetJoystick()
        {
            isDragging = false;
            dragFingerId = -1;
            joystickInput = Vector2.zero;
            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }
        }

        private void UpdateLayoutForOrientation()
        {
            bool isPortrait = Screen.height > Screen.width;

            Vector2 joystickAnchor = isPortrait ? portraitJoystickAnchor : landscapeJoystickAnchor;
            Vector2 buttonAnchor = isPortrait ? portraitButtonAnchor : landscapeButtonAnchor;
            Vector2 pauseAnchor = isPortrait
                ? portraitPauseButtonAnchor
                : landscapePauseButtonAnchor;

            if (joystickBackground != null)
            {
                joystickAnchor = ClampAnchorToSafeArea(joystickBackground, joystickAnchor);
                joystickBackground.anchorMin = joystickAnchor;
                joystickBackground.anchorMax = joystickAnchor;
                joystickBackground.anchoredPosition = Vector2.zero;
                // Force layout rebuild
                LayoutRebuilder.ForceRebuildLayoutImmediate(joystickBackground);
            }

            if (actionButton != null)
            {
                RectTransform buttonRect = actionButton.GetComponent<RectTransform>();
                buttonRect.anchorMin = buttonAnchor;
                buttonRect.anchorMax = buttonAnchor;
                buttonRect.anchoredPosition = Vector2.zero;
                // Force layout rebuild
                LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRect);
            }

            if (pauseButton != null)
            {
                RectTransform pauseRect = pauseButton.GetComponent<RectTransform>();
                pauseAnchor = ClampAnchorToSafeArea(pauseRect, pauseAnchor);
                pauseRect.anchorMin = pauseAnchor;
                pauseRect.anchorMax = pauseAnchor;
                pauseRect.anchoredPosition = Vector2.zero;
                // Force layout rebuild
                LayoutRebuilder.ForceRebuildLayoutImmediate(pauseRect);
            }

            // Force canvas update
            Canvas.ForceUpdateCanvases();
        }

        private Vector2 ClampAnchorToSafeArea(RectTransform control, Vector2 desiredAnchor)
        {
            if (control == null || canvas == null || Screen.width <= 0 || Screen.height <= 0)
                return desiredAnchor;

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect.rect.width <= 0f || canvasRect.rect.height <= 0f)
                return desiredAnchor;

            Rect safeArea = Screen.safeArea;
            Vector2 safeMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height
            );
            Vector2 safeMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height
            );
            Vector2 halfExtent = new Vector2(
                control.rect.width / (canvasRect.rect.width * 2f),
                control.rect.height / (canvasRect.rect.height * 2f)
            );
            const float normalizedPadding = 0.015f;

            return new Vector2(
                Mathf.Clamp(
                    desiredAnchor.x,
                    safeMin.x + halfExtent.x + normalizedPadding,
                    safeMax.x - halfExtent.x - normalizedPadding
                ),
                Mathf.Clamp(
                    desiredAnchor.y,
                    safeMin.y + halfExtent.y + normalizedPadding,
                    safeMax.y - halfExtent.y - normalizedPadding
                )
            );
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDisable()
        {
            // Clean up EnhancedTouch when disabled
            if (EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Disable();
            }
        }

        private void OnEnable()
        {
            // Re-enable EnhancedTouch when re-enabled
            if (!EnhancedTouchSupport.enabled && IsMobilePlatform())
            {
                EnhancedTouchSupport.Enable();
            }
        }
    }
}
