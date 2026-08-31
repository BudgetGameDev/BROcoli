using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class SprayAudioTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void GeneratorBuildsBurstLoopAndReleaseClips()
        {
            var generator = new SprayAudioClipGenerator(8000, 3000f, 0.8f, 0.6f, 0.05f);
            AudioClip burst = generator.GenerateSprayBurst(0.08f);
            AudioClip loop = generator.GenerateSprayLoop(0.08f);
            AudioClip end = generator.GenerateSprayEnd(0.08f);
            try
            {
                AssertClip(burst, "SprayBurst", 640);
                AssertClip(loop, "SprayLoop", 640);
                AssertClip(end, "SprayEnd", 640);
            }
            finally
            {
                Object.DestroyImmediate(burst);
                Object.DestroyImmediate(loop);
                Object.DestroyImmediate(end);
            }
        }

        [Test]
        public void FilterHelpersCoverSaturationNoiseNormalisationAndEnvelopeRegions()
        {
            var filters = new SprayAudioFilters(8000);
            Assert.That(filters.ApplyLowpass(1f, 1000f), Is.Not.EqualTo(0f));
            Assert.That(filters.ApplyHighpass(1f, 1000f), Is.Not.EqualTo(0f));
            Assert.That(filters.ApplyBandpass(1f, 1000f, 1f), Is.Not.EqualTo(0f));
            filters.ResetFilters();

            Assert.That(SprayAudioFilters.SoftClip(2f), Is.InRange(0.7f, 1f));
            Assert.That(SprayAudioFilters.SoftClip(-2f), Is.InRange(-1f, -0.7f));
            Assert.That(SprayAudioFilters.SoftClip(0.5f), Is.InRange(0f, 0.5f));

            float[] samples = { -0.25f, 0.5f };
            SprayAudioFilters.NormalizeBuffer(samples, 1f);
            Assert.That(samples, Is.EqualTo(new[] { -0.5f, 1f }).Within(0.0001f));
            float[] silence = { 0f, 0f };
            SprayAudioFilters.NormalizeBuffer(silence, 1f);
            Assert.That(silence, Is.EqualTo(new[] { 0f, 0f }));

            Assert.That(
                SprayAudioFilters.GeneratePinkNoise(0.25f, new float[2]),
                Is.EqualTo(0.25f)
            );
            Assert.That(
                SprayAudioFilters.GeneratePinkNoise(0.25f, new float[7]),
                Is.Not.EqualTo(0.25f)
            );
            Assert.That(SprayAudioFilters.GetSprayEnvelope(0.01f, 1f), Is.GreaterThan(0f));
            Assert.That(SprayAudioFilters.GetSprayEnvelope(0.5f, 1f), Is.GreaterThan(0f));
            Assert.That(SprayAudioFilters.GetSprayEnvelope(0.95f, 1f), Is.GreaterThan(0f));
        }

        [Test]
        public void ComponentLifecycleSupportsBurstAndIdempotentStartStop()
        {
            GameObject host = new("Spray Audio", typeof(AudioSource), typeof(ProceduralSprayAudio));
            try
            {
                var audio = host.GetComponent<ProceduralSprayAudio>();
                Invoke(audio, "Awake");

                audio.PlaySprayBurst();
                audio.PlaySprayBurst(0.5f);
                audio.StartSpray();
                audio.StartSpray();
                Assert.That(audio.IsSpraying, Is.True);
                audio.StopSpray();
                audio.StopSpray();
                Assert.That(audio.IsSpraying, Is.False);

                var bare = new GameObject("Bare Spray").AddComponent<ProceduralSprayAudio>();
                Assert.DoesNotThrow(() => bare.PlaySprayBurst());
                Object.DestroyImmediate(bare.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertClip(AudioClip clip, string name, int samples)
        {
            Assert.That(clip.name, Is.EqualTo(name));
            Assert.That(clip.samples, Is.EqualTo(samples));
            var data = new float[samples];
            Assert.That(clip.GetData(data, 0), Is.True);
            foreach (float sample in data)
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
        }

        private static object Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Members).Invoke(target, arguments);
    }
}
