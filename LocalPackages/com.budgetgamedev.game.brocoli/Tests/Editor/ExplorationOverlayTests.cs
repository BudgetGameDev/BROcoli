using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class ExplorationOverlayTests
    {
        [Test]
        public void PaneNavigationWrapsInBothDirections()
        {
            Assert.That(
                ExplorationOverlay.NextPane(ExplorationOverlay.Pane.Inventory),
                Is.EqualTo(ExplorationOverlay.Pane.Map)
            );
            Assert.That(
                ExplorationOverlay.NextPane(ExplorationOverlay.Pane.Map),
                Is.EqualTo(ExplorationOverlay.Pane.Inventory)
            );
            Assert.That(
                ExplorationOverlay.PreviousPane(ExplorationOverlay.Pane.Inventory),
                Is.EqualTo(ExplorationOverlay.Pane.Map)
            );
        }

        [Test]
        public void MapProjectionCentersViewAndPreservesRoomOffsets()
        {
            var area = new Rect(-300f, -200f, 600f, 400f);
            var step = new Vector2(50f, 35f);
            var viewCenter = new Vector2(4.5f, -2f);

            Vector2 centered = DungeonMapGraphic.RoomCenter(
                area,
                new Vector2Int(5, -2),
                viewCenter,
                step
            );
            Vector2 northeast = DungeonMapGraphic.RoomCenter(
                area,
                new Vector2Int(6, -1),
                viewCenter,
                step
            );

            Assert.That(centered, Is.EqualTo(new Vector2(25f, 0f)));
            Assert.That(northeast - centered, Is.EqualTo(step));
        }

        [Test]
        public void InventoryNavigationChoosesTheNearestItemInTheRequestedDirection()
        {
            Vector2[] grid = { new(0f, 100f), new(100f, 100f), new(0f, 0f), new(100f, 0f) };

            Assert.That(
                ExplorationOverlay.FindDirectionalItem(grid, 0, Vector2.right),
                Is.EqualTo(1)
            );
            Assert.That(
                ExplorationOverlay.FindDirectionalItem(grid, 0, Vector2.down),
                Is.EqualTo(2)
            );
            Assert.That(
                ExplorationOverlay.FindDirectionalItem(grid, 0, Vector2.up),
                Is.EqualTo(-1)
            );
        }

        [Test]
        public void MovementInputUsesWasdAndKeepsControllerStickSeparate()
        {
            Assert.That(
                PlayerInputHandler.ComposeWasd(false, true, false, true),
                Is.EqualTo(new Vector2(1f, 1f).normalized)
            );
            Assert.That(
                PlayerInputHandler.ResolveMovementInput(
                    Vector2.zero,
                    new Vector2(0.35f, -0.6f),
                    Vector2.left
                ),
                Is.EqualTo(new Vector2(0.35f, -0.6f))
            );
        }

        [Test]
        public void WasdSocdKeepsWalkingWhenTheOpposingKeyIsTheOnlyOtherInput()
        {
            GameObject host = new("SOCD input test");
            try
            {
                PlayerInputHandler input = host.AddComponent<PlayerInputHandler>();

                // A held with a D tap has no other axis to turn against, so the
                // walk continues left until A itself is released.
                Assert.That(input.ResolveWasd(true, false, false, false), Is.EqualTo(Vector2.left));
                Assert.That(input.ResolveWasd(true, true, false, false), Is.EqualTo(Vector2.left));
                Assert.That(input.ResolveWasd(true, false, false, false), Is.EqualTo(Vector2.left));
                Assert.That(input.ResolveWasd(true, true, false, false), Is.EqualTo(Vector2.left));
                Assert.That(input.ResolveWasd(false, true, false, false), Is.EqualTo(Vector2.right));

                Assert.That(input.ResolveWasd(false, false, false, true), Is.EqualTo(Vector2.up));
                Assert.That(input.ResolveWasd(false, false, true, true), Is.EqualTo(Vector2.up));
                Assert.That(input.ResolveWasd(false, false, true, false), Is.EqualTo(Vector2.down));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WasdSocdTurnsAroundWhileTheOtherAxisKeepsItsDirection()
        {
            GameObject host = new("SOCD diagonal input test");
            try
            {
                PlayerInputHandler input = host.AddComponent<PlayerInputHandler>();

                // Hold W and A, then tap D: the turn applies to the horizontal
                // axis alone and releasing D falls back to the still-held A.
                Vector2 upLeft = new Vector2(-1f, 1f).normalized;
                Vector2 upRight = new Vector2(1f, 1f).normalized;

                Assert.That(input.ResolveWasd(true, false, false, true), Is.EqualTo(upLeft));
                Assert.That(input.ResolveWasd(true, true, false, true), Is.EqualTo(upRight));
                Assert.That(input.ResolveWasd(true, false, false, true), Is.EqualTo(upLeft));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WasdSocdUsesTheLastMovedDirectionForSimultaneousOpposites()
        {
            var axis = new PlayerInputHandler.LastInputPriorityAxis();

            // Both from neutral has no direction to fall back on and defaults
            // to the negative key.
            Assert.That(axis.Resolve(true, true, true), Is.EqualTo(-1f));
            Assert.That(axis.Resolve(false, false, true), Is.Zero);

            // The direction last moved outlives the release that follows it.
            Assert.That(axis.Resolve(false, true, true), Is.EqualTo(1f));
            Assert.That(axis.Resolve(false, false, true), Is.Zero);
            Assert.That(axis.Resolve(true, true, true), Is.EqualTo(1f));

            // A fresh press turns the axis around only while the other axis
            // steers; without it the held direction stays.
            Assert.That(axis.Resolve(false, true, true), Is.EqualTo(1f));
            Assert.That(axis.Resolve(true, true, false), Is.EqualTo(1f));
            Assert.That(axis.Resolve(false, true, false), Is.EqualTo(1f));
            Assert.That(axis.Resolve(true, true, true), Is.EqualTo(-1f));

            axis.ResetHeldState();
            Assert.That(axis.Resolve(true, true, true), Is.EqualTo(-1f));
        }

        [Test]
        public void MockItemsTransferIntoTheFirstOpenDestinationSlot()
        {
            var nearby = new List<string> { "RELIC", "KEY" };
            string[] backpack = { "TONIC", null, null };

            bool moved = ExplorationOverlay.TryMoveMockListItemToArray(
                nearby,
                0,
                backpack,
                out int destination
            );

            Assert.That(moved, Is.True);
            Assert.That(destination, Is.EqualTo(1));
            Assert.That(nearby, Is.EqualTo(new[] { "KEY" }));
            Assert.That(backpack[1], Is.EqualTo("RELIC"));
        }

        [Test]
        public void MockEquipSwapsThePreviousGearItemBackToItsSource()
        {
            string[] backpack = { "CHARM" };
            string[] gear = { "SANITIZER" };

            bool equipped = ExplorationOverlay.SwapMockItem(backpack, 0, gear, 0);

            Assert.That(equipped, Is.True);
            Assert.That(gear[0], Is.EqualTo("CHARM"));
            Assert.That(backpack[0], Is.EqualTo("SANITIZER"));
        }

        [Test]
        public void MockDropAppendsANewNearbyListRow()
        {
            string[] backpack = { "TONIC" };
            var nearby = new List<string> { "RELIC" };

            bool dropped = ExplorationOverlay.TryMoveMockArrayItemToList(
                backpack,
                0,
                nearby,
                out int destination
            );

            Assert.That(dropped, Is.True);
            Assert.That(destination, Is.EqualTo(1));
            Assert.That(nearby, Is.EqualTo(new[] { "RELIC", "TONIC" }));
            Assert.That(backpack[0], Is.Null);
        }

        [Test]
        public void UnequippingUsesTheFirstOpenBackpackSlot()
        {
            string[] gear = { "CHARM" };
            string[] backpack = { "TONIC", null };
            var nearby = new List<string> { "RELIC" };

            bool unequipped = ExplorationOverlay.TryUnequipMockItem(
                gear,
                0,
                backpack,
                nearby,
                out InventoryPreviewLocation destination,
                out int destinationIndex
            );

            Assert.That(unequipped, Is.True);
            Assert.That(destination, Is.EqualTo(InventoryPreviewLocation.Backpack));
            Assert.That(destinationIndex, Is.EqualTo(1));
            Assert.That(gear[0], Is.Null);
            Assert.That(backpack[1], Is.EqualTo("CHARM"));
            Assert.That(nearby, Is.EqualTo(new[] { "RELIC" }));
        }

        [Test]
        public void UnequippingDropsNearbyWhenTheBackpackIsFull()
        {
            string[] gear = { "CHARM" };
            string[] backpack = { "TONIC" };
            var nearby = new List<string> { "RELIC" };

            bool unequipped = ExplorationOverlay.TryUnequipMockItem(
                gear,
                0,
                backpack,
                nearby,
                out InventoryPreviewLocation destination,
                out int destinationIndex
            );

            Assert.That(unequipped, Is.True);
            Assert.That(destination, Is.EqualTo(InventoryPreviewLocation.Nearby));
            Assert.That(destinationIndex, Is.EqualTo(1));
            Assert.That(gear[0], Is.Null);
            Assert.That(nearby, Is.EqualTo(new[] { "RELIC", "CHARM" }));
        }

        [Test]
        public void MockItemStatsAreStableAndComplete()
        {
            string[] first = ExplorationOverlay.MockItemStatValues("MOSSY CHARM");
            string[] second = ExplorationOverlay.MockItemStatValues("MOSSY CHARM");
            string[] different = ExplorationOverlay.MockItemStatValues("RUBBER GLOVES");

            Assert.That(first, Has.Length.EqualTo(6));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(different));
        }
    }
}
