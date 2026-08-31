using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonChestPlacementCoverageTests
    {
        [Test]
        public void EveryRoomShapeAndAlleyBranchProducesCandidates()
        {
            foreach (
                DungeonLayout.RoomShape shape in Enum.GetValues(typeof(DungeonLayout.RoomShape))
            )
            {
                var archetype = new DungeonLayout.RoomArchetype(
                    shape,
                    DungeonLayout.RoomTheme.Storage,
                    10f,
                    8f,
                    0
                );
                for (int seed = 0; seed < 100; seed++)
                {
                    Vector2 alley = DungeonPropPlacer.RandomAlleySpot(
                        archetype,
                        new System.Random(seed),
                        0.5f
                    );
                    Vector2 room = DungeonPropPlacer.RandomMainRoomSpot(
                        archetype,
                        new System.Random(seed),
                        0.5f
                    );
                    Assert.That(float.IsFinite(alley.x + alley.y + room.x + room.y), Is.True);
                }
            }

            for (int seed = 0; seed < 100; seed++)
            {
                System.Random random = new(seed);
                DungeonPropPlacer.RandomCrossAlleySpot(random, 4f, -12f, 12f, -8f, 8f, 1f);
                DungeonPropPlacer.RandomHorizontalAlleySpot(random, 4f, -12f, 12f, -8f, 8f, 1f);
                DungeonPropPlacer.RandomVerticalAlleySpot(random, 4f, -12f, 12f, -8f, 8f, 1f);
            }
        }

        [Test]
        public void ChestClearanceAndFallbackCoverSuccessAndExhaustion()
        {
            var open = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.OpenHall,
                DungeonLayout.RoomTheme.Storage,
                10f,
                8f,
                0
            );
            var occupied = new List<DungeonPropPlacer.OccupiedSpot>();
            Assert.That(
                DungeonPropPlacer.IsChestSpotClear(Vector2.zero, open, 0.5f, occupied),
                Is.True
            );
            Assert.That(
                DungeonPropPlacer.IsChestSpotClear(Vector2.one * 100f, open, 0.5f, occupied),
                Is.False
            );
            occupied.Add(new DungeonPropPlacer.OccupiedSpot(Vector2.zero, 1f, false));
            Assert.That(
                DungeonPropPlacer.IsChestSpotClear(Vector2.zero, open, 0.5f, occupied),
                Is.False
            );

            Vector2 available = DungeonPropPlacer.GridFallbackSpot(
                open,
                new System.Random(1),
                0.5f,
                new List<DungeonPropPlacer.OccupiedSpot>()
            );
            Assert.That(float.IsFinite(available.x + available.y), Is.True);

            var blocked = new List<DungeonPropPlacer.OccupiedSpot>
            {
                new(Vector2.zero, 100f, true),
            };
            Assert.That(
                DungeonPropPlacer.GridFallbackSpot(open, new System.Random(1), 0.5f, blocked),
                Is.EqualTo(Vector2.zero)
            );
            Assert.That(
                DungeonPropPlacer.ChestSpot(open, new System.Random(1), 0.5f, blocked),
                Is.EqualTo(Vector2.zero)
            );

            var tiny = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Tiny,
                DungeonLayout.RoomTheme.Storage,
                3f,
                3f,
                0
            );
            bool foundInteriorWall = false;
            for (float x = -6f; x <= 6f && !foundInteriorWall; x += 0.25f)
            for (float y = -6f; y <= 6f && !foundInteriorWall; y += 0.25f)
                foundInteriorWall = !DungeonPropPlacer.IsChestSpotClear(
                    new Vector2(x, y),
                    tiny,
                    0.2f,
                    new List<DungeonPropPlacer.OccupiedSpot>()
                );
            Assert.That(foundInteriorWall, Is.True);
        }

        [Test]
        public void PropPlacementCoversClearBlockedDividerPoolAndRotationPolicies()
        {
            var open = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.OpenHall,
                DungeonLayout.RoomTheme.Storage,
                10f,
                8f,
                0
            );
            var empty = new List<DungeonPropPlacer.OccupiedSpot>();
            Assert.That(
                DungeonPropPlacer.TryRandomSpot(
                    open,
                    new System.Random(1),
                    empty,
                    0.5f,
                    false,
                    out _
                ),
                Is.True
            );
            Assert.That(
                DungeonPropPlacer.TryClusterSpot(open, new System.Random(1), empty, 0.5f, out _),
                Is.True
            );

            var blocked = new List<DungeonPropPlacer.OccupiedSpot>
            {
                new(Vector2.zero, 100f, true),
            };
            Assert.That(
                DungeonPropPlacer.TryRandomSpot(
                    open,
                    new System.Random(1),
                    blocked,
                    1f,
                    true,
                    out _
                ),
                Is.False
            );
            Assert.That(
                DungeonPropPlacer.TryClusterSpot(open, new System.Random(1), blocked, 1f, out _),
                Is.False
            );

            var tiny = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Tiny,
                DungeonLayout.RoomTheme.Storage,
                6f,
                6f,
                0
            );
            for (int seed = 0; seed < 500; seed++)
            {
                DungeonPropPlacer.TryRandomSpot(
                    tiny,
                    new System.Random(seed),
                    empty,
                    0.5f,
                    false,
                    out _
                );
                DungeonPropPlacer.TryClusterSpot(tiny, new System.Random(seed), empty, 0.5f, out _);
            }

            Assert.That(DungeonPropPlacer.IsOnDivider(Vector2.zero, open), Is.False);
            var vertical = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Divided,
                DungeonLayout.RoomTheme.Storage,
                10f,
                8f,
                0
            );
            var horizontal = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Divided,
                DungeonLayout.RoomTheme.Storage,
                10f,
                8f,
                1
            );
            Assert.That(DungeonPropPlacer.IsOnDivider(new Vector2(0f, 4f), vertical), Is.True);
            Assert.That(DungeonPropPlacer.IsOnDivider(new Vector2(0f, 4f), horizontal), Is.True);
            Assert.That(DungeonPropPlacer.IsOnDivider(new Vector2(8f, 0f), horizontal), Is.False);
            Assert.That(DungeonPropPlacer.IsOnDivider(Vector2.one * 20f, horizontal), Is.False);

            var eastWestRoute = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.NarrowHorizontal,
                DungeonLayout.RoomTheme.Storage,
                10.2f,
                2.8f,
                0
            );
            Assert.That(
                DungeonPropPlacer.IsOnDivider(new Vector2(4f, 4f), eastWestRoute),
                "enemy and prop placement ignored an east-west railing"
            );
            Assert.That(DungeonPropPlacer.IsOnDivider(Vector2.zero, eastWestRoute), Is.False);

            for (int variant = 0; variant < 4; variant++)
            {
                var poolRoom = new DungeonLayout.RoomArchetype(
                    DungeonLayout.RoomShape.OpenHall,
                    DungeonLayout.RoomTheme.Flooded,
                    10f,
                    8f,
                    variant
                );
                Vector2 pool = DungeonPropPlacer.PoolSpot(poolRoom, new System.Random(variant));
                Assert.That(pool.sqrMagnitude, Is.GreaterThan(0f));
            }

            Vector2 point = new(2f, 1f);
            Assert.That(DungeonPropPlacer.RotateQuarterTurns(point, 0), Is.EqualTo(point));
            Assert.That(
                DungeonPropPlacer.RotateQuarterTurns(point, 1),
                Is.EqualTo(new Vector2(1f, -2f))
            );
            Assert.That(DungeonPropPlacer.RotateQuarterTurns(point, 2), Is.EqualTo(-point));
            Assert.That(
                DungeonPropPlacer.RotateQuarterTurns(point, 3),
                Is.EqualTo(new Vector2(-1f, 2f))
            );
            Assert.That(
                DungeonPropPlacer.RotateQuarterTurns(point, -1),
                Is.EqualTo(new Vector2(-1f, 2f))
            );
        }
    }
}
