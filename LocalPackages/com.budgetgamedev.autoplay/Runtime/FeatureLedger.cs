using System.Collections.Generic;

namespace BudgetGameDev.Autoplay
{
    /// <summary>Game-independent event coverage for one automated session.</summary>
    public sealed class FeatureLedger
    {
        private readonly Dictionary<string, int> counts = new();

        public void Clear() => counts.Clear();

        public int Count(string feature) =>
            feature != null && counts.TryGetValue(feature, out int seen) ? seen : 0;

        public int Record(string feature)
        {
            if (string.IsNullOrEmpty(feature))
                return 0;
            int count = Count(feature) + 1;
            counts[feature] = count;
            return count;
        }

        public List<string> Missing(IEnumerable<string> required)
        {
            var missing = new List<string>();
            foreach (string feature in required)
                if (Count(feature) == 0)
                    missing.Add(feature);
            return missing;
        }
    }
}
