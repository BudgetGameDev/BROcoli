using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Runtime.InteropServices;

/// <summary>
/// Automatically forces a landscape aspect ratio on ALL cameras in ALL scenes.
/// This script auto-initializes on game start - no need to add it to any scene manually.
/// It adds letterboxing (black bars) when the screen is in portrait mode or too narrow.
/// In portrait mode, it pauses the game and shows a "rotate phone" overlay.
/// Also auto-pauses when the game loses focus (tab switch, app background, etc).
/// Works on native builds, WebGL (including iOS Safari), and all platforms.
/// </summary>
public static class ForceLandscapeAspect
{
    // Configuration
    private const float MIN_ASPECT_RATIO = 16f / 9f; // 1.777... - minimum width/height ratio
    private const float MAX_ASPECT_RATIO = 21f / 9f; // 2.333... - maximum (for ultra-wide)
    private const bool ENFORCE_MAX_ASPECT = false;   // Set to true to also limit ultra-wide
    private const bool DEBUG_MODE = false;           // Set to true for console logging

    private static int _lastScreenWidth;
    private static int _lastScreenHeight;
    private static bool _initialized = false;
    private static bool _isPortrait = false;
    private static bool _isFocusLost = false;
    private static float _savedTimeScale = 1f;
    private static GameObject _rotateOverlay;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RegisterVisibilityChangeCallback();
#endif

    /// <summary>
    /// Auto-initializes when the game starts (before any scene loads)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        if (DEBUG_MODE)
            Debug.Log("[ForceLandscapeAspect] Auto-initializing...");

        // Subscribe to scene loaded event to handle cameras in new scenes
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Create a persistent game object to run updates
        var updater = new GameObject("[ForceLandscapeAspect]");
        updater.AddComponent<AspectRatioUpdater>();
        Object.DontDestroyOnLoad(updater);
        updater.hideFlags = HideFlags.HideInHierarchy;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Register JS callback for visibility change (works in Safari, Chrome, etc)
        RegisterVisibilityChangeCallback();
#endif

        if (DEBUG_MODE)
            Debug.Log("[ForceLandscapeAspect] Initialized successfully");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (DEBUG_MODE)
            Debug.Log($"[ForceLandscapeAspect] Scene loaded: {scene.name}, updating all cameras...");
        
