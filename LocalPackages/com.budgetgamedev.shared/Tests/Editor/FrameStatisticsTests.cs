using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class FrameStatisticsTests
    {
        [Test]
        public void ComputesFrameWeightedFpsAndSlowestOnePercent()
        {
            var stats = new FrameStatistics();
            for (int i = 0; i < 100; i++)
                stats.Add(i * .01, i == 99 ? .1 : .01);
            stats.Calculate();
            Assert.That(stats.Fps, Is.EqualTo(100 / 1.09).Within(.0001));
            Assert.That(stats.P99Milliseconds, Is.EqualTo(10).Within(.0001));
            Assert.That(stats.OnePercentLowFps, Is.EqualTo(10).Within(.0001));
        }

        [Test]
        public void OldHitchesExpireAndInvalidSamplesDoNotPollutePercentiles()
        {
            var stats = new FrameStatistics();
            stats.Add(0, 1);
            stats.Add(11, .004);
            stats.Add(11, double.NaN);
            stats.Add(11, 0);
            stats.Calculate();
            Assert.That(stats.Count, Is.EqualTo(1));
            Assert.That(stats.Fps, Is.EqualTo(250));
            Assert.That(stats.P99Milliseconds, Is.EqualTo(4));
            stats.Clear();
            Assert.That(stats.Count, Is.Zero);
        }
    }
}
