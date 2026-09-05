using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Spawns a room's enemies ahead of the player's arrival. Enemies come from
    /// the shared <see cref="PoolManager"/> and stand visibly in the room but
    /// dormant (their AI component disabled), so there is no pop-in when the
    /// player peers through a doorway; entering the room wakes the whole group.
    /// </summary>
    public static partial class DungeonEnemyPlacer
    {
        private const float CenterClearRadius = 4f;

        /// <summary>
        /// Spawns the room's enemy group dormant and returns it. The mix of enemy
        /// types unlocks with ring distance; elite dens promote low-tier enemies.
        /// </summary>
        public static List<EnemyBase> SpawnDormant(
            IReadOnlyList<EnemyBase> prefabs,
            DungeonLayout layout,
            Vector2Int room,
            DungeonLayout.RoomArchetype archetype
        )
        {
            var spawned = new List<EnemyBase>();
            DungeonLayout.RoomPopulation population = layout.Population(room);
            if (population.Count <= 0 || prefabs == null || prefabs.Count == 0)
                return spawned;

            int ring = DungeonLayout.Ring(room);
            var allowed = new List<EnemyBase>();
            foreach (EnemyBase prefab in prefabs)
            {
                if (prefab == null)
                    continue;
                int minRing = MinRingFor(prefab.name);
                // Elite dens promote the weakest enemies instead of using strong ones.
                if (population.Elite ? minRing <= 1 : ring >= minRing)
                    allowed.Add(prefab);
            }
            if (allowed.Count == 0)
                return spawned;

            EnemyBase swarmSpider = population.IsSpiderSwarm ? FindSpider(allowed) : null;

            System.Random random = layout.RoomRandom(room, 606);
            float power = CurrentPlayerPower();
            float depthScale = layout.EnemyHealthScale(room);
            float healthPowerScale = EnemyScaling.Health(power);
            float damageScale = EnemyScaling.Damage(power);
            float countScale = EnemyScaling.Count(power);
            float speedScale = EnemyScaling.SpeedScale(ring, power);
            float healthScale = depthScale * healthPowerScale;
            Vector2 roomCenter = DungeonLayout.RoomCenter(room);

            int spawnCount = Mathf.Min(population.Count, archetype.EnemyCapacity);
            if (!population.IsSpiderSwarm)
            {
                spawnCount = Mathf.Min(
                    archetype.EnemyCapacity,
                    Mathf.RoundToInt(spawnCount * countScale)
                );
            }
            for (int i = 0; i < spawnCount; i++)
            {
                EnemyBase prefab = PickEnemy(allowed, swarmSpider, archetype, i, random);
                Vector3 position = PickSpot(roomCenter, archetype, random).ToWorld();

                EnemyBase enemy = PoolManager.Existing?.GetEnemy(
                    prefab,
                    position,
                    Quaternion.identity
                );
                if (enemy != null)
                {
                    enemy.SetPooled(true);
                    enemy.ResetForPool();
                }
                else
                {
                    enemy = Object.Instantiate(prefab, position, Quaternion.identity);
                }

                // The room it was placed in is the room it belongs to; wandering more
                // than a room or so from it ends the chase.
                enemy.SetLeashHome(position.ToGround());

                enemy.Health *= healthScale;
                enemy.MaxHealth *= healthScale;
                enemy.Damage *= damageScale;
                enemy.Speed = EnemyScaling.Speed(enemy.Speed, speedScale, prefab.name);

                if (enemy is HydraEnemyScript hydra)
                    hydra.ConfigureForDungeonRing(ring);

                if (population.Elite)
                {
                    enemy.MakeElite();
                    enemy.OnEliteDeath -= DropEliteReward;
                    enemy.OnEliteDeath += DropEliteReward;
                }

                if (enemy.GetComponent<DungeonEnemyNavigator>() == null)
                    enemy.gameObject.AddComponent<DungeonEnemyNavigator>();

                // Visible but dormant: the enemy stands in the room, its AI off.
                enemy.enabled = false;
                spawned.Add(enemy);
            }

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
            GameplayDiagnostics.RoomSpawned?.Invoke(
                ring,
                power,
                depthScale,
                healthPowerScale,
                damageScale,
                countScale,
                speedScale,
                spawned.Count
            );
#endif
            return spawned;
        }

        /// <summary>Wakes a dormant enemy group when its room is entered.</summary>
        public static void Activate(List<EnemyBase> dormant)
        {
            if (dormant == null)
                return;

            foreach (EnemyBase enemy in dormant)
            {
                if (enemy != null)
                    enemy.enabled = true;
            }
            dormant.Clear();
        }

        /// <summary>
        /// Moves dormant enemies onto the freshly baked walkable surface. This
        /// prevents a random spawn from beginning inside a prop or just beyond an
        /// interior-wall NavMesh boundary.
        /// </summary>
        public static void AlignToNavMesh(List<EnemyBase> dormant)
        {
            if (dormant == null)
                return;

            foreach (EnemyBase enemy in dormant)
            {
                if (
                    enemy == null
                    || !NavMesh.SamplePosition(
                        enemy.transform.position,
                        out NavMeshHit hit,
                        4f,
                        NavMesh.AllAreas
                    )
                )
                    continue;

                Vector3 position = hit.position;
                position.y = enemy.transform.position.y;
                AlignEnemy(enemy, position);
            }
        }

        internal static void AlignEnemy(EnemyBase enemy, Vector3 position)
        {
            if (enemy.rb != null)
            {
                enemy.rb.position = position;
                enemy.rb.linearVelocity = Vector3.zero;
                enemy.rb.angularVelocity = Vector3.zero;
            }
            else
                enemy.transform.position = position;
        }

        /// <summary>Returns a still-dormant group to the pool when its room unloads.</summary>
        public static void Despawn(List<EnemyBase> dormant) =>
            Despawn(dormant, PoolManager.Existing, enemy => Object.Destroy(enemy.gameObject));

        internal static void Despawn(
            List<EnemyBase> dormant,
            PoolManager poolManager,
            System.Action<EnemyBase> destroyEnemy
        )
        {
            if (dormant == null)
                return;

            foreach (EnemyBase enemy in dormant)
            {
                if (enemy == null)
                    continue;
                // Re-enable the AI so the pooled instance behaves when reused.
                enemy.enabled = true;
                if (poolManager != null)
                    poolManager.ReturnEnemy(enemy);
                else
                    destroyEnemy(enemy);
            }
            dormant.Clear();
        }

        private static float CurrentPlayerPower()
        {
            PlayerStats stats = PlayerStats.Resolve();
            return stats != null ? stats.ComputePowerScore() : 1f;
        }

        private static void DropEliteReward(Vector3 position) =>
            DropEliteReward(
                position,
                () =>
                    LootChest.PickWeightedBoost(
                        Object.FindAnyObjectByType<BoostHandler>()?.BoostPrefabs
                    ),
                (prefab, spawnPosition) =>
                    Object.Instantiate(prefab, spawnPosition, Quaternion.identity)
            );

        internal static void DropEliteReward(
            Vector3 position,
            System.Func<GameObject> chooseReward,
            System.Action<GameObject, Vector3> instantiateReward
        )
        {
            GameObject prefab = chooseReward();
            if (prefab != null)
                instantiateReward(prefab, position);
        }
    }
}
