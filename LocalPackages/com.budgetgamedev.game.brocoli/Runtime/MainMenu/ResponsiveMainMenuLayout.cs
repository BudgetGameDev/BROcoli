using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Builds a responsive, safe-area-aware presentation around the existing main-menu buttons.
    /// The original Button and onClick instances are retained, so scene navigation remains intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ResponsiveMainMenuLayout : MonoBehaviour
    {
        private const float MaximumCardWidth = 680f;
        private const float MaximumCardHeight = 800f;

        private RectTransform root;
        private RectTransform safeArea;
        private RectTransform card;
        private RectTransform heroField;
        private RectTransform accentBar;
        private TMP_Text eyebrow;
        private TMP_Text title;
        private TMP_Text subtitle;
        private TMP_Text footer;
        private TMP_FontAsset materialFont;
        private CanvasScaler canvasScaler;
        private Button[] mainButtons;

        private Rect lastSafeArea;
        private Vector2 lastRootSize;
        private int lastVisibilitySignature = int.MinValue;
        private bool built;

        private void Awake()
        {
            root = transform as RectTransform;
            canvasScaler = GetComponentInParent<Canvas>()?.GetComponent<CanvasScaler>();
            materialFont = TMP_Settings.defaultFontAsset;

            CacheExistingMenuObjects();
            BuildPresentation();
            PublishNavigationOrder();
            ApplyResponsiveLayout(true);
        }

        private void LateUpdate()
        {
            ApplyResponsiveLayout(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (built)
                ApplyResponsiveLayout(true);
        }

        private void CacheExistingMenuObjects()
        {
            mainButtons = new[]
            {
                FindButton("PlayButton"),
                FindButton("PlayMobileButton"),
                FindButton("InstallAppButton"),
                FindButton("QuitButton"),
            };
        }

        private void BuildPresentation()
        {
            if (root == null)
                return;

            ConfigureCanvasScaler();
            Stretch(root);

            RectTransform background = CreatePanel("MaterialBackground", root, Background);
            background.SetAsFirstSibling();
            Stretch(background);

            heroField = CreatePanel("HeroField", root, HeroSurface);
            heroField.SetSiblingIndex(1);
            heroField.anchorMin = new Vector2(0f, 0.56f);
            heroField.anchorMax = Vector2.one;
            heroField.offsetMin = Vector2.zero;
            heroField.offsetMax = Vector2.zero;

            RectTransform heroDivider = CreatePanel("HeroDivider", heroField, Divider);
            heroDivider.anchorMin = new Vector2(0f, 0f);
            heroDivider.anchorMax = new Vector2(1f, 0f);
            heroDivider.pivot = new Vector2(0.5f, 0f);
            heroDivider.anchoredPosition = Vector2.zero;
            heroDivider.sizeDelta = new Vector2(0f, 2f);

            safeArea = CreateRect("SafeArea", root);
            safeArea.SetAsLastSibling();

            card = CreatePanel("MenuCard", safeArea, CardSurface);
            Image cardImage = card.GetComponent<Image>();
            cardImage.type = Image.Type.Simple;

            AddCardShadow(card);

            accentBar = CreatePanel("AccentBar", card, Primary);
            eyebrow = CreateText("Eyebrow", card, "ACTION SURVIVAL", 18f, Primary);
            title = CreateText("Title", card, "BROCOLI", 58f, OnSurface);
            subtitle = CreateText("Subtitle", card, "CLEANSE. SURVIVE. GROW.", 20f, OnSurfaceMuted);
            footer = CreateText(
                "InputHint",
                card,
                "KEYBOARD  ·  CONTROLLER  ·  TOUCH",
                15f,
                OnSurfaceMuted
            );

            BuildSettingsPresentation();
            ReparentButtons(mainButtons, card);

            StyleButtons(mainButtons);
            StyleButtons(settingsActionButtons);
            SetButtonLabel("PlayButton", "PLAY");
            SetButtonLabel("PlayMobileButton", "PLAY WITH TOUCH");
            SetButtonLabel("SettingsButton", "SETTINGS");
            SetButtonLabel("InstallAppButton", "INSTALL APP");
            SetButtonLabel("QuitButton", "QUIT");

            built = true;
        }

        /// <summary>
        /// Hands the menu the order these buttons are laid out in, top to bottom.
        /// </summary>
        private void PublishNavigationOrder()
        {
            GetComponent<MainMenu>()?.SetNavigationOrder(mainButtons);
        }

        private void ConfigureCanvasScaler()
        {
            if (canvasScaler == null)
                return;

            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
            canvasScaler.referencePixelsPerUnit = 100f;
        }
    }
}
