using System.Runtime.InteropServices;
using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Handles pause menu functionality.
    /// CRITICAL: This script ensures EventSystem is enabled - without it, NO UI buttons work!
    /// </summary>
    public partial class PauseMenu : MonoBehaviour, IPauseController
    {
        [Header("UI References")]
        public GameObject pauseMenuUI;
        public GameObject pauseButton;
        public Button resumeButton;
        public Button mainMenuButton;

        private bool isPaused = false;
        private bool isMobilePlatform = false;
        private EventSystem eventSystem;
        private Canvas mainCanvas;

        // Controller navigation
        private Button[] menuButtons;
        private int selectedButtonIndex = 0;
        private float lastNavTime = 0f;
        private const float NavRepeatDelay = 0.25f;
        private Outline[] buttonOutlines;
        private Vector3[] originalScales;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int IsMobileBrowser();
#endif

        void Awake()
        {
            // Reset state on awake
            isPaused = false;
            Time.timeScale = 1f;

            // Hide pause menu, and give it the shared menu presentation.
            if (pauseMenuUI != null)
            {
                pauseMenuUI.SetActive(false);
                if (pauseMenuUI.GetComponent<ResponsivePauseMenuLayout>() == null)
                    pauseMenuUI.AddComponent<ResponsivePauseMenuLayout>();
            }

            // CRITICAL: Ensure EventSystem is active immediately
            EnsureEventSystemActive();
            // Add GraphicRegistryCleaner if it doesn't exist
            if (FindAnyObjectByType<GraphicRegistryCleaner>() == null)
            {
                gameObject.AddComponent<GraphicRegistryCleaner>();
            }
        }

        void Start()
        {
            // Double-check EventSystem
            EnsureEventSystemActive();

            // Cache the main canvas
            mainCanvas = ScreenCanvasLocator.Find();

            // Detect mobile
#if UNITY_WEBGL && !UNITY_EDITOR
            isMobilePlatform = IsMobileBrowser() == 1;
#endif

#if UNITY_IOS || UNITY_ANDROID
            isMobilePlatform = true;
#endif

            isMobilePlatform = DetectMobilePlatform(
                isMobilePlatform,
                SystemInfo.deviceType,
#if UNITY_EDITOR
                UnityEngine.Device.SystemInfo.deviceType,
                UnityEngine.Device.Application.isMobilePlatform
#else
                DeviceType.Desktop,
                false
#endif
            );
            Debug.Log($"[PauseMenu] Mobile platform detection: {isMobilePlatform}");
            // Pause button visibility is now managed by VirtualController
            // Just ensure the button has a click handler if it exists
            if (pauseButton != null)
            {
                Button btn = pauseButton.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(TogglePause);
                    Debug.Log("[PauseMenu] Pause button click handler connected");
                }
            }

            // Setup buttons
            SetupButtons();

            Debug.Log(
                $"[PauseMenu] Initialized - EventSystem active: {eventSystem != null && eventSystem.gameObject.activeInHierarchy}, isMobile: {isMobilePlatform}"
            );
        }

        /// <summary>
        /// CRITICAL: Without an active EventSystem, UI buttons don't work AT ALL.
        /// The scene has EventSystem disabled - this fixes it.
        /// </summary>
        private void EnsureEventSystemActive()
        {
            // Try to find existing EventSystem (including inactive ones)
            if (eventSystem == null)
            {
                // First try active ones
                eventSystem = FindAnyObjectByType<EventSystem>();

                // If not found, search including inactive
                if (eventSystem == null)
                {
                    EventSystem[] allES = FindObjectsByType<EventSystem>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    );
                    if (allES.Length > 0)
                    {
                        eventSystem = allES[0];
                    }
                }
            }

            if (eventSystem == null)
            {
                // Create new EventSystem if none exists
                Debug.Log("[PauseMenu] Creating new EventSystem");
                GameObject esObj = new GameObject("EventSystem_PauseMenu");
                eventSystem = esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
            else
            {
                // Enable if disabled
                if (!eventSystem.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning("[PauseMenu] EventSystem was DISABLED! Enabling it now.");
                    eventSystem.gameObject.SetActive(true);
                }

                // Ensure it has SOME input module - prefer StandaloneInputModule for reliability
                BaseInputModule inputModule = eventSystem.GetComponent<BaseInputModule>();
                if (inputModule == null)
                {
                    Debug.Log("[PauseMenu] Adding StandaloneInputModule to EventSystem");
                    eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                }
                else if (!inputModule.enabled)
                {
                    Debug.Log("[PauseMenu] Enabling InputModule");
                    inputModule.enabled = true;
                }
            }

            // Force EventSystem to update its current reference
            if (EventSystem.current == null && eventSystem != null)
            {
                Debug.Log("[PauseMenu] Setting EventSystem.current manually");
                // Just accessing eventSystem while it's active should set EventSystem.current
                eventSystem.gameObject.SetActive(false);
                eventSystem.gameObject.SetActive(true);
            }
        }

        private void SetupButtons()
        {
            // Find buttons by name if not assigned
            if (pauseMenuUI != null)
            {
                Button[] allButtons = pauseMenuUI.GetComponentsInChildren<Button>(true);

                resumeButton ??= FindNamedButton(allButtons, "resume");
                mainMenuButton ??= FindNamedButton(allButtons, "mainmenu", "main menu");
            }

            // Connect Resume button
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(Resume);
                Debug.Log($"[PauseMenu] Resume button connected: {resumeButton.gameObject.name}");
            }
            else
            {
                Debug.LogError("[PauseMenu] Resume button not found!");
            }

            // Connect MainMenu button
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(GoToMainMenu);
                Debug.Log(
                    $"[PauseMenu] MainMenu button connected: {mainMenuButton.gameObject.name}"
                );
            }
            else
            {
                Debug.LogError("[PauseMenu] MainMenu button not found!");
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            ProcessToggleInput(
                keyboard != null && keyboard.escapeKey.wasPressedThisFrame,
                gamepad != null && gamepad.startButton.wasPressedThisFrame
            );

            // Handle controller navigation when paused
            if (isPaused)
            {
                HandleControllerNavigation();
                UpdateSelectionVisuals();
            }
        }
    }
}
