using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseBoostCatalog(PlayerStats stats)
        {
            GameObject[] assets = Resources.LoadAll<GameObject>(
                "Brocoli/CursedDevolpmentStudioAss Assets"
            );
            var boosts = new System.Collections.Generic.List<BoostBase>();
            foreach (GameObject asset in assets)
            {
                if (asset.GetComponent<BoostBase>() == null)
                    continue;
                GameObject clone = UnityEngine.Object.Instantiate(
                    asset,
                    stats.transform.position + Vector3.right,
                    Quaternion.identity
                );
                BoostBase boost = clone.GetComponent<BoostBase>();
                Assert.That(boost.Amount, Is.GreaterThan(0f));
                Assert.That(boost.DropWeight, Is.GreaterThan(0f));
                Assert.That(boost.Duration, Is.GreaterThanOrEqualTo(0f));
                boost.Apply(stats);
                stats.ApplyBoost(boost);
                boosts.Add(boost);
            }

            if (boosts.Count > 0)
            {
                Assert.That(
                    BoostBase.IsScreenAreaOccupied(stats.transform.position, null, 10f),
                    Is.True
                );
                Camera camera = Camera.main;
                Assert.That(
                    BoostBase.IsScreenAreaOccupied(stats.transform.position, camera, 10f),
                    Is.True
                );
                InvokeHierarchy(boosts[0], "Start");
            }
            foreach (BoostBase boost in boosts)
                UnityEngine.Object.Destroy(boost.gameObject);

            ExerciseBoostHandler(stats.transform);
        }

        private static void ExerciseBoostHandler(Transform player)
        {
            GameObject root = new("Coverage Boost Handler");
            BoostHandler handler = root.AddComponent<BoostHandler>();
            SetHierarchyField(handler, "_boosters", Array.Empty<GameObject>());
            handler.SpawnBoosterAt(Vector2.zero);

            GameObject prefab = new("Coverage Booster Prefab");
            SetHierarchyField(handler, "_boosters", new[] { prefab });
            handler.SpawnBoosterAt(Vector2.one);
            Camera camera = Camera.main;
            SetHierarchyField(handler, "_mainCamera", camera);
            SetHierarchyField(handler, "_player", player);
            for (int seed = 0; seed < 100; seed++)
            {
                UnityEngine.Random.InitState(seed);
                InvokeHierarchy(handler, "GetOffscreenPosition");
            }
            UnityEngine.Object.Destroy(prefab);
            UnityEngine.Object.Destroy(root);
        }

        private static void ExercisePlayerStats(PlayerStats stats)
        {
            stats.ApplyTemporaryBoost(TemporaryBoostType.MovementSpeed, 0.2f, 1f);
            stats.ApplyTemporaryBoost(TemporaryBoostType.TimeSlow, 0.5f, 1f);
            stats.ApplyTemporaryBoost(TemporaryBoostType.TimeSlow, 0.25f, 2f);
            Assert.That(stats.HasMagnetActive, Is.True);
            Assert.That(stats.MagnetRadius, Is.GreaterThan(0f));

            SetHierarchyField(stats, "_currentHealth", 1f);
            SetHierarchyField(stats, "_currentHealthRegen", 2f);
            SetHierarchyField(stats, "_regenTimer", 1f);
            InvokeHierarchy(stats, "Update");
            stats.ApplyTemporaryBoost(TemporaryBoostType.Damage, 1f, 0f);
            InvokeHierarchy(stats, "UpdateTemporaryBoosts");

            SetHierarchyField(stats, "_currentDodgeChance", 100f);
            float beforeDodge = stats.CurrentHealth;
            stats.ApplyDamage(100f);
            Assert.That(stats.CurrentHealth, Is.EqualTo(beforeDodge));
            SetHierarchyField(stats, "_currentCritChance", 100f);
            Assert.That(stats.CalculateDamageOutput(10f, out bool crit), Is.GreaterThan(10f));
            Assert.That(crit, Is.True);
            stats.ApplyExperience(0f);

            IList activeBoosts = (IList)GetHierarchyField<object>(stats, "_activeBoosts");
            Type activeBoostType = activeBoosts.GetType().GetGenericArguments()[0];
            object expiredActiveBoost = Activator.CreateInstance(activeBoostType);
            activeBoostType.GetField("remainingTime").SetValue(expiredActiveBoost, 0f);
            activeBoosts.Add(expiredActiveBoost);
            object save = InvokeHierarchy(stats, "CaptureRunState");
            InvokeHierarchy(stats, "RestoreRunState", new object[] { null });
            AddInvalidSavedBoosts(save);
            InvokeHierarchy(stats, "RestoreRunState", save);
            save.GetType().GetField("levelUpChoicePending").SetValue(save, true);
            InvokeHierarchy(stats, "RestoreRunState", save);
            stats.StopAllCoroutines();
            GamePreloader preloader = UnityEngine.Object.FindAnyObjectByType<GamePreloader>();
            preloader?.gameObject.SetActive(false);
            Drain((IEnumerator)InvokeHierarchy(stats, "RestorePendingLevelUpChoice"));
            preloader?.gameObject.SetActive(true);
            UnityEngine.Object.FindAnyObjectByType<LevelUpScreen>()?.Hide();
            Assert.That(stats.CurrentHealth, Is.GreaterThan(0f));
            stats.AddDodgeChance(-100f);
            stats.AddArmor(-100f);

            InvokeHierarchy(stats, "ResetGlobalEffectState");
            InvokeHierarchy(stats, "RegisterPickupTarget");
            ExercisePickupAttraction(stats);
            InvokeHierarchy(stats, "DiscoverUIComponents");
            Assert.That(PlayerStats.Resolve(), Is.SameAs(stats));
            SetStaticField(typeof(PlayerStats), "<ActivePlayerTarget>k__BackingField", null);
            Assert.That(PlayerStats.Resolve(), Is.SameAs(stats));
            SetStaticField(
                typeof(PlayerStats),
                "<ActivePlayerTarget>k__BackingField",
                stats.transform
            );

            GameObject duplicateObject = new("Coverage Duplicate Stats");
            duplicateObject.transform.SetParent(stats.transform, false);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("^PlayerStats: '.*' carries 2 PlayerStats")
            );
            duplicateObject.AddComponent<PlayerStats>();
            UnityEngine.Object.Destroy(duplicateObject);
        }

        private static void ExercisePickupAttraction(PlayerStats stats)
        {
            GameObject pickup = new("Coverage Pickup Attraction");
            Rigidbody body = pickup.AddComponent<Rigidbody>();
            body.useGravity = false;
            float speed = 0f;
            bool locked = false;
            PickupAttraction.Reset(body, ref speed, ref locked, null);

            pickup.transform.position = stats.transform.position;
            PickupAttraction.UpdateMotion(body, ref speed, ref locked, null);
            locked = false;
            pickup.transform.position = stats.transform.position + Vector3.right;
            PickupAttraction.UpdateMotion(body, ref speed, ref locked, null);
            Assert.That(locked, Is.True);
            UnityEngine.Object.Destroy(pickup);
        }

        private static void AddInvalidSavedBoosts(object save)
        {
            FieldInfo boostsField = save.GetType().GetField("temporaryBoosts");
            IList boosts = (IList)boostsField.GetValue(save);
            Type boostType = boosts.GetType().GetGenericArguments()[0];
            object expired = Activator.CreateInstance(boostType);
            boostType.GetField("remainingTime").SetValue(expired, -1f);
            boosts.Add(null);
            boosts.Add(expired);
            save.GetType().GetField("levelUpChoicePending").SetValue(save, false);
        }
    }
}
