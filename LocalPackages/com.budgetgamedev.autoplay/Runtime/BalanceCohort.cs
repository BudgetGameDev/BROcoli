using System;
using System.Collections.Generic;

namespace BudgetGameDev.Autoplay
{
    /// <summary>
    /// Combines independent runs without hiding a bad seed behind an average.
    /// Rare-event frequency belongs to total exposure, not each short run.
    /// </summary>
    public sealed class BalanceCohort
    {
        private readonly HashSet<int> seeds = new();
        private readonly List<string> findings = new();
        public int Runs => seeds.Count;
        public float Duration { get; private set; }
        public int Deaths { get; private set; }
        public float DeathsPerHour => Duration > 0f ? Deaths * 3600f / Duration : 0f;

        public void Add(int seed, float duration, int deaths, IEnumerable<string> runFindings)
        {
            if (!seeds.Add(seed))
            {
                findings.Add($"seed {seed} repeated: independent seeds are required");
                return;
            }
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f || deaths < 0)
            {
                findings.Add($"seed {seed}: invalid duration or death count");
                return;
            }
            Duration += duration;
            Deaths += deaths;
            foreach (string finding in runFindings)
                findings.Add($"seed {seed}: {finding}");
        }

        public List<string> Evaluate(
            float minDeathsPerHour,
            float maxDeathsPerHour,
            int minimumSeeds = 3,
            float minimumSeconds = 1800f
        )
        {
            var result = new List<string>(findings);
            if (Runs < minimumSeeds || Duration < minimumSeconds)
                result.Add(
                    $"insufficient exposure: {Runs} seeds / {Duration:0}s; need {minimumSeeds} seeds / {minimumSeconds:0}s"
                );
            else if (DeathsPerHour < minDeathsPerHour || DeathsPerHour > maxDeathsPerHour)
                result.Add(
                    $"cohort deaths out of band: {DeathsPerHour:0.##}/hour; want {minDeathsPerHour:0.##}–{maxDeathsPerHour:0.##}"
                );
            return result;
        }
    }
}
