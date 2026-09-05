using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static List<EnemyBase> ExerciseEnemyCatalog(Vector3 playerPosition)
        {
            PoolManager pool = PoolManager.Instance;
            GameObject[] prefabs = Resources.LoadAll<GameObject>(
                "Brocoli/CursedDevolpmentStudioAss Assets/Waves"
            );
            var enemies = new List<EnemyBase>();
            for (int index = 0; index < prefabs.Length; index++)
            {
                EnemyBase prefab = prefabs[index].GetComponent<EnemyBase>();
                if (prefab == null)
                    continue;

                Vector3 position = playerPosition + new Vector3(0.3f + index * 0.03f, 0f, 0.3f);
                EnemyBase enemy = pool.GetEnemy(prefab, position, Quaternion.identity);
                Assert.That(enemy, Is.Not.Null, prefabs[index].name);
                ExerciseEnemyCombat(enemy);
                enemy.TakeDamage(1f, Vector2.right);
                enemy.ApplyKnockback(Vector2.right, 0.5f);
                enemy.MakeElite();
                if (enemy is HydraEnemyScript hydra)
                {
                    hydra.ConfigureForDungeonRing(index);
                    HydraEnemyScript.ExtraSplitGenerationsForRing(index);
                    HydraEnemyScript.RootScaleMultiplierForExtraSplits(index, 2);
                    HydraEnemyScript.ChildSpeedForScale(2f, 1f, 3f);
                }
                enemies.Add(enemy);
            }

            ExpGain experience = pool.GetExpGain(playerPosition + Vector3.right);
            if (experience != null)
                pool.ReturnExpGain(experience);

            foreach (
                string path in new[]
                {
                    "Brocoli/CursedDevolpmentStudioAss Assets/FireBall",
                    "Brocoli/CursedDevolpmentStudioAss Assets/FireBallBig",
                    "Brocoli/CursedDevolpmentStudioAss Assets/MiniCoronaProjectile",
                }
            )
            {
                GameObject asset = Resources.Load<GameObject>(path);
                EnemyProjectile prefab =
                    asset == null ? null : asset.GetComponent<EnemyProjectile>();
                if (prefab == null)
                    continue;
                EnemyProjectile projectile = pool.GetProjectile(
                    prefab,
                    playerPosition + Vector3.up,
                    Quaternion.identity
                );
                SetHierarchyField(projectile, "initialScale", Vector3.zero);
                projectile.Init(Vector2.right);
                SetHierarchyField(projectile, "spawnTime", Time.time - projectile.lifeTime);
                InvokeHierarchy(projectile, "Update");
                SetHierarchyField(projectile, "travelDirection", Vector2.zero);
                InvokeHierarchy(projectile, "FixedUpdate");
                pool.ReturnProjectile(projectile);
            }

            ExerciseEnemyProjectileWallSweep(playerPosition);
            ExerciseEnemyVariants(playerPosition, enemies);
            return enemies;
        }
    }
}
