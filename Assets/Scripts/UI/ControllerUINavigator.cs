using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles controller/gamepad input for UI navigation across all menus.
/// Provides prominent visual feedback for selected items.
/// Attach to any menu panel that needs controller support.
/// </summary>
public class ControllerUINavigator : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Color selectedBorderColor = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private float selectedBorderWidth = 6f;
    [SerializeField] private float selectedScaleMultiplier = 1.08f;
    [SerializeField] private float hoverScaleMultiplier = 1.04f;
    [SerializeField] private float animationSpeed = 12f;
    
    [Header("Navigation")]
    [SerializeField] private bool horizontal = true;
    [SerializeField] private bool vertical = true;
    [SerializeField] private float inputRepeatDelay = 0.3f;
    [SerializeField] private Button firstSelected;
    
    private List<Button> navigableButtons = new List<Button>();
    private int currentIndex = 0;
    private float lastInputTime;
    private Dictionary<Button, SelectionVisual> buttonVisuals = new Dictionary<Button, SelectionVisual>();
    private bool isActive = false;
    
    private class SelectionVisual
    {
        public GameObject borderObject;
        public Image borderImage;
        public Vector3 originalScale;
        public RectTransform rectTransform;
    }
    
    void OnEnable()
    {
        isActive = true;
        RefreshButtonList();
        
        // Delay initial selection to ensure UI is ready
        Invoke(nameof(SelectInitialButton), 0.05f);
    }
    
    void OnDisable()
    {
        isActive = false;
        ClearAllVisuals();
    }
    
    private void RefreshButtonList()
    {
        navigableButtons.Clear();
        buttonVisuals.Clear();
        
        // Find all active buttons in children
        Button[] allButtons = GetComponentsInChildren<Button>(false);
        foreach (var btn in allButtons)
        {
            if (btn.gameObject.activeInHierarchy && btn.interactable)
            {
                navigableButtons.Add(btn);
                SetupButtonVisual(btn);
            }
        }
    }
    
    private void SetupButtonVisual(Button btn)
    {
        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt == null) return;
        
        var visual = new SelectionVisual
        {
            originalScale = rt.localScale,
            rectTransform = rt
        };
        
        // Create border object
        GameObject border = new GameObject("SelectionBorder");
        border.transform.SetParent(btn.transform, false);
        border.transform.SetAsFirstSibling();
        
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-selectedBorderWidth, -selectedBorderWidth);
        borderRect.offsetMax = new Vector2(selectedBorderWidth, selectedBorderWidth);
        
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = selectedBorderColor;
        borderImg.raycastTarget = false;
        
        // Create inner mask to make it a border (not filled)
        GameObject inner = new GameObject("InnerMask");
        inner.transform.SetParent(border.transform, false);
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(selectedBorderWidth, selectedBorderWidth);
        innerRect.offsetMax = new Vector2(-selectedBorderWidth, -selectedBorderWidth);
        
        // Use a mask component to create the border effect
        Image innerImg = inner.AddComponent<Image>();
        innerImg.color = Color.clear;
        innerImg.raycastTarget = false;
        
        // Actually just use Outline component instead for cleaner border
        Destroy(border);
        Destroy(inner);
        
        // Add outline effect to button image
        Image btnImage = btn.GetComponent<Image>();
        if (btnImage != null)
        {
            Outline outline = btn.gameObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = btn.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = selectedBorderColor;
            outline.effectDistance = new Vector2(selectedBorderWidth, selectedBorderWidth);
            outline.enabled = false;
            visual.borderImage = btnImage;
            
            // Store reference via tag since we can't store Outline in Image
            btn.gameObject.name = btn.gameObject.name; // Keep name
        }
        
        buttonVisuals[btn] = visual;
        
        // Setup hover events for mouse
        EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = btn.gameObject.AddComponent<EventTrigger>();
        }
        
        // Add pointer enter event
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        int btnIndex = navigableButtons.IndexOf(btn);
        enterEntry.callback.AddListener((data) => OnButtonHover(btnIndex));
        trigger.triggers.Add(enterEntry);
        
        // Add pointer exit event
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) => OnButtonUnhover(btnIndex));
        trigger.triggers.Add(exitEntry);
    }
    
    private void SelectInitialButton()
    {
        if (navigableButtons.Count == 0)
        {
            RefreshButtonList();
        }
        
        if (navigableButtons.Count > 0)
        {
            if (firstSelected != null && navigableButtons.Contains(firstSelected))
            {
                currentIndex = navigableButtons.IndexOf(firstSelected);
            }
            else
            {
                currentIndex = 0;
            }
            SelectButton(currentIndex);
        }
    }
    
    void Update()
    {
        if (!isActive || navigableButtons.Count == 0) return;
        
        // Handle gamepad/keyboard input
        HandleNavigationInput();
        HandleSubmitInput();
        
        // Update visual animations
        UpdateVisuals();
    }
    
    private void HandleNavigationInput()
    {
        if (Time.unscaledTime - lastInputTime < inputRepeatDelay) return;
        
        Vector2 nav = Vector2.zero;
        
        // Check keyboard
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) nav.x = -1;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) nav.x = 1;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) nav.y = 1;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) nav.y = -1;
        
        // Check gamepad
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            
            if (Mathf.Abs(dpad.x) > 0.5f) nav.x = Mathf.Sign(dpad.x);
            else if (Mathf.Abs(stick.x) > 0.5f) nav.x = Mathf.Sign(stick.x);
            
            if (Mathf.Abs(dpad.y) > 0.5f) nav.y = Mathf.Sign(dpad.y);
            else if (Mathf.Abs(stick.y) > 0.5f) nav.y = Mathf.Sign(stick.y);
        }
        
        if (nav == Vector2.zero) return;
        
        int newIndex = currentIndex;
        
        // Determine navigation direction based on menu layout
        if (horizontal && Mathf.Abs(nav.x) > 0.1f)
        {
            newIndex += (int)Mathf.Sign(nav.x);
        }
        else if (vertical && Mathf.Abs(nav.y) > 0.1f)
        {
            // Up = -1 (previous), Down = +1 (next) for vertical layouts
            newIndex -= (int)Mathf.Sign(nav.y);
        }
        
        // Clamp to valid range
        newIndex = Mathf.Clamp(newIndex, 0, navigableButtons.Count - 1);
        
        if (newIndex != currentIndex)
        {
            lastInputTime = Time.unscaledTime;
            SelectButton(newIndex);
            PlayNavigationSound();
        }
    }
    
    private void HandleSubmitInput()
    {
        bool submit = false;
        
        // Keyboard
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            submit = true;
        }
        
        // Gamepad A button (South)
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            submit = true;
        }
        
        if (submit && currentIndex >= 0 && currentIndex < navigableButtons.Count)
        {
            Button btn = navigableButtons[currentIndex];
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
            }
        }
    }
    
    private void SelectButton(int index)
    {
        if (index < 0 || index >= navigableButtons.Count) return;
        
        // Deselect old
        if (currentIndex >= 0 && currentIndex < navigableButtons.Count)
        {
            SetButtonSelected(navigableButtons[currentIndex], false);
        }
        
        currentIndex = index;
        
        // Select new
        SetButtonSelected(navigableButtons[currentIndex], true);
        
        // Update EventSystem
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(navigableButtons[currentIndex].gameObject);
        }
    }
    
    private void OnButtonHover(int index)
    {
        if (index >= 0 && index < navigableButtons.Count)
        {
            SelectButton(index);
        }
    }
    
    private void OnButtonUnhover(int index)
    {
        // Keep current selection on mouse exit
    }
    
    private void SetButtonSelected(Button btn, bool selected)
    {
        if (btn == null) return;
        
        Outline outline = btn.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = selected;
        }
    }
    
    private void UpdateVisuals()
    {
        foreach (var kvp in buttonVisuals)
        {
            Button btn = kvp.Key;
            SelectionVisual visual = kvp.Value;
            
            if (btn == null || visual.rectTransform == null) continue;
            
            bool isSelected = (navigableButtons.IndexOf(btn) == currentIndex);
            
            // Target scale
            float targetScale = isSelected ? selectedScaleMultiplier : 1f;
            Vector3 target = visual.originalScale * targetScale;
            
            // Smooth animation (use unscaled time for paused menus)
            visual.rectTransform.localScale = Vector3.Lerp(
                visual.rectTransform.localScale, 
                target, 
                Time.unscaledDeltaTime * animationSpeed
            );
        }
    }
    
    private void PlayNavigationSound()
    {
        // Could add UI navigation sound here
    }
    
    private void ClearAllVisuals()
    {
        foreach (var kvp in buttonVisuals)
        {
            if (kvp.Key != null)
            {
                Outline outline = kvp.Key.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                }
                
                // Reset scale
                if (kvp.Value.rectTransform != null)
                {
                    kvp.Value.rectTransform.localScale = kvp.Value.originalScale;
                }
            }
        }
    }
    
    /// <summary>
    /// Check if gamepad Start/Menu button was pressed (for opening pause menu)
    /// </summary>
    public static bool WasStartButtonPressed()
    {
        if (Gamepad.current != null)
        {
            return Gamepad.current.startButton.wasPressedThisFrame;
        }
        return false;
    }
    
    /// <summary>
    /// Check if gamepad B button was pressed (for back/cancel)
    /// </summary>
    public static bool WasBackButtonPressed()
    {
        if (Gamepad.current != null)
        {
            return Gamepad.current.buttonEast.wasPressedThisFrame;
        }
        return false;
    }
}
