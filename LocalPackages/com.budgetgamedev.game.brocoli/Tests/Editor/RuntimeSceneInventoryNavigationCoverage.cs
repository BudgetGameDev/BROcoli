using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseInventoryNavigationEdges(ExplorationOverlay overlay)
        {
            Assert.That(
                ExplorationOverlay.NormalizeInventoryNavigation(Vector2.right * 2f + Vector2.up),
                Is.EqualTo(Vector2.right)
            );
            Assert.That(
                ExplorationOverlay.NormalizeInventoryNavigation(Vector2.up * 2f + Vector2.right),
                Is.EqualTo(Vector2.up)
            );
            Assert.That(
                ExplorationOverlay.NormalizeInventoryNavigation(Vector2.zero),
                Is.EqualTo(Vector2.zero)
            );

            overlay.ProcessInventoryActions(false, false);
            overlay.ProcessInventoryActions(true, true);
            overlay.ProcessInventoryActions(false, true);
            overlay.ProcessInventoryNavigation(Vector2.zero, 0f);
            overlay.ProcessInventoryNavigation(Vector2.right, 1f);
            overlay.ProcessInventoryNavigation(Vector2.right, 1.01f);
            overlay.ProcessInventoryNavigation(Vector2.right, 2f);

            var items = GetHierarchyField<List<InventoryPreviewItem>>(overlay, "inventoryItems");
            GameObject first = NewInventoryNavigationItem(
                overlay.transform,
                "Coverage inventory A",
                Vector2.zero
            );
            GameObject second = NewInventoryNavigationItem(
                overlay.transform,
                "Coverage inventory B",
                Vector2.right * 100f
            );
            InvokeHierarchy(
                overlay,
                "RegisterInventoryItem",
                first.GetComponent<RectTransform>(),
                InventoryPreviewLocation.Gear,
                0
            );
            InvokeHierarchy(
                overlay,
                "RegisterInventoryItem",
                second.GetComponent<RectTransform>(),
                InventoryPreviewLocation.Gear,
                1
            );
            InventoryPreviewItem firstItem = items[items.Count - 2];
            overlay.SelectInventoryItem(firstItem);
            InvokeHierarchy(overlay, "MoveInventorySelection", Vector2.right);
            Object.Destroy(first);
            Object.Destroy(second);
        }

        private static GameObject NewInventoryNavigationItem(
            Transform parent,
            string name,
            Vector2 position
        )
        {
            GameObject item = new(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.one * 40f;
            rect.anchoredPosition = position;
            return item;
        }
    }
}
