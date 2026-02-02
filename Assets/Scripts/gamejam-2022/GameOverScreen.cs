using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GameOverScreen : MonoBehaviour
{
    private bool open;
    
    [Header("Menu Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    
    // Controller navigation
    private Button[] menuButtons;
    private int selectedIndex = 0;
    private float lastNavTime = 0f;
    private const float NavRepeatDelay = 0.25f;
    private Outline[] buttonOutlines;
    private Vector3[] originalScales;
    
    public bool getOpen() {
        return open;
    }

    public void Start() {
        open = false;
    }
    
    public void Setup()
    {
        open = true;
        gameObject.SetActive(true);
        
        // Reset selection to restart button
        selectedIndex = 0;
        
        // Setup controller navigation
        SetupControllerNavigation();
    }
    
    private void SetupControllerNavigation()
    {
        // Find buttons if not assigned
        if (restartButton == null || mainMenuButton == null)
        {
            Button[] allButtons = GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                string name = btn.gameObject.name.ToLower();
                if (name.Contains("restart") || name.Contains("retry") || name.Contains("endurbyrja"))
                {
                    restartButton = btn;
                }
                else if (name.Contains("main") || name.Contains("menu") || name.Contains("enda") || name.Contains("exit"))
                {
                    mainMenuButton = btn;
                }
            }
        }
        
        // Build button array
        var buttonList = new List<Button>();
        if (restartButton != null) buttonList.Add(restartButton);
        if (mainMenuButton != null) buttonList.Add(mainMenuButton);
        menuButtons = buttonList.ToArray();
        
        buttonOutlines = new Outline[menuButtons.Length];
        originalScales = new Vector3[menuButtons.Length];
        
        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;
            
            // Connect click handlers
            if (menuButtons[i] == restartButton)
            {
                menuButtons[i].onClick.RemoveAllListeners();
                menuButtons[i].onClick.AddListener(Endurbyrja);
            }
            else if (menuButtons[i] == mainMenuButton)
            {
                menuButtons[i].onClick.RemoveAllListeners();
                menuButtons[i].onClick.AddListener(Enda);
            }
            
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
        
        // Select restart button by default (index 0)
        if (menuButtons.Length > 0)
        {
            selectedIndex = 0;
            SelectButton(0);
            
            // Force focus for controller
            if (EventSystem.current != null && menuButtons[0] != null)
            {
                EventSystem.current.SetSelectedGameObject(menuButtons[0].gameObject);
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

    public void Endurbyrja()
    {
        ProceduralUIAudio.PlaySelect();
        Debug.Log("restart");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Enda()
    {
        ProceduralUIAudio.PlaySelect();
        Debug.Log("exit");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
    }

    public void Update() {
        if (!open) return;
        
        // Handle controller navigation
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
            selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, menuButtons.Length - 1);
            SelectButton(selectedIndex);
        }
        
        // Submit with Enter/Space/Gamepad A
        bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            submit = true;
        }
        
        if (submit && menuButtons != null && selectedIndex >= 0 && selectedIndex < menuButtons.Length)
        {
            Button btn = menuButtons[selectedIndex];
            if (btn != null && btn.interactable)
            {
                // Invoke the correct handler directly for reliability
                if (btn == restartButton)
                {
                    Endurbyrja();
                }
                else if (btn == mainMenuButton)
                {
                    Enda();
                }
                else
                {
                    btn.onClick.Invoke();
                }
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
