using System;
using NUnit.Framework;

namespace BudgetGameDev.Synth.Tests
{
    public class NonlinearTests
    {
        [TestCase(32000)] [TestCase(44100)] [TestCase(48000)] [TestCase(96000)]
        [TestCase(64000)] [TestCase(88200)] [TestCase(192000)] [TestCase(384000)]
        public void ExtremeInputAndSampleRateCutoffJumpsRemainBounded(int rate)
        {
            var filter = new NonlinearFilter24();
            var random = new Random(713);
            float peak = 0f;
            bool finite = true;
            for (int i = 0; i < 100000; i++)
            {
                float cutoff = i % 3 == 0 ? 20f : i % 3 == 1 ? 18000f : 20f + (float)random.NextDouble() * 17980f;
                float resonance = i % 2 == 0 ? .95f : (float)random.NextDouble() * .95f;
                float input = i % 7 == 0 ? ((float)random.NextDouble() * 2 - 1) * 12 : i % 2 == 0 ? 12 : -12;
                float output = filter.Process(input, cutoff, resonance, rate);
                finite &= !float.IsNaN(output) && !float.IsInfinity(output);
                peak = Math.Max(peak, Math.Abs(output));
            }
            Assert.That(finite, Is.True);
            Assert.That(peak, Is.GreaterThan(.01f).And.LessThanOrEqualTo(1.00001f));
        }

        [Test]
        public void FourPoleSmallSignalResponseFallsApproximately24DbPerOctave()
        {
            double gain1 = SineGainDb(1000);
            double gain2 = SineGainDb(2000);
            double gain4 = SineGainDb(4000);
            Assert.That(gain1 - gain2, Is.InRange(23.0, 25.0));
            Assert.That(gain2 - gain4, Is.InRange(23.0, 25.0));
        }

        private static double SineGainDb(float frequency)
        {
            var filter = new NonlinearFilter24();
            const int rate = 96000;
            double sum = 0;
            for (int i = 0; i < 2 * rate; i++)
            {
                float input = .001f * (float)Math.Sin(2 * Math.PI * frequency * i / rate);
                float output = filter.Process(input, 200, 0, rate);
                if (i >= rate) sum += (double)output * output;
            }
            return 20 * Math.Log10(Math.Sqrt(sum / rate) / (.001 / Math.Sqrt(2)));
        }

        [Test]
        public void DcBlockerRejectsConstantInputAndResetClearsHistory()
        {
            var blocker = new DcBlocker();
            float tail = 0;
            for (int i = 0; i < 96000; i++) tail = blocker.Process(.7f, 48000);
            Assert.That(Math.Abs(tail), Is.LessThan(1e-6f));
            blocker.Reset();
            Assert.That(blocker.Process(0, 48000), Is.Zero);
        }

        [Test]
        public void NonfiniteControlValuesDoNotPoisonFilterState()
        {
            var filter = new NonlinearFilter24();
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                float output = filter.Process(bad, bad, bad, bad);
                Assert.That(float.IsNaN(output) || float.IsInfinity(output), Is.False);
                Assert.That(Saturation.SoftClip(bad), Is.Zero);
            }
            for (int i = 0; i < 48000; i++) filter.Process(.5f, 180, .4f, 48000);
            filter.Reset();
            Assert.That(filter.Process(0, 180, .4f, 48000), Is.Zero);
        }
    }
}