        UpdateAllCameras();
    }

    /// <summary>
    /// Updates the viewport of all active cameras to enforce landscape aspect ratio
    /// </summary>
    public static void UpdateAllCameras()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        float screenAspect = (float)Screen.width / Screen.height;
        bool wasPortrait = _isPortrait;
        _isPortrait = screenAspect < 1f; // Portrait if height > width
        
        // Handle portrait/landscape transitions
        if (_isPortrait && !wasPortrait)
        {
            OnEnteredPortrait();
        }
        else if (!_isPortrait && wasPortrait)
        {
            OnEnteredLandscape();
        }
        
        Rect targetRect = CalculateViewportRect(screenAspect);

        // Find and update ALL cameras (including inactive ones that might become active)
        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (Camera cam in allCameras)
        {
            if (cam != null && cam.gameObject.name != "[LetterboxClearCamera]")
            {
                cam.rect = targetRect;
            }
        }

        if (DEBUG_MODE)
            Debug.Log($"[ForceLandscapeAspect] Updated {allCameras.Length} cameras. Screen: {Screen.width}x{Screen.height}, Aspect: {screenAspect:F3}, Rect: {targetRect}");
    }

    private static void OnEnteredPortrait()
    {
        if (DEBUG_MODE)
            Debug.Log("[ForceLandscapeAspect] Entered PORTRAIT mode - pausing game");
        
        // Save current time scale and pause
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        
        // Show rotate overlay
        ShowRotateOverlay(true);
    }
    
    private static void OnEnteredLandscape()
    {
        if (DEBUG_MODE)
            Debug.Log("[ForceLandscapeAspect] Entered LANDSCAPE mode - resuming game");
        
        // Hide rotate overlay
        ShowRotateOverlay(false);
        
        // Restore time scale (only if not paused for other reasons)
        if (!_isFocusLost)
        {
            Time.timeScale = _savedTimeScale;
        }
    }
    
    /// <summary>
    /// Called when game loses focus (from JS in WebGL, or from Unity events on native)
    /// </summary>
    public static void OnFocusLost()
    {
        if (_isFocusLost) return; // Already paused for focus
        _isFocusLost = true;
        
        if (DEBUG_MODE)
            Debug.Log("[ForceLandscapeAspect] Focus LOST - triggering pause");
        
        // Try to use existing PauseMenu
        TriggerPauseMenu(true);
    }
    
    /// <summary>
    /// Called when game regains focus
    /// </summary>
    public static void OnFocusRegained()
    {
        if (!_isFocusLost) return; // Wasn't paused for focus
        _isFocusLost = false;
        
        if (DEBUG_MODE)
            Debug.Log("[ForceLandscapeAspect] Focus REGAINED");
        
        // Note: We don't auto-resume - let user tap Resume button in pause menu
        // This is better UX than game suddenly resuming when you switch back
    }
    
    private static void TriggerPauseMenu(bool pause)
    {
        // Find the existing PauseMenu in the scene
        PauseMenu pauseMenu = Object.FindAnyObjectByType<PauseMenu>();
        
        if (pauseMenu != null && pause)
        {
            // Use the existing pause menu
            pauseMenu.Pause();
        }
        else if (pause)
        {
            // Fallback: just pause time if no PauseMenu exists (e.g., in MainMenu scene)
            if (!_isPortrait) // Don't double-save if already in portrait
            {
                _savedTimeScale = Time.timeScale;
            }
            Time.timeScale = 0f;
        }
    }
    
    private static void ShowRotateOverlay(bool show)
    {
        if (show)
        {
            if (_rotateOverlay == null)
            {
                CreateRotateOverlay();
            }
            _rotateOverlay.SetActive(true);
        }
        else
        {
            if (_rotateOverlay != null)
            {
                _rotateOverlay.SetActive(false);
            }
        }
    }
    
    private static void CreateRotateOverlay()
    {
        // Create canvas
        _rotateOverlay = new GameObject("[RotatePhoneOverlay]");
        Object.DontDestroyOnLoad(_rotateOverlay);
        
        Canvas canvas = _rotateOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // On top of everything
        
        CanvasScaler scaler = _rotateOverlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); // Portrait reference
        scaler.matchWidthOrHeight = 0.5f;
        
        _rotateOverlay.AddComponent<GraphicRaycaster>();
        
        // Dark background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(_rotateOverlay.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.9f);
        
        // Container for content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(_rotateOverlay.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(400, 300);
        
        VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 30;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        
        // Phone icon with rotation arrow (using UI elements)
        GameObject iconObj = new GameObject("PhoneIcon");
        iconObj.transform.SetParent(contentObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(120, 120);
        
        // Create a simple phone shape
        CreatePhoneIcon(iconObj);
        
        // Add rotation animation
        iconObj.AddComponent<RotateAnimator>();
        
        // Text message
        GameObject textObj = new GameObject("Message");
        textObj.transform.SetParent(contentObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(350, 100);
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Please rotate your device\nto landscape mode";
        text.fontSize = 32;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        
        if (DEBUG_MODE)
            Debug.Log("[ForceLandscapeAspect] Rotate overlay created");
    }
    
    private static void CreatePhoneIcon(GameObject parent)
    {
        // Phone body (portrait rectangle)
        GameObject body = new GameObject("Body");
        body.transform.SetParent(parent.transform, false);
        RectTransform bodyRect = body.AddComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRect.sizeDelta = new Vector2(60, 100);
        
        Image bodyImg = body.AddComponent<Image>();
        bodyImg.color = Color.white;
        
        // Screen (inner dark rectangle)
        GameObject screen = new GameObject("Screen");
        screen.transform.SetParent(body.transform, false);
        RectTransform screenRect = screen.AddComponent<RectTransform>();
        screenRect.anchorMin = new Vector2(0.5f, 0.5f);
        screenRect.anchorMax = new Vector2(0.5f, 0.5f);
        screenRect.sizeDelta = new Vector2(50, 80);
        
        Image screenImg = screen.AddComponent<Image>();
        screenImg.color = new Color(0.2f, 0.2f, 0.2f);
        
        // Curved arrow indicating rotation
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.SetParent(parent.transform, false);
        RectTransform arrowRect = arrow.AddComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(140, 140);
        arrowRect.localRotation = Quaternion.Euler(0, 0, -45);
        
        // Create arrow using lines
        CreateArrowArc(arrow);
    }
    
    private static void CreateArrowArc(GameObject parent)
    {
        // Create curved arrow segments
        Color arrowColor = new Color(0.3f, 0.7f, 1f); // Light blue
        
        for (int i = 0; i < 6; i++)
        {
            GameObject segment = new GameObject($"Segment{i}");
            segment.transform.SetParent(parent.transform, false);
            RectTransform segRect = segment.AddComponent<RectTransform>();
            segRect.anchorMin = new Vector2(0.5f, 0.5f);
            segRect.anchorMax = new Vector2(0.5f, 0.5f);
            
            float angle = i * 25f - 60f;
            float rad = angle * Mathf.Deg2Rad;
            float radius = 55f;
            
            segRect.anchoredPosition = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
            segRect.sizeDelta = new Vector2(12, 12);
            
            Image segImg = segment.AddComponent<Image>();
            segImg.color = arrowColor;
        }
        
        // Arrow head
        GameObject arrowHead = new GameObject("ArrowHead");
        arrowHead.transform.SetParent(parent.transform, false);
        RectTransform headRect = arrowHead.AddComponent<RectTransform>();
        headRect.anchorMin = new Vector2(0.5f, 0.5f);
        headRect.anchorMax = new Vector2(0.5f, 0.5f);
        
        float endAngle = 5 * 25f - 60f;
        float endRad = endAngle * Mathf.Deg2Rad;
        headRect.anchoredPosition = new Vector2(Mathf.Cos(endRad) * 55f + 10f, Mathf.Sin(endRad) * 55f);
        headRect.sizeDelta = new Vector2(20, 20);
        headRect.localRotation = Quaternion.Euler(0, 0, -30);
        
        Image headImg = arrowHead.AddComponent<Image>();
        headImg.color = new Color(0.3f, 0.7f, 1f);
    }

    private static Rect CalculateViewportRect(float screenAspect)
    {
        // Check if screen is too tall (portrait or narrow aspect)
        if (screenAspect < MIN_ASPECT_RATIO)
        {
            // Screen is too tall, add letterbox (black bars top/bottom)
            float viewportHeight = screenAspect / MIN_ASPECT_RATIO;
            float offsetY = (1f - viewportHeight) / 2f;
            return new Rect(0f, offsetY, 1f, viewportHeight);
        }
        else if (ENFORCE_MAX_ASPECT && screenAspect > MAX_ASPECT_RATIO)
        {
            // Screen is too wide, add pillarbox (black bars left/right)
            float viewportWidth = MAX_ASPECT_RATIO / screenAspect;
            float offsetX = (1f - viewportWidth) / 2f;
            return new Rect(offsetX, 0f, viewportWidth, 1f);
        }
        
        // Aspect ratio is acceptable, use full screen
        return new Rect(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// Checks if screen size changed and updates cameras if needed
    /// </summary>
    public static void CheckForScreenChange()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            UpdateAllCameras();
        }
    }

    /// <summary>
    /// Helper MonoBehaviour that runs the update loop and clears letterbox areas
    /// </summary>
    private class AspectRatioUpdater : MonoBehaviour
    {
        private Camera _clearCamera;
        private bool _initialFocusChecked = false;

        void Start()
        {
            // Create a camera specifically for clearing the letterbox/pillarbox areas to black
            var clearCamObj = new GameObject("[LetterboxClearCamera]");
            clearCamObj.transform.SetParent(transform);
            _clearCamera = clearCamObj.AddComponent<Camera>();
            _clearCamera.depth = -100; // Render first (behind everything)
            _clearCamera.clearFlags = CameraClearFlags.SolidColor;
            _clearCamera.backgroundColor = Color.black;
            _clearCamera.cullingMask = 0; // Don't render any layers
            _clearCamera.rect = new Rect(0, 0, 1, 1); // Full screen to clear letterbox areas
            
            // Initial update
            UpdateAllCameras();
        }

        void Update()
        {
            CheckForScreenChange();
            
            // Check initial focus state after a short delay (let Unity settle)
            if (!_initialFocusChecked)
            {
                _initialFocusChecked = true;
                // Check if we started without focus
                if (!Application.isFocused)
                {
                    if (DEBUG_MODE)
                        Debug.Log("[ForceLandscapeAspect] Game started without focus - pausing");
                    OnFocusLost();
                }
            }
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                OnFocusLost();
            }
            else
            {
                OnFocusRegained();
            }
        }
        
        void OnApplicationPause(bool pauseStatus)
        {
            // Also handle app pause (mobile backgrounding)
            if (pauseStatus)
            {
                OnFocusLost();
            }
            else
            {
                OnFocusRegained();
            }
        }
        
        // Called from JavaScript via SendMessage for WebGL
        public void OnVisibilityLost()
        {
            OnFocusLost();
        }
        
        // Called from JavaScript via SendMessage for WebGL
        public void OnVisibilityRegained()
        {
            OnFocusRegained();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
    
    /// <summary>
    /// Simple animator for the rotate icon
    /// </summary>
    private class RotateAnimator : MonoBehaviour
    {
        private float _angle = 0f;
        private float _targetAngle = -90f;
        private float _animSpeed = 2f;
        private float _pauseTimer = 0f;
        
        void Update()
        {
            // Use unscaled time since game is paused
            if (_pauseTimer > 0f)
            {
                _pauseTimer -= Time.unscaledDeltaTime;
                return;
            }
            
            _angle = Mathf.MoveTowards(_angle, _targetAngle, Time.unscaledDeltaTime * 90f * _animSpeed);
            transform.localRotation = Quaternion.Euler(0, 0, _angle);
            
            if (Mathf.Approximately(_angle, _targetAngle))
            {
                // Swap between portrait (0) and landscape (-90)
                if (_targetAngle == -90f)
                {
                    _pauseTimer = 1f;
                    _targetAngle = 0f;
                }
                else
                {
                    _pauseTimer = 0.5f;
                    _targetAngle = -90f;
                }
            }
        }
    }
}
