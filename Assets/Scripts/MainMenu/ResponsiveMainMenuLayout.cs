using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a responsive, safe-area-aware presentation around the existing main-menu buttons.
/// The original Button and onClick instances are retained, so scene navigation remains intact.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class ResponsiveMainMenuLayout : MonoBehaviour
{
    private static readonly Color Background = Hex("#0F1713");
    private static readonly Color HeroSurface = Hex("#173E2B");
    private static readonly Color CardSurface = Hex("#1D2923");
    private static readonly Color SurfaceVariant = Hex("#2A3831");
    private static readonly Color Primary = Hex("#43A047");
    private static readonly Color PrimaryHover = Hex("#55B95A");
    private static readonly Color PrimaryPressed = Hex("#347C38");
    private static readonly Color OnSurface = Hex("#F4F7F5");
    private static readonly Color OnSurfaceMuted = Hex("#B8C6BE");
    private static readonly Color Divider = new(0.65f, 0.84f, 0.71f, 0.22f);

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
    private TMP_Text modeTitle;
    private TMP_FontAsset materialFont;
    private CanvasScaler canvasScaler;
    private Button[] mainButtons;
    private Button[] modeButtons;
    private RectTransform modePanel;

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

        modeButtons = new[]
        {
            FindButton("WavesButton"),
            FindButton("DungeonButton"),
            FindButton("BackButton"),
        };

        Transform panel = FindDescendant(transform, "ModeSelectPanel");
        modePanel = panel as RectTransform;
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

        Shadow cardShadow = card.gameObject.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
        cardShadow.effectDistance = new Vector2(0f, -12f);
        cardShadow.useGraphicAlpha = true;

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

        if (modePanel != null)
        {
            modePanel.SetParent(card, false);
            modeTitle = CreateText("ModeTitle", modePanel, "CHOOSE A MODE", 22f, OnSurface);
        }

        StyleButtons(mainButtons);
        StyleButtons(modeButtons);
        StyleButtons(settingsActionButtons);
        SetButtonLabel("PlayButton", "PLAY");
        SetButtonLabel("PlayMobileButton", "PLAY WITH TOUCH");
        SetButtonLabel("SettingsButton", "SETTINGS");
        SetButtonLabel("InstallAppButton", "INSTALL APP");
        SetButtonLabel("QuitButton", "QUIT");
        SetButtonLabel("WavesButton", "WAVES");
        SetButtonLabel("DungeonButton", "DUNGEON");
        SetButtonLabel("BackButton", "BACK");

        built = true;
    }

    /// <summary>
    /// Hands the menu the order these buttons are laid out in, top to bottom.
    /// </summary>
    private void PublishNavigationOrder()
    {
        Button[] ordered = new Button[mainButtons.Length + modeButtons.Length];
        mainButtons.CopyTo(ordered, 0);
        modeButtons.CopyTo(ordered, mainButtons.Length);
        GetComponent<MainMenu>()?.SetNavigationOrder(ordered);
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
