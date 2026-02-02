using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Displays a level up screen with 3 upgrade choices.
/// Works with mouse, controller, and touch input.
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

    [Header("Audio")]
    [SerializeField] private ProceduralLevelUpAudio levelUpAudio;

    private bool isShowing = false;
    private UpgradeOption[] currentOptions = new UpgradeOption[3];
    private PlayerStats playerStats;
    private int selectedIndex = 0;

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

        // Generate 3 random upgrade options
        for (int i = 0; i < 3; i++)
        {
            currentOptions[i] = UpgradeOption.GenerateRandom(newLevel);
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

        if (choiceRarityTexts[index] != null)
        {
            choiceRarityTexts[index].text = option.GetRarityName();
            choiceRarityTexts[index].color = rarityColor;
        }

        if (choiceNameTexts[index] != null)
        {
            choiceNameTexts[index].text = option.DisplayName;
        }

        if (choiceDescTexts[index] != null)
        {
            choiceDescTexts[index].text = option.Description;
            choiceDescTexts[index].color = rarityColor;
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

        // Keyboard/controller navigation
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

        // Arrow key / D-pad navigation
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            NavigateSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            NavigateSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            SelectUpgrade(selectedIndex);
        }
    }

    private void NavigateSelection(int direction)
    {
        selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, 2);
        if (choiceButtons[selectedIndex] != null)
        {
            EventSystem.current?.SetSelectedGameObject(choiceButtons[selectedIndex].gameObject);
        }
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
