using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BudgetGameDev.Shared
{
    public class VirtualController : MonoBehaviour
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

        private void Start()
        {
            bool isMobile = IsMobilePlatform();

            // Always setup pause button (it handles its own visibility)
            SetupPauseButton();
            SetupPauseButtonVisual();

            // Clear cached textures to ensure colors are current (important after code changes)
            cachedRingTexture = null;
            cachedHandleTexture = null;
            cachedPauseButtonTexture = null;

            if (isMobile)
            {
                SetupActionButton();
                SetupJoystickVisuals();
                wasPortrait = Screen.height > Screen.width;
                lastSafeArea = Screen.safeArea;
                UpdateLayoutForOrientation();
            }
        }

        private void SetupJoystickVisuals()
        {
            // Create and apply ring sprite for background
            if (joystickBackground != null)
            {
                Image bgImage = joystickBackground.GetComponent<Image>();
                if (bgImage != null)
                {
                    if (cachedRingTexture == null)
                    {
                        cachedRingTexture = CreateRingTexture(
                            128,
                            ringThickness,
                            ringColor,
                            fillColor
                        );
                    }
                    Sprite ringSprite = Sprite.Create(
                        cachedRingTexture,
                        new Rect(0, 0, 128, 128),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    bgImage.sprite = ringSprite;
                    bgImage.type = Image.Type.Simple;
                    bgImage.color = Color.white; // Use white to show texture colors as-is
                }
            }

            // Create and apply circle sprite for handle - make it visually distinct
            if (joystickHandle != null)
            {
                Image handleImage = joystickHandle.GetComponent<Image>();
                if (handleImage != null)
                {
                    if (cachedHandleTexture == null)
                    {
                        cachedHandleTexture = CreateCircleTexture(
                            64,
                            handleColor,
                            handleBorderColor
                        );
                    }
                    Sprite handleSprite = Sprite.Create(
                        cachedHandleTexture,
                        new Rect(0, 0, 64, 64),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    handleImage.sprite = handleSprite;
                    handleImage.type = Image.Type.Simple;
                    handleImage.color = Color.white; // Use white to show texture colors as-is

                    // Ensure handle is rendered on top of background
                    handleImage.raycastTarget = false;
                }
            }
        }

        private Texture2D CreateRingTexture(int size, float thickness, Color ringCol, Color fillCol)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float outerRadius = center - 2f;
            float innerRadius = outerRadius - thickness;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance > outerRadius + 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (distance > outerRadius - 1f)
                    {
                        float alpha = Mathf.Clamp01(outerRadius + 1f - distance);
                        pixels[y * size + x] = new Color(
                            ringCol.r,
                            ringCol.g,
                            ringCol.b,
                            ringCol.a * alpha
                        );
                    }
                    else if (distance > innerRadius + 1f)
                    {
                        pixels[y * size + x] = ringCol;
                    }
                    else if (distance > innerRadius - 1f)
                    {
                        float t = Mathf.Clamp01(distance - innerRadius + 1f);
                        pixels[y * size + x] = Color.Lerp(fillCol, ringCol, t);
                    }
                    else
                    {
                        pixels[y * size + x] = fillCol;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private Texture2D CreateCircleTexture(int size, Color col, Color borderCol)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float radius = center - 2f;
            float borderWidth = 3f; // Border thickness in pixels
            float innerRadius = radius - borderWidth;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance > radius + 1f)
                    {
                        // Outside circle - transparent
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (distance > radius - 1f)
                    {
                        // Outer edge anti-aliasing
                        float alpha = Mathf.Clamp01(radius + 1f - distance);
                        pixels[y * size + x] = new Color(
                            borderCol.r,
                            borderCol.g,
                            borderCol.b,
                            borderCol.a * alpha
                        );
                    }
                    else if (distance > innerRadius)
                    {
                        // Border region
                        pixels[y * size + x] = borderCol;
                    }
                    else if (distance > innerRadius - 1f)
                    {
                        // Inner edge anti-aliasing (border to fill transition)
                        float t = Mathf.Clamp01(innerRadius - distance + 1f);
                        pixels[y * size + x] = Color.Lerp(borderCol, col, t);
                    }
                    else
                    {
                        // Inner fill
                        pixels[y * size + x] = col;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void SetupPauseButton()
        {
            if (pauseButton != null)
            {
                // Connect to whatever pause screen this game provides
                IPauseController pauseMenu = PauseControllerLocator.Find();
                if (pauseMenu != null)
                {
                    pauseButton.onClick.RemoveAllListeners();
                    pauseButton.onClick.AddListener(() => pauseMenu.TogglePause());
                    Debug.Log(
                        "[VirtualController] Pause button connected to the game's pause screen"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[VirtualController] No IPauseController in scene - pause button won't work"
                    );
                }
            }
        }

        private void SetupPauseButtonVisual()
        {
            if (pauseButton == null)
                return;

            Image buttonImage = pauseButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                if (cachedPauseButtonTexture == null)
                {
                    cachedPauseButtonTexture = CreatePauseIconTexture(
                        64,
                        pauseButtonColor,
                        pauseIconColor
                    );
                }
                Sprite pauseSprite = Sprite.Create(
                    cachedPauseButtonTexture,
                    new Rect(0, 0, 64, 64),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                buttonImage.sprite = pauseSprite;
                buttonImage.type = Image.Type.Simple;
                buttonImage.color = Color.white;
            }

            // Hide any text child (we use icon instead)
            TMPro.TMP_Text textComponent = pauseButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (textComponent != null)
            {
                textComponent.gameObject.SetActive(false);
            }
            // Also check for legacy Text
            Text legacyText = pauseButton.GetComponentInChildren<Text>();
            if (legacyText != null)
            {
                legacyText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Creates a circular pause button with pause icon (two vertical bars)
        /// </summary>
        private Texture2D CreatePauseIconTexture(int size, Color bgColor, Color iconColor)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float radius = center - 2f;

            // Pause icon dimensions (two vertical bars)
            float barWidth = size * 0.12f;
            float barHeight = size * 0.4f;
            float barSpacing = size * 0.12f; // Space between bars
            float barLeft1 = center - barSpacing - barWidth;
            float barRight1 = center - barSpacing;
            float barLeft2 = center + barSpacing;
            float barRight2 = center + barSpacing + barWidth;
            float barTop = center + barHeight / 2f;
            float barBottom = center - barHeight / 2f;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Check if inside circle
                    if (distance > radius + 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (distance > radius - 1f)
                    {
                        // Anti-aliased edge
                        float alpha = Mathf.Clamp01(radius + 1f - distance);
                        pixels[y * size + x] = new Color(
                            bgColor.r,
                            bgColor.g,
                            bgColor.b,
                            bgColor.a * alpha
                        );
                    }
                    else
                    {
                        // Inside circle - check if on pause icon bars
                        bool onBar1 =
                            x >= barLeft1 && x <= barRight1 && y >= barBottom && y <= barTop;
                        bool onBar2 =
                            x >= barLeft2 && x <= barRight2 && y >= barBottom && y <= barTop;

                        if (onBar1 || onBar2)
                        {
                            // Add slight anti-aliasing at bar edges
                            float edgeAA = 1f;
                            if (onBar1)
                            {
                                float distToEdge = Mathf.Min(
                                    x - barLeft1,
                                    barRight1 - x,
                                    y - barBottom,
                                    barTop - y
                                );
                                edgeAA = Mathf.Clamp01(distToEdge);
                            }
                            else
                            {
                                float distToEdge = Mathf.Min(
                                    x - barLeft2,
                                    barRight2 - x,
                                    y - barBottom,
                                    barTop - y
                                );
                                edgeAA = Mathf.Clamp01(distToEdge);
                            }
                            pixels[y * size + x] = Color.Lerp(bgColor, iconColor, edgeAA);
                        }
                        else
                        {
                            pixels[y * size + x] = bgColor;
                        }
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

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

            // Also handle mouse for editor testing (when not using device simulator)
#if UNITY_EDITOR
            if (activeTouches.Count == 0)
            {
                ProcessMouse(
                    Input.GetMouseButtonDown(0),
                    Input.GetMouseButton(0),
                    Input.GetMouseButtonUp(0),
                    Input.mousePosition
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
