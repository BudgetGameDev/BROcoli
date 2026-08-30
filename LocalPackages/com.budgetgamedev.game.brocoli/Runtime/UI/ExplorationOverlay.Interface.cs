using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private static readonly Color OverlayScrim = new(0.02f, 0.05f, 0.035f, 0.18f);
        private static readonly Color TranslucentCard = new(0.055f, 0.095f, 0.07f, 0.76f);
        private static readonly Color TranslucentSurface = new(0.09f, 0.15f, 0.115f, 0.7f);

        private void BuildInterface()
        {
            RectTransform canvasRoot = transform as RectTransform;
            if (canvasRoot == null)
                return;

            overlayRoot = CreatePanel("ExplorationOverlay", canvasRoot, OverlayScrim);
            Stretch(overlayRoot);
            overlayRoot.GetComponent<Image>().raycastTarget = false;

            safeArea = CreateRect("SafeArea", overlayRoot);
            Stretch(safeArea);
            card = CreatePanel("ExplorationCard", safeArea, TranslucentCard);
            AddCardShadow(card);
            card.GetComponent<Image>().raycastTarget = false;

            title = CreateText(
                "Title",
                card,
                "INVENTORY",
                30f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );

            Button previous = CreateOverlayButton("PreviousPane", card, "<", false);
            previous.onClick.AddListener(() => SwitchPane(-1));
            Button next = CreateOverlayButton("NextPane", card, ">", false);
            next.onClick.AddListener(() => SwitchPane(1));
            Button close = CreateOverlayButton("CloseOverlay", card, "X", false);
            close.onClick.AddListener(Close);

            LayoutHeaderButton(previous.transform as RectTransform, 24f, false);
            LayoutHeaderButton(next.transform as RectTransform, 90f, true);
            LayoutHeaderButton(close.transform as RectTransform, 24f, true);

            inventoryPanel = CreatePanel("InventoryPanel", card, TranslucentSurface);
            inventoryPanel.GetComponent<Image>().color = Color.clear;
            inventoryPanel.GetComponent<Image>().raycastTarget = false;
            BuildInventoryInterface();

            mapPanel = CreatePanel("MapPanel", card, TranslucentSurface);
            RectTransform mapViewport = CreateRect("MapViewport", mapPanel);
            Stretch(mapViewport);
            mapViewport.gameObject.AddComponent<RectMask2D>();
            mapGraphic = mapViewport.gameObject.AddComponent<DungeonMapGraphic>();
            mapGraphic.color = Color.white;

            mapStatus = CreateText(
                "MapStatus",
                mapPanel,
                string.Empty,
                15f,
                OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            mapStatus.alignment = TextAlignmentOptions.TopLeft;

            Button zoomOut = CreateOverlayButton("ZoomOut", mapPanel, "-", false);
            zoomOut.onClick.AddListener(() => mapGraphic.ZoomBy(-0.22f));
            Button recenter = CreateOverlayButton("Recenter", mapPanel, "C", false);
            recenter.onClick.AddListener(() => mapGraphic.FocusPlayer());
            Button zoomIn = CreateOverlayButton("ZoomIn", mapPanel, "+", true);
            zoomIn.onClick.AddListener(() => mapGraphic.ZoomBy(0.22f));
            LayoutMapButton(zoomOut.transform as RectTransform, 0);
            LayoutMapButton(recenter.transform as RectTransform, 1);
            LayoutMapButton(zoomIn.transform as RectTransform, 2);

            footer = CreateText(
                "Footer",
                card,
                string.Empty,
                12f,
                OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            mapPanel.SetAsFirstSibling();
            inventoryPanel.SetAsFirstSibling();

            CreateTouchButtons(canvasRoot);
            ShowPane(activePane);
            overlayRoot.gameObject.SetActive(false);
        }

        private static Button CreateOverlayButton(
            string objectName,
            RectTransform parent,
            string label,
            bool primary
        )
        {
            RectTransform rect = CreatePanel(objectName, parent, Color.white);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            TMP_Text text = CreateText(
                "Label",
                rect,
                label,
                label.Length > 1 ? 15f : 27f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );
            Stretch(text.rectTransform);
            StyleButton(button, primary, TMP_Settings.defaultFontAsset);
            text.margin = Vector4.zero;
            return button;
        }

        private static void LayoutHeaderButton(RectTransform rect, float inset, bool right)
        {
            float anchor = right ? 1f : 0f;
            rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1f);
            rect.pivot = new Vector2(anchor, 1f);
            rect.anchoredPosition = new Vector2(right ? -inset : inset, -18f);
            rect.sizeDelta = new Vector2(54f, 48f);
        }

        private static void LayoutMapButton(RectTransform rect, int index)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-18f - (2 - index) * 62f, 96f);
            rect.sizeDelta = new Vector2(52f, 48f);
        }

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
                return;

            lastSafeArea = currentSafeArea;
            lastRootSize = rootSize;
            ApplySafeArea(safeArea, currentSafeArea);
            Canvas.ForceUpdateCanvases();

            float width = Mathf.Max(480f, safeArea.rect.width);
            float height = Mathf.Max(320f, safeArea.rect.height);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, height);
            card.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = Color.clear;

            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            title.rectTransform.sizeDelta = new Vector2(Mathf.Min(600f, width - 300f), 50f);

            Stretch(inventoryPanel);
            Stretch(mapPanel);
            ApplyInventoryLayout(width, height);
            footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            footer.rectTransform.anchoredPosition = new Vector2(
                0f,
                activePane == Pane.Inventory ? (height < 620f ? 126f : 154f) : 96f
            );
            footer.rectTransform.sizeDelta = new Vector2(-32f, 30f);

            mapStatus.rectTransform.anchorMin = new Vector2(0f, 1f);
            mapStatus.rectTransform.anchorMax = new Vector2(1f, 1f);
            mapStatus.rectTransform.pivot = new Vector2(0.5f, 1f);
            mapStatus.rectTransform.anchoredPosition = new Vector2(0f, -72f);
            mapStatus.rectTransform.sizeDelta = new Vector2(-28f, 30f);
        }

        private void UpdateMapStatus()
        {
            if (mapStatus == null)
                return;

            DungeonManager dungeon = mapGraphic != null ? mapGraphic.Dungeon : null;
            mapStatus.text =
                dungeon != null && dungeon.HasCurrentRoom
                    ? $"ROOM {dungeon.CurrentRoom.x}, {dungeon.CurrentRoom.y}     ·     {dungeon.RoomsVisited} VISITED"
                    : "DISCOVERING DUNGEON…";
        }
    }
}
