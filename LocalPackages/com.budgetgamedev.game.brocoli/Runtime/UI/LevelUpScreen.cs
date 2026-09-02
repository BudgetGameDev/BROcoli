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

        private static LevelUpScreen active;

        /// <summary>Whether the upgrade choice is in front of the player anywhere.</summary>
        public static bool AnyShowing => active != null && active.isShowing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            active = null;
        }

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
            active = this;
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

        void OnDestroy()
        {
            if (active == this)
                active = null;
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
    }
}
