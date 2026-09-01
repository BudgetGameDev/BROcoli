using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class DungeonVisibilityAwareGenerationTests
    {
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
    }
}
