using UnityEngine;
using static MenuTheme;

public sealed partial class DiabloHud
{
    private void ApplyResponsiveLayout(bool force)
    {
        if (safeArea == null)
            return;

        Rect currentSafeArea = Screen.safeArea;
        RectTransform root = transform as RectTransform;
        Vector2 rootSize = root != null ? root.rect.size : Vector2.zero;
        if (
            !force
            && Approximately(currentSafeArea, lastSafeArea)
            && Vector2.SqrMagnitude(rootSize - lastRootSize) < 0.25f
        )
        {
            return;
        }

        lastSafeArea = currentSafeArea;
        lastRootSize = rootSize;
        ApplySafeArea(safeArea, currentSafeArea);
        Canvas.ForceUpdateCanvases();

        float width = Mathf.Max(480f, safeArea.rect.width);
        bool compact = width < 900f;
        float margin = compact ? 20f : 32f;
        float resourceWidth = Mathf.Min(compact ? 300f : 420f, width * 0.39f);
        float resourceHeight = compact ? 30f : 36f;
        float resourceBottom = compact ? 42f : 52f;

        SetBottomCorner(
            playerHealthBar?.transform as RectTransform,
            false,
            margin,
            resourceBottom,
            resourceWidth,
            resourceHeight
        );
        SetBottomCorner(manaPanel, true, margin, resourceBottom, resourceWidth, resourceHeight);

        RectTransform experienceRect = experienceBar?.transform as RectTransform;
        if (experienceRect != null)
        {
            experienceRect.anchorMin = new Vector2(0f, 0f);
            experienceRect.anchorMax = new Vector2(1f, 0f);
            experienceRect.pivot = new Vector2(0.5f, 0f);
            experienceRect.anchoredPosition = new Vector2(0f, compact ? 10f : 14f);
            experienceRect.sizeDelta = new Vector2(-margin * 2f, compact ? 16f : 20f);
        }

        if (enemyPanel != null)
        {
            enemyPanel.anchorMin = new Vector2(0.5f, 1f);
            enemyPanel.anchorMax = new Vector2(0.5f, 1f);
            enemyPanel.pivot = new Vector2(0.5f, 1f);
            enemyPanel.anchoredPosition = new Vector2(0f, compact ? -18f : -24f);
            enemyPanel.sizeDelta = new Vector2(
                Mathf.Min(compact ? 520f : 720f, width - margin * 2f),
                compact ? 34f : 42f
            );
        }

        if (playerHealthLabel != null)
            playerHealthLabel.fontSize = compact ? 13f : 16f;
        if (manaLabel != null)
            manaLabel.fontSize = compact ? 13f : 16f;
        if (experienceLabel != null)
            experienceLabel.fontSize = compact ? 11f : 14f;
        if (enemyLabel != null)
            enemyLabel.fontSize = compact ? 16f : 20f;
    }

    private static void SetBottomCorner(
        RectTransform rect,
        bool right,
        float margin,
        float bottom,
        float width,
        float height
    )
    {
        if (rect == null)
            return;

        float x = right ? 1f : 0f;
        rect.anchorMin = new Vector2(x, 0f);
        rect.anchorMax = new Vector2(x, 0f);
        rect.pivot = new Vector2(x, 0f);
        rect.anchoredPosition = new Vector2(right ? -margin : margin, bottom);
        rect.sizeDelta = new Vector2(width, height);
    }
}
