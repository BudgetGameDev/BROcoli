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
        public void PlayableLevelIsTwoRoomsDeepAndFollowsLongDiagonalRuns()
        {
            foreach (int seed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(seed);
                int previousSouth = SouthY(layout, -120);
                int previousDirection = 0;
                int diagonalRun = 0;
                int longestDiagonalRun = 0;
                var usedRows = new HashSet<int>();
                usedRows.Add(previousSouth);
                usedRows.Add(previousSouth + 1);
                for (int x = -119; x <= 120; x++)
                {
                    int south = SouthY(layout, x);
                    int direction = south - previousSouth;
                    Assert.That(
                        Mathf.Abs(direction),
                        Is.EqualTo(1),
                        $"seed {seed}: platform stopped running diagonally between {x - 1} and {x}"
                    );
                    if (direction == previousDirection)
                        diagonalRun++;
                    else
                        diagonalRun = 1;
                    longestDiagonalRun = Mathf.Max(longestDiagonalRun, diagonalRun);
                    previousDirection = direction;
                    previousSouth = south;

                    Assert.That(layout.IsPlayableRoom(new Vector2Int(x, south)), Is.True);
                    Assert.That(layout.IsPlayableRoom(new Vector2Int(x, south + 1)), Is.True);
                    Assert.That(layout.IsPlayableRoom(new Vector2Int(x, south - 1)), Is.False);
                    Assert.That(layout.IsPlayableRoom(new Vector2Int(x, south + 2)), Is.False);
                    usedRows.Add(south);
                    usedRows.Add(south + 1);
                }

                Assert.That(
                    usedRows.Count,
                    Is.GreaterThanOrEqualTo(12),
                    $"seed {seed}: platform stayed too horizontal"
                );
                Assert.That(
                    longestDiagonalRun,
                    Is.GreaterThanOrEqualTo(10),
                    $"seed {seed}: diagonal direction changed too often"
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
                            direction == DungeonLayout.North
                            || direction == DungeonLayout.South
                        )
                            // Every horizontal slot, including the old grid-post
                            // positions, opens so no east-west wall stands in front
                            // of playable floor.
                            Assert.That(
                                fromRoom.OpeningMask,
                                Is.EqualTo(FullOpeningMask(direction))
                            );
                        else if (
                            layout.IsClusterInternalEdge(
                                DungeonLayout.EdgeBetween(lowerRoom, direction)
                            )
                        )
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
        public void GeneratedInteriorsIncludeEastWestRoutesAndNorthSouthRoutes()
        {
            int generatedRooms = 0;
            int diagonalRooms = 0;
            int eastWestPieces = 0;
            int northSouthPieces = 0;
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
                        generatedRooms++;
                        if (archetype.Shape == DungeonLayout.RoomShape.DiagonalGallery)
                            diagonalRooms++;
                        var walls = new List<DungeonWallPiece>();
                        DungeonRoomGeometry.AppendInteriorWalls(walls, room, archetype);
                        foreach (DungeonWallPiece wall in walls)
                        {
                            // Interior pieces are low route-shaping runs, except
                            // for the rare sealed-off feature wall landmark.
                            Assert.That(
                                wall.Kind,
                                Is.EqualTo(DungeonWallKind.Interior)
                                    .Or.EqualTo(DungeonWallKind.InteriorFeature)
                            );
                            if (wall.Kind == DungeonWallKind.InteriorFeature)
                                Assert.That(
                                    DungeonRoomGeometry.HasFeatureWall(archetype),
                                    $"seed {seed}: room {room} built a feature wall "
                                        + "its archetype does not sanction"
                                );
                            if (wall.AlongX)
                                eastWestPieces++;
                            else
                                northSouthPieces++;
                        }

                        if (
                            archetype.Shape
                            is DungeonLayout.RoomShape.NarrowHorizontal
                                or DungeonLayout.RoomShape.LongHorizontal
                        )
                            Assert.That(
                                walls.Exists(wall => wall.AlongX),
                                $"seed {seed}: horizontal room {room} has no guiding railings"
                            );
                    }
                }
            }
            Assert.That(eastWestPieces, Is.GreaterThan(0), "the dungeon built no east-west route");
            Assert.That(
                northSouthPieces,
                Is.GreaterThan(0),
                "the dungeon built no north-south route"
            );
            Assert.That(
                diagonalRooms,
                Is.GreaterThanOrEqualTo(generatedRooms / 4),
                $"only {diagonalRooms} of {generatedRooms} generated rooms were diagonal galleries"
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
                Transform parapet = edge.transform.Find("Low Dungeon Railing");
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
        /// The crossing between the platform's two rows is the wall run the camera
        /// always looks over, so it stays completely open. Even the old corner
        /// posts were full east-west slabs a player could stand behind.
        /// </summary>
        [Test]
        public void PlayableRowCrossingsBuildNoWallPieces()
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
                        Is.EqualTo(DungeonEdgeStyle.OpenCrossing),
                        $"seed {seed}, column {x}"
                    );
                    Assert.That(
                        layout.PlayablePassage(lowerRoom, DungeonLayout.North).OpeningMask,
                        Is.EqualTo(FullOpeningMask(DungeonLayout.North)),
                        $"seed {seed}, column {x}: row crossing was not fully open"
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
                    DungeonEdgeStyle.OpenCrossing
                );

                Assert.That(edge.transform.childCount, Is.Zero);
                Assert.That(edge.GetComponentsInChildren<Collider>(), Is.Empty);
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
                        int expected = edge.Horizontal
                            ? FullOpeningMask(direction)
                            : BetweenPostsMask(direction);
                        Assert.That(
                            layout.PlayablePassage(room, direction).OpeningMask,
                            Is.EqualTo(expected),
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

        private static int FullOpeningMask(int direction)
        {
            int slots =
                direction == DungeonLayout.North || direction == DungeonLayout.South
                    ? DungeonLayout.RoomTilesX
                    : DungeonLayout.RoomTilesZ;
            return (1 << slots) - 1;
        }
    }
}
