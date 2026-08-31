using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BudgetGameDev.Shared
{
    public partial class VirtualController : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int IsiOSMobile();

        [DllImport("__Internal")]
        private static extern int IsAndroidMobile();

        [DllImport("__Internal")]
        private static extern int IsMobileBrowser();

        [DllImport("__Internal")]
        private static extern int IsSafariBrowser();

        [DllImport("__Internal")]
        private static extern void EnableTouchEvents();

        [DllImport("__Internal")]
        private static extern string GetMobileDeviceInfo();
#endif

        [Header("References")]
        [SerializeField]
        private RectTransform joystickBackground;

        [SerializeField]
        private RectTransform joystickHandle;

        [SerializeField]
        private Button actionButton;

        [SerializeField]
        private Button pauseButton;

        [SerializeField]
        private Canvas canvas;

        [Header("Joystick Settings")]
        [SerializeField]
        private float joystickRange = 82f;

        [SerializeField]
        private float deadZone = 0.06f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Values above 1 give the inner stick travel more fine-speed control.")]
        private float analogResponseExponent = 1.25f;

        [Header("Joystick Visuals")]
        [SerializeField]
        private float ringThickness = 12f;

        [SerializeField]
        private Color ringColor = new Color(0.4f, 0.4f, 0.45f, 0.55f);

        [SerializeField]
        private Color fillColor = new Color(0.55f, 0.55f, 0.6f, 0.35f);

        [SerializeField]
        private Color handleColor = new Color(0.15f, 0.15f, 0.2f, 1.0f); // Dark, fully opaque for maximum visibility

        [SerializeField]
        private Color handleBorderColor = new Color(0.5f, 0.5f, 0.55f, 1.0f); // Lighter border for contrast

        [Header("Portrait Position")]
        [SerializeField]
        private Vector2 portraitJoystickAnchor = new Vector2(0.75f, 0.7f);

        [SerializeField]
        private Vector2 portraitButtonAnchor = new Vector2(0.85f, 0.25f);

        [SerializeField]
        private Vector2 portraitPauseButtonAnchor = new Vector2(0.08f, 0.92f);

        [Header("Landscape Position")]
        [SerializeField]
        private Vector2 landscapeJoystickAnchor = new Vector2(0.78f, 0.68f);

        [SerializeField]
        private Vector2 landscapeButtonAnchor = new Vector2(0.85f, 0.5f);

        [SerializeField]
        private Vector2 landscapePauseButtonAnchor = new Vector2(0.06f, 0.9f);

        [Header("Pause Button Visual")]
        [SerializeField]
        private Color pauseButtonColor = new Color(0.2f, 0.2f, 0.25f, 0.85f);

        [SerializeField]
        private Color pauseIconColor = new Color(1f, 1f, 1f, 0.95f);

        private Vector2 joystickInput;
        private bool isDragging;
        private int dragFingerId = -1;
        private bool wasPortrait;
        private float lastOrientationCheck;
        private static Texture2D cachedRingTexture;
        private static Texture2D cachedHandleTexture;
        private static Texture2D cachedPauseButtonTexture;
        private bool isMobileCached;
        private bool isMobileCacheSet;
        private Rect lastSafeArea;

        public Vector2 JoystickInput => joystickInput;
        public static VirtualController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            int showControllerPref = PlayerPrefs.GetInt("ShowVirtualController", -1);

            bool showController;
            if (showControllerPref == 0)
            {
                showController = false;
                Debug.Log("[VirtualController] User selected 'Play' - hiding virtual controller");
            }
            else if (showControllerPref == 1)
            {
                // User pressed "Play on Mobile" - show virtual controller
                showController = true;
                Debug.Log(
                    "[VirtualController] User selected 'Play on Mobile' - showing virtual controller"
                );
            }
            else
            {
                // No preference set, use platform auto-detection
                showController = IsMobilePlatform();
                Debug.Log(
                    $"[VirtualController] No preference set, auto-detecting: {(showController ? "mobile" : "desktop")}"
                );
            }

            // Keep Start and Update consistent with the user choice resolved here.
            isMobileCached = showController;
            isMobileCacheSet = true;
            Debug.Log(
                $"[VirtualController] Awake - Platform: {Application.platform}, DeviceType: {SystemInfo.deviceType}, showController: {showController}"
            );

            // Hide or show based on user choice or platform detection
            if (!showController)
            {
                Debug.Log("[VirtualController] Hiding joystick controls, keeping pause button");
                // Hide joystick and action button
                if (joystickBackground != null)
                    joystickBackground.gameObject.SetActive(false);
                if (actionButton != null)
                    actionButton.gameObject.SetActive(false);
                // Pause button stays visible and managed by the game's pause screen
                return;
            }

            // Enable EnhancedTouch for new Input System (required for iOS)
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
                Debug.Log("[VirtualController] EnhancedTouchSupport enabled");
            }

            // For WebGL on iOS Safari, enable touch events via JavaScript
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                EnableTouchEvents();
                Debug.Log("[VirtualController] WebGL touch events enabled via JavaScript");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[VirtualController] Failed to enable WebGL touch events: {e.Message}"
                );
            }
