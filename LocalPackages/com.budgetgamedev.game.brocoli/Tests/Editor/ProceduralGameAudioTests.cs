using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Exercises every procedural clip family through the same pre-warm entry
    /// points used by the loading screen. Generating the complete preset catalog
    /// catches invalid sample maths before a first shot or pickup reaches a player.
    /// </summary>
    public sealed class ProceduralGameAudioTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<AudioClip> instanceClips = new();

        [TearDown]
        public void DestroyInstanceClips()
        {
            foreach (AudioClip clip in instanceClips)
            {
                if (clip != null)
                    UnityEngine.Object.DestroyImmediate(clip);
            }

            instanceClips.Clear();
        }

        [Test]
        public void EveryProceduralAudioCatalogPrewarmsWithoutInvalidSamples()
        {
            LogAssert.Expect(
                LogType.Log,
                "[ProceduralEnemyWalkAudio] Pre-warmed all walk sound types"
            );
            Assert.DoesNotThrow(() =>
            {
                ProceduralEnemyGunAudio.PrewarmAll();
                ProceduralEnemyMeleeAudio.PrewarmAll();
                ProceduralEnemyProjectileHitAudio.PrewarmAll();
                ProceduralEnemyWalkAudio.PrewarmAll();
                ProceduralFootstepAudio.PrewarmAll();
                ProceduralGunAudio.PrewarmAll();
                ProceduralLevelUpAudio.PrewarmAll();
                ProceduralProjectileHitAudio.PrewarmAll();
                ProceduralXPPickupAudio.PrewarmAll();
                ProceduralBoostAudio.PrewarmAll();
            });

            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
            Assert.That(clips, Is.Not.Empty);

            foreach (AudioClip clip in clips)
            {
                if (!IsGeneratedGameClip(clip.name))
                    continue;

                Assert.That(clip.samples, Is.Positive, clip.name);
                Assert.That(clip.channels, Is.EqualTo(1), clip.name);

                var samples = new float[clip.samples];
                Assert.That(clip.GetData(samples, 0), Is.True, clip.name);
                foreach (float sample in samples)
                {
                    Assert.That(float.IsNaN(sample), Is.False, clip.name);
                    Assert.That(float.IsInfinity(sample), Is.False, clip.name);
                    Assert.That(Mathf.Abs(sample), Is.LessThanOrEqualTo(1.001f), clip.name);
                }
            }
        }

        [Test]
        public void EveryComponentGeneratorBuildsEverySerializedPreset()
        {
            GenerateEveryPreset<ProceduralEnemyGunAudio, ProceduralEnemyGunAudio.EnemyGunSoundType>(
                "GetPreset",
                "currentPreset",
                "GenerateGunClip"
            );
            GenerateEveryPreset<
                ProceduralEnemyMeleeAudio,
                ProceduralEnemyMeleeAudio.MeleeSoundType
            >("GetPreset", "currentPreset", "GenerateMeleeClip");
            GenerateEveryPreset<
                ProceduralEnemyWalkAudio,
                ProceduralEnemyWalkAudio.EnemyWalkSoundType
            >("GetPreset", "currentPreset", "GenerateStepClip");
            GenerateEveryPreset<ProceduralGunAudio, ProceduralGunAudio.GunSoundType>(
                "GetPreset",
                "currentPreset",
                "GenerateGunClip"
            );
            GenerateEveryPreset<
                ProceduralEnemyProjectileHitAudio,
                ProceduralEnemyProjectileHitAudio.EnemyHitSoundType
            >("GetPreset", null, "GenerateHitClip");
            GenerateEveryPreset<
                ProceduralProjectileHitAudio,
                ProceduralProjectileHitAudio.HitSoundType
            >("GetPreset", null, "GenerateHitClip");

            GenerateSingle<ProceduralFootstepAudio>("GenerateFootstepClip");
            GenerateSingle<ProceduralLevelUpAudio>("GenerateLevelUpClip");

            Assert.That(instanceClips, Is.Not.Empty);
            foreach (AudioClip clip in instanceClips)
            {
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.samples, Is.Positive, clip.name);
                Assert.That(clip.channels, Is.EqualTo(1), clip.name);
            }
        }

        [Test]
        public void EnemyGunPlaybackCoversUncachedAndOutOfRangeShots()
        {
            GameObject player = new("Coverage distant player");
            GameObject host = new("Coverage enemy gun");
            try
            {
                ProceduralEnemyGunAudio component = host.AddComponent<ProceduralEnemyGunAudio>();
                SetStaticField(typeof(ProceduralEnemyGunAudio), "cachedClips", null);
                SetStaticField(typeof(ProceduralEnemyGunAudio), "isPrewarmed", false);
                SetStaticField(
                    typeof(ProceduralEnemyGunAudio),
                    "playerTransform",
                    player.transform
                );
                Invoke(component, "Awake");

                player.transform.position = Vector3.right * 100f;
                component.PlayGunSound();
                component.PlayGunSound(0.5f);
                component.PlayGunSound(ProceduralEnemyGunAudio.EnemyGunSoundType.Sneeze);
                component.PlayGunSound(ProceduralEnemyGunAudio.EnemyGunSoundType.VoidCannon, 0.5f);

                player.transform.position = host.transform.position;
                component.PlayGunSound();
                component.PlayGunSound(0.5f);
                component.PlayGunSound(ProceduralEnemyGunAudio.EnemyGunSoundType.Sneeze);
                component.PlayGunSound(ProceduralEnemyGunAudio.EnemyGunSoundType.VoidCannon, 0.5f);
            }
            finally
            {
                SetStaticField(typeof(ProceduralEnemyGunAudio), "playerTransform", null);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static bool IsGeneratedGameClip(string name) =>
            name.StartsWith("Enemy", StringComparison.Ordinal)
            || name.StartsWith("GunShot_", StringComparison.Ordinal)
            || name.StartsWith("Melee_", StringComparison.Ordinal)
            || name.StartsWith("Footstep_", StringComparison.Ordinal)
            || name.StartsWith("Boost", StringComparison.Ordinal)
            || name.StartsWith("LevelUp", StringComparison.Ordinal)
            || name.StartsWith("Projectile", StringComparison.Ordinal)
            || name.StartsWith("XP", StringComparison.Ordinal);

        private void GenerateEveryPreset<TComponent, TPreset>(
            string presetMethod,
            string presetField,
            string generateMethod
        )
            where TComponent : MonoBehaviour
            where TPreset : Enum
        {
            GameObject host = new(typeof(TComponent).Name + "Tests");
            try
            {
                TComponent component = host.AddComponent<TComponent>();
                Invoke(component, "Awake");

                foreach (TPreset type in Enum.GetValues(typeof(TPreset)))
                {
                    object preset = Invoke(component, presetMethod, type);
                    if (presetField != null)
                        SetField(component, presetField, preset);

                    instanceClips.Add(
                        (AudioClip)Invoke(
                            component,
                            generateMethod,
                            presetField == null ? new[] { preset } : Array.Empty<object>()
                        )
                    );
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private void GenerateSingle<TComponent>(string generateMethod)
            where TComponent : MonoBehaviour
        {
            GameObject host = new(typeof(TComponent).Name + "Tests");
            try
            {
                TComponent component = host.AddComponent<TComponent>();
                Invoke(component, "Awake");
                instanceClips.Add((AudioClip)Invoke(component, generateMethod));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo found = target.GetType().GetMethod(method, InstanceMembers);
            Assert.That(found, Is.Not.Null, $"{target.GetType().Name}.{method}");
            return found.Invoke(target, arguments);
        }

        private static void SetField(object target, string field, object value)
        {
            FieldInfo found = target.GetType().GetField(field, InstanceMembers);
            Assert.That(found, Is.Not.Null, $"{target.GetType().Name}.{field}");
            found.SetValue(target, value);
        }

        private static void SetStaticField(Type type, string field, object value)
        {
            type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, value);
        }
    }
}
