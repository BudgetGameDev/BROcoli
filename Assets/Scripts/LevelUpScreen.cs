using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Displays a level up screen with 3 upgrade choices.
/// Works with mouse, controller, and touch input.
/// Features prominent visual selection feedback.
/// </summary>
public class LevelUpScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private TextMeshProUGUI levelText;
    
    [Header("Choice Buttons")]
    [SerializeField] private Button[] choiceButtons = new Button[3];
    [SerializeField] private TextMeshProUGUI[] choiceRarityTexts = new TextMeshProUGUI[3];
    [SerializeField] private TextMeshProUGUI[] choiceNameTexts = new TextMeshProUGUI[3];
    [SerializeField] private TextMeshProUGUI[] choiceDescTexts = new TextMeshProUGUI[3];
    [SerializeField] private Image[] choiceBackgrounds = new Image[3];
    
    [Header("Selection Visuals")]
    [SerializeField] private Color selectedBorderColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private float selectedBorderWidth = 8f;
    [SerializeField] private float selectedScale = 1.1f;
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float scaleAnimSpeed = 15f;

    [Header("Audio")]
    [SerializeField] private ProceduralLevelUpAudio levelUpAudio;

    private bool isShowing = false;
    private UpgradeOption[] currentOptions = new UpgradeOption[3];
    private PlayerStats playerStats;
    private int selectedIndex = 0;
    private float lastNavTime = 0f;
    private const float NavRepeatDelay = 0.2f;
    
    // Visual components
    private Outline[] buttonOutlines = new Outline[3];
    private Vector3[] originalScales = new Vector3[3];

    void Awake()
    {
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }

        if (levelUpAudio == null)
        {
            levelUpAudio = GetComponent<ProceduralLevelUpAudio>();
            if (levelUpAudio == null)
            {
                levelUpAudio = gameObject.AddComponent<ProceduralLevelUpAudio>();
            }
        }
    }

    void Start()
    {
        SetupButtons();
        SetupSelectionVisuals();
    }
    
    private void SetupSelectionVisuals()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null) continue;
            
            RectTransform rt = choiceButtons[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                originalScales[i] = rt.localScale;
            }
            
            // Add outline for selection border
            Outline outline = choiceButtons[i].GetComponent<Outline>();
            if (outline == null)
            {
                outline = choiceButtons[i].gameObject.AddComponent<Outline>();
            }
            outline.effectColor = selectedBorderColor;
            outline.effectDistance = new Vector2(selectedBorderWidth, selectedBorderWidth);
            outline.enabled = false;
            buttonOutlines[i] = outline;
            
            // Setup hover events
            int index = i;
            EventTrigger trigger = choiceButtons[i].GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = choiceButtons[i].gameObject.AddComponent<EventTrigger>();
            }
            
            // Clear existing triggers
            trigger.triggers.Clear();
            
            // Pointer enter
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => SetSelectedIndex(index));
            trigger.triggers.Add(enterEntry);
            
            // Pointer click (for touch)
            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) => SelectUpgrade(index));
            trigger.triggers.Add(clickEntry);
        }
    }

    private void SetupButtons()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
            {
                int index = i;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => SelectUpgrade(index));
            }
        }
    }

    public void Show(int newLevel, PlayerStats stats)
    {
        if (levelUpPanel == null)
        {
            Debug.LogWarning("[LevelUpScreen] Panel not assigned");
            return;
        }

        isShowing = true;
        playerStats = stats;
        selectedIndex = 0;

        if (levelUpAudio != null)
        {
            levelUpAudio.PlayLevelUpSound();
        }

        if (levelText != null)
        {
            levelText.text = $"LEVEL {newLevel}";
        }

        // Generate 3 upgrade options - one might be a troll upgrade
        for (int i = 0; i < 3; i++)
        {
            // 25% chance for troll upgrade on each slot, higher at higher levels
            float trollChance = Mathf.Min(0.15f + newLevel * 0.02f, 0.35f);
            
            if (Random.value < trollChance)
            {
                currentOptions[i] = UpgradeOption.GenerateTrollUpgrade(newLevel);
            }
            else
            {
                currentOptions[i] = UpgradeOption.GenerateRandom(newLevel);
            }
            UpdateChoiceUI(i, currentOptions[i]);
        }

        EnsureEventSystemActive();

        levelUpPanel.SetActive(true);
        levelUpPanel.transform.SetAsLastSibling();
        Time.timeScale = 0f;

        // Select first button for controller/keyboard navigation
        if (choiceButtons[0] != null)
        {
            EventSystem.current?.SetSelectedGameObject(choiceButtons[0].gameObject);
        }
    }

    private void UpdateChoiceUI(int index, UpgradeOption option)
    {
        if (index < 0 || index >= 3) return;

        Color rarityColor = option.GetRarityColor();
        
        // Troll upgrades get a special yellow/orange tint
        if (option.IsTrollUpgrade)
        {
            rarityColor = new Color(1f, 0.6f, 0.2f); // Orange for trade-offs
        }

        if (choiceRarityTexts[index] != null)
        {
            string rarityText = option.IsTrollUpgrade ? "TRADE-OFF" : option.GetRarityName();
            choiceRarityTexts[index].text = rarityText;
            choiceRarityTexts[index].color = rarityColor;
        }

        if (choiceNameTexts[index] != null)
        {
            choiceNameTexts[index].text = option.DisplayName;
        }

        if (choiceDescTexts[index] != null)
        {
            // Troll upgrades already have colored description
            if (option.IsTrollUpgrade)
            {
                choiceDescTexts[index].text = option.Description;
                choiceDescTexts[index].color = Color.white; // White base, colors in rich text
            }
            else
            {
                choiceDescTexts[index].text = option.Description;
                choiceDescTexts[index].color = rarityColor;
            }
        }

        if (choiceBackgrounds[index] != null)
        {
            // Darken the rarity color for background
            Color bgColor = rarityColor * 0.3f;
            bgColor.a = 0.9f;
            choiceBackgrounds[index].color = bgColor;
        }
    }

    private void SelectUpgrade(int index)
    {
        if (index < 0 || index >= currentOptions.Length) return;
        if (currentOptions[index] == null) return;
        if (playerStats == null) return;

        // Use hyped sound for level-up stat selection!
        ProceduralUIAudio.PlayLevelUpSelect();
        currentOptions[index].ApplyTo(playerStats);
        Hide();
    }

    public void Hide()
    {
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        isShowing = false;
        playerStats = null;
    }

    public bool IsShowing() => isShowing;

    void Update()
    {
        if (!isShowing) return;

        // Handle gamepad/keyboard navigation
        HandleControllerNavigation();
        
        // Keyboard number shortcuts
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectUpgrade(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectUpgrade(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SelectUpgrade(2);
        }
        
        // Update selection visuals
        UpdateSelectionVisuals();
    }
    
    private void HandleControllerNavigation()
    {
        // Rate limit navigation
        if (Time.unscaledTime - lastNavTime < NavRepeatDelay) return;
        
        float horizontal = 0f;
        
        // Keyboard arrows
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) horizontal = -1f;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) horizontal = 1f;
        
        // Gamepad
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            
            if (Mathf.Abs(dpad.x) > 0.5f) horizontal = Mathf.Sign(dpad.x);
            else if (Mathf.Abs(stick.x) > 0.5f) horizontal = Mathf.Sign(stick.x);
        }
        
        // Navigate
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            lastNavTime = Time.unscaledTime;
            int newIndex = selectedIndex + (int)Mathf.Sign(horizontal);
            newIndex = Mathf.Clamp(newIndex, 0, 2);
            SetSelectedIndex(newIndex);
        }
        
        // Submit with Enter/Space/Gamepad A
        bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            submit = true;
        }
        
        if (submit)
        {
            SelectUpgrade(selectedIndex);
        }
    }
    
    private void SetSelectedIndex(int index)
    {
        if (index < 0 || index > 2) return;
        
        // Play hover sound if index changed
        if (index != selectedIndex)
        {
            ProceduralUIAudio.PlayHover();
        }
        
        selectedIndex = index;
        
        // Update EventSystem selection
        if (choiceButtons[selectedIndex] != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(choiceButtons[selectedIndex].gameObject);
        }
    }
    
    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null) continue;
            
            bool isSelected = (i == selectedIndex);
            
            // Update outline
            if (buttonOutlines[i] != null)
            {
                buttonOutlines[i].enabled = isSelected;
            }
            
            // Animate scale
            RectTransform rt = choiceButtons[i].GetComponent<RectTransform>();
            if (rt != null && i < originalScales.Length)
            {
                float targetScale = isSelected ? selectedScale : normalScale;
                Vector3 target = originalScales[i] * targetScale;
                rt.localScale = Vector3.Lerp(rt.localScale, target, Time.unscaledDeltaTime * scaleAnimSpeed);
            }
        }
    }

    private void NavigateSelection(int direction)
    {
        selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, 2);
        SetSelectedIndex(selectedIndex);
    }

    private void EnsureEventSystemActive()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();

        if (eventSystem == null)
        {
            var allES = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allES.Length > 0)
            {
                eventSystem = allES[0];
            }
        }

        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem_LevelUp");
            eventSystem = esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }
        else if (!eventSystem.gameObject.activeInHierarchy)
        {
            eventSystem.gameObject.SetActive(true);
        }
    }
}
