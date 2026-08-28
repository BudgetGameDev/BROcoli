using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ResponsiveMainMenuLayout
{
    private void ApplyResponsiveLayout(bool force)
    {
        if (!built || root == null || safeArea == null || card == null)
            return;

        Rect currentSafeArea = Screen.safeArea;
        Vector2 currentRootSize = root.rect.size;
        int visibilitySignature = GetVisibilitySignature();

        if (
            !force
            && Approximately(currentSafeArea, lastSafeArea)
            && Vector2.SqrMagnitude(currentRootSize - lastRootSize) < 0.25f
            && visibilitySignature == lastVisibilitySignature
        )
        {
            return;
        }

        lastSafeArea = currentSafeArea;
        lastRootSize = currentRootSize;
        lastVisibilitySignature = visibilitySignature;

        ApplySafeArea(currentSafeArea);
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
            Mathf.Max(430f, available.y - verticalMargin * 2f)
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
        SetTopAnchored(title.rectTransform, compact ? 48f : 68f, innerWidth, compact ? 50f : 70f);
        title.fontSize = narrow ? (compact ? 34f : 44f) : (compact ? 42f : 56f);
        SetTopAnchored(
            subtitle.rectTransform,
            compact ? 98f : 138f,
            innerWidth,
            compact ? 24f : 32f
        );
        subtitle.fontSize = narrow ? 15f : (compact ? 16f : 19f);

        footer.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        footer.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        footer.rectTransform.pivot = new Vector2(0.5f, 0f);
        footer.rectTransform.sizeDelta = new Vector2(innerWidth, compact ? 32f : 40f);
        footer.rectTransform.anchoredPosition = new Vector2(0f, compact ? 12f : 18f);
        footer.fontSize = narrow ? 11f : (compact ? 12f : 14f);

        float headerSpace = compact ? 145f : 215f;
        float footerSpace = compact ? 52f : 70f;
        float actionTop = top - headerSpace;
        float actionBottom = -top + footerSpace;

        LayoutActiveButtons(mainButtons, innerWidth, actionTop, actionBottom, compact, narrow);
        LayoutModePanel(innerWidth, actionTop, actionBottom, compact, narrow);
        LayoutSettingsPanel(innerWidth, actionTop, actionBottom, compact, narrow);

        heroField.anchorMin = new Vector2(0f, available.x > available.y ? 0.56f : 0.66f);
    }

    private void ApplySafeArea(Rect pixelSafeArea)
    {
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        safeArea.anchorMin = new Vector2(pixelSafeArea.xMin / width, pixelSafeArea.yMin / height);
        safeArea.anchorMax = new Vector2(pixelSafeArea.xMax / width, pixelSafeArea.yMax / height);
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
    }

    private void LayoutActiveButtons(
        Button[] buttons,
        float width,
        float top,
        float bottom,
        bool compact,
        bool narrow
    )
    {
        int count = 0;
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.activeInHierarchy)
                count++;
        }

        if (count == 0)
            return;

        float gap = compact ? 8f : 13f;
        float availableHeight = Mathf.Max(1f, top - bottom);
        float buttonHeight = Mathf.Clamp(
            (availableHeight - gap * (count - 1)) / count,
            compact ? 44f : 52f,
            compact ? 58f : 70f
        );
        float contentHeight = buttonHeight * count + gap * (count - 1);
        float y = (top + bottom) * 0.5f + contentHeight * 0.5f - buttonHeight * 0.5f;

        foreach (Button button in buttons)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            SetCenteredRect(button.GetComponent<RectTransform>(), width, buttonHeight, y);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.fontSize = narrow ? (compact ? 17f : 20f) : (compact ? 19f : 23f);
            y -= buttonHeight + gap;
        }
    }

    private void LayoutModePanel(float width, float top, float bottom, bool compact, bool narrow)
    {
        if (modePanel == null)
            return;

        Stretch(modePanel);

        float titleHeight = compact ? 26f : 34f;
        float titleGap = compact ? 8f : 14f;
        float buttonTop = top - titleHeight - titleGap;

        if (modeTitle != null)
        {
            modeTitle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            modeTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            modeTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            modeTitle.rectTransform.sizeDelta = new Vector2(width, titleHeight);
            modeTitle.rectTransform.anchoredPosition = new Vector2(0f, top - titleHeight * 0.5f);
            modeTitle.fontSize = narrow ? 18f : (compact ? 19f : 22f);
        }

        float gap = compact ? 8f : 13f;
        float availableHeight = Mathf.Max(1f, buttonTop - bottom);
        float buttonHeight = Mathf.Clamp(
            (availableHeight - gap * (modeButtons.Length - 1)) / modeButtons.Length,
            compact ? 44f : 52f,
            compact ? 58f : 70f
        );
        float contentHeight = buttonHeight * modeButtons.Length + gap * (modeButtons.Length - 1);
        float y = (buttonTop + bottom) * 0.5f + contentHeight * 0.5f - buttonHeight * 0.5f;

        foreach (Button button in modeButtons)
        {
            if (button == null)
                continue;

            SetCenteredRect(button.GetComponent<RectTransform>(), width, buttonHeight, y);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.fontSize = narrow ? (compact ? 17f : 20f) : (compact ? 19f : 23f);
            y -= buttonHeight + gap;
        }
    }
}
