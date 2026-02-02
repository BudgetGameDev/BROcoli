using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Victory screen at wave 15 with infinite mode option. Full controller/mobile support.
/// </summary>
public class VictoryScreen : MonoBehaviour
{
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button endRunButton;
    
    public event Action OnContinueToInfinite;
    public event Action OnEndRun;
    
    private Button[] menuButtons;
    private int selectedIndex = 0;
    private float lastNavTime = 0f;
    private const float NavRepeatDelay = 0.25f;
    private Outline[] buttonOutlines;
    private Vector3[] originalScales;
    private bool isActive = false;
    
    void Awake() { if (victoryPanel != null) victoryPanel.SetActive(false); }
    
    public void Show()
    {
        isActive = true;
        Time.timeScale = 0f;
        if (victoryPanel == null) CreateVictoryUI();
        victoryPanel.SetActive(true);
        SetupControllerNavigation();
    }
    
    public void Hide()
    {
        isActive = false;
        Time.timeScale = 1f;
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }
    
    private void CreateVictoryUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        
        victoryPanel = new GameObject("VictoryPanel");
        victoryPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = victoryPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        victoryPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
        
        GameObject content = new GameObject("Content");
        content.transform.SetParent(victoryPanel.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(600, 400);
        
        CreateText(content.transform, "VICTORY!", 64, new Vector2(0, 120), new Color(1f, 0.9f, 0.2f), FontStyles.Bold);
        CreateText(content.transform, "You survived 15 waves!\nEnter Infinite Mode?", 28, new Vector2(0, 40), Color.white, FontStyles.Normal);
        
        GameObject btnsObj = new GameObject("Buttons");
        btnsObj.transform.SetParent(content.transform, false);
        RectTransform btnsRect = btnsObj.AddComponent<RectTransform>();
        btnsRect.anchorMin = btnsRect.anchorMax = new Vector2(0.5f, 0f);
        btnsRect.anchoredPosition = new Vector2(0, 80); btnsRect.sizeDelta = new Vector2(500, 60);
        var layout = btnsObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        
        continueButton = CreateButton(btnsObj.transform, "INFINITE MODE", new Color(0.2f, 0.7f, 0.3f));
        endRunButton = CreateButton(btnsObj.transform, "END RUN", new Color(0.6f, 0.3f, 0.3f));
        continueButton.onClick.AddListener(OnContinueClicked);
        endRunButton.onClick.AddListener(OnEndRunClicked);
    }
    
    private void CreateText(Transform parent, string text, int size, Vector2 pos, Color color, FontStyles style)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(500, 80);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color; tmp.fontStyle = style;
    }
    
    private Button CreateButton(Transform parent, string text, Color bgColor)
    {
        GameObject btnObj = new GameObject(text.Replace(" ", ""));
        btnObj.transform.SetParent(parent, false);
        btnObj.AddComponent<RectTransform>().sizeDelta = new Vector2(220, 55);
        var bg = btnObj.AddComponent<Image>(); bg.color = bgColor;
        Button btn = btnObj.AddComponent<Button>(); btn.targetGraphic = bg;
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 26; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.fontStyle = FontStyles.Bold;
        return btn;
    }
    
    private void SetupControllerNavigation()
    {
        var buttonList = new List<Button>();
        if (continueButton != null) buttonList.Add(continueButton);
        if (endRunButton != null) buttonList.Add(endRunButton);
        menuButtons = buttonList.ToArray();
        if (menuButtons.Length == 0) return;
        
        buttonOutlines = new Outline[menuButtons.Length];
        originalScales = new Vector3[menuButtons.Length];
        
        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;
            RectTransform rt = menuButtons[i].GetComponent<RectTransform>();
            originalScales[i] = rt != null ? rt.localScale : Vector3.one;
            
            Outline outline = menuButtons[i].GetComponent<Outline>() ?? menuButtons[i].gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.9f, 0.2f, 1f);
            outline.effectDistance = new Vector2(6f, 6f); outline.enabled = false;
            buttonOutlines[i] = outline;
            
            int index = i;
            EventTrigger trigger = menuButtons[i].GetComponent<EventTrigger>() ?? menuButtons[i].gameObject.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => SelectButton(index));
            trigger.triggers.Add(enterEntry);
        }
        selectedIndex = 0; SelectButton(0);
        if (EventSystem.current != null && menuButtons[0] != null)
            EventSystem.current.SetSelectedGameObject(menuButtons[0].gameObject);
    }
    
    void Update()
    {
        if (!isActive) return;
        HandleControllerNavigation();
        UpdateSelectionVisuals();
    }
    
    private void HandleControllerNavigation()
    {
        if (menuButtons == null || menuButtons.Length == 0) return;
        if (Time.unscaledTime - lastNavTime < NavRepeatDelay) return;
        
        float nav = 0f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) nav = -1f;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) nav = 1f;
        
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (Mathf.Abs(dpad.x) > 0.5f) nav = Mathf.Sign(dpad.x);
            else if (Mathf.Abs(stick.x) > 0.5f) nav = Mathf.Sign(stick.x);
        }
        
        if (Mathf.Abs(nav) > 0.1f)
        {
            lastNavTime = Time.unscaledTime;
            int newIndex = Mathf.Clamp(selectedIndex + (int)Mathf.Sign(nav), 0, menuButtons.Length - 1);
            if (newIndex != selectedIndex) SelectButton(newIndex);
        }
        
        bool select = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        if (Gamepad.current != null) select |= Gamepad.current.buttonSouth.wasPressedThisFrame;
        if (select && menuButtons[selectedIndex] != null)
        {
            menuButtons[selectedIndex].onClick.Invoke();
        }
    }
    
    private void SelectButton(int index)
    {
        if (menuButtons == null || index < 0 || index >= menuButtons.Length) return;
        selectedIndex = index;
        if (EventSystem.current != null && menuButtons[index] != null)
            EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
    }
    
    private void UpdateSelectionVisuals()
    {
        if (menuButtons == null) return;
        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;
            bool isSelected = (i == selectedIndex);
            if (buttonOutlines[i] != null) buttonOutlines[i].enabled = isSelected;
            RectTransform rt = menuButtons[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                float targetScale = isSelected ? 1.08f : 1f;
                rt.localScale = Vector3.Lerp(rt.localScale, originalScales[i] * targetScale, Time.unscaledDeltaTime * 12f);
            }
        }
    }
    
    private void OnContinueClicked() { Hide(); OnContinueToInfinite?.Invoke(); }
    private void OnEndRunClicked() { Time.timeScale = 1f; OnEndRun?.Invoke(); SceneManager.LoadScene("EndGame"); }
}
