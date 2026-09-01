using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class DungeonPropRemainderCoverageTests
    {
        [Test]
        public void EnvironmentDoorwayAndWallPoliciesCoverRemainingDirections()
        {
            Assert.That(
                DungeonEnvironmentProfile.Of(DungeonLayout.EnvironmentTheme.Plains).BoundaryStyle,
                Is.EqualTo(DungeonBoundaryStyle.Undressed)
            );
            var opening = new DungeonPassage(true, 1, 0);
            var doorways = new DungeonLayout.RoomDoorways(default, default, opening, default);
            Assert.That(
                doorways.BlocksDoorway(
                    new Vector2(DungeonPassage.SlotOffset(0, DungeonLayout.RoomTilesX), -100f),
                    0f
                ),
                Is.True
            );
            Assert.That(
                DungeonWallDressing.RequiredShellWall(
                    new Vector2(0f, DungeonWallDressing.InnerFace(-DungeonLayout.RoomDepth / 2f))
                ),
                Is.EqualTo(DungeonLayout.South)
            );
            Assert.That(
                DungeonPropPlacer.OverlapsInteriorWall(
                    Vector2.zero,
                    0.1f,
                    Room(DungeonLayout.RoomShape.OpenHall, DungeonLayout.RoomTheme.Empty, 0)
                ),
                Is.False
            );

            GameObject emptyMesh = new("Coverage wall with empty mesh", typeof(MeshFilter));
            DungeonWallBaseTrim.RemoveLooseBase(emptyMesh);
            Object.DestroyImmediate(emptyMesh);
            var kept =
                (List<int>)
                    InvokeStatic(
                        typeof(DungeonWallBaseTrim),
                        "KeptTriangles",
                        new[] { 0, 1, 2, 3, 4, 5 },
                        new[] { false, false, false, true, true, true }
                    );
            Assert.That(kept, Is.EqualTo(new[] { 3, 4, 5 }));
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void FeaturePathwayAndThemePropsCoverPromotionVariants()
        {
            GameObject host = new("Coverage promotion placer");
            GameObject parent = new("Coverage promotion parent");
            GameObject rocks = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject structure = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rocks.name = DungeonPropTokens.WoodSupport;
            pot.name = DungeonPropTokens.Pot;
            structure.name = DungeonPropTokens.WoodStructure;
            try
            {
                DungeonPropPlacer placer = host.AddComponent<DungeonPropPlacer>();
                Set(placer, "propPrefabs", new[] { rocks, pot, structure });
                var occupied = new List<DungeonPropPlacer.OccupiedSpot>();
                var feature = new DungeonLayout.RoomArchetype(
                    DungeonLayout.RoomShape.OpenHall,
                    DungeonLayout.RoomTheme.Storage,
                    10.2f,
                    6.4f,
                    1
                );
                Invoke(
                    placer,
                    "BuildFeatureWallScreens",
                    parent.transform,
                    Vector2.zero,
                    feature,
                    new System.Random(1),
                    occupied
                );
                Assert.That(occupied, Is.Not.Empty);

                var divided = new DungeonLayout.RoomArchetype(
                    DungeonLayout.RoomShape.Divided,
                    DungeonLayout.RoomTheme.Sparse,
                    10f,
                    8f,
                    0
                );
                Invoke(
                    placer,
                    "BuildPathwayDressing",
                    parent.transform,
                    Vector2.zero,
                    divided,
                    new System.Random(2),
                    new List<DungeonPropPlacer.OccupiedSpot>()
                );
                Invoke(
                    placer,
                    "BuildPathwayDressing",
                    parent.transform,
                    Vector2.zero,
                    Room(DungeonLayout.RoomShape.LongHorizontal, DungeonLayout.RoomTheme.Sparse, 0),
                    new System.Random(2),
                    new List<DungeonPropPlacer.OccupiedSpot>()
                );
                var blocked = new List<DungeonPropPlacer.OccupiedSpot>
                {
                    new(Vector2.zero, 100f, true),
                };
                Invoke(
                    placer,
                    "TryPlacePathProp",
                    parent.transform,
                    Vector2.zero,
                    Room(DungeonLayout.RoomShape.OpenHall, DungeonLayout.RoomTheme.Sparse, 0),
                    new System.Random(3),
                    blocked,
                    Vector2.zero
                );

                InvokeTheme(placer, parent.transform, DungeonLayout.RoomTheme.Shrine, 1);
                InvokeTheme(placer, parent.transform, DungeonLayout.RoomTheme.Arena, 0);
                Invoke(
                    placer,
                    "PlaceNamed",
                    parent.transform,
                    Vector2.zero,
                    DungeonPropTokens.Pot,
                    Vector2.zero,
                    0f,
                    new List<DungeonPropPlacer.OccupiedSpot> { new(Vector2.zero, 1f, false, true) },
                    1f,
                    0f
                );
                Invoke(
                    placer,
                    "PlaceNamed",
                    parent.transform,
                    Vector2.zero,
                    DungeonPropTokens.Pot,
                    Vector2.zero,
                    0f,
                    new List<DungeonPropPlacer.OccupiedSpot>(),
                    1f,
                    0f
                );

                rocks.name = DungeonPropTokens.Rocks;
                pot.name = DungeonPropTokens.Stones;
                var caveProfile = DungeonEnvironmentProfile.Of(DungeonLayout.EnvironmentTheme.Cave);
                var keepOuts = new List<Rect>();
                DungeonRoomGeometry.AppendFeatureKeepOuts(keepOuts, Vector2Int.zero, feature);
                Invoke(
                    placer,
                    "PlaceLowBarrier",
                    parent.transform,
                    Vector2.zero,
                    keepOuts[0].center,
                    feature,
                    new System.Random(4),
                    new List<DungeonPropPlacer.OccupiedSpot>(),
                    caveProfile
                );
                Invoke(
                    placer,
                    "PlaceLowBarrier",
                    parent.transform,
                    Vector2.zero,
                    Vector2.zero,
                    Room(DungeonLayout.RoomShape.OpenHall, DungeonLayout.RoomTheme.Sparse, 0),
                    new System.Random(5),
                    new List<DungeonPropPlacer.OccupiedSpot>(),
                    caveProfile
                );

                var reported =
                    (HashSet<string>)
                        typeof(DungeonPropPlacer)
                            .GetField("ReportedMissingTokens", Hidden)
                            .GetValue(null);
                reported.Add(DungeonPropTokens.WoodSupport);
                reported.Add(DungeonPropTokens.WoodStructure);
                reported.Add(DungeonPropTokens.Pot);
                reported.Add(DungeonPropTokens.Rocks);
                reported.Add(DungeonPropTokens.Stones);
                Set(placer, "propPrefabs", System.Array.Empty<GameObject>());
                Invoke(
                    placer,
                    "BuildFeatureWallScreens",
                    parent.transform,
                    Vector2.zero,
                    feature,
                    new System.Random(6),
                    new List<DungeonPropPlacer.OccupiedSpot>()
                );
                Invoke(
                    placer,
                    "PlaceLowBarrier",
                    parent.transform,
                    Vector2.zero,
                    Vector2.zero,
                    feature,
                    new System.Random(7),
                    new List<DungeonPropPlacer.OccupiedSpot>(),
                    caveProfile
                );
                Assert.That(
                    Invoke(
                        placer,
                        "SpawnScaledProp",
                        parent.transform,
                        null,
                        Vector2.zero,
                        Quaternion.identity,
                        Vector3.one,
                        0f
                    ),
                    Is.Null
                );
            }
            finally
            {
                Object.DestroyImmediate(structure);
                Object.DestroyImmediate(pot);
                Object.DestroyImmediate(rocks);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AtmosphereAndReservationCoverRemainingEarlyReturns()
        {
            GameObject host = new("Coverage promotion atmosphere");
            GameObject parent = new("Coverage promotion atmosphere parent");
            GameObject torch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                DungeonPropPlacer placer = host.AddComponent<DungeonPropPlacer>();
                Set(placer, "torchPrefab", torch);
                Set(placer, "waterPrefab", null);
                foreach (
                    DungeonLayout.RoomTheme theme in new[]
                    {
                        DungeonLayout.RoomTheme.Shrine,
                        DungeonLayout.RoomTheme.Arena,
                    }
                )
                {
                    placer.BuildAtmosphere(
                        parent.transform,
                        Vector2Int.right,
                        Room(DungeonLayout.RoomShape.OpenHall, theme, 0),
                        default,
                        new System.Random(4)
                    );
                }

                Set(placer, "waterPrefab", torch);
                Set(placer, "waterChance", 0f);
                placer.BuildAtmosphere(
                    parent.transform,
                    Vector2Int.right,
                    Room(DungeonLayout.RoomShape.OpenHall, DungeonLayout.RoomTheme.Sparse, 0),
                    default,
                    new System.Random(5)
                );

                var reserved = new List<DungeonPropPlacer.OccupiedSpot>
                {
                    new(Vector2.zero, 1f, false, true),
                };
                InvokeStatic(
                    typeof(DungeonPropPlacer),
                    "OverlapsReservedChest",
                    Vector2.zero,
                    1f,
                    reserved
                );
            }
            finally
            {
                Object.DestroyImmediate(torch);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(host);
            }
        }
    }
}
