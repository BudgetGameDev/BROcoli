using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;
using TMPro;

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
    
    [Header("Stats Display")]
    public TextMeshProUGUI statsText;
    
    [Header("NVIDIA Tech Display")]
    public TextMeshProUGUI nvidiaTechText;
    
    private bool isPaused = false;
    private bool isMobilePlatform = false;
    private EventSystem eventSystem;
    private Canvas mainCanvas;
    private PlayerStats playerStats;
    
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
        
        // Find player stats
        playerStats = FindAnyObjectByType<PlayerStats>();
        
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
        
        // Gamepad Start/Menu button to toggle pause
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
        {
            TogglePause();
        }
        
        // Handle controller navigation when paused
        if (isPaused)
        {
            HandleControllerNavigation();
            UpdateSelectionVisuals();
        }
    }
    
    private void HandleControllerNavigation()
    {
        if (menuButtons == null || menuButtons.Length == 0) return;
        
        // Rate limit
        if (Time.unscaledTime - lastNavTime < NavRepeatDelay) return;
        
        float vertical = 0f;
        
        // Keyboard
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) vertical = 1f;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) vertical = -1f;
        
        // Gamepad
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            
            if (Mathf.Abs(dpad.y) > 0.5f) vertical = Mathf.Sign(dpad.y);
            else if (Mathf.Abs(stick.y) > 0.5f) vertical = Mathf.Sign(stick.y);
        }
        
        // Navigate (up = previous, down = next)
        if (Mathf.Abs(vertical) > 0.1f)
        {
            lastNavTime = Time.unscaledTime;
            int direction = vertical > 0 ? -1 : 1;  // Up goes to previous (lower index)
            int newIndex = Mathf.Clamp(selectedButtonIndex + direction, 0, menuButtons.Length - 1);
            if (newIndex != selectedButtonIndex)
            {
                SelectMenuButton(newIndex);
            }
        }
        
        // Submit with Enter/Space/Gamepad A
        bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            submit = true;
        }
        
        if (submit && selectedButtonIndex >= 0 && selectedButtonIndex < menuButtons.Length)
        {
            Button btn = menuButtons[selectedButtonIndex];
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
            }
        }
        
        // B button to resume (back)
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            Resume();
        }
    }
    
    private void SelectMenuButton(int index)
    {
        if (menuButtons == null || index < 0 || index >= menuButtons.Length) return;
        
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
        if (menuButtons == null || buttonOutlines == null) return;
        
        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;
            
            bool isSelected = (i == selectedButtonIndex);
            
            // Update outline
            if (buttonOutlines != null && i < buttonOutlines.Length && buttonOutlines[i] != null)
            {
                buttonOutlines[i].enabled = isSelected;
            }
            
            // Animate scale
            if (originalScales != null && i < originalScales.Length)
            {
                RectTransform rt = menuButtons[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    float targetScale = isSelected ? 1.1f : 1f;
                    Vector3 target = originalScales[i] * targetScale;
                    rt.localScale = Vector3.Lerp(rt.localScale, target, Time.unscaledDeltaTime * 12f);
                }
            }
        }
    }
    
    private void SetupMenuNavigation()
    {
        if (pauseMenuUI == null) return;
        
        // Get all buttons in pause menu
        Button[] allButtons = pauseMenuUI.GetComponentsInChildren<Button>(true);
        var buttonList = new System.Collections.Generic.List<Button>();
        
        foreach (var btn in allButtons)
        {
            if (btn != null && btn.interactable)
            {
                buttonList.Add(btn);
            }
        }
        
        menuButtons = buttonList.ToArray();
        buttonOutlines = new Outline[menuButtons.Length];
        originalScales = new Vector3[menuButtons.Length];
        
        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;
            
            RectTransform rt = menuButtons[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                originalScales[i] = rt.localScale;
            }
            
            // Add outline
            Outline outline = menuButtons[i].GetComponent<Outline>();
            if (outline == null)
            {
                outline = menuButtons[i].gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(1f, 0.9f, 0.2f, 1f);
            outline.effectDistance = new Vector2(6f, 6f);
            outline.enabled = false;
            buttonOutlines[i] = outline;
            
            // Setup hover
            int index = i;
            EventTrigger trigger = menuButtons[i].GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = menuButtons[i].gameObject.AddComponent<EventTrigger>();
            }
            
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => SelectMenuButton(index));
            trigger.triggers.Add(enterEntry);
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
        
        // Update stats display
        UpdateStatsDisplay();
        
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
        
        ProceduralUIAudio.PlaySelect();
        
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
    
    /// <summary>
    /// Updates the stats display text with current player stats.
    /// Colors: White = base, Green = positive, Red = negative
    /// </summary>
    private void UpdateStatsDisplay()
    {
        if (statsText == null)
        {
            // Try to find it by name
            if (pauseMenuUI != null)
            {
                var texts = pauseMenuUI.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.gameObject.name.ToLower().Contains("stats"))
                    {
                        statsText = t;
                        break;
                    }
                }
            }
        }
        
        if (statsText == null || playerStats == null)
        {
            if (playerStats == null)
                playerStats = FindAnyObjectByType<PlayerStats>();
            if (statsText == null || playerStats == null)
                return;
        }
        
        // Base values for comparison
        float baseMaxHealth = 100f, baseDamage = 10f, baseSpeed = 4f;
        float baseAttackSpeed = 0.6f, baseDetection = 12f;
        float baseCritChance = 5f, baseCritDamage = 150f;
        float baseDodge = 0f, baseArmor = 0f, baseRegen = 0f, baseLifeSteal = 0f;
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=28><b>STATS</b></size>");
        sb.AppendLine();
        
        sb.AppendLine(FormatStat("Level", playerStats.CurrentLevel, 1f, true));
        sb.AppendLine(FormatStat("HP", playerStats.CurrentHealth, baseMaxHealth, true, $"/{playerStats.CurrentMaxHealth:F0}"));
        sb.AppendLine(FormatStat("Max HP", playerStats.CurrentMaxHealth, baseMaxHealth));
        sb.AppendLine(FormatStat("Damage", playerStats.CurrentDamage, baseDamage));
        sb.AppendLine(FormatStat("Speed", playerStats.CurrentMovementSpeed, baseSpeed));
        sb.AppendLine(FormatStat("Atk Spd", playerStats.CurrentAttackSpeed, baseAttackSpeed, false, "", true));
        sb.AppendLine(FormatStat("Detect", playerStats.CurrentDetectionRadius, baseDetection));
        sb.AppendLine();
        sb.AppendLine(FormatStat("Crit %", playerStats.CurrentCritChance, baseCritChance, false, "%"));
        sb.AppendLine(FormatStat("Crit DMG", playerStats.CurrentCritDamage * 100f, baseCritDamage, false, "%"));
        sb.AppendLine(FormatStat("Dodge", playerStats.CurrentDodgeChance, baseDodge, false, "%"));
        sb.AppendLine(FormatStat("Armor", playerStats.CurrentArmor, baseArmor));
        sb.AppendLine(FormatStat("Regen", playerStats.CurrentHealthRegen, baseRegen, false, "/s"));
        sb.AppendLine(FormatStat("Lifesteal", playerStats.CurrentLifeSteal, baseLifeSteal, false, "%"));
        
        statsText.text = sb.ToString();
        
        // Also update NVIDIA tech display
        UpdateNvidiaTechDisplay();
    }
    
    /// <summary>
    /// Updates the NVIDIA technology status display with REAL detected values.
    /// This queries the actual native plugin to get current running state.
    /// </summary>
    private void UpdateNvidiaTechDisplay()
    {
        if (nvidiaTechText == null)
        {
            // Try to find or create it
            if (pauseMenuUI != null)
            {
                var texts = pauseMenuUI.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.gameObject.name.ToLower().Contains("nvidia") || t.gameObject.name.ToLower().Contains("tech"))
                    {
                        nvidiaTechText = t;
                        break;
                    }
                }
            }
        }
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=24><b>NVIDIA TECH</b></size>");
        sb.AppendLine();
        
        // Platform check
        string gpu = SystemInfo.graphicsDeviceName;
        bool isNvidia = gpu.ToLower().Contains("nvidia") || gpu.ToLower().Contains("geforce") || gpu.ToLower().Contains("rtx");
        sb.AppendLine($"<size=18>GPU: {gpu}</size>");
        sb.AppendLine();
        
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Query REAL status from native plugin
        try
        {
            // Reflex Status
            bool reflexAvailable = StreamlineReflexPlugin.IsAvailable();
            bool reflexSupported = reflexAvailable && StreamlineReflexPlugin.IsReflexSupported();
            var reflexMode = StreamlineReflexPlugin.GetMode();
            
            string reflexStatus = "<color=#FF4C4C>OFF</color>";
            if (reflexSupported && reflexMode != StreamlineReflexPlugin.ReflexMode.Off)
            {
                reflexStatus = reflexMode == StreamlineReflexPlugin.ReflexMode.LowLatencyWithBoost 
                    ? "<color=#4CFF4C>ON + BOOST</color>" 
                    : "<color=#4CFF4C>ON</color>";
            }
            else if (!reflexSupported)
            {
                reflexStatus = "<color=#888888>Not Supported</color>";
            }
            sb.AppendLine($"Reflex: {reflexStatus}");
            
            // DLSS Status
            bool dlssSupported = StreamlineDLSSPlugin.IsDLSSSupported();
            var dlssMode = StreamlineDLSSPlugin.GetDLSSMode();
            
            string dlssModeStr = dlssMode switch
            {
                StreamlineDLSSPlugin.DLSSMode.Off => "<color=#FF4C4C>OFF</color>",
                StreamlineDLSSPlugin.DLSSMode.MaxPerformance => "<color=#4CFF4C>Performance</color>",
                StreamlineDLSSPlugin.DLSSMode.Balanced => "<color=#4CFF4C>Balanced</color>",
                StreamlineDLSSPlugin.DLSSMode.MaxQuality => "<color=#4CFF4C>Quality</color>",
                StreamlineDLSSPlugin.DLSSMode.UltraPerformance => "<color=#4CFF4C>Ultra Perf</color>",
                StreamlineDLSSPlugin.DLSSMode.UltraQuality => "<color=#4CFF4C>Ultra Quality</color>",
                StreamlineDLSSPlugin.DLSSMode.DLAA => "<color=#4CFF4C>DLAA</color>",
                _ => "<color=#888888>Unknown</color>"
            };
            
            if (!dlssSupported)
            {
                dlssModeStr = "<color=#888888>Not Supported</color>";
            }
            sb.AppendLine($"DLSS: {dlssModeStr}");
            
            // Frame Generation Status
            bool frameGenSupported = StreamlineDLSSPlugin.IsFrameGenSupported();
            var frameGenMode = StreamlineDLSSPlugin.GetFrameGenMode();
            int framesGenerated = StreamlineDLSSPlugin.GetNumFramesToGenerate();
            
            string frameGenStr;
            if (!frameGenSupported)
            {
                frameGenStr = "<color=#888888>Not Supported (RTX 40+)</color>";
            }
            else if (frameGenMode == StreamlineDLSSPlugin.DLSSGMode.Off || framesGenerated == 0)
            {
                frameGenStr = "<color=#FF4C4C>OFF</color>";
            }
            else
            {
                int multiplier = framesGenerated + 1; // 1 generated = 2x, 2 generated = 3x
                frameGenStr = $"<color=#4CFF4C>{multiplier}x ({framesGenerated} gen)</color>";
            }
            sb.AppendLine($"Frame Gen: {frameGenStr}");
            
            // Latency stats if available
            if (StreamlineReflexPlugin.IsPCLSupported())
            {
                if (StreamlineReflexPlugin.GetLatencyStats(out var stats) && stats.TotalLatencyMs > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"<size=18>Latency: {stats.TotalLatencyMs:F1}ms</size>");
                }
            }
        }
        catch (System.DllNotFoundException)
        {
            sb.AppendLine("<color=#888888>Plugin not loaded</color>");
            sb.AppendLine("<size=16>Run build-reflex-plugin.ps1</size>");
        }
        catch (System.Exception e)
        {
            sb.AppendLine($"<color=#FF4C4C>Error: {e.Message}</color>");
        }
