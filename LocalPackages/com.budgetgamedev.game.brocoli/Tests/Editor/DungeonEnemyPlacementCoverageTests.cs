using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonEnemyPlacementCoverageTests
    {
        [Test]
        [TestMustExpectAllLogs(false)]
        public void SpawnDormantCoversFilteringInstantiationHydraAndEliteConfiguration()
        {
            foreach (
                PoolManager manager in UnityEngine.Object.FindObjectsByType<PoolManager>(
                    FindObjectsSortMode.None
                )
            )
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            PoolManager.ResetInstance();

            (DungeonLayout layout, Vector2Int occupied) = FindRoom(population =>
                population.Count > 0
            );
            Assert.That(
                DungeonEnemyPlacer.SpawnDormant(
                    new EnemyBase[] { null },
                    layout,
                    occupied,
                    layout.Archetype(occupied)
                ),
                Is.Empty
            );

            EnemyScript basic = CreatePrefab<EnemyScript>("Basic Enemy");
            HydraEnemyScript hydra = CreatePrefab<HydraEnemyScript>("Hydra Enemy");
            var spawned = new List<EnemyBase>();
            try
            {
                (DungeonLayout normalLayout, Vector2Int normalRoom) = FindRoom(
                    population => population.Count > 0 && !population.Elite,
                    room => DungeonLayout.Ring(room) >= 4
                );
                spawned.AddRange(
                    DungeonEnemyPlacer.SpawnDormant(
                        new[] { hydra },
                        normalLayout,
                        normalRoom,
                        normalLayout.Archetype(normalRoom)
                    )
                );

                (DungeonLayout eliteLayout, Vector2Int eliteRoom) = FindRoom(population =>
                    population.Count > 0 && population.Elite
                );
                spawned.AddRange(
                    DungeonEnemyPlacer.SpawnDormant(
                        new[] { basic },
                        eliteLayout,
                        eliteRoom,
                        eliteLayout.Archetype(eliteRoom)
                    )
                );
                Assert.That(spawned, Is.Not.Empty);

                var entries = new List<EnemyBase> { null };
                DungeonEnemyPlacer.Activate(entries);
                entries.Add(null);
                DungeonEnemyPlacer.Despawn(entries);
            }
            finally
            {
                foreach (EnemyBase enemy in spawned)
                    if (enemy != null)
                        UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                UnityEngine.Object.DestroyImmediate(basic.gameObject);
                UnityEngine.Object.DestroyImmediate(hydra.gameObject);
            }
        }

        private static T CreatePrefab<T>(string name)
            where T : EnemyBase
        {
            GameObject root = new(name);
            root.AddComponent<Rigidbody>();
            root.AddComponent<CapsuleCollider>();
            return root.AddComponent<T>();
        }

        private static (DungeonLayout, Vector2Int) FindRoom(
            Func<DungeonLayout.RoomPopulation, bool> populationMatch,
            Func<Vector2Int, bool> roomMatch = null
        )
        {
            for (int seed = 1; seed < 200; seed++)
            {
                DungeonLayout layout = new(seed);
                for (int x = -12; x <= 12; x++)
                for (int y = -12; y <= 12; y++)
                {
                    var room = new Vector2Int(x, y);
                    if (
                        (roomMatch == null || roomMatch(room))
                        && populationMatch(layout.Population(room))
                    )
                        return (layout, room);
                }
            }

            Assert.Fail("No deterministic room matched the requested population.");
            return default;
        }
    }
}
