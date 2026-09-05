using System;

namespace BudgetGameDev.Autoplay
{
    /// <summary>Accounts for real work awaited outside an accelerated simulation clock.</summary>
    public sealed class SimulationReadinessGate
    {
        private double lastTime;
        private double waitStarted;
        public double TimeoutSeconds { get; }
        public bool Waiting { get; private set; }
        public bool TimedOut { get; private set; }
        public int WaitCount { get; private set; }
        public double TotalSeconds { get; private set; }
        public double MaximumWaitSeconds { get; private set; }

        public SimulationReadinessGate(double timeoutSeconds = 30)
        {
            if (
                double.IsNaN(timeoutSeconds)
                || double.IsInfinity(timeoutSeconds)
                || timeoutSeconds <= 0
            )
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            TimeoutSeconds = timeoutSeconds;
        }

        public void Observe(bool ready, double realtime)
        {
            if (double.IsNaN(realtime) || double.IsInfinity(realtime))
                throw new ArgumentOutOfRangeException(nameof(realtime));
            if (Waiting)
            {
                TotalSeconds += Math.Max(0, realtime - lastTime);
                double duration = Math.Max(0, realtime - waitStarted);
                MaximumWaitSeconds = Math.Max(MaximumWaitSeconds, duration);
                TimedOut |= duration >= TimeoutSeconds;
            }
            if (!ready && !Waiting)
            {
                waitStarted = realtime;
                WaitCount++;
            }
            Waiting = !ready;
            lastTime = realtime;
        }
    }
}
