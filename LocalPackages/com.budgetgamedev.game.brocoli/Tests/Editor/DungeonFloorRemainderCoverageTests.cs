using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class DungeonPropRemainderCoverageTests
    {
        [Test]
        public void FloorPatternsAndWeightedLootCoverFloodedTreasureAndFallbacks()
        {
            GameObject host = new("Coverage floor builder");
            GameObject invalid = new("Coverage invalid boost");
            GameObject boost = new("Coverage boost", typeof(HealthBoost));
            try
            {
                DungeonRoomBuilder builder = host.AddComponent<DungeonRoomBuilder>();
                Invoke(
                    builder,
                    "UsesDetailedFloor",
                    1,
                    1,
                    Room(DungeonLayout.RoomShape.OpenHall, DungeonLayout.RoomTheme.Flooded, 0),
                    new System.Random(1)
                );
                Invoke(
                    builder,
                    "UsesDetailedFloor",
                    1,
                    1,
                    Room(DungeonLayout.RoomShape.OpenHall, DungeonLayout.RoomTheme.Armory, 0),
                    new System.Random(1)
                );
                Invoke(
                    builder,
                    "UsesDetailedFloor",
                    1,
                    1,
                    Room(
                        DungeonLayout.RoomShape.LongHorizontal,
                        DungeonLayout.RoomTheme.Banquet,
                        0
                    ),
                    new System.Random(1)
                );
                Invoke(
                    builder,
                    "UsesDetailedFloor",
                    1,
                    1,
                    Room(DungeonLayout.RoomShape.OpenHall, DungeonLayout.RoomTheme.Banquet, 0),
                    new System.Random(1)
                );
                Invoke(
                    builder,
                    "UsesDetailedFloor",
                    1,
                    1,
                    Room(
                        DungeonLayout.RoomShape.OpenHall,
                        DungeonLayout.RoomTheme.TreasureVault,
                        0
                    ),
                    new System.Random(1)
                );
                Assert.That(
                    LootChest.PickWeightedBoost(new[] { invalid, boost }, total => total + 1f),
                    Is.SameAs(boost)
                );
            }
            finally
            {
                Object.DestroyImmediate(boost);
                Object.DestroyImmediate(invalid);
                Object.DestroyImmediate(host);
            }
        }
    }
}
