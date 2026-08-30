using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public enum InventoryPreviewLocation
    {
        Nearby,
        Gear,
        Backpack,
    }

    /// <summary>
    /// Pointer-facing visual state for a mock inventory cell. Selection has no
    /// gameplay effect; it only gives mouse, touch, and controller users feedback.
    /// </summary>
    public sealed class InventoryPreviewItem
        : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerClickHandler
    {
        private ExplorationOverlay owner;
        private Image targetGraphic;
        private Color normalColor;
        private bool isHovered;

        public bool IsSelected { get; private set; }
        public bool IsHovered => isHovered;
        public InventoryPreviewLocation Location { get; private set; }
        public int SlotIndex { get; private set; }

        internal RectTransform RectTransform => transform as RectTransform;

        internal void Configure(
            ExplorationOverlay overlay,
            Image image,
            InventoryPreviewLocation location,
            int slotIndex
        )
        {
            owner = overlay;
            targetGraphic = image;
            normalColor = image.color;
            Location = location;
            SlotIndex = slotIndex;
            targetGraphic.raycastTarget = true;
            RefreshColor();
        }

        internal void SetNormalColor(Color value)
        {
            normalColor = value;
            RefreshColor();
        }

        internal void SetSlotIndex(int value)
        {
            SlotIndex = value;
        }

        internal void SetSelected(bool value)
        {
            IsSelected = value;
            RefreshColor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            RefreshColor();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.HandleInventoryPointerClick(this, eventData);
        }

        private void OnDisable()
        {
            isHovered = false;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (targetGraphic == null)
                return;

            Color color = normalColor;
            if (IsSelected)
                color = Color.Lerp(color, new Color(0.94f, 0.68f, 0.2f, 1f), 0.48f);
            if (isHovered)
                color = Color.Lerp(color, Color.white, IsSelected ? 0.18f : 0.3f);
            targetGraphic.color = color;
        }
    }
}
