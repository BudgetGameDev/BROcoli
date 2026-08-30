using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Displays a level up screen with 3 upgrade choices.
    /// Works with mouse, controller, and touch input.
    /// Features prominent visual selection feedback.
    /// </summary>
    public partial class LevelUpScreen : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        private GameObject levelUpPanel;

        [SerializeField]
        private TextMeshProUGUI levelText;

        [Header("Choice Buttons")]
        [SerializeField]
        private Button[] choiceButtons = new Button[3];

        [SerializeField]
        private TextMeshProUGUI[] choiceRarityTexts = new TextMeshProUGUI[3];

        [SerializeField]
        private TextMeshProUGUI[] choiceNameTexts = new TextMeshProUGUI[3];

        [SerializeField]
        private TextMeshProUGUI[] choiceDescTexts = new TextMeshProUGUI[3];

        [SerializeField]
        private Image[] choiceBackgrounds = new Image[3];

        [Header("Confirmation")]
        [Tooltip(
            "Optional scene-wired button. A styled confirmation button is created when omitted."
        )]
        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private TextMeshProUGUI confirmButtonText;

        [Header("Selection Visuals")]
        [SerializeField]
        private Color selectedBorderColor = new Color(1f, 0.9f, 0.2f, 1f);

        [SerializeField]
        private float selectedBorderWidth = 8f;

        [SerializeField]
        private float selectedScale = 1.1f;

        [SerializeField]
        private float normalScale = 1f;

        [SerializeField]
        private float scaleAnimSpeed = 15f;

        [Header("Audio")]
        [SerializeField]
        private ProceduralLevelUpAudio levelUpAudio;

        private bool isShowing = false;
        private UpgradeOption[] currentOptions = new UpgradeOption[3];
        private PlayerStats playerStats;
        private int selectedIndex = 0;
        private bool hasPendingSelection;
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
            EnsureConfirmButton();
            SetupButtons();
            SetupSelectionVisuals();
            UpdateConfirmButton();
        }

        private void EnsureConfirmButton()
        {
            if (confirmButton != null)
            {
                if (confirmButtonText == null)
                    confirmButtonText = confirmButton.GetComponentInChildren<TextMeshProUGUI>(true);
                return;
            }

            if (levelUpPanel == null)
                return;

            GameObject buttonObject = new GameObject(
                "ConfirmUpgradeButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.transform.SetParent(levelUpPanel.transform, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.08f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.08f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(320f, 72f);
            buttonRect.anchoredPosition = Vector2.zero;

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.35f, 0.18f, 0.98f);

            confirmButton = buttonObject.GetComponent<Button>();
            confirmButton.targetGraphic = background;
            ColorBlock colors = confirmButton.colors;
            colors.highlightedColor = new Color(0.2f, 0.55f, 0.28f, 1f);
            colors.pressedColor = new Color(0.08f, 0.25f, 0.12f, 1f);
            colors.disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.75f);
            confirmButton.colors = colors;

            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 1f, 0.78f, 0.85f);
            outline.effectDistance = new Vector2(3f, 3f);

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);

            confirmButtonText = labelObject.GetComponent<TextMeshProUGUI>();
            confirmButtonText.alignment = TextAlignmentOptions.Center;
            confirmButtonText.fontSize = 28f;
            confirmButtonText.fontStyle = FontStyles.Bold;
            confirmButtonText.color = Color.white;
            confirmButtonText.raycastTarget = false;

            buttonObject.transform.SetAsLastSibling();
        }

        private void SetupSelectionVisuals()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null)
                    continue;

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
                    choiceButtons[i].onClick.AddListener(() => ChooseUpgrade(index));
                }
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(ConfirmSelectedUpgrade);
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
            hasPendingSelection = false;

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
            UpdateConfirmButton();

            // Select first button for controller/keyboard navigation
            if (choiceButtons[0] != null)
            {
                EventSystem.current?.SetSelectedGameObject(choiceButtons[0].gameObject);
            }
        }

        private void UpdateChoiceUI(int index, UpgradeOption option)
        {
            if (index < 0 || index >= 3)
                return;

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

        /// <summary>
        /// Autoplay/E2E hook: programmatically pick an upgrade (mirrors a button click)
        /// so an unattended run never stalls on the paused level-up menu.
        /// </summary>
        public void AutoSelectUpgrade(int index) => ApplyUpgrade(index);

        /// <summary>Autoplay/E2E hook: number of upgrade options currently offered.</summary>
        public int OptionCount => currentOptions?.Length ?? 0;

        /// <summary>Autoplay/E2E hook: read an offered option so a bot can score/choose it.</summary>
        public UpgradeOption GetOption(int index) =>
            (currentOptions != null && index >= 0 && index < currentOptions.Length)
                ? currentOptions[index]
                : null;

        private void ChooseUpgrade(int index)
        {
            if (index < 0 || index >= currentOptions.Length)
                return;
            if (currentOptions[index] == null)
                return;

            SetSelectedIndex(index);
            hasPendingSelection = true;
            UpdateConfirmButton();
        }

        private void ConfirmSelectedUpgrade()
        {
            if (!hasPendingSelection)
                return;
            ApplyUpgrade(selectedIndex);
        }

        private void ApplyUpgrade(int index)
        {
            if (index < 0 || index >= currentOptions.Length)
                return;
            if (currentOptions[index] == null)
                return;
            if (playerStats == null)
                return;

            // Use hyped sound for level-up stat selection!
            ProceduralUIAudio.PlayLevelUpSelect();
            PlayerStats upgradedStats = playerStats;
            currentOptions[index].ApplyTo(upgradedStats);
            Hide();
            upgradedStats.CompleteLevelUpChoice();
        }

        public void Hide()
        {
            if (levelUpPanel != null)
            {
                levelUpPanel.SetActive(false);
            }

            Time.timeScale = 1f;
            isShowing = false;
            hasPendingSelection = false;
            playerStats = null;
            UpdateConfirmButton();
        }

        public bool IsShowing() => isShowing;

        public bool HasPendingSelection => hasPendingSelection;

        void Update()
        {
            if (!isShowing)
                return;

            // Handle gamepad/keyboard navigation
            HandleControllerNavigation();

            // Keyboard number shortcuts
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                ChooseUpgrade(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                ChooseUpgrade(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                ChooseUpgrade(2);
            }

            // Update selection visuals
            UpdateSelectionVisuals();
        }
    }
}
