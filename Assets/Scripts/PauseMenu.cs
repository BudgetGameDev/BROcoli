using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;

/// <summary>
/// Handles pause menu functionality.
/// CRITICAL: This script ensures EventSystem is enabled - without it, NO UI buttons work!
/// </summary>
public class PauseMenu : MonoBehaviour
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

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsMobileBrowser();
#endif

    void Awake()
    {
        // Reset state on awake
        isPaused = false;
        Time.timeScale = 1f;
        
        // Hide pause menu
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        
        // CRITICAL: Ensure EventSystem is active immediately
        EnsureEventSystemActive();        
        // Add GraphicRegistryCleaner if it doesn't exist
        if (FindAnyObjectByType<GraphicRegistryCleaner>() == null)
        {
            gameObject.AddComponent<GraphicRegistryCleaner>();
        }    }

    void Start()
    {
        // Double-check EventSystem
        EnsureEventSystemActive();
        
        // Cache the main canvas
        mainCanvas = FindAnyObjectByType<Canvas>();
        
        // Detect mobile
#if UNITY_WEBGL && !UNITY_EDITOR
        isMobilePlatform = IsMobileBrowser() == 1;
#endif

#if UNITY_IOS || UNITY_ANDROID
        isMobilePlatform = true;
#endif

        // Also check device type for runtime detection
        if (SystemInfo.deviceType == DeviceType.Handheld)
        {
            isMobilePlatform = true;
        }
        
#if UNITY_EDITOR
        // Check Device Simulator in editor
        if (UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld ||
            UnityEngine.Device.Application.isMobilePlatform)
        {
            isMobilePlatform = true;
            Debug.Log("[PauseMenu] Device Simulator detected as mobile");
        }
#endif
        
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
        
        Debug.Log($"[PauseMenu] Initialized - EventSystem active: {eventSystem != null && eventSystem.gameObject.activeInHierarchy}, isMobile: {isMobilePlatform}");
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
                EventSystem[] allES = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
            
            foreach (var btn in allButtons)
            {
                if (btn == null) continue;
                
                string name = btn.gameObject.name.ToLower();
                
                if (resumeButton == null && name.Contains("resume"))
                {
                    resumeButton = btn;
                }
                if (mainMenuButton == null && (name.Contains("mainmenu") || name.Contains("main menu")))
                {
                    mainMenuButton = btn;
                }
            }
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
            Debug.Log($"[PauseMenu] MainMenu button connected: {mainMenuButton.gameObject.name}");
        }
        else
        {
            Debug.LogError("[PauseMenu] MainMenu button not found!");
        }
    }

    void Update()
    {
        // Escape key to toggle pause
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
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
        
        // Pause button visibility is managed by VirtualController
        
        // Pause game
        Time.timeScale = 0f;
        isPaused = true;
        
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
            mainCanvas = FindAnyObjectByType<Canvas>();
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
        
        // Hide menu
        pauseMenuUI.SetActive(false);
        
        // Pause button visibility is managed by VirtualController
        
        // Resume game
        Time.timeScale = 1f;
        isPaused = false;
        
        Debug.Log("[PauseMenu] Game RESUMED");
    }

    public void GoToMainMenu()
    {
        Debug.Log("[PauseMenu] Going to MainMenuScene");
        
        // Reset time before loading
        Time.timeScale = 1f;
        isPaused = false;
        
        SceneManager.LoadScene("MainMenuScene");
    }

    public bool IsPaused() => isPaused;
    
    // Re-check EventSystem when this component is enabled
    void OnEnable()
    {
        EnsureEventSystemActive();
    }
}
