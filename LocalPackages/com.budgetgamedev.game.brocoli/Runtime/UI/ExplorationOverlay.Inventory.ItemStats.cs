using TMPro;
using UnityEngine;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private static readonly string[] MockItemStatNames =
        {
            "POWER",
            "ARMOR",
            "HANDLING",
            "WEIGHT",
            "VALUE",
            "CONDITION",
        };

        private void BuildSelectedItemStats()
        {
            selectedItemStatsSurface = CreatePanel(
                "SelectedItemStats",
                statsSurface,
                new Color(0.03f, 0.055f, 0.04f, 0.52f)
            );
            SetGraphicRaycast(selectedItemStatsSurface, false);
            AddInventoryOutline(
                selectedItemStatsSurface.gameObject,
                new Color(GearAccent.r, GearAccent.g, GearAccent.b, 0.42f)
            );

            selectedItemTitle = CreateText(
                "SelectedItemTitle",
                selectedItemStatsSurface,
                "SELECTED ITEM",
                12f,
                OnSurface,
                TMP_Settings.defaultFontAsset
            );
            selectedItemTitle.fontStyle = FontStyles.Bold;
            selectedItemTitle.alignment = TextAlignmentOptions.Left;
            selectedItemTitle.textWrappingMode = TextWrappingModes.NoWrap;
            selectedItemTitle.raycastTarget = false;

            selectedItemHint = CreateInventoryHint(
                "SelectedItemHint",
                selectedItemStatsSurface,
                "SINGLE-CLICK AN ITEM  ·  MOCK STATS"
            );
            for (int i = 0; i < MockItemStatNames.Length; i++)
                CreateSelectedItemStatCell(i);
            RefreshSelectedItemStats();
        }

        private void CreateSelectedItemStatCell(int index)
        {
            RectTransform cell = CreatePanel(
                $"SelectedItemStat{index + 1:00}",
                selectedItemStatsSurface,
                InventorySlot
            );
            SetGraphicRaycast(cell, false);
            AddInventoryOutline(cell.gameObject, new Color(1f, 1f, 1f, 0.1f));
            selectedItemStatCells.Add(cell);

            TMP_Text label = CreateText(
                "Label",
                cell,
                $"{MockItemStatNames[index]}\n—",
                9f,
                OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = true;
            label.raycastTarget = false;
            selectedItemStatLabels.Add(label);
        }

        private void LayoutSelectedItemStats(float width, float height, bool compact)
        {
            float inset = compact ? 5f : 8f;
            float headingHeight = compact ? 15f : 19f;
            float hintHeight = compact ? 11f : 15f;
            float gridTop = compact ? 32f : 43f;
            float gap = compact ? 3f : 5f;
            float cellWidth = (width - inset * 2f - gap * 2f) / 3f;
            float cellHeight = Mathf.Max(12f, (height - gridTop - inset - gap) * 0.5f);

            SetTopRect(
                selectedItemTitle.rectTransform,
                inset,
                inset,
                width - inset * 2f,
                headingHeight
            );
            selectedItemTitle.fontSize = compact ? 8f : 12f;
            SetTopRect(
                selectedItemHint.rectTransform,
                inset,
                inset + headingHeight,
                width - inset * 2f,
                hintHeight
            );
            selectedItemHint.fontSize = compact ? 5.5f : 8f;

            for (int i = 0; i < selectedItemStatCells.Count; i++)
            {
                int column = i % 3;
                int row = i / 3;
                RectTransform cell = selectedItemStatCells[i];
                SetTopRect(
                    cell,
                    inset + column * (cellWidth + gap),
                    gridTop + row * (cellHeight + gap),
                    cellWidth,
                    cellHeight
                );
                selectedItemStatLabels[i].fontSize = compact ? 5.5f : 8.5f;
            }
        }

        private void RefreshSelectedItemStats()
        {
            if (selectedItemTitle == null || selectedItemStatLabels.Count == 0)
                return;

            string itemName = SelectedItemName();
            if (string.IsNullOrEmpty(itemName))
            {
                selectedItemTitle.text = "EMPTY SLOT";
                selectedItemHint.text = "NO ITEM STATS TO DISPLAY";
                for (int i = 0; i < selectedItemStatLabels.Count; i++)
                    selectedItemStatLabels[i].text = MockItemStatLine(MockItemStatNames[i], "—");
                return;
            }

            string[] values = MockItemStatValues(itemName);
            selectedItemTitle.text = itemName;
            selectedItemHint.text = $"{SelectedItemLocationLabel()}  ·  MOCK ITEM STATS";
            for (int i = 0; i < selectedItemStatLabels.Count; i++)
                selectedItemStatLabels[i].text = MockItemStatLine(MockItemStatNames[i], values[i]);
        }

        private string SelectedItemName()
        {
            if (selectedInventoryItem == null)
                return null;

            int index = selectedInventoryItem.SlotIndex;
            return selectedInventoryItem.Location switch
            {
                InventoryPreviewLocation.Nearby => ItemAt(nearbyItems, index),
                InventoryPreviewLocation.Gear => ItemAt(gearItems, index),
                InventoryPreviewLocation.Backpack => ItemAt(backpackItems, index),
                _ => null,
            };
        }

        private string SelectedItemLocationLabel() =>
            selectedInventoryItem.Location switch
            {
                InventoryPreviewLocation.Nearby => "NEARBY",
                InventoryPreviewLocation.Gear => "EQUIPPED",
                InventoryPreviewLocation.Backpack => "BACKPACK",
                _ => "ITEM",
            };

        internal static string[] MockItemStatValues(string itemName)
        {
            int seed = 17;
            foreach (char character in itemName ?? string.Empty)
                seed = (seed * 31 + character) % 9973;

            return new[]
            {
                $"{8 + seed % 34}",
                $"{seed / 7 % 24}",
                $"+{2 + seed / 11 % 17}%",
                $"{0.6f + seed % 31 * 0.1f:0.0}",
                $"{18 + seed / 5 % 83}",
                $"{62 + seed % 39}%",
            };
        }

        private static string MockItemStatLine(string label, string value) =>
            $"<color=#9CB2A2>{label}</color>\n<b><color=#F1F4ED>{value}</color></b>";
    }
}
