using System;
using System.Collections;
using System.Reflection;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class PresentationRegressionTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void HudStylesGameplayBarsEvenWhenAnotherCanvasRanksHigher()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var overlay = new GameObject("Canvas", typeof(Canvas));
            overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var host = new GameObject("Gameplay");
            var canvasHost = new GameObject("Gameplay UI", typeof(Canvas));
            canvasHost.transform.SetParent(host.transform);
            canvasHost.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var health = new GameObject(
                "HealthBar",
                typeof(RectTransform),
                typeof(Slider),
                typeof(Bar)
            );
            var xp = new GameObject(
                "ExperienceBar",
                typeof(RectTransform),
                typeof(Slider),
                typeof(Bar)
            );
            health.transform.SetParent(canvasHost.transform, false);
            xp.transform.SetParent(canvasHost.transform, false);
            try
            {
                Assert.That(ScreenCanvasLocator.Find(), Is.EqualTo(overlay.GetComponent<Canvas>()));
                var hud = DiabloHud.EnsurePresent();
                Assert.That(hud.gameObject, Is.EqualTo(canvasHost));
                // Ordinary MonoBehaviours do not receive Awake in edit mode.
                typeof(DiabloHud).GetMethod("Awake", Hidden).Invoke(hud, null);
                Assert.That(health.transform.parent.name, Is.EqualTo("DiabloHudSafeArea"));
                Assert.That(xp.transform.parent, Is.EqualTo(health.transform.parent));
                Assert.That(((RectTransform)health.transform).anchorMin.y, Is.Zero);
                Assert.That(DiabloHud.EnsurePresent(), Is.SameAs(hud));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(overlay);
            }
        }

        [Test]
        public void SerializedLegacySprayCannotReplaceLayeredEffect()
        {
            var host = new GameObject("Spray regression");
            var legacyHost = new GameObject("Authored legacy particles", typeof(ParticleSystem));
            legacyHost.transform.SetParent(host.transform);
            var legacyChildHost = new GameObject("Legacy mist child", typeof(ParticleSystem));
            legacyChildHost.transform.SetParent(legacyHost.transform);
            var legacyChild = legacyChildHost.GetComponent<ParticleSystem>();
            var legacy = legacyHost.GetComponent<ParticleSystem>();
            legacy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var spray = host.AddComponent<SanitizerSpray>();
            typeof(SanitizerSpray).GetField("sprayParticles", Hidden).SetValue(spray, legacy);
            var initialize = typeof(SanitizerSpray).GetMethod("InitializeComponents", Hidden);
            try
            {
                initialize.Invoke(spray, null);
                var particles = (ParticleSystem)
                    typeof(SanitizerSpray).GetField("sprayParticles", Hidden).GetValue(spray);
                Assert.That(particles, Is.Not.SameAs(legacy));
                Assert.That(particles.name, Is.EqualTo("CoreSpray"));
                Assert.That(legacy.GetComponent<ParticleSystemRenderer>().enabled, Is.False);
                Assert.That(legacy.emission.enabled, Is.False);
                Assert.That(legacyChild.emission.enabled, Is.False);
                Assert.That(legacyChild.GetComponent<ParticleSystemRenderer>().enabled, Is.False);
                Assert.That(host.transform.Find("SprayParticlesLegacy"), Is.Null);
                Assert.That(host.transform.Find("SprayParticleLayers/MistLayer"), Is.Not.Null);
                initialize.Invoke(spray, null);
                Assert.That(
                    typeof(SanitizerSpray).GetField("sprayParticles", Hidden).GetValue(spray),
                    Is.SameAs(particles)
                );
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RewardAudioHasAudibleSamplesAndRewarmsAfterItsSourceIsDestroyed()
        {
            var type = typeof(ProceduralBoostAudio);
            ProceduralBoostAudio.PrewarmAll();
            var sourceField = type.GetField("sharedAudioSource", Hidden);
            Object.DestroyImmediate(((AudioSource)sourceField.GetValue(null)).gameObject);
            ProceduralBoostAudio.PrewarmAll();
            var source = (AudioSource)sourceField.GetValue(null);
            try
            {
                Assert.That(
                    source.priority,
                    Is.LessThan(128),
                    "Reward feedback takes priority over ordinary combat voices."
                );
                var clips = (IDictionary)type.GetField("cachedClips", Hidden).GetValue(null);
                Assert.That(
                    clips.Count,
                    Is.EqualTo(Enum.GetValues(typeof(ProceduralBoostAudio.BoostSoundType)).Length)
                );
                foreach (AudioClip clip in clips.Values)
                    AssertAudible(clip);
                var chest = ProceduralChestAudio.GetOrCreateClip();
                AssertAudible(chest);
                Assert.That(ProceduralChestAudio.GetOrCreateClip(), Is.SameAs(chest));
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
            }
        }

        private static void AssertAudible(AudioClip clip)
        {
            var samples = new float[clip.samples];
            Assert.That(clip.GetData(samples, 0), Is.True);
            double energy = 0;
            foreach (float sample in samples)
            {
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False, clip.name);
                Assert.That(Mathf.Abs(sample), Is.LessThanOrEqualTo(1f), clip.name);
                energy += sample * sample;
            }
            Assert.That(Math.Sqrt(energy / samples.Length), Is.GreaterThan(0.01), clip.name);
        }
    }
}
