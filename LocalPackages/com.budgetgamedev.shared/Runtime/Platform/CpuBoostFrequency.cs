using System;
using System.Collections.Generic;

namespace BudgetGameDev.Shared
{
    internal static class CpuBoostFrequency
    {
        // Pair each logical processor's nominal frequency with its performance ratio.
        // This interval estimate includes boost; it is not the advertised boost ceiling.
        internal static double? EstimatePeak(IReadOnlyDictionary<string, double> frequencies,
            IReadOnlyDictionary<string, double> performance)
        {
            double? peak = null;
            foreach (var pair in frequencies)
            {
                if (pair.Key.EndsWith("_Total", StringComparison.OrdinalIgnoreCase)
                    || !performance.TryGetValue(pair.Key, out double percent)) continue;
                double clock = pair.Value * percent / 100;
                if (pair.Value <= 0 || pair.Value > 20000 || percent <= 0 || percent > 1000
                    || double.IsNaN(clock) || double.IsInfinity(clock) || clock <= 0 || clock > 20000) continue;
                peak = peak.HasValue ? Math.Max(peak.Value, clock) : clock;
            }
            return peak;
        }
    }
}
