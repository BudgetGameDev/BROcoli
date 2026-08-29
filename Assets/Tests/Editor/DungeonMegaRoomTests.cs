using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Invariants of mega-room clusters (several grid cells merged into one open
/// hall) and of the forced-door rules that keep dead-end rooms rare.
/// </summary>
public sealed class DungeonMegaRoomTests
{
    private const int SweepRadius = 24;

    /// <summary>
    /// Every cell of a cluster must agree on the cluster's anchor and size,
    /// and every cell inside the anchored rectangle must be a member. A
    /// disagreement would make two rooms build different walls on the same
    /// shared edge.
    /// </summary>
    [Test]
    public void ClusterMembershipIsConsistent()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        {
            var layout = new DungeonLayout(seed);
            bool anyCluster = false;
            for (int x = -SweepRadius; x <= SweepRadius; x++)
            for (int y = -SweepRadius; y <= SweepRadius; y++)
            {
                var room = new Vector2Int(x, y);
                if (!layout.TryGetMegaCluster(room, out Vector2Int anchor, out Vector2Int size))
                    continue;

                anyCluster = true;
                Assert.That(
                    x >= anchor.x && x < anchor.x + size.x,
                    $"seed {seed}: room {room} lies outside its own cluster {anchor}+{size}"
                );
                Assert.That(
                    y >= anchor.y && y < anchor.y + size.y,
                    $"seed {seed}: room {room} lies outside its own cluster {anchor}+{size}"
                );

                for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                {
                    Vector2Int member = anchor + new Vector2Int(dx, dy);
                    Assert.That(
                        layout.TryGetMegaCluster(
                            member,
                            out Vector2Int memberAnchor,
                            out Vector2Int memberSize
                        ),
                        $"seed {seed}: cell {member} denies membership of cluster {anchor}"
                    );
                    Assert.That(memberAnchor, Is.EqualTo(anchor), $"seed {seed}: cell {member}");
                    Assert.That(memberSize, Is.EqualTo(size), $"seed {seed}: cell {member}");
                }
            }

            Assert.That(anyCluster, $"seed {seed}: the sweep found no mega room at all");
        }
    }

    /// <summary>
    /// The edges inside a cluster open everything between their grid posts
    /// and never carry an archway, so the merged cells read as one continuous
    /// space with only two pillar stubs marking the old boundary. The cells
    /// also share the mega archetype and one cluster-wide theme.
    /// </summary>
    [Test]
    public void ClusterInternalEdgesAreFullyOpen()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        {
            var layout = new DungeonLayout(seed);
            for (int x = -SweepRadius; x <= SweepRadius; x++)
            for (int y = -SweepRadius; y <= SweepRadius; y++)
            {
                var room = new Vector2Int(x, y);
                if (!layout.TryGetMegaCluster(room, out Vector2Int anchor, out _))
                    continue;

                DungeonLayout.RoomArchetype archetype = layout.Archetype(room);
                Assert.That(
                    archetype.Shape,
                    Is.EqualTo(DungeonLayout.RoomShape.MegaSection),
                    $"seed {seed}: cluster cell {room} kept an ordinary shape"
                );
                Assert.That(
                    archetype.Theme,
                    Is.EqualTo(layout.Archetype(anchor).Theme),
                    $"seed {seed}: cluster cell {room} disagrees with its anchor's theme"
                );

                for (int direction = 0; direction < 4; direction++)
                {
                    DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                    if (!layout.IsClusterInternalEdge(edge))
                        continue;

                    Assert.That(
                        layout.IsDoorOpen(room, direction),
                        $"seed {seed}: internal edge of {room} is not open"
                    );
                    DungeonPassage passage = layout.Passage(edge, true);
                    int slots = DungeonGeometryModel.SlotCount(direction);
                    int betweenPosts = ((1 << slots) - 1) & ~(1 | (1 << (slots - 1)));
                    Assert.That(
                        passage.OpeningMask,
                        Is.EqualTo(betweenPosts),
                        $"seed {seed}: internal edge of {room} does not open exactly "
                            + "the slots between its posts"
                    );
                    Assert.That(
                        passage.ArchwayMask,
                        Is.Zero,
                        $"seed {seed}: internal edge of {room} grew an archway"
                    );
                }
            }
        }
    }

    /// <summary>
    /// The spawn block never merges: the run must always begin in an ordinary
    /// room, and the guaranteed OpenHall at the origin must stay what it is.
    /// </summary>
    [Test]
    public void SpawnBlockNeverMerges()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        {
            var layout = new DungeonLayout(seed);
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            {
                Assert.That(
                    layout.IsMegaRoomCell(new Vector2Int(x, y)),
                    Is.False,
                    $"seed {seed}: spawn-block cell ({x}, {y}) merged into a mega room"
                );
            }
        }
    }

    /// <summary>
    /// The connectivity contract behind the layout: no room is ever sealed,
    /// and rooms with a single doorway side (dead ends that force the player
    /// to backtrack) are rare instead of routine.
    /// </summary>
    [Test]
    public void DeadEndRoomsAreRare()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        {
            var layout = new DungeonLayout(seed);
            int rooms = 0;
            int deadEnds = 0;
            for (int x = -SweepRadius; x <= SweepRadius; x++)
            for (int y = -SweepRadius; y <= SweepRadius; y++)
            {
                var room = new Vector2Int(x, y);
                if (DungeonLayout.Ring(room) == 0)
                    continue;

                int openSides = 0;
                for (int direction = 0; direction < 4; direction++)
                {
                    if (layout.IsDoorOpen(room, direction))
                        openSides++;
                }

                Assert.That(openSides, Is.GreaterThan(0), $"seed {seed}: room {room} is sealed");
                rooms++;
                if (openSides == 1)
                    deadEnds++;
            }

            float deadEndShare = deadEnds / (float)rooms;
            Assert.That(
                deadEndShare,
                Is.LessThan(0.05f),
                $"seed {seed}: {deadEndShare:P1} of rooms are dead ends"
            );
        }
    }
}