#endif
            // Show joystick
            if (joystickBackground != null)
                joystickBackground.gameObject.SetActive(true);
            // Action button stays hidden for now (user requested)
            if (actionButton != null)
                actionButton.gameObject.SetActive(false);
            // Pause button visible
            if (pauseButton != null)
                pauseButton.gameObject.SetActive(true);

            Debug.Log("[VirtualController] Visible and ready");
        }

        private bool IsMobilePlatform()
        {
            // Return cached value if already determined
            if (isMobileCacheSet)
                return isMobileCached;

            bool result = CheckIsMobilePlatform();
            isMobileCached = result;
            isMobileCacheSet = true;
            return result;
        }

        private bool CheckIsMobilePlatform()
        {
            // For WebGL builds, use JavaScript-based detection (most reliable for Safari on iOS)
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                bool isMobileWebGL = IsMobileBrowser() == 1;
                bool isiOS = IsiOSMobile() == 1;
                bool isAndroid = IsAndroidMobile() == 1;
                bool isSafari = IsSafariBrowser() == 1;

                Debug.Log(
                    $"[VirtualController] WebGL detection - isMobile: {isMobileWebGL}, iOS: {isiOS}, Android: {isAndroid}, Safari: {isSafari}"
                );

                if (isMobileWebGL || isiOS || isAndroid)
                {
                    Debug.Log("[VirtualController] Mobile browser detected via JavaScript");
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[VirtualController] JavaScript mobile detection failed: {e.Message}"
                );
                // Fall through to other detection methods
            }
#endif
            // Always return true for iOS native builds - most reliable for iPhone
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log("[VirtualController] iOS build detected via preprocessor");
            return true;
#endif
            // Always return true for Android native builds
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[VirtualController] Android build detected via preprocessor");
            return true;
#endif
            bool detected = IsMobileDevice(
                Application.platform,
                SystemInfo.deviceType,
                Input.touchSupported,
#if UNITY_EDITOR
                UnityEngine.Device.SystemInfo.deviceType,
                UnityEngine.Device.Application.isMobilePlatform
#else
                DeviceType.Desktop,
                false
#endif
            );
            Debug.Log($"[VirtualController] Runtime mobile detection: {detected}");
            return detected;
        }

        internal static bool IsMobileDevice(
            RuntimePlatform platform,
            DeviceType deviceType,
            bool touchSupported,
            DeviceType simulatedDeviceType,
            bool simulatedMobile
        ) =>
            platform == RuntimePlatform.IPhonePlayer
            || platform == RuntimePlatform.Android
            || deviceType == DeviceType.Handheld
            || (platform == RuntimePlatform.WebGLPlayer && touchSupported)
            || simulatedDeviceType == DeviceType.Handheld
            || simulatedDeviceType != DeviceType.Desktop
            || simulatedMobile;
    }
}
