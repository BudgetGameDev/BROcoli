using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private const float InventoryNavigationInitialDelay = 0.34f;
        private const float InventoryNavigationRepeatDelay = 0.11f;

        private readonly List<InventoryPreviewItem> inventoryItems = new();
        private InventoryPreviewItem selectedInventoryItem;
        private Vector2 heldInventoryNavigation;
        private float nextInventoryNavigation;

        internal string SelectedInventoryItemName =>
            selectedInventoryItem != null ? selectedInventoryItem.name : string.Empty;

        private void RegisterInventoryItem(
            RectTransform rect,
            InventoryPreviewLocation location,
            int slotIndex
        )
        {
            Image image = rect.GetComponent<Image>();
            InventoryPreviewItem item = rect.gameObject.AddComponent<InventoryPreviewItem>();
            item.Configure(this, image, location, slotIndex);
            inventoryItems.Add(item);
        }

        internal void SelectInventoryItem(InventoryPreviewItem item)
        {
            if (item == null || item == selectedInventoryItem)
                return;

            selectedInventoryItem?.SetSelected(false);
            selectedInventoryItem = item;
            selectedInventoryItem.SetSelected(true);
            if (item.Location == InventoryPreviewLocation.Gear)
                SetActiveGearSlot(item.SlotIndex);
            else if (item.Location == InventoryPreviewLocation.Nearby)
                EnsureNearbyItemVisible(item.SlotIndex);
            RefreshSelectedItemStats();
        }

        private void EnsureInventorySelection()
        {
            if (selectedInventoryItem == null && inventoryItems.Count > 0)
                SelectInventoryItem(inventoryItems[0]);
        }

        private void HandleInventoryNavigationInput()
        {
            HandleInventoryActionInput();
            Vector2 direction = ReadInventoryNavigationInput();
            if (direction == Vector2.zero)
            {
                ResetInventoryNavigationRepeat();
                return;
            }

            bool changed = direction != heldInventoryNavigation;
            if (!changed && Time.unscaledTime < nextInventoryNavigation)
                return;

            MoveInventorySelection(direction);
            heldInventoryNavigation = direction;
            nextInventoryNavigation =
                Time.unscaledTime
                + (changed ? InventoryNavigationInitialDelay : InventoryNavigationRepeatDelay);
        }

        private void HandleInventoryActionInput()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool transfer =
                (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            bool equip =
                (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);

            if (transfer)
                TransferSelectedInventoryItem();
            else if (equip)
                EquipSelectedInventoryItem();
        }

        private static Vector2 ReadInventoryNavigationInput()
        {
            Vector2 direction = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                direction.x += keyboard.rightArrowKey.isPressed ? 1f : 0f;
                direction.x -= keyboard.leftArrowKey.isPressed ? 1f : 0f;
                direction.y += keyboard.upArrowKey.isPressed ? 1f : 0f;
                direction.y -= keyboard.downArrowKey.isPressed ? 1f : 0f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
                direction += gamepad.dpad.ReadValue();

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                return new Vector2(Mathf.Sign(direction.x), 0f);
            if (Mathf.Abs(direction.y) > 0.1f)
                return new Vector2(0f, Mathf.Sign(direction.y));
            return Vector2.zero;
        }

        private void MoveInventorySelection(Vector2 direction)
        {
            EnsureInventorySelection();
            int current = inventoryItems.IndexOf(selectedInventoryItem);
            if (current < 0)
                return;

            var positions = new List<Vector2>(inventoryItems.Count);
            var sourceIndices = new List<int>(inventoryItems.Count);
            int activeCurrent = -1;
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                InventoryPreviewItem item = inventoryItems[i];
                if (item == null || !item.gameObject.activeInHierarchy)
                    continue;

                if (i == current)
                    activeCurrent = positions.Count;
                positions.Add(ItemPosition(item));
                sourceIndices.Add(i);
            }

            int target = FindDirectionalItem(positions, activeCurrent, direction);
            if (target >= 0)
                SelectInventoryItem(inventoryItems[sourceIndices[target]]);
        }

        private static Vector2 ItemPosition(InventoryPreviewItem item)
        {
            RectTransform rect = item.RectTransform;
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            return new Vector2(worldCenter.x, worldCenter.y);
        }

        internal static int FindDirectionalItem(
            IReadOnlyList<Vector2> positions,
            int current,
            Vector2 direction
        )
        {
            if (positions == null || current < 0 || current >= positions.Count)
                return -1;

            direction.Normalize();
            Vector2 perpendicular = new(-direction.y, direction.x);
            Vector2 origin = positions[current];
            int best = -1;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < positions.Count; i++)
            {
                if (i == current)
                    continue;

                Vector2 delta = positions[i] - origin;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 0.5f)
                    continue;

                float lateral = Mathf.Abs(Vector2.Dot(delta, perpendicular));
                float score = forward + lateral * 2.5f + lateral * lateral / forward;
                if (score < bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            return best;
        }

        private void ResetInventoryNavigationRepeat()
        {
            heldInventoryNavigation = Vector2.zero;
            nextInventoryNavigation = 0f;
        }
    }
}
