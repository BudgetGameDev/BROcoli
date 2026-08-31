using UnityEngine;
using UnityEngine.InputSystem;

namespace BudgetGameDev.Shared
{
    [DefaultExecutionOrder(-1)]
    public partial class InputManager : Singleton<InputManager>
    {
        #region Events

        public delegate void StartTouch(Vector2 position, float time);
        public event StartTouch OnStartTouch;

        public delegate void EndTouch(Vector2 position, float time);
        public event EndTouch OnEndTouch;

        public delegate void SwipeDirection(Vector2 direction);
        public event SwipeDirection OnSwipeDirection;

        public delegate void UP(float axis);
        public event UP OnUP;

        public delegate void DOWN(float axis);
        public event DOWN OnDOWN;

        public delegate void LEFT(float axis);
        public event LEFT OnLEFT;

        public delegate void RIGHT(float axis);
        public event RIGHT OnRIGHT;

        #endregion

        internal Camera mainCamera;

        internal TouchAction touchAction;

        internal void Awake()
        {
            touchAction = new TouchAction();
            mainCamera = Camera.main;
        }

        internal void OnEnable()
        {
            if (touchAction != null)
                touchAction.Enable();
        }

        internal void OnDisable()
        {
            if (touchAction != null)
                touchAction.Disable();
        }

        internal void Start()
        {
            touchAction.Touch.PrimaryContact.started += ctx => StartTouchPrimary(ctx);
            touchAction.Touch.PrimaryContact.canceled += ctx => EndTouchPrimary(ctx);

            touchAction.Touch.UP.performed += ctx => UPPrimary(ctx);
            touchAction.Touch.DOWN.performed += ctx => DOWNPrimary(ctx);
            touchAction.Touch.LEFT.performed += ctx => LEFTPrimary(ctx);
            touchAction.Touch.RIGHT.performed += ctx => RIGHTPrimary(ctx);

            // Activate VirtualController on mobile platforms
            ActivateVirtualControllerIfMobile();
        }

        internal void StartTouchPrimary(InputAction.CallbackContext context)
        {
            if (OnStartTouch != null)
                OnStartTouch(
                    Utils.ScreenToWorld(
                        mainCamera,
                        touchAction.Touch.PrimaryPosition.ReadValue<Vector2>()
                    ),
                    (float)context.startTime
                );
        }

        internal void EndTouchPrimary(InputAction.CallbackContext context)
        {
            if (OnEndTouch != null)
                OnEndTouch(
                    Utils.ScreenToWorld(
                        mainCamera,
                        touchAction.Touch.PrimaryPosition.ReadValue<Vector2>()
                    ),
                    (float)context.time
                );
        }

        internal void UPPrimary(InputAction.CallbackContext context)
        {
            if (OnUP != null)
                OnUP(touchAction.Touch.UP.ReadValue<float>());
        }

        internal void DOWNPrimary(InputAction.CallbackContext context)
        {
            if (OnDOWN != null)
                OnDOWN(touchAction.Touch.DOWN.ReadValue<float>());
        }

        internal void LEFTPrimary(InputAction.CallbackContext context)
        {
            if (OnLEFT != null)
                OnLEFT(touchAction.Touch.LEFT.ReadValue<float>());
        }

        internal void RIGHTPrimary(InputAction.CallbackContext context)
        {
            if (OnRIGHT != null)
                OnRIGHT(touchAction.Touch.RIGHT.ReadValue<float>());
        }

        public Vector2 PrimaryPosition()
        {
            return Utils.ScreenToWorld(
                mainCamera,
                touchAction.Touch.PrimaryPosition.ReadValue<Vector2>()
            );
        }

        public void TriggerSwipeDirection(Vector2 direction)
        {
            if (OnSwipeDirection != null)
                OnSwipeDirection(direction);
        }
    }
}
