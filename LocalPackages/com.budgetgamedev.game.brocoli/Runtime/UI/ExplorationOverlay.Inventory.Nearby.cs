using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private static readonly Color NearbyListRowColor = new(0.12f, 0.17f, 0.14f, 0.56f);

        private RectTransform nearbyViewport;
        private RectTransform nearbyContent;
        private RectTransform nearbyScrollbarRect;
        private ScrollRect nearbyScrollRect;
        private Scrollbar nearbyScrollbar;
        private float nearbyRowHeight = 64f;
        private float nearbyRowGap = 7f;
        private float nearbyViewportHeight;
        private bool nearbyCompact;

        private void BuildNearbyInterface()
        {
            nearbySurface = CreateInventorySurface("NearbySurface", inventoryPanel);
            nearbyTitle = CreateInventoryHeading("NearbyTitle", nearbySurface, "NEARBY");
            nearbyHint = CreateInventoryHint(
                "NearbyHint",
                nearbySurface,
                "PROXIMITY LIST  ·  MOCK ITEMS"
            );

            nearbyViewport = CreatePanel(
                "NearbyViewport",
                nearbySurface,
                new Color(1f, 1f, 1f, 0.001f)
            );
            nearbyViewport.GetComponent<Image>().raycastTarget = true;
            nearbyViewport.gameObject.AddComponent<RectMask2D>();
            nearbyContent = CreateRect("NearbyContent", nearbyViewport);

            nearbyScrollbarRect = CreatePanel(
                "NearbyScrollbar",
                nearbySurface,
                new Color(0.02f, 0.04f, 0.03f, 0.7f)
            );
            RectTransform handle = CreatePanel(
                "Handle",
                nearbyScrollbarRect,
                new Color(GearAccent.r, GearAccent.g, GearAccent.b, 0.78f)
            );
            Stretch(handle);
            handle.offsetMin = new Vector2(2f, 2f);
            handle.offsetMax = new Vector2(-2f, -2f);
            nearbyScrollbar = nearbyScrollbarRect.gameObject.AddComponent<Scrollbar>();
            nearbyScrollbar.handleRect = handle;
            nearbyScrollbar.targetGraphic = handle.GetComponent<Image>();
            nearbyScrollbar.direction = Scrollbar.Direction.BottomToTop;
            nearbyScrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            nearbyScrollRect = nearbySurface.gameObject.AddComponent<ScrollRect>();
            nearbyScrollRect.content = nearbyContent;
            nearbyScrollRect.viewport = nearbyViewport;
            nearbyScrollRect.horizontal = false;
            nearbyScrollRect.vertical = true;
            nearbyScrollRect.movementType = ScrollRect.MovementType.Clamped;
            nearbyScrollRect.inertia = true;
            nearbyScrollRect.decelerationRate = 0.12f;
            nearbyScrollRect.scrollSensitivity = 34f;
            nearbyScrollRect.verticalScrollbar = nearbyScrollbar;
            nearbyScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            nearbyScrollRect.verticalScrollbarSpacing = 5f;

            RefreshNearbyList();
            nearbyScrollRect.verticalNormalizedPosition = 1f;
        }

        private RectTransform CreateNearbyListRow(int index, string itemName)
        {
            RectTransform row = CreatePanel(
                $"NearbyItem{index + 1:00}",
                nearbyContent,
                NearbyListRowColor
            );
            RegisterInventoryItem(row, InventoryPreviewLocation.Nearby, index);

            RectTransform icon = CreatePanel("Icon", row, OccupiedSlot);
            SetGraphicRaycast(icon, false);
            TMP_Text glyph = CreateText(
                "Glyph",
                icon,
                itemName.Substring(0, 1),
                18f,
                GearAccent,
                TMP_Settings.defaultFontAsset
            );
            Stretch(glyph.rectTransform);
            glyph.fontStyle = FontStyles.Bold;
            glyph.raycastTarget = false;

            TMP_Text label = CreateText(
                "ItemLabel",
                row,
                NearbyLabel(itemName),
                12f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return row;
        }

        private void RefreshNearbyList()
        {
            if (nearbyItems == null || nearbyContent == null)
                return;

            while (nearbyRows.Count < nearbyItems.Count)
                nearbyRows.Add(
                    CreateNearbyListRow(nearbyRows.Count, nearbyItems[nearbyRows.Count])
                );

            for (int i = 0; i < nearbyRows.Count; i++)
            {
                bool visible = i < nearbyItems.Count;
                RectTransform row = nearbyRows[i];
                row.gameObject.SetActive(visible);
                InventoryPreviewItem preview = row.GetComponent<InventoryPreviewItem>();
                preview.SetSlotIndex(visible ? i : -1);
                if (visible)
                    RefreshNearbyRow(i, row, preview);
            }

            if (nearbyHint != null)
                nearbyHint.text = $"PROXIMITY LIST  ·  {nearbyItems.Count} MOCK ITEMS";
            UpdateNearbyRowsLayout();
        }

        private void RefreshNearbyRow(int index, RectTransform row, InventoryPreviewItem preview)
        {
            string itemName = nearbyItems[index];
            preview.SetNormalColor(NearbyListRowColor);
            Transform iconTransform = FindDescendant(row, "Icon");
            TMP_Text glyph =
                iconTransform != null
                    ? iconTransform.Find("Glyph")?.GetComponent<TMP_Text>()
                    : null;
            if (glyph != null)
                glyph.text = itemName.Substring(0, 1);
            TMP_Text label = FindDescendant(row, "ItemLabel")?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = NearbyLabel(itemName);
        }

        private void LayoutNearbyList(float width, float height, bool compact)
        {
            float inset = compact ? 8f : 14f;
            float top = compact ? 54f : 64f;
            float bottom = compact ? 8f : 14f;
            float scrollbarWidth = compact ? 8f : 10f;
            float scrollbarGap = compact ? 4f : 6f;
            nearbyCompact = compact;
            nearbyRowHeight = compact ? 46f : 64f;
            nearbyRowGap = compact ? 5f : 7f;
            nearbyViewportHeight = Mathf.Max(40f, height - top - bottom);
            float viewportWidth = width - inset * 2f - scrollbarWidth - scrollbarGap;

            SetTopRect(nearbyViewport, inset, top, viewportWidth, nearbyViewportHeight);
            SetTopRect(
                nearbyScrollbarRect,
                inset + viewportWidth + scrollbarGap,
                top,
                scrollbarWidth,
                nearbyViewportHeight
            );
            UpdateNearbyRowsLayout();
        }

        private void UpdateNearbyRowsLayout()
        {
            if (nearbyContent == null || nearbyItems == null)
                return;

            float contentHeight = Mathf.Max(
                nearbyViewportHeight,
                nearbyItems.Count * nearbyRowHeight
                    + Mathf.Max(0, nearbyItems.Count - 1) * nearbyRowGap
            );
            nearbyContent.anchorMin = new Vector2(0f, 1f);
            nearbyContent.anchorMax = new Vector2(1f, 1f);
            nearbyContent.pivot = new Vector2(0.5f, 1f);
            nearbyContent.anchoredPosition = Vector2.zero;
            nearbyContent.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < nearbyItems.Count; i++)
            {
                RectTransform row = nearbyRows[i];
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -i * (nearbyRowHeight + nearbyRowGap));
                row.sizeDelta = new Vector2(0f, nearbyRowHeight);

                RectTransform icon = FindDescendant(row, "Icon") as RectTransform;
                float iconSize = Mathf.Min(nearbyRowHeight - 8f, nearbyCompact ? 32f : 44f);
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.pivot = new Vector2(0f, 0.5f);
                icon.anchoredPosition = new Vector2(5f, 0f);
                icon.sizeDelta = new Vector2(iconSize, iconSize);

                TMP_Text label = FindDescendant(row, "ItemLabel")?.GetComponent<TMP_Text>();
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(nearbyCompact ? 42f : 56f, 2f);
                label.rectTransform.offsetMax = new Vector2(-4f, -2f);
                label.fontSize = nearbyCompact ? 8f : 12f;
            }
        }

        private void EnsureNearbyItemVisible(int index)
        {
            if (
                nearbyScrollRect == null
                || nearbyContent == null
                || index < 0
                || index >= nearbyItems.Count
            )
                return;

            Canvas.ForceUpdateCanvases();
            float maxScroll = Mathf.Max(0f, nearbyContent.rect.height - nearbyViewport.rect.height);
            if (maxScroll <= 0.01f)
                return;

            float offset = (1f - nearbyScrollRect.verticalNormalizedPosition) * maxScroll;
            float rowTop = index * (nearbyRowHeight + nearbyRowGap);
            float rowBottom = rowTop + nearbyRowHeight;
            if (rowTop < offset)
                offset = rowTop;
            else if (rowBottom > offset + nearbyViewport.rect.height)
                offset = rowBottom - nearbyViewport.rect.height;
            nearbyScrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / maxScroll);
        }
    }
}
