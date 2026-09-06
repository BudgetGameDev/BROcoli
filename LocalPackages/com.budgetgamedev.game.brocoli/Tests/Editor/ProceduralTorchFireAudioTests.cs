using System;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class ProceduralTorchFireAudioTests
    {
        [Test]
        public void FuelLoopIsQuietFiniteSeamlessAndFreeOfDc()
        {
            float[] samples = ProceduralTorchFireAudio.SynthesizeBed();
            Assert.That(samples.Length, Is.EqualTo(4 * 22050));
            VerifySignal(samples, 0.46f);
            Assert.That(Math.Abs(samples[1]) + Math.Abs(samples[^2]), Is.LessThan(0.0001));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SynchronizedCracklesHaveSoftEdgesAndBoundedEnergy(int variant)
        {
            float[] samples = ProceduralTorchFireAudio.SynthesizeCrackle(variant);
            VerifySignal(samples, 0.55f);
            Assert.That(samples.Length, Is.InRange(3900, 5700));
            double first = 0,
                last = 0;
            for (int index = 0; index < samples.Length / 3; index++)
            {
                first += samples[index] * samples[index];
                last += samples[^(index + 1)] * samples[^(index + 1)];
            }
            Assert.That(last, Is.LessThan(first * 0.2), "a pop decays into a gentle sizzle");
        }

        [Test]
        public void VariantsAreDistinctButReproducibleWithoutGameplayRandomness()
        {
            Assert.That(
                ProceduralTorchFireAudio.SynthesizeCrackle(0),
                Is.EqualTo(ProceduralTorchFireAudio.SynthesizeCrackle(0))
            );
            Assert.That(
                ProceduralTorchFireAudio.SynthesizeCrackle(1),
                Is.Not.EqualTo(ProceduralTorchFireAudio.SynthesizeCrackle(0))
            );
        }

        [Test]
        public void PlayerDistanceFallsSmoothlyFromNearbyToSilentAtTwelveMeters()
        {
            Assert.That(ProceduralTorchFireAudio.DistanceGain(0), Is.EqualTo(1));
            Assert.That(ProceduralTorchFireAudio.DistanceGain(1), Is.EqualTo(1));
            Assert.That(ProceduralTorchFireAudio.DistanceGain(12), Is.Zero);
            Assert.That(ProceduralTorchFireAudio.DistanceGain(100), Is.Zero);
            Assert.That(ProceduralTorchFireAudio.DistanceGain(float.NaN), Is.Zero);
            float previous = 1;
            for (float distance = 1; distance <= 12; distance += 0.25f)
            {
                float gain = ProceduralTorchFireAudio.DistanceGain(distance);
                Assert.That(gain, Is.InRange(0f, previous));
                previous = gain;
            }
            Assert.That(ProceduralTorchFireAudio.DistanceGain(6.5f), Is.EqualTo(0.25f));
        }

        private static void VerifySignal(float[] samples, float bound)
        {
            double sum = 0,
                energy = 0;
            foreach (float sample in samples)
            {
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
                Assert.That(Math.Abs(sample), Is.LessThanOrEqualTo(bound + 0.000001f));
                sum += sample;
                energy += sample * sample;
            }
            Assert.That(Math.Abs(sum / samples.Length), Is.LessThan(0.000001));
            Assert.That(Math.Sqrt(energy / samples.Length), Is.InRange(0.005, 0.2));
            Assert.That(samples[0], Is.Zero);
            Assert.That(samples[^1], Is.Zero);
        }
    }
}
