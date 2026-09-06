using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class PromotionRemainderCoverageTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [TearDown]
        public void ResetStatics()
        {
            AutoplayCaptureTriggers.Reset();
            SetStatic(typeof(PlayerStats), "<ActiveMagnetTarget>k__BackingField", null);
            SetStatic(typeof(PlayerStats), "<ActivePlayerTarget>k__BackingField", null);
        }

        [Test]
        public void CaptureManifestAndSummaryCoverMultipleAndCompleteRequests()
        {
            AutoplayCaptureTriggers.Arm(new[] { "first*", "second" });
            AutoplayCaptureTriggers.Notify("first", 1);
            AutoplayCaptureTriggers.Notify("first", 2);
            while (
                AutoplayCaptureTriggers.TryTakeReady(out AutoplayCaptureTriggers.Request request)
            )
                AutoplayCaptureTriggers.Record(
                    request,
                    request.Event + request.Occurrence + ".png"
                );

            Assert.That(AutoplayCaptureTriggers.ToJson(), Does.Contain("},{"));
            Assert.That(DescribeCaptures(), Does.Contain("second"));

            AutoplayCaptureTriggers.Notify("second", 1);
            Assert.That(
                AutoplayCaptureTriggers.TryTakeReady(out AutoplayCaptureTriggers.Request final),
                Is.True
            );
            AutoplayCaptureTriggers.Record(final, "second.png");
            Assert.That(DescribeCaptures(), Does.Contain("Every requested"));
        }

        [Test]
        public void EncirclementAffectsAttackAndRetreatScoresAndClosesAFullRing()
        {
            var situation = new BotSituation(
                true,
                5f,
                1,
                1f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                false,
                0.75f
            );
            var tuning = new BotTuning(2.5f, 5, 0.4f, 14f, 16f);
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Engage, situation, tuning),
                Is.GreaterThan(float.NegativeInfinity)
            );
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Retreat, situation, tuning),
                Is.GreaterThan(30f)
            );

            var threats = new List<Vector2>();
            for (int sector = 0; sector < BotEncirclement.Sectors; sector++)
            {
                float angle = sector * Mathf.PI * 2f / BotEncirclement.Sectors;
                threats.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
            BotEncirclement.Measure(
                Vector2.zero,
                threats,
                3f,
                out float coverage,
                out Vector2 escape
            );
            Assert.That(coverage, Is.EqualTo(1f));
            Assert.That(escape, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void BotExplorationCanUnwedgeAndStageTowardRoomCentre()
        {
            GameObject host = new("Promotion bot exploration");
            host.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                Invoke(bot, "Awake");
                Set(bot, "player", host.transform);
                host.AddComponent<CapsuleCollider>();
                Set(bot, "movement", host.AddComponent<PlayerMovement>());
                Set(bot, "lastProgress", Time.time);
                Vector2 centre = DungeonLayout.RoomCenter(Vector2Int.zero);
                Vector2 away = centre + Vector2.right * 4f;

                Set(bot, "unwedgeUntil", float.PositiveInfinity);
                Assert.That((Vector2)Invoke(bot, "GetExplorationTarget", away), Is.EqualTo(centre));

                Set(bot, "unwedgeUntil", 0f);
                Set(bot, "stagingRoom", Vector2Int.zero);
                Set(bot, "stagingDeadline", float.PositiveInfinity);
                Assert.That((Vector2)Invoke(bot, "GetExplorationTarget", away), Is.EqualTo(centre));

                var enemies = new BotDriver.EnemyObservation(
                    3,
                    3,
                    1f,
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.left,
                    0.75f,
                    Vector2.right
                );
                Invoke(bot, "NavigateCombat", Vector2.zero, enemies, true);

                Set(bot, "lastEscape", Vector2.right);
                Set(bot, "recoveriesBeforeAbandoning", 1);
                Invoke(bot, "BeginStuckRecovery");
                Set(bot, "lastEscape", Vector2.zero);
                Set(bot, "recoveriesSinceProgress", 0);
                Invoke(bot, "BeginStuckRecovery");

                GameObject dungeonRoot = new("Promotion dungeon");
                try
                {
                    DungeonManager dungeon = dungeonRoot.AddComponent<DungeonManager>();
                    Set(dungeon, "layout", new DungeonLayout(123));
                    Set(bot, "dungeon", dungeon);
                    Invoke(bot, "PickExplorationRoom", Vector2Int.zero);
                    bot.ApplyExplorationDirection(Vector2Int.zero, 0);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dungeonRoot);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ArchetypePacingCoversShootersAndChunkyEnemies()
        {
            Assert.That(EnemyScaling.ArchetypePace("ShootingHard"), Is.EqualTo(0.8f));
            Assert.That(EnemyScaling.ArchetypePace("EnemyEasyChunky"), Is.EqualTo(0.85f));
            Assert.That(
                ResponsivePauseMenuLayout.ResolvePauseGamepadAxis(Vector2.up, 0f, 0f),
                Is.EqualTo(Vector2.up)
            );
        }

        [Test]
        public void CoincidentPickupStopsAndGlowSkipsMissingRenderers()
        {
            GameObject target = new("Promotion magnet target");
            GameObject pickup = new("Promotion pickup", typeof(Rigidbody));
            GameObject rendererHost = new("Promotion glow renderer", typeof(MeshRenderer));
            try
            {
                SetStatic(
                    typeof(PlayerStats),
                    "<ActiveMagnetTarget>k__BackingField",
                    target.transform
                );
                Rigidbody body = pickup.GetComponent<Rigidbody>();
                float speed = 10f;
                bool locked = false;
                PickupAttraction.UpdateMotion(body, ref speed, ref locked, null);
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));

                pickup.SetActive(false);
                XpGlowPresentation glow = pickup.AddComponent<XpGlowPresentation>();
                Invoke(glow, "Awake");
                Set(glow, "glowRenderers", null);
                Invoke(glow, "LateUpdate");

                MeshRenderer destroyed = rendererHost.GetComponent<MeshRenderer>();
                UnityEngine.Object.DestroyImmediate(rendererHost);
                rendererHost = null;
                Set(glow, "glowRenderers", new[] { destroyed });
                Set(glow, "appliedIntensity", 0f);
                Invoke(glow, "LateUpdate");

                MeshRenderer valid = pickup.AddComponent<MeshRenderer>();
                Set(glow, "glowRenderers", new[] { valid });
                Set(glow, "appliedIntensity", 2f);
                Invoke(glow, "LateUpdate");
                Assert.That(
                    PickupVisual3D.GetGlowMaterial(
                        (PickupVisual3D.GlowShell)999,
                        _ => null,
                        _ => null
                    ),
                    Is.Null
                );
                XpGlowPresentation.ApplyShellColors(null, PickupVisual3D.GlowShell.Core, false);
            }
            finally
            {
                if (rendererHost != null)
                    UnityEngine.Object.DestroyImmediate(rendererHost);
                UnityEngine.Object.DestroyImmediate(pickup);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ProjectileHitAudioCoversCachedAndFallbackPlayback()
        {
            GameObject host = new("Promotion projectile hit audio");
            try
            {
                var audio = host.AddComponent<ProceduralEnemyProjectileHitAudio>();
                Invoke(audio, "Awake");
                typeof(ProceduralEnemyProjectileHitAudio)
                    .GetField("isPrewarmed", Hidden)
                    .SetValue(null, false);
                ProceduralEnemyProjectileHitAudio.PrewarmAll();
                audio.PlayHitSound(
                    ProceduralEnemyProjectileHitAudio.EnemyHitSoundType.PlasmaImpact
                );

                var clips = (System.Collections.IDictionary)
                    typeof(ProceduralEnemyProjectileHitAudio)
                        .GetField("cachedClips", Hidden)
                        .GetValue(null);
                clips.Remove(ProceduralEnemyProjectileHitAudio.EnemyHitSoundType.PlasmaImpact);
                ProceduralEnemyProjectileHitAudio.InitializeFallbackForTests = fallback =>
                    Invoke(fallback, "Awake");
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "^EnemyProjectileHitSound: Destroy may not be called from edit mode"
                    )
                );
                ProceduralEnemyProjectileHitAudio.PlayHit(
                    Vector3.zero,
                    ProceduralEnemyProjectileHitAudio.EnemyHitSoundType.PlasmaImpact
                );
            }
            finally
            {
                ProceduralEnemyProjectileHitAudio.InitializeFallbackForTests = null;
                GameObject temporary = GameObject.Find("EnemyProjectileHitSound");
                if (temporary != null)
                    UnityEngine.Object.DestroyImmediate(temporary);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
