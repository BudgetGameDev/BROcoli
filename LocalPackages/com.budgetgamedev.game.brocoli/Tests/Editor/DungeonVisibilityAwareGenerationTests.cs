using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonVisibilityAwareGenerationTests
    {
        private const string WallPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonWall.prefab";

        [Test]
        public void PlayableLevelIsTwoRoomsDeepAndMeandersOneRowAtATime()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                int previousSouth = SouthY(layout, -120);
                var usedRows = new HashSet<int>();
                for (int x = -120; x <= 120; x++)
                {
                    int south = SouthY(layout, x);
                    Assert.That(
                        Mathf.Abs(south - previousSouth),
                        Is.LessThanOrEqualTo(1),
                        $"seed {seed}: platform jumped between columns {x - 1} and {x}"
                    );
                    previousSouth = south;

                    int playable = 0;
                    for (int y = -5; y <= 5; y++)
                    {
                        if (!layout.IsPlayableRoom(new Vector2Int(x, y)))
                            continue;
                        playable++;
                        usedRows.Add(y);
                    }
                    Assert.That(playable, Is.EqualTo(2), $"seed {seed}, column {x}");
                }

                Assert.That(
                    usedRows.Count,
                    Is.LessThanOrEqualTo(4),
                    $"seed {seed}: platform spread too far north/south"
                );
            }
        }

        [Test]
        public void SouthBoundaryIsACliffAndPlayableCrossingsAreBroadAndSymmetric()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -24; x <= 24; x++)
                {
                    int south = SouthY(layout, x);
                    var lowerRoom = new Vector2Int(x, south);
                    var upperRoom = lowerRoom + Vector2Int.up;
                    Assert.That(
                        layout.PlayableEdgeStyle(
                            DungeonLayout.EdgeBetween(lowerRoom, DungeonLayout.South)
                        ),
                        Is.EqualTo(DungeonEdgeStyle.SouthCliff)
                    );
                    Assert.That(
                        layout.PlayableEdgeStyle(
                            DungeonLayout.EdgeBetween(upperRoom, DungeonLayout.North)
                        ),
                        Is.EqualTo(DungeonEdgeStyle.SolidBoundary)
                    );

                    for (int direction = 0; direction < 4; direction++)
                    {
                        Vector2Int neighbour =
                            lowerRoom + DungeonLayout.DirectionOffsets[direction];
                        DungeonPassage fromRoom = layout.PlayablePassage(lowerRoom, direction);
                        DungeonPassage fromNeighbour = layout.PlayablePassage(
                            neighbour,
                            (direction + 2) % 4
                        );
                        Assert.That(fromNeighbour.OpeningMask, Is.EqualTo(fromRoom.OpeningMask));
                        if (!layout.IsPlayableRoom(neighbour))
                            Assert.That(fromRoom.OpeningCount, Is.Zero);
                        else if (
                            layout.IsClusterInternalEdge(
                                DungeonLayout.EdgeBetween(lowerRoom, direction)
                            )
                        )
                            // A merged hall opens every slot between its posts; see
                            // MergedMegaRoomsStayOneOpenHallInsideThePlatform.
                            Assert.That(
                                fromRoom.OpeningMask,
                                Is.EqualTo(BetweenPostsMask(direction))
                            );
                        else
                            Assert.That(fromRoom.OpeningCount, Is.EqualTo(3));
                    }
                }
            }
        }

        [Test]
        public void GeneratedInteriorsPutNoFullHeightWallSouthOfRoomCentre()
        {
            bool foundDiagonal = false;
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -80; x <= 80; x++)
                {
                    int south = SouthY(layout, x);
                    for (int y = south; y <= south + 1; y++)
                    {
                        var room = new Vector2Int(x, y);
                        DungeonLayout.RoomArchetype archetype = layout.Archetype(room);
                        foundDiagonal |= archetype.Shape == DungeonLayout.RoomShape.DiagonalGallery;
                        var walls = new List<DungeonWallPiece>();
                        DungeonRoomGeometry.AppendInteriorWalls(walls, room, archetype);
                        float centerZ = DungeonLayout.RoomCenter(room).y;
                        foreach (DungeonWallPiece wall in walls)
                        {
                            Assert.That(
                                !wall.AlongX || wall.Anchor.y >= centerZ,
                                $"seed {seed}: {room} generated a camera-facing interior wall at {wall.Anchor}"
                            );
                        }
                    }
                }
            }
            Assert.That(
                foundDiagonal,
                Is.True,
                "the deterministic corpus never built a diagonal room"
            );
        }

        [Test]
        public void BuiltSouthCliffStaysLowAboveFloorAndContinuesBelowIt()
        {
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            Assert.That(wallPrefab, Is.Not.Null, WallPrefabPath);

            var host = new GameObject("Builder Host");
            var root = new GameObject("Generated Edge");
            try
            {
                DungeonRoomBuilder builder = host.AddComponent<DungeonRoomBuilder>();
                var serialized = new SerializedObject(builder);
                serialized.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
                serialized.ApplyModifiedProperties();

                GameObject edge = builder.BuildEdge(
                    root.transform,
                    new DungeonEdge(0, -1, true),
                    new DungeonPassage(false, 0, 0),
                    DungeonEdgeStyle.SouthCliff
                );
                Transform parapet = edge.transform.Find("Low South Parapet");
                Transform cliff = edge.transform.Find("Cliff Face Below Floor");
                Assert.That(parapet, Is.Not.Null);
                Assert.That(cliff, Is.Not.Null);

                BoxCollider[] parapetColliders = parapet.GetComponentsInChildren<BoxCollider>();
                BoxCollider[] cliffColliders = cliff.GetComponentsInChildren<BoxCollider>();
                Physics.SyncTransforms();
                Assert.That(parapetColliders.Length, Is.EqualTo(DungeonLayout.RoomTilesX));
                Assert.That(cliffColliders.Length, Is.EqualTo(DungeonLayout.RoomTilesX * 2));
                foreach (BoxCollider collider in parapetColliders)
                    Assert.That(collider.bounds.size.y, Is.LessThan(1.5f));
                float deepest = 0f;
                foreach (BoxCollider collider in cliffColliders)
                {
                    Assert.That(collider.bounds.max.y, Is.LessThan(0.02f));
                    deepest = Mathf.Min(deepest, collider.bounds.min.y);
                }
                Assert.That(deepest, Is.LessThan(-6f), "the cliff no longer reads as a tall drop");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// The crossing between the platform's two rows is the one wall run the
        /// camera always looks over, so it must never be built as architecture
        /// the visibility system has to keep lowering: knee-high ledge pieces
        /// below occlusion adoption height everywhere except the two grid posts.
        /// </summary>
        [Test]
        public void RowCrossingIsALowLedgeWithFullHeightGridPostsOnly()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -24; x <= 24; x++)
                {
                    var lowerRoom = new Vector2Int(x, SouthY(layout, x));
                    Assert.That(
                        layout.PlayableEdgeStyle(
                            DungeonLayout.EdgeBetween(lowerRoom, DungeonLayout.North)
                        ),
                        Is.EqualTo(DungeonEdgeStyle.RowDivider),
                        $"seed {seed}, column {x}"
                    );
                    if (layout.IsPlayableRoom(lowerRoom + Vector2Int.right))
                        Assert.That(
                            layout.PlayableEdgeStyle(
                                DungeonLayout.EdgeBetween(lowerRoom, DungeonLayout.East)
                            ),
                            Is.EqualTo(DungeonEdgeStyle.Interior),
                            $"seed {seed}, column {x}: vertical crossings stay full walls"
                        );
                }
            }

            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            Assert.That(wallPrefab, Is.Not.Null, WallPrefabPath);

            var host = new GameObject("Builder Host");
            var root = new GameObject("Generated Edge");
            try
            {
                DungeonRoomBuilder builder = host.AddComponent<DungeonRoomBuilder>();
                var serialized = new SerializedObject(builder);
                serialized.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
                serialized.ApplyModifiedProperties();

                var layout = new DungeonLayout(DungeonGeometryModel.Seeds[0]);
                var lowerRoom = new Vector2Int(0, SouthY(layout, 0));
                int middle = DungeonLayout.RoomTilesX / 2;
                int crossing = (1 << (middle - 1)) | (1 << middle) | (1 << (middle + 1));
                GameObject edge = builder.BuildEdge(
                    root.transform,
                    DungeonLayout.EdgeBetween(lowerRoom, DungeonLayout.North),
                    new DungeonPassage(true, crossing, 0),
                    DungeonEdgeStyle.RowDivider
                );

                Transform posts = edge.transform.Find("Occlusion Section - Divider Posts");
                Transform ledge = edge.transform.Find("Low Divider Ledge");
                Assert.That(posts, Is.Not.Null);
                Assert.That(ledge, Is.Not.Null);
                Assert.That(posts.GetComponent<DungeonOcclusionSection>(), Is.Not.Null);

                BoxCollider[] postColliders = posts.GetComponentsInChildren<BoxCollider>();
                BoxCollider[] ledgeColliders = ledge.GetComponentsInChildren<BoxCollider>();
                Physics.SyncTransforms();

                float runCenterX = DungeonLayout.RoomCenter(lowerRoom).x;
                Assert.That(postColliders.Length, Is.EqualTo(2));
                foreach (BoxCollider collider in postColliders)
                {
                    Assert.That(collider.bounds.size.y, Is.GreaterThan(1.5f));
                    Assert.That(
                        Mathf.Abs(collider.bounds.center.x - runCenterX),
                        Is.GreaterThan(10f),
                        "a full-height piece stood away from the grid posts"
                    );
                }

                Assert.That(ledgeColliders.Length, Is.EqualTo(2));
                foreach (BoxCollider collider in ledgeColliders)
                {
                    Assert.That(
                        collider.bounds.size.y,
                        Is.LessThan(1.5f),
                        "a ledge piece is tall enough for occlusion adoption"
                    );
                    Assert.That(
                        Mathf.Abs(collider.bounds.center.x - runCenterX),
                        Is.InRange(6f, 10f)
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// A mega room whose cells both survive inside the platform still has to
        /// read as one hall. The broad crossing used everywhere else would leave a
        /// wall run standing across its middle, so cluster edges keep the cluster's
        /// own fully open passage.
        /// </summary>
        [Test]
        public void MergedMegaRoomsStayOneOpenHallInsideThePlatform()
        {
            bool foundInternalEdge = false;
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -80; x <= 80; x++)
                {
                    int south = SouthY(layout, x);
                    for (int y = south; y <= south + 1; y++)
                    for (int direction = 0; direction < 4; direction++)
                    {
                        var room = new Vector2Int(x, y);
                        Vector2Int neighbour = room + DungeonLayout.DirectionOffsets[direction];
                        DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                        if (
                            !layout.IsPlayableRoom(neighbour) || !layout.IsClusterInternalEdge(edge)
                        )
                            continue;

                        foundInternalEdge = true;
                        Assert.That(
                            layout.PlayablePassage(room, direction).OpeningMask,
                            Is.EqualTo(BetweenPostsMask(direction)),
                            $"seed {seed}: mega room {room} is bisected by a wall run"
                        );
                    }
                }
            }
            Assert.That(
                foundInternalEdge,
                Is.True,
                "the deterministic corpus never merged two platform rooms"
            );
        }

        /// <summary>
        /// The bot and the map read connectivity from the same predicate the
        /// generator builds from. Steering by the unbounded grid's door graph
        /// would walk the bot into the sealed boundary and draw corridors the
        /// player can never take.
        /// </summary>
        [Test]
        public void ExplorationNeverLeavesThePlatform()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -40; x <= 40; x++)
                {
                    int south = SouthY(layout, x);
                    for (int y = south; y <= south + 1; y++)
                    {
                        var room = new Vector2Int(x, y);
                        for (int direction = 0; direction < 4; direction++)
                        {
                            Vector2Int neighbour = room + DungeonLayout.DirectionOffsets[direction];
                            Assert.That(
                                layout.IsPlayableDoorOpen(room, direction),
                                Is.EqualTo(layout.IsPlayableRoom(neighbour)),
                                $"seed {seed}: {room} disagrees with the built boundary"
                            );
                        }

                        int chosen = BotDecisionPolicy.ChooseExplorationDirection(
                            layout,
                            room,
                            new HashSet<Vector2Int>(),
                            1f,
                            -1
                        );
                        Assert.That(chosen, Is.GreaterThanOrEqualTo(0), $"seed {seed}: {room}");
                        Assert.That(
                            layout.IsPlayableRoom(room + DungeonLayout.DirectionOffsets[chosen]),
                            $"seed {seed}: bot left the platform from {room}"
                        );
                    }
                }
            }
        }

        private static int SouthY(DungeonLayout layout, int x)
        {
            return layout.ClampToPlayableBand(new Vector2Int(x, -1000)).y;
        }

        /// <summary>Every slot of a crossing except the two standing on grid posts.</summary>
        private static int BetweenPostsMask(int direction)
        {
            int slots =
                direction == DungeonLayout.North || direction == DungeonLayout.South
                    ? DungeonLayout.RoomTilesX
                    : DungeonLayout.RoomTilesZ;
            return ((1 << slots) - 1) & ~(1 | (1 << (slots - 1)));
        }
    }
}
