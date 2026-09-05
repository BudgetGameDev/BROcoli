using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Autoplay.Tests
{
    public sealed class SimulationReadinessTests
    {
        [Test]
        public void AwaitedWorkCountsRealSecondsWithoutAdvancingTheSimulationClock()
        {
            float capture = Time.captureDeltaTime;
            bool waiting = AutoplayTimeControl.WaitingForReadiness;
            try
            {
                Time.captureDeltaTime = 0.08f;
                var gate = new SimulationReadinessGate(30);
                gate.Observe(false, 10);
                AutoplayTimeControl.WaitingForReadiness = gate.Waiting;
                float elapsed = 12;
                for (int frame = 0; frame < 100; frame++)
                    elapsed += AutoplayTimeControl.GameDelta;
                Assert.That(
                    elapsed,
                    Is.EqualTo(12),
                    "balance duration and progress clocks cannot include awaited work"
                );
                gate.Observe(false, 13);
                gate.Observe(true, 15);
                AutoplayTimeControl.WaitingForReadiness = gate.Waiting;
                Assert.That(AutoplayTimeControl.GameDelta, Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(gate.WaitCount, Is.EqualTo(1));
                Assert.That(gate.TotalSeconds, Is.EqualTo(5));
                Assert.That(gate.MaximumWaitSeconds, Is.EqualTo(5));
                Assert.That(gate.TimedOut, Is.False);
            }
            finally
            {
                Time.captureDeltaTime = capture;
                AutoplayTimeControl.WaitingForReadiness = waiting;
            }
        }

        [Test]
        public void TimeoutIsBoundedInRealTimeAndCannotBeErasedByLaterReadiness()
        {
            var gate = new SimulationReadinessGate(30);
            gate.Observe(false, 0);
            gate.Observe(false, 30);
            Assert.That(gate.TimedOut, Is.True);
            gate.Observe(true, 31);
            gate.Observe(false, 32);
            gate.Observe(true, 34);
            Assert.That(gate.TimedOut, Is.True);
            Assert.That(gate.WaitCount, Is.EqualTo(2));
            Assert.That(gate.TotalSeconds, Is.EqualTo(33));
            Assert.That(gate.MaximumWaitSeconds, Is.EqualTo(31));
        }
    }
}
