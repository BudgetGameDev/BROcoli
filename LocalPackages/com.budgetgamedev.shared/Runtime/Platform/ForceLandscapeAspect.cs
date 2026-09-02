using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Automatically forces a landscape aspect ratio on ALL cameras in ALL scenes.
    /// This script auto-initializes on game start - no need to add it to any scene manually.
    /// It adds letterboxing (black bars) when the screen is in portrait mode or too narrow.
    /// In portrait mode, it pauses the game and shows a "rotate phone" overlay.
    /// Also auto-pauses gameplay when the game loses focus (tab switch, app background,
    /// etc). Menu scenes are left running - they have no pause menu to resume from -
    /// and so is the editor's own player, which loses focus to the editor constantly.
    /// Works on native builds, WebGL (including iOS Safari), and all platforms.
    /// </summary>
    public static partial class ForceLandscapeAspect
    {
        // Configuration
        private const float MIN_ASPECT_RATIO = 16f / 9f; // 1.777... - minimum width/height ratio
        private const float MAX_ASPECT_RATIO = 21f / 9f; // 2.333... - maximum (for ultra-wide)

        // Switches rather than constants: the pillarbox path and the verbose log
        // trail are both meant to be turned on without editing this file.
        internal static bool ENFORCE_MAX_ASPECT = false; // Set to true to also limit ultra-wide
        internal static bool DEBUG_MODE = false; // Set to true for console logging

        private static int _lastScreenWidth;
        private static int _lastScreenHeight;
        private static bool _initialized = false;
        internal static bool _isPortrait = false;
        internal static bool _isFocusLost = false;
        private static float _savedTimeScale = 1f;
        internal static GameObject _rotateOverlay;

        // Debounce timer to prevent rapid state changes (especially on iOS Safari offline)
        private static float _lastOrientationChangeTime = -999f;
        private const float ORIENTATION_CHANGE_DEBOUNCE = 0.5f; // Minimum seconds between state changes
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RegisterVisibilityChangeCallback();
#endif

        /// <summary>
        /// Auto-initializes when the game starts (before any scene loads)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;

            if (DEBUG_MODE)
                Debug.Log("[ForceLandscapeAspect] Auto-initializing...");

            // Subscribe to scene loaded event to handle cameras in new scenes
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Create a persistent game object to run updates
            var updater = new GameObject("[ForceLandscapeAspect]");
            updater.AddComponent<AspectRatioUpdater>();
            KeepAcrossScenes(updater);
            updater.hideFlags = HideFlags.HideInHierarchy;

#if UNITY_WEBGL && !UNITY_EDITOR
            // Register JS callback for visibility change (works in Safari, Chrome, etc)
            RegisterVisibilityChangeCallback();
#endif

            if (DEBUG_MODE)
                Debug.Log("[ForceLandscapeAspect] Initialized successfully");
        }

        internal static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (DEBUG_MODE)
                Debug.Log(
                    $"[ForceLandscapeAspect] Scene loaded: {scene.name}, updating all cameras..."
                );

            UpdateAllCameras();
        }

        /// <summary>
        /// Updates the viewport of all active cameras to enforce landscape aspect ratio
        /// </summary>
        public static void UpdateAllCameras() =>
            UpdateAllCameras(Screen.width, Screen.height, Time.realtimeSinceStartup);

        /// <summary>
        /// The same work with the screen size and the clock supplied by the caller,
        /// so the portrait transition and its debounce can be driven on a desktop
        /// editor that can neither rotate nor rewind.
        /// </summary>
        internal static void UpdateAllCameras(int screenWidth, int screenHeight, float now)
        {
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;

            float screenAspect = (float)screenWidth / screenHeight;
            bool wasPortrait = _isPortrait;
            bool nowPortrait = screenAspect < 1f; // Portrait if height > width

            // Handle portrait/landscape transitions with debouncing
            // This prevents rapid state changes on iOS Safari offline where viewport may jitter
            float timeSinceLastChange = now - _lastOrientationChangeTime;
            bool canChangeState = timeSinceLastChange >= ORIENTATION_CHANGE_DEBOUNCE;

            if (nowPortrait != wasPortrait && canChangeState)
            {
                _isPortrait = nowPortrait;
                _lastOrientationChangeTime = now;

                if (_isPortrait)
                {
                    OnEnteredPortrait();
                }
                else
                {
                    OnEnteredLandscape();
                }
            }

            Rect targetRect = CalculateViewportRect(screenAspect);

            // Find and update ALL cameras (including inactive ones that might become active)
            Camera[] allCameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (Camera cam in allCameras)
            {
                if (cam != null && cam.gameObject.name != "[LetterboxClearCamera]")
                {
                    cam.rect = targetRect;
                }
            }

            if (DEBUG_MODE)
                Debug.Log(
                    $"[ForceLandscapeAspect] Updated {allCameras.Length} cameras. Screen: {screenWidth}x{screenHeight}, Aspect: {screenAspect:F3}, Rect: {targetRect}"
                );
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
            if (_isFocusLost)
                return; // Already paused for focus

            // Play mode shares focus with the rest of the editor, so pausing on every
            // click into the console or the inspector would make it unplayable.
            if (IsEditorPlayer())
            {
                if (DEBUG_MODE)
                    Debug.Log("[ForceLandscapeAspect] Focus LOST in the editor - not pausing");
                return;
            }

            // Only gameplay auto-pauses. Menu scenes expose no IPauseController, so pausing
            // there would freeze the menu with no way to resume it.
            IPauseController pauseMenu = FindPauseController();
            if (pauseMenu == null)
            {
                if (DEBUG_MODE)
                    Debug.Log("[ForceLandscapeAspect] Focus LOST outside gameplay - not pausing");
                return;
            }

            _isFocusLost = true;

            if (DEBUG_MODE)
                Debug.Log("[ForceLandscapeAspect] Focus LOST - triggering pause");

            pauseMenu.Pause();
        }

        /// <summary>
        /// Called when game regains focus
        /// </summary>
        public static void OnFocusRegained()
        {
            if (!_isFocusLost)
                return; // Wasn't paused for focus
            _isFocusLost = false;

            if (DEBUG_MODE)
                Debug.Log("[ForceLandscapeAspect] Focus REGAINED");

            // Note: We don't auto-resume - let user tap Resume button in pause menu
            // This is better UX than game suddenly resuming when you switch back
        }

        internal static void ShowRotateOverlay(bool show)
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

        internal static Rect CalculateViewportRect(float screenAspect)
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

        // Rate limiting for screen change checks
        private static float _lastScreenChangeCheck = 0f;
        private const float SCREEN_CHANGE_CHECK_INTERVAL = 0.1f; // Max 10 checks per second

        /// <summary>
        /// Checks if screen size changed and updates cameras if needed.
        /// Rate-limited to prevent excessive updates on iOS Safari offline where viewport may jitter.
        /// </summary>
        public static void CheckForScreenChange() =>
            CheckForScreenChange(Screen.width, Screen.height, Time.realtimeSinceStartup);

        /// <summary>
        /// The same check with the screen size and the clock supplied by the caller,
        /// so the rate limit can be observed without waiting on a real clock.
        /// </summary>
        internal static void CheckForScreenChange(int screenWidth, int screenHeight, float now)
        {
            // Rate limit screen change checks to prevent overwhelming the system
            if (now - _lastScreenChangeCheck < SCREEN_CHANGE_CHECK_INTERVAL)
            {
                return;
            }
            _lastScreenChangeCheck = now;

            if (screenWidth != _lastScreenWidth || screenHeight != _lastScreenHeight)
            {
                UpdateAllCameras(screenWidth, screenHeight, now);
            }
        }
    }
}
