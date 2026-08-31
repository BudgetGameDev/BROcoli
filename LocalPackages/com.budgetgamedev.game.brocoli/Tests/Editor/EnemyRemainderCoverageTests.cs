using System;
using System.Collections.Generic;
using System.Reflection;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class EnemyRemainderCoverageTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void SpatialHashLifecycleAndDeadEntriesCoverCleanupPaths()
        {
            SetStatic(typeof(EnemySpatialHash), "_instance", null);
            SetStatic(typeof(EnemySpatialHash), "_applicationIsQuitting", false);
            SetStatic(typeof(EnemySpatialHash), "_isSceneUnloading", false);
            GameObject host = new("Coverage spatial hash remainder");
            try
            {
                EnemySpatialHash hash = host.AddComponent<EnemySpatialHash>();
                Invoke(hash, "OnApplicationQuit");
                Assert.That(EnemySpatialHash.Instance, Is.Null);
                SetStatic(typeof(EnemySpatialHash), "_applicationIsQuitting", false);
                InvokeStatic(
                    typeof(EnemySpatialHash),
                    "OnSceneLoaded",
                    default(UnityEngine.SceneManagement.Scene),
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                );

                var grid = Get<Dictionary<long, List<EnemyBase>>>(hash, "_grid");
                grid[0] = new List<EnemyBase> { null };
                Assert.That(hash.GetNearbyEnemies(Vector2.zero, 1f), Is.Empty);
            }
            finally
            {
                SetStatic(typeof(EnemySpatialHash), "_applicationIsQuitting", false);
                SetStatic(typeof(EnemySpatialHash), "_isSceneUnloading", false);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EnemyColorAndEliteMaterialCoverMissingAndSharedRendererStates()
        {
            GameObject host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            host.SetActive(false);
            try
            {
                EnemyColorVariant colors = host.AddComponent<EnemyColorVariant>();
                Invoke(colors, "Awake");
                Set(colors, "variants", null);
                Invoke(colors, "OnEnable");
                colors.ApplyRandomVariant();
                Set(colors, "renderers", new Renderer[] { null });
                colors.Apply(new EnemyColorVariant.Variant(Color.red));

                Renderer renderer = host.GetComponent<Renderer>();
                var original = new MaterialPropertyBlock();
                var elite = new MaterialPropertyBlock();
                EliteEnemyEffects effects = host.AddComponent<EliteEnemyEffects>();
                Invoke(
                    effects,
                    "SetTintedColor",
                    renderer,
                    original,
                    elite,
                    Shader.PropertyToID("_BaseColor")
                );
                Assert.That(elite.HasColor(Shader.PropertyToID("_BaseColor")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExperienceDropCoversZeroDirectionAndPooledReturnPath()
        {
            GameObject enemyObject = new("Coverage enemy experience");
            enemyObject.SetActive(false);
            enemyObject.AddComponent<BoxCollider>();
            enemyObject.AddComponent<Rigidbody>();
            GameObject expObject = new("Coverage pooled experience");
            expObject.SetActive(false);
            try
            {
                EnemyScript enemy = enemyObject.AddComponent<EnemyScript>();
                expObject.AddComponent<BoxCollider>();
                expObject.AddComponent<Rigidbody>();
                ExpGain experience = expObject.AddComponent<ExpGain>();
                Set(experience, "_isPooled", true);
                Set(enemy, "expGainPrefab", experience);
                enemy.SpawnExpGain(Vector2.zero, 0.25f, _ => experience);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expObject);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void RendererHealthBarAndColliderSearchCoverSkippedCandidates()
        {
            GameObject enemyObject = new("Coverage rendererless enemy");
            enemyObject.SetActive(false);
            GameObject wrongBar = new("NotHealthBar", typeof(RectTransform), typeof(Bar));
            GameObject rightBar = new("HealthBar", typeof(RectTransform), typeof(Bar));
            wrongBar.transform.SetParent(enemyObject.transform, false);
            rightBar.transform.SetParent(enemyObject.transform, false);
            GameObject trigger = new("Coverage trigger", typeof(BoxCollider));
            trigger.transform.SetParent(enemyObject.transform, false);
            trigger.GetComponent<BoxCollider>().isTrigger = true;
            GameObject disabledMesh = new("Coverage disabled mesh", typeof(MeshRenderer));
            disabledMesh.transform.SetParent(enemyObject.transform, false);
            disabledMesh.GetComponent<MeshRenderer>().enabled = false;
            try
            {
                enemyObject.AddComponent<Rigidbody>();
                enemyObject.AddComponent<BoxCollider>();
                enemyObject.SetActive(true);
                EnemyScript enemy = enemyObject.AddComponent<EnemyScript>();
                enemyObject.SetActive(false);
                Invoke(enemy, "Awake");
                Set(enemy, "healthBar", null);
                Invoke(enemy, "DisableWorldHealthBar");
                Assert.That(Get<Bar>(enemy, "healthBar"), Is.SameAs(rightBar.GetComponent<Bar>()));
                enemyObject.GetComponent<BoxCollider>().isTrigger = true;
                Assert.That(
                    InvokeStatic(typeof(EnemyBase), "FindSolidCollider", enemy.transform),
                    Is.Null
                );
                enemy.player = null;
                Invoke(enemy, "PerformMeleeAttack");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void HydraFallbackAudioAndUnpooledDeathUseInjectedRuntimeBoundaries()
        {
            GameObject hydraObject = new("Coverage hydra remainder");
            hydraObject.SetActive(false);
            GameObject fallback = new("Coverage non-hydra child");
            fallback.SetActive(false);
            try
            {
                hydraObject.AddComponent<BoxCollider>();
                hydraObject.AddComponent<Rigidbody>();
                HydraEnemyScript hydra = hydraObject.AddComponent<HydraEnemyScript>();
                hydra.SpawnChildren(_ => null, _ => fallback);
                GameObject instantiatedChild = hydra.InstantiateChild(Vector3.one);
                UnityEngine.Object.DestroyImmediate(instantiatedChild);

                ProceduralEnemyMeleeAudio audio =
                    hydraObject.AddComponent<ProceduralEnemyMeleeAudio>();
                Invoke(audio, "Awake");
                Set(hydra, "meleeAudio", audio);
                hydraObject.SetActive(true);
                hydra.PlaySuccessfulMeleeAudio();
                hydraObject.SetActive(false);

                hydra.SetPooled(true);
                bool destroyed = false;
                hydra.CompleteDeath(null, _ => destroyed = true);
                Assert.That(destroyed, Is.True);
                hydra.SetPooled(false);
                destroyed = false;
                hydra.CompleteDeath(null, _ => destroyed = true);
                Assert.That(destroyed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fallback);
                UnityEngine.Object.DestroyImmediate(hydraObject);
            }
        }

        [Test]
        public void DungeonEnemyPlacementAndProjectileReturnCoverFallbackAndPoolPaths()
        {
            GameObject enemyObject = new("Coverage transform-aligned enemy");
            enemyObject.SetActive(false);
            GameObject reward = new("Coverage elite reward");
            GameObject projectileObject = new("Coverage projectile prefab");
            projectileObject.SetActive(false);
            try
            {
                enemyObject.AddComponent<Rigidbody>();
                enemyObject.AddComponent<BoxCollider>();
                EnemyScript enemy = enemyObject.AddComponent<EnemyScript>();
                enemy.rb = null;
                Vector3 aligned = new(3f, 0f, 4f);
                DungeonEnemyPlacer.AlignEnemy(enemy, aligned);
                Assert.That(enemy.transform.position, Is.EqualTo(aligned));

                bool instantiated = false;
                DungeonEnemyPlacer.DropEliteReward(
                    Vector3.one,
                    () => reward,
                    (prefab, position) => instantiated = prefab == reward && position == Vector3.one
                );
                Assert.That(instantiated, Is.True);

                var dormant = new List<EnemyBase> { enemy };
                bool despawned = false;
                DungeonEnemyPlacer.Despawn(dormant, null, _ => despawned = true);
                Assert.That(despawned, Is.True);

                projectileObject.AddComponent<Rigidbody>();
                projectileObject.AddComponent<BoxCollider>();
                EnemyProjectile prefab = projectileObject.AddComponent<EnemyProjectile>();
                PoolManager pool = PoolManager.Instance;
                EnemyProjectile projectile = pool.GetProjectile(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity
                );
                Assert.That(projectile, Is.Not.Null);
                pool.ReturnProjectile(projectile);
                pool.ClearAll();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectileObject);
                UnityEngine.Object.DestroyImmediate(reward);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
                foreach (MethodInfo method in type.GetMethods(Hidden))
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static object InvokeStatic(Type type, string name, params object[] arguments) =>
            type.GetMethod(name, Hidden).Invoke(null, arguments);

        private static void Set(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Hidden);
                if (field == null)
                    continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static T Get<T>(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Hidden);
                if (field != null)
                    return (T)field.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void SetStatic(Type type, string name, object value) =>
            type.GetField(name, Hidden).SetValue(null, value);
    }
}
