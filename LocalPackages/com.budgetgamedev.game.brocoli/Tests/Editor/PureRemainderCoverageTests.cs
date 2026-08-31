using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class PureRemainderCoverageTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void MockInventoryAndInvalidDataCoverFallbackPolicies()
        {
            var source = new List<string> { "NEW ITEM" };
            string[] emptyGear = { null };
            Assert.That(ExplorationOverlay.SwapMockListItem(source, 0, emptyGear, 0), Is.True);
            Assert.That(source, Is.Empty);

            Assert.That(
                ExplorationOverlay.TryUnequipMockItem(
                    new[] { "ITEM" },
                    0,
                    null,
                    new List<string>(),
                    out InventoryPreviewLocation destination,
                    out int destinationIndex
                ),
                Is.False
            );
            Assert.That(destination, Is.EqualTo(InventoryPreviewLocation.Gear));
            Assert.That(destinationIndex, Is.EqualTo(-1));
            Assert.That(BrocoliSaveSystem.TryDeserialize("null", out _), Is.False);
            Assert.That(BrocoliSaveSystem.TryDeserialize("{}", _ => null, out _), Is.False);
            Assert.That(
                LevelUpScreen.ResolveHorizontalInput(true, false, Vector2.zero, Vector2.zero),
                Is.EqualTo(-1f)
            );
            Assert.That(
                LevelUpScreen.ResolveHorizontalInput(false, true, Vector2.zero, Vector2.zero),
                Is.EqualTo(1f)
            );
            Assert.That(
                LevelUpScreen.ResolveHorizontalInput(false, false, Vector2.left, Vector2.right),
                Is.EqualTo(-1f)
            );
            Assert.That(
                LevelUpScreen.ResolveHorizontalInput(false, false, Vector2.zero, Vector2.right),
                Is.EqualTo(1f)
            );

            var invalidStat = (UpgradeOption.StatType)int.MaxValue;
            var invalidOption = new UpgradeOption { Type = invalidStat, Amount = 3f };
            Assert.That(LevelUpAutoResolver.Score(invalidOption, default), Is.EqualTo(9f));
            MethodInfo generateBoost = typeof(ProceduralBoostAudio).GetMethod(
                "GenerateClip",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            ProceduralBoostAudio.PrewarmAll();
            Assert.That(
                generateBoost.Invoke(
                    null,
                    new object[] { (ProceduralBoostAudio.BoostSoundType)int.MaxValue }
                ),
                Is.Not.Null
            );
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void AudioDistanceAndWaveshapingCoverSilentAndNegativePaths()
        {
            GameObject player = new("Coverage distant audio player");
            GameObject host = new("Coverage distant melee audio");
            host.SetActive(false);
            try
            {
                player.transform.position = Vector3.right * 100f;
                SetStatic(typeof(ProceduralEnemyMeleeAudio), "playerTransform", player.transform);
                ProceduralEnemyMeleeAudio melee = host.AddComponent<ProceduralEnemyMeleeAudio>();
                Assert.That((float)Invoke(melee, "GetDistanceAttenuation"), Is.Zero);
                melee.PlayMeleeSound();
                melee.PlayMeleeSound(0.5f);

                ProceduralEnemyProjectileHitAudio projectile =
                    host.AddComponent<ProceduralEnemyProjectileHitAudio>();
                Assert.That((float)Invoke(projectile, "SoftClip", -2f), Is.LessThan(0f));
            }
            finally
            {
                SetStatic(typeof(ProceduralEnemyMeleeAudio), "playerTransform", null);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void BoostOccupancyAndTriggerGuardsCoverEmptyOutcomes()
        {
            GameObject host = new("Coverage boost remainder");
            GameObject other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject cameraObject = new("Coverage boost camera", typeof(Camera));
            host.SetActive(false);
            try
            {
                typeof(BoostBase)
                    .GetMethod("ResetPickupRegistry", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
                HealthBoost boost = host.AddComponent<HealthBoost>();
                Invoke(boost, "OnEnable");
                host.transform.position = Vector3.zero;
                cameraObject.transform.position = Vector3.back * 10f;
                Assert.That(
                    BoostBase.IsScreenAreaOccupied(Vector3.right * 100f, null, 1f),
                    Is.False
                );
                Assert.That(
                    BoostBase.IsScreenAreaOccupied(
                        Vector3.right * 10000f,
                        cameraObject.GetComponent<Camera>(),
                        1f
                    ),
                    Is.False
                );

                Invoke(boost, "OnTriggerEnter", other.GetComponent<Collider>());
                Set(boost, "_isCollected", true);
                Invoke(boost, "OnTriggerEnter", other.GetComponent<Collider>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SprayBoostsAndUnpreparedDeathVisualCoverSpecializedPlayerPaths()
        {
            GameObject player = new("Coverage specialized player paths");
            GameObject boosts = new("Coverage specialized boosts");
            player.SetActive(false);
            boosts.SetActive(false);
            try
            {
                PlayerStats stats = player.AddComponent<PlayerStats>();
                stats.ResetStats();
                stats.ApplyBoost(boosts.AddComponent<SprayRangeBoost>());
                stats.ApplyBoost(boosts.AddComponent<SprayWidthBoost>());

                PlayerDeathVisual death = player.AddComponent<PlayerDeathVisual>();
                IEnumerator routine = death.FallAndSettle(0.2f, 0f);
                Assert.That(routine.MoveNext(), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boosts);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void LifecycleCooldownAndFrameCaptureCoverDeferredRuntimeWork()
        {
            GameObject host = new("Coverage deferred runtime work");
            host.SetActive(false);
            try
            {
                GameContext context = host.AddComponent<GameContext>();
                SetStatic(typeof(GameContext), "_instance", context);
                Invoke(context, "OnDestroy");

                LevelUpAutoResolver resolver = host.AddComponent<LevelUpAutoResolver>();
                Set(resolver, "_cooldown", 1f);
                Invoke(resolver, "Update");

                FrameCapture capture = host.AddComponent<FrameCapture>();
                Set(capture, "_framesDir", "/tmp/coverage-frames");
                string captured = null;
                IEnumerator routine = capture.CaptureLoop(path => captured = path);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(captured, Does.EndWith("frame_00000.png"));
            }
            finally
            {
                SetStatic(typeof(GameContext), "_instance", null);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
                foreach (MethodInfo method in type.GetMethods(PrivateInstance))
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static void Set(object target, string name, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, PrivateInstance);
                if (field == null)
                    continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void SetStatic(System.Type type, string name, object value) =>
            type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
}
