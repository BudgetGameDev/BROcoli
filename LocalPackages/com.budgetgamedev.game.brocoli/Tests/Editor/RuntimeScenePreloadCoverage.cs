using System.Collections;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExercisePreloaderAndPools()
        {
            GamePreloader preloader = Object.FindAnyObjectByType<GamePreloader>();
            Assert.That(preloader, Is.Not.Null);
            SetHierarchyField(preloader, "logPreloadSteps", true);
            InvokeHierarchy(preloader, "WarmupShaders");
            InvokeHierarchy(preloader, "WarmupAudio");
            InvokeHierarchy(preloader, "WarmupPhysics");
            Drain((IEnumerator)InvokeHierarchy(preloader, "WarmupCoroutinesAndAudio"));
            Drain((IEnumerator)InvokeHierarchy(preloader, "WaitFramesRealtime", 2));
            InvokeHierarchy(preloader, "CollectPoolPrefabs");

            var screen = new LoadingScreenUI(
                preloader.transform,
                Color.black,
                Color.gray,
                Color.green,
                "Coverage"
            );
            SetHierarchyField(preloader, "_loadingScreen", screen);
            Drain((IEnumerator)InvokeHierarchy(preloader, "PrewarmPrefabs", 0f, 1f));
            screen.Destroy();

            var routineScreen = new LoadingScreenUI(
                preloader.transform,
                Color.black,
                Color.gray,
                Color.green,
                "Routine Coverage"
            );
            SetHierarchyField(preloader, "_loadingScreen", routineScreen);
            SetHierarchyField(preloader, "warmupShaders", true);
            SetHierarchyField(preloader, "prewarmMaterials", true);
            SetHierarchyField(preloader, "prewarmPhysics", true);
            SetHierarchyField(preloader, "prewarmAudio", true);
            SetHierarchyField(preloader, "prewarmPrefabs", true);
            SetHierarchyField(preloader, "prewarmPools", true);
            Drain((IEnumerator)InvokeHierarchy(preloader, "PreloadRoutine"));
            GamePreloader.ResetPreloadFlag();

            GameObject repeatObject = new("Coverage Repeat Preloader");
            repeatObject.SetActive(false);
            GamePreloader repeat = repeatObject.AddComponent<GamePreloader>();
            SetHierarchyField(repeat, "_hasPreloaded", true);
            SetHierarchyField(repeat, "prewarmPools", true);
            SetHierarchyField(repeat, "logPreloadSteps", true);
            InvokeHierarchy(repeat, "Awake");
            GamePreloader.ResetPreloadFlag();

            PoolManager pool = PoolManager.Instance;
            pool.ClearAll();
            var enemyPrefabs = GetHierarchyField<System.Collections.Generic.List<GameObject>>(
                preloader,
                "_enemyPrefabs"
            );
            var projectilePrefabs = GetHierarchyField<System.Collections.Generic.List<GameObject>>(
                preloader,
                "_projectilePrefabs"
            );
            ExpGain collectedExp = GetHierarchyField<ExpGain>(preloader, "_expGainPrefab");
            GameObject[] enemyArguments = new GameObject[enemyPrefabs.Count + 1];
            enemyPrefabs.CopyTo(enemyArguments, 1);
            GameObject[] projectileArguments = new GameObject[projectilePrefabs.Count + 1];
            projectilePrefabs.CopyTo(projectileArguments, 1);
            pool.PreWarmAll(enemyArguments, collectedExp, projectileArguments);
            pool.PreWarmAll(enemyArguments, collectedExp, projectileArguments);

            ExpGain pooledExp = pool.GetExpGain(Vector3.zero);
            if (pooledExp != null)
                pool.ReturnExpGain(pooledExp);
            Assert.That(pool.GetEnemy(null, Vector3.zero, Quaternion.identity), Is.Null);
            pool.ReturnEnemy(null);
            Assert.That(pool.GetProjectile(null, Vector3.zero, Quaternion.identity), Is.Null);
            pool.ReturnProjectile(null);

            object expPool = GetHierarchyField<object>(pool, "_expGainPool");
            SetHierarchyField(pool, "_expGainPool", null);
            LogAssert.Expect(LogType.Warning, "[PoolManager] ExpGain pool not initialized");
            Assert.That(pool.GetExpGain(Vector3.zero), Is.Null);
            pool.ReturnExpGain(null);
            SetHierarchyField(pool, "_expGainPool", expPool);

            GameObject[] enemies = Resources.LoadAll<GameObject>(
                "Brocoli/CursedDevolpmentStudioAss Assets/Waves"
            );
            GameObject projectileAsset = Resources.Load<GameObject>(
                "Brocoli/CursedDevolpmentStudioAss Assets/FireBall"
            );
            if (enemies.Length > 0)
            {
                EnemyBase enemy = Object.Instantiate(enemies[0]).GetComponent<EnemyBase>();
                InvokeHierarchy(pool, "OnEnemyGet", enemy);
                InvokeHierarchy(pool, "OnEnemyReturn", enemy);
                pool.ReturnEnemy(enemy);
            }
            if (projectileAsset != null)
            {
                EnemyProjectile pooledProjectile = pool.GetProjectile(
                    projectileAsset.GetComponent<EnemyProjectile>(),
                    Vector3.zero,
                    Quaternion.identity
                );
                if (pooledProjectile != null)
                    pool.ReturnProjectile(pooledProjectile);
                EnemyProjectile projectile = Object
                    .Instantiate(projectileAsset)
                    .GetComponent<EnemyProjectile>();
                InvokeHierarchy(pool, "OnProjectileGet", projectile);
                InvokeHierarchy(pool, "OnProjectileReturn", projectile);
                pool.ReturnProjectile(projectile);
            }

            ExpGain expPrefab = Resources
                .LoadAll<GameObject>("Brocoli/CursedDevolpmentStudioAss Assets")
                .SelectExpGain();
            if (expPrefab != null)
            {
                ExpGain exp = Object.Instantiate(expPrefab);
                InvokeHierarchy(pool, "OnExpGainGet", exp);
                InvokeHierarchy(pool, "OnExpGainReturn", exp);
                Object.Destroy(exp.gameObject);
            }
            pool.ClearAll();
            GameObject duplicateObject = new("Coverage Duplicate Pool Manager");
            PoolManager duplicate = duplicateObject.AddComponent<PoolManager>();
            InvokeHierarchy(duplicate, "Awake");
            PoolManager.ResetInstance();
        }

        private static void Drain(IEnumerator routine)
        {
            int moves = 0;
            while (routine != null && routine.MoveNext() && moves++ < 1000)
            {
                if (
                    routine.Current is IEnumerator nested
                    && routine.Current is not YieldInstruction
                    && routine.Current is not CustomYieldInstruction
                )
                    Drain(nested);
            }
            Assert.That(moves, Is.LessThan(1000));
        }
    }

    internal static class RuntimeSceneResourceExtensions
    {
        internal static ExpGain SelectExpGain(this GameObject[] assets)
        {
            foreach (GameObject asset in assets)
            {
                ExpGain exp = asset.GetComponent<ExpGain>();
                if (exp != null)
                    return exp;
            }
            return null;
        }
    }
}
