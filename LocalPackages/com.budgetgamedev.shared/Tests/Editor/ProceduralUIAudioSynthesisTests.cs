using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the sample maths behind the UI sounds. The clips are generated into
    /// plain float buffers, so the shape of each sound is checked by reading the
    /// samples back rather than by listening to them.
    /// </summary>
    public sealed class ProceduralUIAudioSynthesisTests
    {
        private const int Rate = 44100;

        private readonly List<AudioClip> clips = new();

        [TearDown]
        public void DestroyClips()
        {
            foreach (AudioClip clip in clips)
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }

            clips.Clear();
        }

        [Test]
        public void TheHoverTickIsAShortNormalisedBlip()
        {
            AudioClip clip = Keep(ProceduralUIAudio.GenerateHoverSound(Rate));
            float[] samples = Read(clip);

            Assert.That(clip.name, Is.EqualTo("UIHover"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(Rate));
            Assert.That(clip.length, Is.EqualTo(0.06f).Within(0.001f));
            Assert.That(Peak(samples, 0, samples.Length), Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void TheSelectConfirmIsLouderAndLongerThanTheHoverTick()
        {
            AudioClip hover = Keep(ProceduralUIAudio.GenerateHoverSound(Rate));
            AudioClip select = Keep(ProceduralUIAudio.GenerateSelectSound(Rate));
            float[] samples = Read(select);

            Assert.That(select.name, Is.EqualTo("UISelect"));
            Assert.That(select.length, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(select.samples, Is.GreaterThan(hover.samples));
            Assert.That(Peak(samples, 0, samples.Length), Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void TheLevelUpArpeggioStaysLoudAfterItsFinalNoteEnters()
        {
            AudioClip clip = Keep(ProceduralUIAudio.GenerateLevelUpSelectSound(Rate));
            float[] samples = Read(clip);
            float[] hover = Read(Keep(ProceduralUIAudio.GenerateHoverSound(Rate)));

            Assert.That(clip.name, Is.EqualTo("UILevelUpSelect"));
            Assert.That(clip.length, Is.EqualTo(0.28f).Within(0.001f));

            float peak = Peak(samples, 0, samples.Length);
            Assert.That(peak, Is.EqualTo(0.85f).Within(0.001f));

            // The last note of the arpeggio enters at 40%, so the second half is still
            // near full scale, where the one-shot tick has already faded away.
            float late = Peak(samples, samples.Length * 4 / 10, samples.Length * 8 / 10);
            Assert.That(late, Is.GreaterThan(peak * 0.5f));

            float hoverLate = Peak(hover, hover.Length * 4 / 10, hover.Length * 8 / 10);
            Assert.That(hoverLate, Is.LessThan(Peak(hover, 0, hover.Length) * 0.5f));
        }

        [Test]
        public void EveryClipOpensAndClosesInSilence()
        {
            foreach (AudioClip clip in AllClips())
            {
                float[] samples = Read(clip);
                float peak = Peak(samples, 0, samples.Length);

                Assert.That(samples[0], Is.EqualTo(0f).Within(1e-6f), $"{clip.name} clicks open.");
                Assert.That(
                    Mathf.Abs(samples[samples.Length - 1]),
                    Is.LessThan(1e-4f),
                    $"{clip.name} is cut off before it decays."
                );
                Assert.That(
                    Peak(samples, samples.Length * 95 / 100, samples.Length),
                    Is.LessThan(peak * 0.1f),
                    $"{clip.name} has no tail."
                );
            }
        }

        [Test]
        public void EveryClipStaysInsideTheSignedUnitRange()
        {
            foreach (AudioClip clip in AllClips())
            {
                float[] samples = Read(clip);
                foreach (float sample in samples)
                    Assert.That(Mathf.Abs(sample), Is.LessThanOrEqualTo(1f), $"{clip.name} clips.");
            }
        }

        [Test]
        public void TheSampleCountFollowsTheRequestedRate()
        {
            AudioClip clip = Keep(ProceduralUIAudio.GenerateHoverSound(8000));

            Assert.That(clip.frequency, Is.EqualTo(8000));
            Assert.That(clip.samples, Is.EqualTo(480));
            Assert.That(clip.length, Is.EqualTo(0.06f).Within(0.001f));
        }

        [Test]
        public void NormalisingScalesEverySampleOntoTheTargetPeak()
        {
            float[] samples = { 0.1f, -0.2f, 0.05f };

            ProceduralUIAudio.NormalizeSamples(samples, 0.8f);

            Assert.That(samples[0], Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(samples[1], Is.EqualTo(-0.8f).Within(1e-6f));
            Assert.That(samples[2], Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void NormalisingLeavesNearSilenceAlone()
        {
            // Below the floor the gain would be enormous, so quiet buffers are left as
            // they are instead of being amplified into noise.
            float[] quiet = { 0.005f, -0.01f, 0f };
            float[] silent = { 0f, 0f };

            ProceduralUIAudio.NormalizeSamples(quiet, 0.8f);
            ProceduralUIAudio.NormalizeSamples(silent, 0.8f);

            Assert.That(quiet, Is.EqualTo(new[] { 0.005f, -0.01f, 0f }));
            Assert.That(silent, Is.EqualTo(new[] { 0f, 0f }));
        }

        private IEnumerable<AudioClip> AllClips()
        {
            yield return Keep(ProceduralUIAudio.GenerateHoverSound(Rate));
            yield return Keep(ProceduralUIAudio.GenerateSelectSound(Rate));
            yield return Keep(ProceduralUIAudio.GenerateLevelUpSelectSound(Rate));
        }

        private AudioClip Keep(AudioClip clip)
        {
            clips.Add(clip);
            return clip;
        }

        private static float[] Read(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            return samples;
        }

        private static float Peak(float[] samples, int from, int to)
        {
            float peak = 0f;
            for (int index = from; index < to; index++)
                peak = Mathf.Max(peak, Mathf.Abs(samples[index]));

            return peak;
        }
    }
}
