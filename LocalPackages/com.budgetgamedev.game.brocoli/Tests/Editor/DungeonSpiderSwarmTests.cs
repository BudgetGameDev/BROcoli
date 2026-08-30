using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonSpiderSwarmTests
    {
        private const string SpiderPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Resources/Brocoli/CursedDevolpmentStudioAss Assets/Waves/EnemySpider.prefab";

        [Test]
        public void SpiderHasEnoughHealthToSurviveTheOpeningHit()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPrefabPath);
            Assert.That(prefab, Is.Not.Null, SpiderPrefabPath);

            EnemyBase spider = prefab.GetComponent<EnemyBase>();
            Assert.That(spider, Is.Not.Null);
            Assert.That(spider.Health, Is.EqualTo(45f));
            Assert.That(spider.MaxHealth, Is.EqualTo(45f));
        }

        [Test]
        public void SpiderSwarmsAreRareFiveEnemyGroupsInSuitableRooms()
        {
            int populatedRooms = 0;
            int swarmRooms = 0;

            for (int seed = 0; seed < 24; seed++)
            {
                var layout = new DungeonLayout(seed);
                for (int x = -8; x <= 8; x++)
                for (int y = -8; y <= 8; y++)
                {
                    var room = new Vector2Int(x, y);
                    DungeonLayout.RoomPopulation population = layout.Population(room);
                    if (population.Count > 0)
                        populatedRooms++;
                    if (!population.IsSpiderSwarm)
                        continue;

                    swarmRooms++;
                    Assert.That(
                        population.Count,
                        Is.EqualTo(DungeonLayout.RoomPopulation.SpiderSwarmSize)
                    );
                    Assert.That(population.Elite, Is.False);
                    Assert.That(DungeonLayout.Ring(room), Is.GreaterThanOrEqualTo(2));
                    Assert.That(
                        layout.Archetype(room).EnemyCapacity,
                        Is.GreaterThanOrEqualTo(DungeonLayout.RoomPopulation.SpiderSwarmSize)
                    );
                }
            }

            Assert.That(swarmRooms, Is.GreaterThan(0));
            Assert.That(swarmRooms, Is.LessThan(populatedRooms / 4));
        }

        [Test]
        public void SpiderSwarmRoomSpawnsFiveSpidersTogether()
        {
            GameObject spiderObject = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPrefabPath);
            GameObject easyObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.budgetgamedev.game.brocoli/Resources/Brocoli/CursedDevolpmentStudioAss Assets/Waves/EnemyEasy.prefab"
            );
            var prefabs = new List<EnemyBase>
            {
                easyObject.GetComponent<EnemyBase>(),
                spiderObject.GetComponent<EnemyBase>(),
            };

            Assert.That(TryFindSwarmRoom(out DungeonLayout layout, out Vector2Int room), Is.True);
            List<EnemyBase> spawned = DungeonEnemyPlacer.SpawnDormant(
                prefabs,
                layout,
                room,
                layout.Archetype(room)
            );

            try
            {
                Assert.That(spawned, Has.Count.EqualTo(5));
                foreach (EnemyBase enemy in spawned)
                    Assert.That(enemy.name, Does.Contain("Spider"));
            }
            finally
            {
                foreach (EnemyBase enemy in spawned)
                {
                    if (enemy != null)
                        Object.DestroyImmediate(enemy.gameObject);
                }
            }
        }

        private static bool TryFindSwarmRoom(out DungeonLayout layout, out Vector2Int room)
        {
            for (int seed = 0; seed < 24; seed++)
            {
                layout = new DungeonLayout(seed);
                for (int x = -8; x <= 8; x++)
                for (int y = -8; y <= 8; y++)
                {
                    room = new Vector2Int(x, y);
                    if (layout.Population(room).IsSpiderSwarm)
                        return true;
                }
            }

            layout = null;
            room = default;
            return false;
        }
    }
}
