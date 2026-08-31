using System.Collections.Generic;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseDungeonMap(DungeonMapGraphic map)
        {
            InvokeHierarchy(map, "Awake");
            map.RefreshFromDungeon(true);
            map.RefreshFromDungeon();
            map.FocusPlayer();
            map.Pan(Vector2.one);
            map.ZoomBy(100f);
            map.ZoomBy(-100f);

            var pointer = new PointerEventData(EventSystem.current)
            {
                delta = new Vector2(24f, -18f),
                scrollDelta = Vector2.up,
            };
            map.OnDrag(pointer);
            map.OnScroll(pointer);

            DungeonManager dungeon = map.Dungeon;
            var visited = GetHierarchyField<List<Vector2Int>>(map, "visitedRooms");
            var lookup = GetHierarchyField<HashSet<Vector2Int>>(map, "visitedLookup");
            visited.Clear();
            lookup.Clear();
            Vector2Int current = dungeon.CurrentRoom;
            foreach (
                Vector2Int room in new[]
                {
                    current,
                    current + DungeonLayout.DirectionOffsets[DungeonLayout.North],
                    current + DungeonLayout.DirectionOffsets[DungeonLayout.East],
                    Vector2Int.zero,
                }
            )
            {
                if (!lookup.Add(room))
                    continue;
                visited.Add(room);
            }

            using (var vertices = new VertexHelper())
            {
                map.PopulateMesh(vertices);
                Assert.That(vertices.currentVertCount, Is.GreaterThan(0));
            }

            visited.Clear();
            using (var vertices = new VertexHelper())
                map.PopulateMesh(vertices);
            map.RefreshFromDungeon(true);
        }

        private static void ExerciseExplorationOverlay(ExplorationOverlay overlay)
        {
            RectTransform nearbySurface = GetHierarchyField<RectTransform>(
                overlay,
                "nearbySurface"
            );
            SetHierarchyField(overlay, "nearbySurface", null);
            InvokeHierarchy(overlay, "ApplyInventoryLayout", 1000f, 700f);
            SetHierarchyField(overlay, "nearbySurface", nearbySurface);
            Assert.That(ExplorationOverlay.EnsurePresent(), Is.SameAs(overlay));
            _ = overlay.ActivePane;
            InvokeHierarchy(overlay, "Update");
            InvokeHierarchy(overlay, "TogglePane", ExplorationOverlay.Pane.Inventory);
            InvokeHierarchy(overlay, "TogglePane", ExplorationOverlay.Pane.Inventory);
            InvokeHierarchy(overlay, "TogglePane", ExplorationOverlay.Pane.Map);
            InvokeHierarchy(overlay, "HandleMapControllerInput");

            float timeScale = Time.timeScale;
            Time.timeScale = 1f;
            InvokeHierarchy(overlay, "Open", ExplorationOverlay.Pane.Inventory);
            Time.timeScale = 0f;
            InvokeHierarchy(overlay, "Open", ExplorationOverlay.Pane.Map);
            Time.timeScale = timeScale;
            RectTransform overlayRoot = GetHierarchyField<RectTransform>(overlay, "overlayRoot");
            Assert.That(overlayRoot, Is.Not.Null);
            overlayRoot.gameObject.SetActive(true);
            SetHierarchyField(overlay, "activePane", ExplorationOverlay.Pane.Map);
            InvokeHierarchy(overlay, "Update");
            SetHierarchyField(overlay, "activePane", ExplorationOverlay.Pane.Inventory);
            SetHierarchyField(overlay, "nextInventoryRefresh", -1f);
            InvokeHierarchy(overlay, "Update");
            overlay.ProcessMapInput(Vector2.one, 1f, 0.1f);
            overlay.ProcessMapInput(Vector2.zero, 0f, 0.1f);
            overlay.ProcessGlobalInput(true, false, false, false, false, false);
            overlay.ProcessGlobalInput(false, true, false, false, false, false);
            overlay.ProcessGlobalInput(false, false, true, false, false, false);
            overlay.ProcessGlobalInput(false, false, true, false, false, false);
            InvokeHierarchy(overlay, "Open", ExplorationOverlay.Pane.Map);
            overlayRoot.gameObject.SetActive(true);
            overlay.ProcessGlobalInput(false, false, false, true, false, false);
            InvokeHierarchy(overlay, "Open", ExplorationOverlay.Pane.Map);
            overlayRoot.gameObject.SetActive(true);
            overlay.ProcessGlobalInput(false, false, false, false, true, false);
            overlay.ProcessGlobalInput(false, false, false, false, false, true);

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                SetHierarchyField(overlay, "activePane", ExplorationOverlay.Pane.Map);
                InvokeHierarchy(overlay, "HandleMapControllerInput");
                InvokeHierarchy(overlay, "Open", ExplorationOverlay.Pane.Map);
                Pulse(
                    gamepad,
                    new GamepadState { rightStick = Vector2.one, rightTrigger = 1f },
                    () => InvokeHierarchy(overlay, "Update")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.LeftShoulder),
                    () => InvokeHierarchy(overlay, "HandleGlobalInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.RightShoulder),
                    () => InvokeHierarchy(overlay, "HandleGlobalInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.Select),
                    () => InvokeHierarchy(overlay, "HandleGlobalInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.Select),
                    () => InvokeHierarchy(overlay, "HandleGlobalInput")
                );

                InvokeHierarchy(overlay, "Open", ExplorationOverlay.Pane.Inventory);
                SetHierarchyField(overlay, "nextInventoryNavigation", -1f);
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadRight),
                    () => InvokeHierarchy(overlay, "HandleInventoryNavigationInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South),
                    () => InvokeHierarchy(overlay, "HandleInventoryNavigationInput")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.West),
                    () => InvokeHierarchy(overlay, "HandleInventoryNavigationInput")
                );
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }

            var items = GetHierarchyField<List<InventoryPreviewItem>>(overlay, "inventoryItems");
            foreach (InventoryPreviewItem item in new List<InventoryPreviewItem>(items))
            {
                if (item == null)
                    continue;
                overlay.SelectInventoryItem(item);
                var click = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Right,
                    clickCount = 1,
                };
                overlay.HandleInventoryPointerClick(item, click);
                click.button = PointerEventData.InputButton.Left;
                click.clickCount = 2;
                overlay.HandleInventoryPointerClick(item, click);
            }

            overlay.HandleInventoryPointerClick(null, null);
            overlay.Close();
            Time.timeScale = timeScale;
        }
    }
}
