using System;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonAmbienceTests
    {
        [Test]
        public void StereoCavityAirIsBoundedSeamlessAndHasSubtleWidth()
        {
            float[] samples = ProceduralDungeonAmbience.SynthesizeBed();
            Assert.That(samples.Length, Is.EqualTo(12 * 22050 * 2));
            VerifySignal(samples, 2, 0.42);
            double sideEnergy = 0,
                centreEnergy = 0;
            for (int frame = 0; frame < samples.Length / 2; frame++)
            {
                double centre = samples[frame * 2] + samples[frame * 2 + 1];
                double side = samples[frame * 2] - samples[frame * 2 + 1];
                centreEnergy += centre * centre;
                sideEnergy += side * side;
            }
            Assert.That(sideEnergy / centreEnergy, Is.InRange(0.01, 0.25));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void DistantDetailsHaveSoftOnsetsAndAnAudibleDecayingEcho(int variant)
        {
            float[] samples = ProceduralDungeonAmbience.SynthesizeDetail(variant);
            VerifySignal(samples, 1, 0.49);
            double initial = 0,
                echo = 0,
                tail = 0;
            int rate = ProceduralDungeonAmbience.SampleRate;
            for (int frame = 0; frame < rate; frame++)
            {
                initial += samples[frame] * samples[frame];
                echo += samples[frame + rate] * samples[frame + rate];
                tail += samples[frame + 2 * rate] * samples[frame + 2 * rate];
            }
            Assert.That(echo, Is.GreaterThan(initial * 0.00001));
            Assert.That(tail, Is.LessThan(echo));
            Assert.That(echo, Is.LessThan(initial * 0.1));
        }

        [Test]
        public void DetailsAreReproducibleAndSchedulingStaysSparse()
        {
            Assert.That(
                ProceduralDungeonAmbience.SynthesizeDetail(2),
                Is.EqualTo(ProceduralDungeonAmbience.SynthesizeDetail(2))
            );
            Assert.That(
                ProceduralDungeonAmbience.SynthesizeDetail(2),
                Is.Not.EqualTo(ProceduralDungeonAmbience.SynthesizeDetail(4))
            );
            Assert.That(ProceduralDungeonAmbience.NextDelay(0), Is.EqualTo(3));
            Assert.That(ProceduralDungeonAmbience.NextDelay(0.5), Is.EqualTo(6));
            Assert.That(ProceduralDungeonAmbience.NextDelay(1), Is.EqualTo(9));
        }

        private static void VerifySignal(float[] samples, int channels, double peak)
        {
            int frames = samples.Length / channels;
            for (int channel = 0; channel < channels; channel++)
            {
                double sum = 0,
                    energy = 0,
                    greatest = 0;
                for (int frame = 0; frame < frames; frame++)
                {
                    float sample = samples[frame * channels + channel];
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                        Assert.Fail("Synthesis produced a non-finite sample.");
                    sum += sample;
                    energy += sample * sample;
                    greatest = Math.Max(greatest, Math.Abs(sample));
                }
                Assert.That(Math.Abs(sum / frames), Is.LessThan(0.000001));
                Assert.That(greatest, Is.InRange(0.01, peak + 0.000001));
                Assert.That(energy / frames, Is.GreaterThan(0.000001));
                Assert.That(samples[channel], Is.Zero);
                Assert.That(samples[(frames - 1) * channels + channel], Is.Zero);
                Assert.That(Math.Abs(samples[channels + channel]), Is.LessThan(0.0001));
            }
        }
    }
}
