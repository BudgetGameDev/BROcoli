using System;

namespace BudgetGameDev.Shared
{
    /// <summary>Rendered-frame statistics over a bounded ten-second window.</summary>
    public sealed class FrameStatistics
    {
        public const double WindowSeconds = 10;
        private readonly double[] times = new double[8192];
        private readonly double[] durations = new double[8192];
        private readonly double[] sorted = new double[8192];
        private int start,
            count;
        public int Count => count;
        public double Fps { get; private set; }
        public double MeanMilliseconds { get; private set; }
        public double P99Milliseconds { get; private set; }
        public double OnePercentLowFps { get; private set; }

        public void Add(double now, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                return;
            while (count > 0 && (now - times[start] > WindowSeconds || count == times.Length))
            {
                start = (start + 1) % times.Length;
                count--;
            }
            int index = (start + count++) % times.Length;
            times[index] = now;
            durations[index] = seconds;
        }

        public void Calculate()
        {
            if (count == 0)
                return;
            double total = 0;
            for (int i = 0; i < count; i++)
            {
                sorted[i] = durations[(start + i) % times.Length];
                total += sorted[i];
            }
            Array.Sort(sorted, 0, count);
            Fps = count / total;
            MeanMilliseconds = 1000 * total / count;
            P99Milliseconds = 1000 * sorted[Math.Max(0, (int)Math.Ceiling(count * .99) - 1)];
            int lowCount = Math.Max(1, (int)Math.Ceiling(count * .01));
            double slowTotal = 0;
            for (int i = count - lowCount; i < count; i++)
                slowTotal += sorted[i];
            OnePercentLowFps = lowCount / slowTotal;
        }

        public void Clear()
        {
            start = count = 0;
            Fps = MeanMilliseconds = P99Milliseconds = OnePercentLowFps = 0;
        }
    }
}
