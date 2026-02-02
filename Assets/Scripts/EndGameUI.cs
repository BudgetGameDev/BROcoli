using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// End game screen UI with full controller/keyboard navigation support.
/// </summary>
public class EndGameUI : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    
    // Controller navigation
    private Button[] menuButtons;
    private int selectedIndex = 0;
    private float lastNavTime = 0f;
    private const float NavRepeatDelay = 0.25f;
    private Outline[] buttonOutlines;
    private Vector3[] originalScales;
    
    void Start()
    {
        SetupControllerNavigation();
    }
    
    private void SetupControllerNavigation()
    {
        // Find buttons if not assigned
        if (restartButton == null || mainMenuButton == null)
        {
            Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var btn in allButtons)
            {
                string name = btn.gameObject.name.ToLower();
                if (name.Contains("restart"))
                {
                    restartButton = btn;
                }
                else if (name.Contains("mainmenu") || name.Contains("main"))
                {
                    mainMenuButton = btn;
                }
            }
        }
        
        // Build button array (restart first so it's default)
        var buttonList = new List<Button>();
        if (restartButton != null) buttonList.Add(restartButton);
        if (mainMenuButton != null) buttonList.Add(mainMenuButton);
        menuButtons = buttonList.ToArray();
        
        if (menuButtons.Length == 0)
        {
            Debug.LogWarning("[EndGameUI] No buttons found for controller navigation");
            return;
        }
        
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
            
            // Add outline for selection border
            Outline outline = menuButtons[i].GetComponent<Outline>();
            if (outline == null)
            {
                outline = menuButtons[i].gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(1f, 0.9f, 0.2f, 1f); // Yellow
            outline.effectDistance = new Vector2(6f, 6f);
            outline.enabled = false;
            buttonOutlines[i] = outline;
            
            // Setup hover events for mouse
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
        
        // Select restart button by default
        selectedIndex = 0;
        SelectButton(0);
        
        // Focus for controller
        if (EventSystem.current != null && menuButtons[0] != null)
        {
            EventSystem.current.SetSelectedGameObject(menuButtons[0].gameObject);
        }
    }
    
    void Update()
    {
        HandleControllerNavigation();
        UpdateSelectionVisuals();
    }
    
    private void HandleControllerNavigation()
    {
        if (menuButtons == null || menuButtons.Length == 0) return;
        
        // Rate limit
        if (Time.unscaledTime - lastNavTime < NavRepeatDelay) return;
        
        float nav = 0f;
        
        // Keyboard - support both horizontal and vertical
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || 
            Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) nav = -1f;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ||
                 Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) nav = 1f;
        
        // Gamepad
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            
            if (Mathf.Abs(dpad.y) > 0.5f) nav = -Mathf.Sign(dpad.y);
            else if (Mathf.Abs(dpad.x) > 0.5f) nav = Mathf.Sign(dpad.x);
            else if (Mathf.Abs(stick.y) > 0.5f) nav = -Mathf.Sign(stick.y);
            else if (Mathf.Abs(stick.x) > 0.5f) nav = Mathf.Sign(stick.x);
        }
        
        // Navigate
        if (Mathf.Abs(nav) > 0.1f)
        {
            lastNavTime = Time.unscaledTime;
            int direction = (int)Mathf.Sign(nav);
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
                // Play select sound and invoke action
                ProceduralUIAudio.PlaySelect();
                if (btn == restartButton)
                {
                    Restart();
                }
                else if (btn == mainMenuButton)
                {
                    GoToMainMenu();
                }
            }
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

    public void Restart()
    {
        Debug.Log("Restarting game");
        SceneManager.LoadScene("Game");
    }

    public void GoToMainMenu()
    {
        Debug.Log("Going to main menu");
        SceneManager.LoadScene("MainMenuScene");
    }
}
