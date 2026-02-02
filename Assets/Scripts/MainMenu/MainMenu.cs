using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour
{
    [Header("PWA Install Button (Optional)")]
    [Tooltip("Assign a button to show/hide based on PWA install status")]
    public GameObject installAppButton;
    
    [Header("Menu Buttons")]
    [SerializeField] private Button[] menuButtons;
    
    // Controller navigation
    private int selectedIndex = 0;
    private float lastNavTime = 0f;
    private const float NavRepeatDelay = 0.25f;
    private Outline[] buttonOutlines;
    private Vector3[] originalScales;
    
    void Start()
    {
        // Hide install button if already running as installed PWA
        if (installAppButton != null)
        {
            bool showInstallButton = !PWAHelper.IsInstalledAsPWA;
            installAppButton.SetActive(showInstallButton);
            
            if (PWAHelper.IsInstalledAsPWA)
            {
                Debug.Log("[MainMenu] Running as installed PWA - hiding install button");
            }
        }
        
        PWAHelper.LogStatus();
        
        // Setup controller navigation
        SetupControllerNavigation();
    }
    
    private void SetupControllerNavigation()
    {
        // Auto-find buttons if not assigned
        if (menuButtons == null || menuButtons.Length == 0)
        {
            menuButtons = GetComponentsInChildren<Button>(true);
        }
        
        // Filter to only active and interactable buttons
        var buttonList = new List<Button>();
        foreach (var btn in menuButtons)
        {
            if (btn != null && btn.gameObject.activeInHierarchy && btn.interactable)
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
            enterEntry.callback.AddListener((data) => SelectButton(index));
            trigger.triggers.Add(enterEntry);
        }
        
        // Select first button
        if (menuButtons.Length > 0)
        {
            SelectButton(0);
        }
    }
    
    private void SelectButton(int index)
    {
        if (menuButtons == null || index < 0 || index >= menuButtons.Length) return;
        
        // Play hover sound if index changed
        if (index != selectedIndex)
        {
            ProceduralUIAudio.PlayHover();
        }
        
        selectedIndex = index;
        
        if (EventSystem.current != null && menuButtons[index] != null)
        {
            EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
        }
    }
    
    /// <summary>
    /// Shows the PWA install wizard. Hook this up to an "Install App" button.
    /// </summary>
    public void ShowInstallPrompt()
    {
        Debug.Log("[MainMenu] Install App button pressed");
        PWAHelper.ShowInstallPrompt();
    }
    
    /// <summary>
    /// Toggles fullscreen mode. Useful for players who didn't install as PWA.
    /// </summary>
    public void ToggleFullscreen()
    {
        Debug.Log("[MainMenu] Fullscreen toggle pressed");
        PWAHelper.ToggleFullscreen();
    }
  
    public void playGame()
    {
        ProceduralUIAudio.PlaySelect();
        Debug.Log("Play Game has been pressed - Virtual Controller HIDDEN");
        PlayerPrefs.SetInt("ShowVirtualController", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
    
    public void playGameMobile()
    {
        ProceduralUIAudio.PlaySelect();
        Debug.Log("Play Game (Mobile) has been pressed - Virtual Controller SHOWN");
        PlayerPrefs.SetInt("ShowVirtualController", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void GoToSettingsMenu()
    {
        Debug.Log("Settings has been pressed");
        SceneManager.LoadScene("SettingsMenuScene");
    }

    public void GoToMainMenu()
    {
        Debug.Log("Back has been pressed");
        SceneManager.LoadScene("MainMenuScene");
    }

    public void quitGame()
    {
        Debug.Log("Quit Game has been pressed");
        PWAHelper.Quit();
    }

    public void Update() {
        // Handle controller navigation
        HandleControllerNavigation();
        UpdateSelectionVisuals();
        
        // Legacy keyboard shortcut (press any key to start)
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            // Don't auto-start if using navigation keys
            if (!Keyboard.current.upArrowKey.isPressed && !Keyboard.current.downArrowKey.isPressed &&
                !Keyboard.current.leftArrowKey.isPressed && !Keyboard.current.rightArrowKey.isPressed &&
                !Keyboard.current.wKey.isPressed && !Keyboard.current.sKey.isPressed &&
                !Keyboard.current.enterKey.isPressed && !Keyboard.current.spaceKey.isPressed)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            }
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
            int direction = vertical > 0 ? -1 : 1;
            int newIndex = Mathf.Clamp(selectedIndex + direction, 0, menuButtons.Length - 1);
            if (newIndex != selectedIndex)
            {
                SelectButton(newIndex);
            }
        }
        
        // Submit with Enter/Space/Gamepad A
        bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            submit = true;
        }
        
        if (submit && selectedIndex >= 0 && selectedIndex < menuButtons.Length)
        {
            Button btn = menuButtons[selectedIndex];
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
            }
        }
    }
    
    private void UpdateSelectionVisuals()
    {
        if (menuButtons == null || buttonOutlines == null) return;
        
        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;
            
            bool isSelected = (i == selectedIndex);
            
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
}
