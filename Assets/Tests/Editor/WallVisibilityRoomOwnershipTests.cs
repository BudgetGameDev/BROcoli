using NUnit.Framework;
using UnityEngine;

/// <summary>Walls may lower only for the logical room the player occupies.</summary>
public sealed class WallVisibilityRoomOwnershipTests
{
    [Test]
    public void AnInteriorSectionBelongsOnlyToItsRoom()
    {
        var root = new GameObject("Room-owned section");
        try
        {
            DungeonOcclusionSection section = root.AddComponent<DungeonOcclusionSection>();
            section.ConfigureRoom(new Vector2Int(2, -3));

            Assert.That(section.BelongsToRoom(new Vector2Int(2, -3), null));
            Assert.That(section.BelongsToRoom(new Vector2Int(3, -3), null), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ASharedEdgeBelongsToBothRoomsAndNoOthers()
    {
        var root = new GameObject("Edge-owned section");
        try
        {
            DungeonOcclusionSection section = root.AddComponent<DungeonOcclusionSection>();
            section.ConfigureEdge(new DungeonEdge(4, 7, horizontal: true));

            Assert.That(section.BelongsToRoom(new Vector2Int(4, 7), null));
            Assert.That(section.BelongsToRoom(new Vector2Int(4, 8), null));
            Assert.That(section.BelongsToRoom(new Vector2Int(5, 7), null), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void MergedCellsCountAsOneLogicalRoom()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        {
            var layout = new DungeonLayout(seed);
            foreach (Vector2Int cell in WallVisibilityFixtures.SweepRooms())
            {
                if (!layout.TryGetMegaCluster(cell, out Vector2Int anchor, out Vector2Int size))
                    continue;

                Vector2Int other = anchor + new Vector2Int(size.x - 1, size.y - 1);
                Assert.That(layout.AreInSameRoom(cell, other));
                Vector2 firstCenter = DungeonLayout.RoomCenter(cell);
                Vector2 otherCenter = DungeonLayout.RoomCenter(other);
                Assert.That(
                    EnemyRevealGate.IsRevealed(
                        new Vector3(firstCenter.x, 0f, firstCenter.y),
                        new Vector3(otherCenter.x, 0f, otherCenter.y),
                        layout
                    ),
                    "an enemy in another cell of the same mega room was hidden"
                );
                return;
            }
        }

        Assert.Fail("the test corpus contains no merged mega room");
    }
}
