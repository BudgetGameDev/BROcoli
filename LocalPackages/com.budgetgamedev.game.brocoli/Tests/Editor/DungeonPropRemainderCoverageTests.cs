using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class DungeonPropRemainderCoverageTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void PoolPlacementCoversImpossibleDividerAndExhaustedSearches()
        {
            Type placementType = typeof(DungeonPropPlacer).GetNestedType("PoolPlacement", Hidden);
            Type listType = typeof(List<>).MakeGenericType(placementType);
            object empty = Activator.CreateInstance(listType);
            var impossible = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Tiny,
                DungeonLayout.RoomTheme.Flooded,
                1f,
                1f,
                0
            );
            Assert.That(TryPoolSpot(impossible, empty, Vector2.zero, 1f), Is.False);

            var divided = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.Divided,
                DungeonLayout.RoomTheme.Flooded,
                10f,
                8f,
                0
            );
            TryPoolSpot(divided, empty, new Vector2(0f, 4f), 0.25f);

            IList blocked = (IList)Activator.CreateInstance(listType);
            blocked.Add(
                Activator.CreateInstance(
                    placementType,
                    Hidden | BindingFlags.Public,
                    null,
                    new object[] { Vector2.zero, 100f, 0f },
                    null
                )
            );
            var open = new DungeonLayout.RoomArchetype(
                DungeonLayout.RoomShape.OpenHall,
                DungeonLayout.RoomTheme.Flooded,
                10f,
                8f,
                0
            );
            Assert.That(TryPoolSpot(open, blocked, Vector2.zero, 0.25f), Is.False);

            var reserved = new List<DungeonPropPlacer.OccupiedSpot>
            {
                new(Vector2.zero, 1f, false, true),
            };
            Assert.That(
                InvokeStatic(
                    typeof(DungeonPropPlacer),
                    "OverlapsReservedChest",
                    Vector2.one * 100f,
                    1f,
                    reserved
                ),
                Is.False
            );
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void ThemesAtmosphereAndSpawningCoverRemainingVariants()
        {
            GameObject host = new("Coverage prop placer");
            GameObject parent = new("Coverage prop parent");
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                DungeonPropPlacer placer = host.AddComponent<DungeonPropPlacer>();
                Set(placer, "chestPrefab", prefab);
                Set(placer, "goldenChestPrefab", prefab);
                Set(placer, "chestChance", 1f);
                Set(placer, "goldenChestChance", 1f);
                Set(placer, "propPrefabs", new[] { prefab });
                Set(placer, "torchPrefab", prefab);
                Set(placer, "waterPrefab", prefab);

                DungeonLayout.RoomArchetype treasure = Room(
                    DungeonLayout.RoomShape.Tiny,
                    DungeonLayout.RoomTheme.TreasureVault,
                    0
                );
                placer.BuildContents(
                    parent.transform,
                    Vector2Int.right,
                    treasure,
                    new System.Random(1),
                    new HashSet<int> { 0 }
                );
                placer.BuildAtmosphere(
                    parent.transform,
                    Vector2Int.right,
                    treasure,
                    default,
                    new System.Random(1)
                );

                foreach (
                    DungeonLayout.RoomShape shape in new[]
                    {
                        DungeonLayout.RoomShape.Tiny,
                        DungeonLayout.RoomShape.Compact,
                    }
                )
                {
                    DungeonLayout.RoomArchetype flooded = Room(
                        shape,
                        DungeonLayout.RoomTheme.Flooded,
                        0
                    );
                    placer.BuildContents(
                        parent.transform,
                        Vector2Int.right,
                        flooded,
                        new System.Random(2),
                        null
                    );
                    placer.BuildAtmosphere(
                        parent.transform,
                        Vector2Int.right,
                        flooded,
                        default,
                        new System.Random(2)
                    );
                }

                placer.BuildAtmosphere(
                    parent.transform,
                    Vector2Int.right,
                    new DungeonLayout.RoomArchetype(
                        DungeonLayout.RoomShape.Tiny,
                        DungeonLayout.RoomTheme.Flooded,
                        1f,
                        1f,
                        0
                    ),
                    default,
                    new System.Random(3)
                );

                InvokeTheme(placer, parent.transform, DungeonLayout.RoomTheme.Armory, 1);
                InvokeTheme(placer, parent.transform, DungeonLayout.RoomTheme.Banquet, 0);
                InvokeTheme(placer, parent.transform, DungeonLayout.RoomTheme.Shrine, 2);
                for (int seed = 0; seed < 8; seed++)
                    InvokeTheme(placer, parent.transform, DungeonLayout.RoomTheme.Sparse, 0, seed);
                placer.BuildAtmosphere(
                    parent.transform,
                    Vector2Int.right,
                    Room(
                        DungeonLayout.RoomShape.LongHorizontal,
                        DungeonLayout.RoomTheme.Banquet,
                        0
                    ),
                    default,
                    new System.Random(4)
                );
                Invoke(
                    placer,
                    "SpawnProp",
                    parent.transform,
                    prefab,
                    Vector2.zero,
                    Quaternion.identity,
                    2f,
                    0f
                );

                FieldInfo wallLayer = typeof(DungeonPropPlacer).GetField("cachedWallLayer", Hidden);
                wallLayer.SetValue(null, -1);
                InvokeStatic(typeof(DungeonPropPlacer), "EnrolAsOccluder", prefab);
                wallLayer.SetValue(null, LayerMask.NameToLayer("Wall"));
                prefab.layer = 0;
                InvokeStatic(typeof(DungeonPropPlacer), "EnrolAsOccluder", prefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static bool TryPoolSpot(
            DungeonLayout.RoomArchetype room,
            object placed,
            Vector2 preferred,
            float scale
        )
        {
            object[] arguments = { room, new System.Random(1), placed, preferred, scale, 0f, null };
            return (bool)InvokeStatic(typeof(DungeonPropPlacer), "TryPoolSpot", arguments);
        }

        private static DungeonLayout.RoomArchetype Room(
            DungeonLayout.RoomShape shape,
            DungeonLayout.RoomTheme theme,
            int variant
        ) => new(shape, theme, shape == DungeonLayout.RoomShape.Tiny ? 2.8f : 4.7f, 4.7f, variant);

        private static void InvokeTheme(
            DungeonPropPlacer placer,
            Transform parent,
            DungeonLayout.RoomTheme theme,
            int variant,
            int seed = 1
        )
        {
            Invoke(
                placer,
                "BuildThemeProps",
                parent,
                Vector2.zero,
                Room(DungeonLayout.RoomShape.OpenHall, theme, variant),
                new System.Random(seed),
                new List<DungeonPropPlacer.OccupiedSpot>()
            );
        }

        private static object Invoke(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, Hidden).Invoke(target, arguments);

        private static object InvokeStatic(Type type, string name, params object[] arguments) =>
            type.GetMethod(name, Hidden).Invoke(null, arguments);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
    }
}
