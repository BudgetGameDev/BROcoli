using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Runtime.InteropServices;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

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
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;
    [SerializeField] private Button actionButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Canvas canvas;

    [Header("Joystick Settings")]
    [SerializeField] private float joystickRange = 50f;
    [SerializeField] private float deadZone = 0.1f;

    [Header("Joystick Visuals")]
    [SerializeField] private float ringThickness = 12f;
    [SerializeField] private Color ringColor = new Color(0.4f, 0.4f, 0.45f, 0.55f);
    [SerializeField] private Color fillColor = new Color(0.55f, 0.55f, 0.6f, 0.35f);
    [SerializeField] private Color handleColor = new Color(0.15f, 0.15f, 0.2f, 1.0f); // Dark, fully opaque for maximum visibility
    [SerializeField] private Color handleBorderColor = new Color(0.5f, 0.5f, 0.55f, 1.0f); // Lighter border for contrast

    [Header("Portrait Position (centered but 25% lower for ergonomics)")]
    [SerializeField] private Vector2 portraitJoystickAnchor = new Vector2(0.5f, 0.25f);
    [SerializeField] private Vector2 portraitButtonAnchor = new Vector2(0.85f, 0.25f);
    [SerializeField] private Vector2 portraitPauseButtonAnchor = new Vector2(0.92f, 0.92f);

    [Header("Landscape Position (left side for thumb)")]
    [SerializeField] private Vector2 landscapeJoystickAnchor = new Vector2(0.15f, 0.3f);
    [SerializeField] private Vector2 landscapeButtonAnchor = new Vector2(0.85f, 0.3f);
    [SerializeField] private Vector2 landscapePauseButtonAnchor = new Vector2(0.95f, 0.9f);

    [Header("Pause Button Visual")]
    [SerializeField] private Color pauseButtonColor = new Color(0.2f, 0.2f, 0.25f, 0.85f);
    [SerializeField] private Color pauseIconColor = new Color(1f, 1f, 1f, 0.95f);

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

    public Vector2 JoystickInput => joystickInput;
    public static VirtualController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        
        // Check if user explicitly chose to show/hide virtual controller via main menu
        // 0 = hide (Play button), 1 = show (Play on Mobile button), -1 = not set (use auto-detection)
        int showControllerPref = PlayerPrefs.GetInt("ShowVirtualController", -1);
        
        bool showController;
        if (showControllerPref == 0)
        {
            // User pressed "Play" - hide virtual controller
            showController = false;
            Debug.Log("[VirtualController] User selected 'Play' - hiding virtual controller");
        }
        else if (showControllerPref == 1)
        {
            // User pressed "Play on Mobile" - show virtual controller
            showController = true;
            Debug.Log("[VirtualController] User selected 'Play on Mobile' - showing virtual controller");
        }
        else
        {
            // No preference set, use platform auto-detection
            showController = IsMobilePlatform();
            Debug.Log($"[VirtualController] No preference set, auto-detecting: {(showController ? "mobile" : "desktop")}");
        }
        
        Debug.Log($"[VirtualController] Awake - Platform: {Application.platform}, DeviceType: {SystemInfo.deviceType}, showController: {showController}");
        
        // Hide or show based on user choice or platform detection
        if (!showController)
        {
            Debug.Log("[VirtualController] Hiding joystick controls, keeping pause button");
            // Hide joystick and action button
            if (joystickBackground != null)
                joystickBackground.gameObject.SetActive(false);
            if (actionButton != null)
                actionButton.gameObject.SetActive(false);
            // Pause button stays visible and managed by PauseMenu
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
            Debug.LogWarning($"[VirtualController] Failed to enable WebGL touch events: {e.Message}");
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
            
            Debug.Log($"[VirtualController] WebGL detection - isMobile: {isMobileWebGL}, iOS: {isiOS}, Android: {isAndroid}, Safari: {isSafari}");
            
            if (isMobileWebGL || isiOS || isAndroid)
            {
                Debug.Log("[VirtualController] Mobile browser detected via JavaScript");
                return true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[VirtualController] JavaScript mobile detection failed: {e.Message}");
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
        
        // Runtime platform check as backup
        RuntimePlatform platform = Application.platform;
        
        if (platform == RuntimePlatform.IPhonePlayer)
        {
            Debug.Log("[VirtualController] IPhonePlayer runtime detected");
            return true;
        }
        
        if (platform == RuntimePlatform.Android)
        {
            Debug.Log("[VirtualController] Android runtime detected");
            return true;
        }
        
        // For WebGL, check if touch is supported (additional fallback)
        if (platform == RuntimePlatform.WebGLPlayer)
        {
            if (Input.touchSupported)
            {
                Debug.Log("[VirtualController] WebGL with touch support detected");
                return true;
            }
            // Also check if device type reports handheld
            if (SystemInfo.deviceType == DeviceType.Handheld)
            {
                Debug.Log("[VirtualController] WebGL on handheld device detected");
                return true;
            }
        }
        
        // Check device type - works on actual devices
        if (SystemInfo.deviceType == DeviceType.Handheld)
        {
            Debug.Log("[VirtualController] Handheld device type detected");
            return true;
        }
        
        // In Unity Editor, check if we're simulating a mobile device
        #if UNITY_EDITOR
        // Check if device simulator is active - UnityEngine.Device namespace provides simulated values
        if (UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld)
        {
            Debug.Log("[VirtualController] Editor Device Simulator detected (Handheld)");
            return true;
        }
        // Also enable if we detect touch capability in editor (Device Simulator)
        if (UnityEngine.Device.SystemInfo.deviceType != DeviceType.Desktop)
        {
            Debug.Log("[VirtualController] Editor Device Simulator detected (non-Desktop)");
            return true;
        }
        // Check Application.isMobilePlatform through Device namespace
        if (UnityEngine.Device.Application.isMobilePlatform)
        {
            Debug.Log("[VirtualController] Editor reports mobile platform via Device.Application");
            return true;
        }
        #endif
        
        return false;
    }

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
                    cachedRingTexture = CreateRingTexture(128, ringThickness, ringColor, fillColor);
                }
                Sprite ringSprite = Sprite.Create(cachedRingTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
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
                    cachedHandleTexture = CreateCircleTexture(64, handleColor, handleBorderColor);
                }
                Sprite handleSprite = Sprite.Create(cachedHandleTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
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
                    pixels[y * size + x] = new Color(ringCol.r, ringCol.g, ringCol.b, ringCol.a * alpha);
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
                    pixels[y * size + x] = new Color(borderCol.r, borderCol.g, borderCol.b, borderCol.a * alpha);
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
            // Find PauseMenu and connect to it
            PauseMenu pauseMenu = FindAnyObjectByType<PauseMenu>();
            if (pauseMenu != null)
            {
                pauseButton.onClick.RemoveAllListeners();
                pauseButton.onClick.AddListener(() => pauseMenu.TogglePause());
                Debug.Log("[VirtualController] Pause button connected to PauseMenu");
            }
            else
            {
                Debug.LogWarning("[VirtualController] PauseMenu not found - pause button won't work");
            }
        }
    }
    
    private void SetupPauseButtonVisual()
    {
        if (pauseButton == null) return;
        
        Image buttonImage = pauseButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            if (cachedPauseButtonTexture == null)
            {
                cachedPauseButtonTexture = CreatePauseIconTexture(64, pauseButtonColor, pauseIconColor);
            }
            Sprite pauseSprite = Sprite.Create(cachedPauseButtonTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
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
                    pixels[y * size + x] = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * alpha);
                }
                else
                {
                    // Inside circle - check if on pause icon bars
                    bool onBar1 = x >= barLeft1 && x <= barRight1 && y >= barBottom && y <= barTop;
                    bool onBar2 = x >= barLeft2 && x <= barRight2 && y >= barBottom && y <= barTop;
                    
                    if (onBar1 || onBar2)
                    {
                        // Add slight anti-aliasing at bar edges
                        float edgeAA = 1f;
                        if (onBar1)
                        {
                            float distToEdge = Mathf.Min(
                                x - barLeft1, barRight1 - x,
                                y - barBottom, barTop - y
                            );
                            edgeAA = Mathf.Clamp01(distToEdge);
                        }
                        else
                        {
                            float distToEdge = Mathf.Min(
                                x - barLeft2, barRight2 - x,
                                y - barBottom, barTop - y
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
        if (!IsMobilePlatform()) return;
        
        // Check for orientation changes using screen dimensions (more reliable than Screen.orientation)
        // Only check every 0.1 seconds to avoid constant recalculations
        if (Time.unscaledTime - lastOrientationCheck > 0.1f)
        {
            lastOrientationCheck = Time.unscaledTime;
            bool isPortrait = Screen.height > Screen.width;
            
            if (isPortrait != wasPortrait)
            {
                wasPortrait = isPortrait;
                Debug.Log($"[VirtualController] Orientation changed - isPortrait: {isPortrait}, screen: {Screen.width}x{Screen.height}");
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
                
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, 
                new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        }
    }

    private void HandleJoystickInput()
    {
        if (joystickBackground == null || joystickHandle == null) return;

        // Use EnhancedTouch API (works reliably on iOS and Android)
        var activeTouches = Touch.activeTouches;
        
        for (int i = 0; i < activeTouches.Count; i++)
        {
            var touch = activeTouches[i];
            
            if (touch.phase == TouchPhase.Began)
            {
                if (IsTouchOnJoystick(touch.screenPosition) && !isDragging)
                {
                    isDragging = true;
                    dragFingerId = touch.finger.index;
                    Debug.Log($"[VirtualController] Touch began on joystick, finger: {dragFingerId}");
                }
            }
            else if (touch.finger.index == dragFingerId)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    UpdateJoystickPosition(touch.screenPosition);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    ResetJoystick();
                }
            }
        }

        // Also handle mouse for editor testing (when not using device simulator)
        #if UNITY_EDITOR
        if (activeTouches.Count == 0)
        {
            if (Input.GetMouseButtonDown(0) && IsTouchOnJoystick(Input.mousePosition))
            {
                isDragging = true;
            }
            if (isDragging && Input.GetMouseButton(0))
            {
                UpdateJoystickPosition(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(0))
            {
                ResetJoystick();
            }
        }
        #endif
    }

    private bool IsTouchOnJoystick(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground, screenPosition, canvas.worldCamera, out Vector2 localPoint);
        
        float radius = joystickBackground.sizeDelta.x * 0.5f;
        return localPoint.magnitude <= radius * 1.5f; // Slightly larger touch area
    }

    private void UpdateJoystickPosition(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground, screenPosition, canvas.worldCamera, out Vector2 localPoint);

        // Clamp to joystick range
        Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, joystickRange);
        joystickHandle.anchoredPosition = clampedPoint;

        // Calculate normalized input
        joystickInput = clampedPoint / joystickRange;
        
        // Apply dead zone
        if (joystickInput.magnitude < deadZone)
        {
            joystickInput = Vector2.zero;
        }
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
        Vector2 pauseAnchor = isPortrait ? portraitPauseButtonAnchor : landscapePauseButtonAnchor;

        if (joystickBackground != null)
        {
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
            pauseRect.anchorMin = pauseAnchor;
            pauseRect.anchorMax = pauseAnchor;
            pauseRect.anchoredPosition = Vector2.zero;
            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(pauseRect);
        }
        
        // Force canvas update
        Canvas.ForceUpdateCanvases();
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
