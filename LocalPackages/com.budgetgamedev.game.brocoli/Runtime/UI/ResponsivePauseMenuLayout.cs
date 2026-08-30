using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Dresses the scene's pause panel in the main-menu presentation: a dimmed
    /// backdrop, one card with an accent bar and heading, the run's stats on their
    /// own surface and the existing buttons restyled. The scene's Button and
    /// onClick instances are kept, so <see cref="PauseMenu"/> keeps working.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResponsivePauseMenuLayout : MonoBehaviour
    {
        private const float MaximumCardWidth = 680f;
        private const float MaximumCardHeight = 800f;

        private RectTransform root;
        private RectTransform safeArea;
        private RectTransform card;
        private RectTransform accentBar;
        private RectTransform statsSurface;
        private TMP_Text eyebrow;
        private TMP_Text title;
        private TMP_Text statsText;
        private TMP_Text footer;
        private TMP_FontAsset materialFont;
        private Button[] buttons;

        private Rect lastSafeArea;
        private Vector2 lastRootSize;
        private bool built;

        private void Awake()
        {
            root = transform as RectTransform;
            materialFont = TMP_Settings.defaultFontAsset;
            BuildPresentation();
            ApplyResponsiveLayout(true);
        }

        private void OnEnable() => ApplyResponsiveLayout(true);

        private void LateUpdate() => ApplyResponsiveLayout(false);

        private void OnRectTransformDimensionsChange()
        {
            if (built)
                ApplyResponsiveLayout(true);
        }

        private void BuildPresentation()
        {
            if (root == null)
                return;

            Stretch(root);

            // The panel's own image becomes the scrim over the frozen gameplay.
            Image scrim = root.GetComponent<Image>();
            if (scrim == null)
                scrim = root.gameObject.AddComponent<Image>();
            scrim.sprite = null;
            scrim.type = Image.Type.Simple;
            scrim.color = new Color(Background.r, Background.g, Background.b, 0.88f);
            scrim.raycastTarget = true;

            safeArea = CreateRect("SafeArea", root);
            card = CreatePanel("PauseCard", safeArea, CardSurface);
            AddCardShadow(card);

            accentBar = CreatePanel("AccentBar", card, Primary);
            eyebrow = CreateText("Eyebrow", card, "BROCOLI", 17f, Primary, materialFont);

            // The scene's own heading and stats label carry on as the card's text.
            title = FindText("PausedText");
            if (title == null)
                title = CreateText("PausedText", card, "PAUSED", 56f, OnSurface, materialFont);
            StyleText(title, "PAUSED", 56f, OnSurface, materialFont);
            title.rectTransform.SetParent(card, false);

            statsSurface = CreatePanel("StatsSurface", card, SurfaceVariant);
            statsText = FindText("StatsText");
            if (statsText != null)
            {
                statsText.rectTransform.SetParent(statsSurface, false);
                if (materialFont != null)
                    statsText.font = materialFont;
                statsText.color = OnSurfaceMuted;
                statsText.alignment = TextAlignmentOptions.TopLeft;
                statsText.fontStyle = FontStyles.Normal;
                statsText.characterSpacing = 0f;
                statsText.enableAutoSizing = true;
                statsText.fontSizeMin = 10f;
                statsText.textWrappingMode = TextWrappingModes.Normal;
                statsText.overflowMode = TextOverflowModes.Truncate;
                statsText.margin = new Vector4(20f, 16f, 20f, 16f);
                Stretch(statsText.rectTransform);
            }

            footer = CreateText(
                "InputHint",
                card,
                "ESC  ·  START  ·  B  TO RESUME",
                14f,
                OnSurfaceMuted,
                materialFont
            );

            buttons = new[] { FindButton("ResumeButton"), FindButton("MainMenuButton") };
            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                button.transform.SetParent(card, false);
                StyleButton(button, button.name == "ResumeButton", materialFont);
            }

            SetButtonLabel("ResumeButton", "RESUME");
            SetButtonLabel("MainMenuButton", "MAIN MENU");

            built = true;
        }

        private void ApplyResponsiveLayout(bool force)
        {
            if (!built || root == null || safeArea == null || card == null)
                return;

            Rect currentSafeArea = Screen.safeArea;
            Vector2 currentRootSize = root.rect.size;

            if (
                !force
                && Approximately(currentSafeArea, lastSafeArea)
                && Vector2.SqrMagnitude(currentRootSize - lastRootSize) < 0.25f
            )
            {
                return;
            }

            lastSafeArea = currentSafeArea;
            lastRootSize = currentRootSize;

            ApplySafeArea(safeArea, currentSafeArea);
            Canvas.ForceUpdateCanvases();

            Vector2 available = safeArea.rect.size;
            float horizontalMargin = available.x < 700f ? 20f : 42f;
            float verticalMargin = available.y < 700f ? 18f : 36f;
            float cardWidth = Mathf.Min(
                MaximumCardWidth,
                Mathf.Max(280f, available.x - horizontalMargin * 2f)
            );
            float cardHeight = Mathf.Min(
                MaximumCardHeight,
                Mathf.Max(360f, available.y - verticalMargin * 2f)
            );
            card.sizeDelta = new Vector2(cardWidth, cardHeight);
            card.anchoredPosition = Vector2.zero;

            bool compact = cardHeight < 650f;
            bool narrow = cardWidth < 500f;
            float innerWidth = cardWidth - (narrow ? 40f : 72f);
            float top = cardHeight * 0.5f;

            SetTopAnchored(accentBar, 0f, innerWidth, compact ? 5f : 7f);
            SetTopAnchored(eyebrow.rectTransform, compact ? 22f : 34f, innerWidth, 24f);
            eyebrow.fontSize = compact ? 14f : 17f;
            SetTopAnchored(
                title.rectTransform,
                compact ? 48f : 68f,
                innerWidth,
                compact ? 50f : 70f
            );
            title.fontSize = narrow ? (compact ? 34f : 44f) : (compact ? 42f : 56f);

            footer.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            footer.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            footer.rectTransform.sizeDelta = new Vector2(innerWidth, compact ? 32f : 40f);
            footer.rectTransform.anchoredPosition = new Vector2(0f, compact ? 12f : 18f);
            footer.fontSize = narrow ? 11f : (compact ? 12f : 14f);

            float contentTop = top - (compact ? 108f : 152f);
            float contentBottom = -top + (compact ? 52f : 70f);

            float gap = compact ? 8f : 13f;
            float buttonHeight = compact ? 52f : 64f;
            float actionsHeight = buttonHeight * buttons.Length + gap * (buttons.Length - 1);
            float y = contentBottom + actionsHeight - buttonHeight * 0.5f;

            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                SetCenteredRect(button.GetComponent<RectTransform>(), innerWidth, buttonHeight, y);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.fontSize = narrow ? (compact ? 17f : 20f) : (compact ? 19f : 23f);
                y -= buttonHeight + gap;
            }

            float statsBottom = contentBottom + actionsHeight + gap;
            float statsHeight = Mathf.Max(0f, contentTop - statsBottom);
            statsSurface.gameObject.SetActive(statsHeight > 60f);
            SetCenteredRect(
                statsSurface,
                innerWidth,
                statsHeight,
                statsBottom + statsHeight * 0.5f
            );
            if (statsText != null)
                statsText.fontSizeMax = narrow ? (compact ? 15f : 17f) : (compact ? 16f : 19f);
        }

        private void SetButtonLabel(string buttonName, string value)
        {
            TMP_Text label = FindButton(buttonName)?.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = value;
        }

        private Button FindButton(string objectName)
        {
            Transform match = FindDescendant(transform, objectName);
            return match != null ? match.GetComponent<Button>() : null;
        }

        private TMP_Text FindText(string objectName)
        {
            Transform match = FindDescendant(transform, objectName);
            return match != null ? match.GetComponent<TMP_Text>() : null;
        }
    }
}
