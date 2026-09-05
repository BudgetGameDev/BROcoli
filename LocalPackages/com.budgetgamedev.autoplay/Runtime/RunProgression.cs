using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Autoplay
{
    /// <summary>One step the run took -- a level, or a ring -- and what it cost.</summary>
    public readonly struct LevelStep
    {
        public readonly int Level;
        public readonly float Time;
        public readonly float Seconds;
        public readonly int Kills;

        public LevelStep(int level, float time, float seconds, int kills)
        {
            Level = level;
            Time = time;
            Seconds = seconds;
            Kills = kills;
        }
    }

    /// <summary>
    /// Watches a run's progression and the pressure it played under: when each level
    /// landed, how many kills it took, how much of the run was spent hurt, and how
    /// often the player died.
    ///
    /// This is what turns "the run survived" into a statement about difficulty. A
    /// bot that never drops below full health and a bot that dies every ninety
    /// seconds both finish their runs, and every other measurement the harness takes
    /// reads the same for the two of them.
    /// </summary>
    public sealed class RunProgression
    {
        /// <summary>Health fraction below which the run counts as being in trouble.</summary>
        private readonly int EarlyLevels;

        public RunProgression(int earlyLevels = 5) => EarlyLevels = Mathf.Max(2, earlyLevels);

        public const float DangerHealthFraction = 0.35f;

        /// <summary>Health fraction at or above which nothing is threatening the run.</summary>
        public const float SafeHealthFraction = 0.98f;

        private readonly List<LevelStep> steps = new();
        private readonly List<float> ringSeconds = new();

        private int level = 1;
        private int ring;
        private int deepestRing;
        private float lifeLevelTime;
        private float lifeRingTime;
        private int lifeLevelKills;
        private int samples;
        private float healthFractionTotal;
        private float lowestHealthFraction = 1f;
        private int dangerSamples;
        private int safeSamples;
        private int deaths;
        private int lives = 1;
        private int peakLevel = 1;

        public IReadOnlyList<LevelStep> Steps => steps;
        public int DeepestRing => deepestRing;
        public int Deaths => deaths;
        public int Lives => lives;
        public int PeakLevel => peakLevel;

        /// <summary>
        /// Folds one telemetry sample in. Levels are read from the same samples the
        /// run already writes rather than from a level-up callback, so the ledger
        /// cannot disagree with the telemetry beside it.
        /// </summary>
        public void Sample(
            float time,
            float level,
            float health,
            float maxHealth,
            int kills,
            int ring
        )
        {
            int reached = Mathf.Max(1, Mathf.RoundToInt(level));
            if (reached < this.level)
                BeginLife(time, kills);

            SampleDepth(time, ring);

            int gained = reached - this.level;
            if (gained > 0)
            {
                // Several levels can land between two samples, and charging the whole
                // gap to the first of them would report the rest as free. Split it.
                float seconds = (time - lifeLevelTime) / gained;
                int killed = Mathf.Max(0, kills - lifeLevelKills);
                for (int step = 0; step < gained; step++)
                {
                    this.level++;
                    steps.Add(
                        new LevelStep(
                            this.level,
                            time,
                            seconds,
                            Mathf.RoundToInt(killed / (float)gained)
                        )
                    );
                    peakLevel = Mathf.Max(peakLevel, this.level);
                }
                lifeLevelTime = time;
                lifeLevelKills = kills;
            }

            if (maxHealth <= 0f)
                return;

            float fraction = Mathf.Clamp01(health / maxHealth);
            samples++;
            healthFractionTotal += fraction;
            lowestHealthFraction = Mathf.Min(lowestHealthFraction, fraction);
            if (fraction < DangerHealthFraction)
                dangerSamples++;
            if (fraction >= SafeHealthFraction)
                safeSamples++;
        }

        /// <summary>Records a death; the life that follows starts its own level clock.</summary>
        public void NoteDeath() => deaths++;

        /// <summary>
        /// A fresh life resets the level, so the pacing clock restarts with it.
        /// Carrying it over would charge the next life for the whole of the last one.
        /// </summary>
        private void BeginLife(float time, int kills)
        {
            level = 1;
            lives++;
            lifeLevelTime = time;
            lifeLevelKills = kills;
            ring = 0;
            lifeRingTime = time;
        }

        /// <summary>
        /// Folds in how deep the run has got. Depth is charged the same way a level is
        /// -- only a new personal best costs anything -- because a bot that walks back
        /// through ring two on its way somewhere has not undone reaching ring three,
        /// and a fresh life starts its own descent.
        /// </summary>
        private void SampleDepth(float time, int reachedRing)
        {
            if (reachedRing <= ring)
                return;

            int gained = reachedRing - ring;
            float seconds = (time - lifeRingTime) / gained;
            for (int step = 0; step < gained; step++)
                ringSeconds.Add(seconds);
            ring = reachedRing;
            deepestRing = Mathf.Max(deepestRing, ring);
            lifeRingTime = time;
        }

        public ProgressionSummary Summarize(float duration)
        {
            return new ProgressionSummary(
                new LevelPacing(
                    peakLevel,
                    steps.Count,
                    SecondsPerLevel(1, int.MaxValue),
                    SecondsPerLevel(1, EarlyLevels),
                    SecondsPerLevel(EarlyLevels + 1, int.MaxValue),
                    KillsPerLevel(1, EarlyLevels),
                    KillsPerLevel(EarlyLevels + 1, int.MaxValue)
                ),
                new DepthPacing(deepestRing, ringSeconds.Count, MeanRingSeconds()),
                new HealthPressure(
                    samples > 0 ? healthFractionTotal / samples : 1f,
                    samples > 0 ? lowestHealthFraction : 1f,
                    samples > 0 ? dangerSamples / (float)samples : 0f,
                    samples > 0 ? safeSamples / (float)samples : 1f
                ),
                lives,
                deaths,
                duration
            );
        }

        /// <summary>Mean seconds a ring took, over every ring the run pushed out to.</summary>
        private float MeanRingSeconds()
        {
            if (ringSeconds.Count == 0)
                return 0f;
            float total = 0f;
            foreach (float seconds in ringSeconds)
                total += seconds;
            return total / ringSeconds.Count;
        }

        /// <summary>Mean seconds a level took, over the levels in a band.</summary>
        private float SecondsPerLevel(int from, int to) => Mean(from, to, step => step.Seconds);

        /// <summary>Mean kills a level took, over the levels in a band.</summary>
        private float KillsPerLevel(int from, int to) => Mean(from, to, step => step.Kills);

        private float Mean(int from, int to, System.Func<LevelStep, float> read)
        {
            float total = 0f;
            int counted = 0;
            foreach (LevelStep step in steps)
            {
                if (step.Level < from || step.Level > to)
                    continue;
                total += read(step);
                counted++;
            }
            return counted > 0 ? total / counted : 0f;
        }
    }
}
