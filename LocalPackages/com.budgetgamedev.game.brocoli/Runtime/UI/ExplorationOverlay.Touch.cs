using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private void CreateTouchButtons(RectTransform canvasRoot)
        {
            touchMapButton = CreateTouchButton(
                canvasRoot,
                "TouchMapButton",
                "MAP",
                Pane.Map,
                0.065f
            );
            touchInventoryButton = CreateTouchButton(
                canvasRoot,
                "TouchInventoryButton",
                "INV",
                Pane.Inventory,
                0.125f
            );
        }

        private Button CreateTouchButton(
            RectTransform canvasRoot,
            string objectName,
            string label,
            Pane pane,
            float horizontalAnchor
        )
        {
            Button button = CreateOverlayButton(objectName, canvasRoot, label, false);
            RectTransform rect = button.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(horizontalAnchor, 0.965f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(92f, 56f);
            button.onClick.AddListener(() => TogglePane(pane));
            button.gameObject.SetActive(false);
            return button;
        }

        private void UpdateTouchButtonVisibility()
        {
            if (touchMapButton == null || touchInventoryButton == null)
                return;

            if (virtualController == null)
                virtualController = VirtualController.Instance;

            // Keep these available to a mouse as well as touch. Paused screens
            // own the canvas while time is stopped, so exploration controls hide.
            bool visible = Time.timeScale > 0f;
            if (touchMapButton.gameObject.activeSelf != visible)
                touchMapButton.gameObject.SetActive(visible);
            if (touchInventoryButton.gameObject.activeSelf != visible)
                touchInventoryButton.gameObject.SetActive(visible);
        }
    }
}