#else
        // Not Windows standalone build
        sb.AppendLine("<color=#888888>NVIDIA tech only available</color>");
        sb.AppendLine("<color=#888888>in Windows builds</color>");
#endif
        
        if (nvidiaTechText != null)
        {
            nvidiaTechText.text = sb.ToString();
        }
        else
        {
            // Log to console if no UI element
            Debug.Log("[PauseMenu] NVIDIA Tech Status:\n" + sb.ToString().Replace("<color=#4CFF4C>", "").Replace("<color=#FF4C4C>", "").Replace("<color=#888888>", "").Replace("</color>", ""));
        }
    }
    
    private string FormatStat(string name, float value, float baseValue, bool noColor = false, string suffix = "", bool lowerIsBetter = false)
    {
        string valueStr = value.ToString("F1") + suffix;
        
        if (noColor)
            return $"{name}: <color=white>{valueStr}</color>";
        
        float diff = value - baseValue;
        if (lowerIsBetter) diff = -diff;
        
        string color = "white";
        if (diff > 0.01f) color = "#4CFF4C";
        else if (diff < -0.01f) color = "#FF4C4C";
        
        return $"{name}: <color={color}>{valueStr}</color>";
    }
    
    // Re-check EventSystem when this component is enabled
    void OnEnable()
    {
        EnsureEventSystemActive();
    }
}
