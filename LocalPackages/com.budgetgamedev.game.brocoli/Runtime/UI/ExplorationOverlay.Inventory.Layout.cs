using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private void ApplyInventoryLayout(float width, float height)
        {
            if (nearbySurface == null || loadoutSurface == null || backpackSurface == null)
                return;

            bool compact = width < 920f || height < 620f;
            float horizontalPadding = compact ? 12f : 24f;
            float gap = compact ? 8f : 16f;
            float topInset = compact ? 70f : 82f;
            float bottomInset = compact ? 64f : 92f;
            float contentHeight = Mathf.Max(180f, height - topInset - bottomInset);
            float contentY = (bottomInset - topInset) * 0.5f;
            float availableWidth = Mathf.Max(440f, width - horizontalPadding * 2f);
            float nearbyWidth = Mathf.Clamp(width * 0.18f, 160f, 300f);
            float rightGridWidth = Mathf.Clamp(width * 0.38f, 300f, 720f);
            rightGridWidth = Mathf.Min(
                rightGridWidth,
                availableWidth - nearbyWidth - (compact ? 16f : 96f)
            );
            float rightGridHeight = contentHeight;
            float rowHeight = (rightGridHeight - gap) * 0.5f;
            float cellWidth = (rightGridWidth - gap) * 0.5f;
            float nearbyHeight = rightGridHeight;
            float rightCenterX = width * 0.5f - horizontalPadding - rightGridWidth * 0.5f;
            float topRowY = contentY + (rowHeight + gap) * 0.5f;
            float bottomRowY = contentY - (rowHeight + gap) * 0.5f;

            SetInventoryRect(
                nearbySurface,
                -width * 0.5f + horizontalPadding + nearbyWidth * 0.5f,
                contentY,
                nearbyWidth,
                nearbyHeight
            );
            SetInventoryRect(
                statsSurface,
                rightCenterX - (cellWidth + gap) * 0.5f,
                topRowY,
                cellWidth,
                rowHeight
            );
            SetInventoryRect(
                loadoutSurface,
                rightCenterX + (cellWidth + gap) * 0.5f,
                topRowY,
                cellWidth,
                rowHeight
            );
            SetInventoryRect(backpackSurface, rightCenterX, bottomRowY, rightGridWidth, rowHeight);

            LayoutSurfaceHeader(nearbyTitle, nearbyHint, nearbyWidth, compact);
            LayoutSurfaceHeader(statsTitle, runSummary, cellWidth, compact);
            LayoutSurfaceHeader(loadoutTitle, loadoutHint, cellWidth, compact);
            LayoutSurfaceHeader(backpackTitle, backpackHint, rightGridWidth, compact);
            LayoutNearbyList(nearbyWidth, nearbyHeight, compact);
            LayoutStats(cellWidth, rowHeight, compact);
            LayoutLoadout(cellWidth, rowHeight, compact);
            LayoutBackpack(rightGridWidth, rowHeight, compact);
            LayoutInventoryActionButtons(compact);

            inventoryDisclaimer.rectTransform.anchorMin = new Vector2(0f, 0f);
            inventoryDisclaimer.rectTransform.anchorMax = new Vector2(1f, 0f);
            inventoryDisclaimer.rectTransform.pivot = new Vector2(0.5f, 0f);
            inventoryDisclaimer.rectTransform.anchoredPosition = new Vector2(
                0f,
                compact ? 38f : 48f
            );
            inventoryDisclaimer.rectTransform.sizeDelta = new Vector2(-32f, 20f);
            inventoryDisclaimer.fontSize = compact ? 8f : 11f;
        }

        private void LayoutInventoryActionButtons(bool compact)
        {
            float width = compact ? 104f : 132f;
            float height = compact ? 38f : 44f;
            float gap = compact ? 8f : 12f;
            float y = compact ? 80f : 104f;
            float offset = (width + gap) * 0.5f;

            SetBottomCenterRect(
                inventoryTransferButton.transform as RectTransform,
                -offset,
                y,
                width,
                height
            );
            SetBottomCenterRect(
                inventoryEquipButton.transform as RectTransform,
                offset,
                y,
                width,
                height
            );
        }

        private static void LayoutSurfaceHeader(
            TMP_Text heading,
            TMP_Text hint,
            float width,
            bool compact
        )
        {
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0f, 1f);
            heading.rectTransform.pivot = new Vector2(0f, 1f);
            heading.rectTransform.anchoredPosition = new Vector2(compact ? 10f : 16f, -10f);
            heading.rectTransform.sizeDelta = new Vector2(width - (compact ? 20f : 32f), 25f);
            heading.fontSize = compact ? 13f : 19f;

            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0f, 1f);
            hint.rectTransform.pivot = new Vector2(0f, 1f);
            hint.rectTransform.anchoredPosition = new Vector2(
                compact ? 10f : 16f,
                compact ? -34f : -39f
            );
            hint.rectTransform.sizeDelta = new Vector2(width - (compact ? 20f : 32f), 18f);
            hint.fontSize = compact ? 7f : 10f;
        }

        private void LayoutLoadout(float width, float height, bool compact)
        {
            float inset = compact ? 8f : 14f;
            float top = compact ? 54f : 64f;
            float gearHeight = Mathf.Max(70f, height - top - inset);

            SetTopRect(gearStage, inset, top, width - inset * 2f, gearHeight);
            LayoutGearStage(width - inset * 2f, gearHeight, compact);
        }

        private void LayoutGearStage(float width, float height, bool compact)
        {
            float slotWidth = Mathf.Clamp(width * 0.215f, 30f, 72f);
            float slotHeight = Mathf.Clamp(height * 0.205f, 18f, 46f);
            float sideX = width * 0.5f - slotWidth * 0.5f - (compact ? 5f : 10f);
            float verticalSpace = Mathf.Max(0f, height - slotHeight * 4f);

            playerSilhouette.anchorMin =
                playerSilhouette.anchorMax =
                playerSilhouette.pivot =
                    new Vector2(0.5f, 0.5f);
            playerSilhouette.anchoredPosition = Vector2.zero;
            playerSilhouette.sizeDelta = new Vector2(
                Mathf.Max(42f, width - slotWidth * 2f - (compact ? 22f : 40f)),
                Mathf.Max(36f, height - (compact ? 16f : 26f))
            );

            for (int i = 0; i < gearSlots.Count; i++)
            {
                bool right = i >= 4;
                int verticalIndex = i % 4;
                float y =
                    height * 0.5f
                    - slotHeight * 0.5f
                    - verticalIndex * (slotHeight + verticalSpace / 3f);
                SetInventoryRect(gearSlots[i], right ? sideX : -sideX, y, slotWidth, slotHeight);
                TMP_Text label = gearSlots[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.fontSize = compact ? 6.5f : 9f;
            }
        }

        private void LayoutStats(float width, float height, bool compact)
        {
            float inset = compact ? 7f : 12f;
            float top = compact ? 54f : 64f;
            float columnWidth = (width - inset * 2f - 8f) * 0.5f;
            float availableHeight = Mathf.Max(0f, height - top - inset);
            float playerStatsHeight = Mathf.Min(
                compact ? 82f : 132f,
                availableHeight * (compact ? 0.42f : 0.4f)
            );
            float detailsGap = compact ? 5f : 9f;
            float detailsTop = top + playerStatsHeight + detailsGap;
            float detailsHeight = Mathf.Max(0f, height - detailsTop - inset);

            SetTopRect(statsLeft.rectTransform, inset, top, columnWidth, playerStatsHeight);
            SetTopRect(
                statsRight.rectTransform,
                inset + columnWidth + 8f,
                top,
                columnWidth,
                playerStatsHeight
            );
            statsLeft.fontSize = statsRight.fontSize = compact
                ? Mathf.Clamp(width / 48f, 5.5f, 7f)
                : Mathf.Clamp(height / 24f, 8f, 11f);
            statsLeft.lineSpacing = statsRight.lineSpacing = compact ? -10f : -4f;

            SetTopRect(
                selectedItemStatsSurface,
                inset,
                detailsTop,
                width - inset * 2f,
                detailsHeight
            );
            LayoutSelectedItemStats(width - inset * 2f, detailsHeight, compact);
        }

        private static void SetInventoryRect(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height
        )
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopRect(
            RectTransform rect,
            float left,
            float top,
            float width,
            float height
        )
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, Mathf.Max(0f, height));
        }

        private static void SetBottomCenterRect(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height
        )
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetGraphicRaycast(RectTransform rect, bool value)
        {
            Image image = rect != null ? rect.GetComponent<Image>() : null;
            if (image != null)
                image.raycastTarget = value;
        }

        private static void AddInventoryOutline(GameObject target, Color color)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
    }
}
