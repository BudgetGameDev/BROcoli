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

        private void HandleControllerNavigation()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Vector2 dpad = gamepad?.dpad.ReadValue() ?? Vector2.zero;
            Vector2 stick = gamepad?.leftStick.ReadValue() ?? Vector2.zero;
            float vertical = ResolveVerticalInput(
                keyboard != null && (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed),
                keyboard != null && (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed),
                dpad.y,
                stick.y
            );
            bool submit =
                (
                    keyboard != null
                    && (
                        keyboard.enterKey.wasPressedThisFrame
                        || keyboard.spaceKey.wasPressedThisFrame
                    )
                ) || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            bool back = gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            ProcessControllerNavigation(vertical, submit, back);
        }

        private void SelectMenuButton(int index)
        {
            if (menuButtons == null || index < 0 || index >= menuButtons.Length)
                return;

            // Play hover sound if index changed
            if (index != selectedButtonIndex)
            {
                ProceduralUIAudio.PlayHover();
            }

            selectedButtonIndex = index;

            if (EventSystem.current != null && menuButtons[index] != null)
            {
                EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
            }
        }

        private void UpdateSelectionVisuals()
        {
            if (menuButtons == null || buttonOutlines == null)
                return;

            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] == null)
                    continue;

                bool isSelected = (i == selectedButtonIndex);

                // Update outline
                if (
                    buttonOutlines != null
                    && i < buttonOutlines.Length
                    && buttonOutlines[i] != null
                )
                {
                    buttonOutlines[i].enabled = isSelected;
                }

                // Animate scale
                if (originalScales != null && i < originalScales.Length)
                {
                    RectTransform rt = menuButtons[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        float targetScale = isSelected ? MenuTheme.SelectedScale : 1f;
                        Vector3 target = originalScales[i] * targetScale;
                        rt.localScale = Vector3.Lerp(
                            rt.localScale,
                            target,
                            Time.unscaledDeltaTime * MenuTheme.SelectionLerpSpeed
                        );
                    }
                }
            }
        }

        public void TogglePause()
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (pauseMenuUI == null)
            {
                Debug.LogError("[PauseMenu] pauseMenuUI is null!");
                return;
            }

            // Ensure EventSystem is active before showing menu
            EnsureEventSystemActive();

            // Force canvas to rebuild its graphic registry (fixes MissingReferenceException)
            RefreshCanvasGraphics();

            // Bring to front (last sibling = on top)
            pauseMenuUI.transform.SetAsLastSibling();

            // Show menu
            pauseMenuUI.SetActive(true);

            // Setup controller navigation
            SetupMenuNavigation();
            selectedButtonIndex = 0;
            SelectMenuButton(0);

            // Pause button visibility is managed by VirtualController

            // Pause game
            Time.timeScale = 0f;
            isPaused = true;
            GameAudioSettings.SetPauseMenuOpen(true);

            Debug.Log("[PauseMenu] Game PAUSED");
        }

        /// <summary>
        /// Forces Canvas to rebuild its internal graphic list, removing any destroyed references.
        /// This fixes MissingReferenceException in GraphicRaycaster.
        /// </summary>
        private void RefreshCanvasGraphics()
        {
            if (mainCanvas == null)
            {
                mainCanvas = ScreenCanvasLocator.Find();
            }

            if (mainCanvas != null)
            {
                // Get the GraphicRaycaster and force it to rebuild
                GraphicRaycaster raycaster = mainCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    // Disable and re-enable to force rebuild of graphic list
                    raycaster.enabled = false;
                    raycaster.enabled = true;
                }

                // Force canvas update
                Canvas.ForceUpdateCanvases();
            }
        }

        public void Resume()
        {
            if (pauseMenuUI == null)
            {
                Debug.LogError("[PauseMenu] pauseMenuUI is null!");
                return;
            }

            ProceduralUIAudio.PlaySelect();

            ResetMenuNavigation();

            // Hide menu
            pauseMenuUI.SetActive(false);

            // Pause button visibility is managed by VirtualController

            // Resume game
            Time.timeScale = 1f;
            isPaused = false;
            GameAudioSettings.SetPauseMenuOpen(false);

            Debug.Log("[PauseMenu] Game RESUMED");
        }

        public void GoToMainMenu() => GoToMainMenu(SceneManager.LoadScene);

        internal void GoToMainMenu(System.Action<string> loadScene)
        {
            Debug.Log("[PauseMenu] Going to MainMenuScene");
            BrocoliAutosaveController.SaveNow();
            // Reset time before loading
            Time.timeScale = 1f;
            isPaused = false;

            loadScene("Brocoli_MainMenu");
        }

        internal static Button FindNamedButton(Button[] buttons, params string[] names)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;
                string objectName = button.gameObject.name.ToLowerInvariant();
                foreach (string name in names)
                    if (objectName.Contains(name))
                        return button;
            }
            return null;
        }

        public bool IsPaused() => isPaused;

        /// <summary>Shared input and focus handling drive pause through this.</summary>
        bool IPauseController.IsPaused => isPaused;

        // Re-check EventSystem when this component is enabled
        void OnEnable()
        {
            EnsureEventSystemActive();
        }
    }
}
