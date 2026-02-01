using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically forces a landscape aspect ratio on ALL cameras in ALL scenes.
/// This script auto-initializes on game start - no need to add it to any scene manually.
/// It adds letterboxing (black bars) when the screen is in portrait mode or too narrow.
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
        Rect targetRect = CalculateViewportRect(screenAspect);

        // Find and update ALL cameras (including inactive ones that might become active)
        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (Camera cam in allCameras)
        {
            if (cam != null)
            {
                cam.rect = targetRect;
            }
        }

        if (DEBUG_MODE)
            Debug.Log($"[ForceLandscapeAspect] Updated {allCameras.Length} cameras. Screen: {Screen.width}x{Screen.height}, Aspect: {screenAspect:F3}, Rect: {targetRect}");
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
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
