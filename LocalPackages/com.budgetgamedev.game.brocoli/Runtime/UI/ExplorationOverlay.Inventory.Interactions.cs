using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private const string DefaultInventoryInteractionHint =
            "DOUBLE-CLICK / A / TRANSFER  MOVE     RIGHT-CLICK / X / EQUIP  USE     CLICK LOADOUT SLOT  TARGET";

        private Button inventoryTransferButton;
        private Button inventoryEquipButton;

        private void BuildInventoryActionButtons()
        {
            inventoryTransferButton = CreateOverlayButton(
                "InventoryTransferAction",
                inventoryPanel,
                "TRANSFER  A",
                false
            );
            inventoryTransferButton.onClick.AddListener(TransferSelectedInventoryItem);

            inventoryEquipButton = CreateOverlayButton(
                "InventoryEquipAction",
                inventoryPanel,
                "EQUIP  X",
                true
            );
            inventoryEquipButton.onClick.AddListener(EquipSelectedInventoryItem);
        }

        internal void HandleInventoryPointerClick(
            InventoryPreviewItem item,
            PointerEventData eventData
        )
        {
            if (item == null || eventData == null)
                return;

            SelectInventoryItem(item);
            if (item.Location == InventoryPreviewLocation.Gear)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                    EquipSelectedInventoryItem();
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
                EquipSelectedInventoryItem();
            else if (
                eventData.button == PointerEventData.InputButton.Left
                && eventData.clickCount >= 2
            )
                TransferSelectedInventoryItem();
        }

        private void SetActiveGearSlot(int index)
        {
            if (gearItems == null || index < 0 || index >= gearItems.Length)
                return;

            activeGearSlotIndex = index;
            RefreshMockInventoryVisuals();
            ShowInventoryStatus($"EQUIP TARGET  ·  {GearSlotNames[index]}");
        }

        private void TransferSelectedInventoryItem()
        {
            if (selectedInventoryItem == null)
                return;

            int sourceIndex = selectedInventoryItem.SlotIndex;
            string itemName;
            int destinationIndex;
            InventoryPreviewLocation destinationLocation;
            string destinationName;
            if (selectedInventoryItem.Location == InventoryPreviewLocation.Nearby)
            {
                itemName = ItemAt(nearbyItems, sourceIndex);
                if (
                    !TryMoveMockListItemToArray(
                        nearbyItems,
                        sourceIndex,
                        backpackItems,
                        out destinationIndex
                    )
                )
                {
                    ShowInventoryStatus("NO EMPTY SPACE IN BACKPACK");
                    return;
                }
                destinationLocation = InventoryPreviewLocation.Backpack;
                destinationName = "BACKPACK";
            }
            else if (selectedInventoryItem.Location == InventoryPreviewLocation.Backpack)
            {
                itemName = ItemAt(backpackItems, sourceIndex);
                if (
                    !TryMoveMockArrayItemToList(
                        backpackItems,
                        sourceIndex,
                        nearbyItems,
                        out destinationIndex
                    )
                )
                {
                    ShowInventoryStatus("THAT SLOT IS EMPTY");
                    return;
                }
                destinationLocation = InventoryPreviewLocation.Nearby;
                destinationName = "NEARBY";
            }
            else
            {
                ShowInventoryStatus("LOADOUT SLOTS ARE EQUIP TARGETS");
                return;
            }

            RefreshMockInventoryVisuals();
            SelectInventoryItem(FindInventoryItem(destinationLocation, destinationIndex));
            ShowInventoryStatus($"{itemName} MOVED TO {destinationName}");
        }

        private void EquipSelectedInventoryItem()
        {
            if (selectedInventoryItem == null)
                return;
            if (selectedInventoryItem.Location == InventoryPreviewLocation.Gear)
            {
                UnequipSelectedGearItem();
                return;
            }

            int sourceIndex = selectedInventoryItem.SlotIndex;
            bool fromNearby = selectedInventoryItem.Location == InventoryPreviewLocation.Nearby;
            string itemName = fromNearby
                ? ItemAt(nearbyItems, sourceIndex)
                : ItemAt(backpackItems, sourceIndex);
            if (string.IsNullOrEmpty(itemName))
            {
                ShowInventoryStatus("CHOOSE AN ITEM FROM NEARBY OR BACKPACK");
                return;
            }

            string previousItem = gearItems[activeGearSlotIndex];
            if (fromNearby)
                SwapMockListItem(nearbyItems, sourceIndex, gearItems, activeGearSlotIndex);
            else
                SwapMockItem(backpackItems, sourceIndex, gearItems, activeGearSlotIndex);

            string sourceName = fromNearby ? "NEARBY" : "BACKPACK";
            RefreshMockInventoryVisuals();
            SelectInventoryItem(
                FindInventoryItem(InventoryPreviewLocation.Gear, activeGearSlotIndex)
            );
            ShowInventoryStatus(
                string.IsNullOrEmpty(previousItem)
                    ? $"{itemName} EQUIPPED IN {GearSlotNames[activeGearSlotIndex]}"
                    : $"{itemName} EQUIPPED  ·  {previousItem} MOVED TO {sourceName}"
            );
        }

        private void UnequipSelectedGearItem()
        {
            int gearIndex = selectedInventoryItem.SlotIndex;
            string itemName = ItemAt(gearItems, gearIndex);
            if (string.IsNullOrEmpty(itemName))
            {
                ShowInventoryStatus("THAT LOADOUT SLOT IS EMPTY");
                return;
            }

            if (
                !TryUnequipMockItem(
                    gearItems,
                    gearIndex,
                    backpackItems,
                    nearbyItems,
                    out InventoryPreviewLocation destination,
                    out int destinationIndex
                )
            )
                return;

            RefreshMockInventoryVisuals();
            SelectInventoryItem(FindInventoryItem(destination, destinationIndex));
            ShowInventoryStatus(
                destination == InventoryPreviewLocation.Backpack
                    ? $"{itemName} MOVED TO BACKPACK"
                    : $"BACKPACK FULL  ·  {itemName} DROPPED NEARBY"
            );
        }

        private void RefreshMockInventoryVisuals()
        {
            if (nearbyItems == null)
                return;

            RefreshNearbyList();
            for (int i = 0; i < gearSlots.Count; i++)
                RefreshGearSlot(i);
            for (int i = 0; i < backpackSlots.Count; i++)
                RefreshBackpackSlot(i);

            if (loadoutHint != null)
                loadoutHint.text = $"TARGET  ·  {GearSlotNames[activeGearSlotIndex]}";
            RefreshSelectedItemStats();
        }

        private void RefreshGearSlot(int index)
        {
            bool occupied = !string.IsNullOrEmpty(gearItems[index]);
            Color color = occupied ? OccupiedSlot : InventorySlot;
            if (index == activeGearSlotIndex)
                color = Color.Lerp(color, GearAccent, 0.24f);

            RectTransform slot = gearSlots[index];
            slot.GetComponent<InventoryPreviewItem>()?.SetNormalColor(color);
            TMP_Text label = slot.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = GearLabel(index);
                label.color = occupied ? OnSurface : OnSurfaceMuted;
                label.fontStyle = occupied ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void RefreshBackpackSlot(int index)
        {
            string itemName = backpackItems[index];
            bool occupied = !string.IsNullOrEmpty(itemName);
            backpackSlots[index]
                .GetComponent<InventoryPreviewItem>()
                ?.SetNormalColor(occupied ? OccupiedSlot : InventorySlot);
            backpackLabels[index].text = occupied ? itemName : string.Empty;
            backpackLabels[index].color = occupied ? OnSurface : OnSurfaceMuted;
        }

        private InventoryPreviewItem FindInventoryItem(
            InventoryPreviewLocation location,
            int index
        ) => inventoryItems.Find(item => item.Location == location && item.SlotIndex == index);

        private void ShowInventoryStatus(string value)
        {
            if (inventoryDisclaimer != null)
                inventoryDisclaimer.text = value;
        }
    }
}
