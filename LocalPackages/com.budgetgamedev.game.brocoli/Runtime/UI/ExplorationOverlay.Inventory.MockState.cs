using System;
using System.Collections.Generic;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private static readonly string[] GearSlotNames =
        {
            "HEAD",
            "HANDS",
            "CHEST",
            "FEET",
            "AMULET",
            "MAIN HAND",
            "OFF HAND",
            "RING",
        };

        private static readonly string[] InitialGearItems =
        {
            null,
            "RUBBER GLOVES",
            null,
            null,
            "MOSSY CHARM",
            "SANITIZER",
            null,
            null,
        };

        private List<string> nearbyItems;
        private string[] gearItems;
        private string[] backpackItems;
        private int activeGearSlotIndex = 5;

        private void InitializeMockInventory()
        {
            nearbyItems = new List<string>(NearbyPreviewItems);
            gearItems = (string[])InitialGearItems.Clone();
            backpackItems = new string[20];
            Array.Copy(BackpackPreview, backpackItems, BackpackPreview.Length);
        }

        private string GearLabel(int index) =>
            $"{GearSlotNames[index]}\n{(string.IsNullOrEmpty(gearItems[index]) ? "EMPTY" : gearItems[index])}";

        private static string NearbyLabel(string itemName) =>
            itemName + "\n<size=9><color=#9CB2A2>NEARBY ITEM</color></size>";

        private static string ItemAt(string[] items, int index) =>
            items != null && index >= 0 && index < items.Length ? items[index] : null;

        private static string ItemAt(List<string> items, int index) =>
            items != null && index >= 0 && index < items.Count ? items[index] : null;

        internal static bool TryMoveMockListItemToArray(
            List<string> source,
            int sourceIndex,
            string[] destination,
            out int destinationIndex
        )
        {
            destinationIndex =
                destination != null ? Array.FindIndex(destination, string.IsNullOrEmpty) : -1;
            string itemName = ItemAt(source, sourceIndex);
            if (string.IsNullOrEmpty(itemName) || destinationIndex < 0)
                return false;

            destination[destinationIndex] = itemName;
            source.RemoveAt(sourceIndex);
            return true;
        }

        internal static bool TryMoveMockArrayItemToList(
            string[] source,
            int sourceIndex,
            List<string> destination,
            out int destinationIndex
        )
        {
            string itemName = ItemAt(source, sourceIndex);
            if (string.IsNullOrEmpty(itemName) || destination == null)
            {
                destinationIndex = -1;
                return false;
            }

            destinationIndex = destination.Count;
            destination.Add(itemName);
            source[sourceIndex] = null;
            return true;
        }

        internal static bool SwapMockListItem(
            List<string> source,
            int sourceIndex,
            string[] gear,
            int gearIndex
        )
        {
            string itemName = ItemAt(source, sourceIndex);
            if (
                string.IsNullOrEmpty(itemName)
                || gear == null
                || gearIndex < 0
                || gearIndex >= gear.Length
            )
                return false;

            if (string.IsNullOrEmpty(gear[gearIndex]))
                source.RemoveAt(sourceIndex);
            else
                source[sourceIndex] = gear[gearIndex];
            gear[gearIndex] = itemName;
            return true;
        }

        internal static bool SwapMockItem(
            string[] source,
            int sourceIndex,
            string[] gear,
            int gearIndex
        )
        {
            if (
                string.IsNullOrEmpty(ItemAt(source, sourceIndex))
                || gear == null
                || gearIndex < 0
                || gearIndex >= gear.Length
            )
                return false;

            (source[sourceIndex], gear[gearIndex]) = (gear[gearIndex], source[sourceIndex]);
            return true;
        }

        internal static bool TryUnequipMockItem(
            string[] gear,
            int gearIndex,
            string[] backpack,
            List<string> nearby,
            out InventoryPreviewLocation destination,
            out int destinationIndex
        )
        {
            string itemName = ItemAt(gear, gearIndex);
            if (string.IsNullOrEmpty(itemName) || backpack == null || nearby == null)
            {
                destination = InventoryPreviewLocation.Gear;
                destinationIndex = -1;
                return false;
            }

            destinationIndex = Array.FindIndex(backpack, string.IsNullOrEmpty);
            if (destinationIndex >= 0)
            {
                backpack[destinationIndex] = itemName;
                destination = InventoryPreviewLocation.Backpack;
            }
            else
            {
                destinationIndex = nearby.Count;
                nearby.Add(itemName);
                destination = InventoryPreviewLocation.Nearby;
            }

            gear[gearIndex] = null;
            return true;
        }
    }
}
