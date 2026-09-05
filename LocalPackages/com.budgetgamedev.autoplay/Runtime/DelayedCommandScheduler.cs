using System;
using System.Collections.Generic;

namespace BudgetGameDev.Autoplay
{
    /// <summary>
    /// Delays commands using the caller's simulation clock. Rendering and wall time
    /// do not affect observation cadence or activation latency.
    /// </summary>
    public sealed class DelayedCommandScheduler<T>
    {
        // Serialized float durations promoted to double carry a few nanoseconds
        // of error. Do not turn that error into an extra 120 Hz physics step.
        private const double ClockToleranceSeconds = 1e-7;
        private readonly double interval;
        private readonly double delay;
        private readonly Queue<(double due, T command)> pending = new();
        private double nextObservation = double.NegativeInfinity;
        private double lastObservation = double.NegativeInfinity;
        public long ObservationCount { get; private set; }
        public long ActivationCount { get; private set; }

        public DelayedCommandScheduler(
            double observationIntervalSeconds,
            double reactionDelaySeconds
        )
        {
            Validate(observationIntervalSeconds);
            Validate(reactionDelaySeconds);
            interval = observationIntervalSeconds;
            delay = reactionDelaySeconds;
        }

        public bool TryObserve(double now)
        {
            if (now <= lastObservation || now + ClockToleranceSeconds < nextObservation)
                return false;
            lastObservation = now;
            nextObservation = now + interval;
            ObservationCount++;
            return true;
        }

        public void Enqueue(T command, double observedAt) =>
            pending.Enqueue((observedAt + delay, command));

        public bool TryActivate(double now, out T command)
        {
            command = default;
            bool activated = false;
            // If simulation advances by several steps, apply the newest mature
            // command once. Never apply one observed too recently to have matured.
            while (pending.Count > 0 && pending.Peek().due <= now + ClockToleranceSeconds)
            {
                command = pending.Dequeue().command;
                activated = true;
            }
            if (activated)
                ActivationCount++;
            return activated;
        }

        public void Reset()
        {
            pending.Clear();
            nextObservation = lastObservation = double.NegativeInfinity;
            ObservationCount = ActivationCount = 0;
        }

        private static void Validate(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
        }
    }
}
