using System.Collections.Generic;
using NUnit.Framework;

namespace BudgetGameDev.Autoplay.Tests
{
    public sealed class DelayedCommandSchedulerTests
    {
        [Test]
        public void FloatConfigurationUsesTheTwelfthAndTwentyFourth120HzTicks()
        {
            var scheduler = new DelayedCommandScheduler<int>(.1f, .2f);
            Assert.That(scheduler.TryObserve(0), Is.True);
            scheduler.Enqueue(7, 0);
            for (int tick = 1; tick < 12; tick++)
                Assert.That(scheduler.TryObserve(tick / 120.0), Is.False);
            Assert.That(scheduler.TryObserve(12 / 120.0), Is.True);
            for (int tick = 0; tick < 24; tick++)
                Assert.That(scheduler.TryActivate(tick / 120.0, out _), Is.False);
            Assert.That(scheduler.TryActivate(24 / 120.0, out int command), Is.True);
            Assert.That(command, Is.EqualTo(7));
        }

        [Test]
        public void NewObservationsCannotChangeCommandsBeforeTheirReactionDelay()
        {
            var scheduler = new DelayedCommandScheduler<string>(.1, .2);
            Assert.That(scheduler.TryObserve(0), Is.True);
            scheduler.Enqueue("left", 0);
            Assert.That(scheduler.TryObserve(.05), Is.False);
            Assert.That(scheduler.TryObserve(.1), Is.True);
            scheduler.Enqueue("right", .1);
            Assert.That(scheduler.TryActivate(.199, out _), Is.False);
            Assert.That(scheduler.TryActivate(.2, out var command), Is.True);
            Assert.That(command, Is.EqualTo("left"));
            Assert.That(scheduler.TryActivate(.3, out command), Is.True);
            Assert.That(command, Is.EqualTo("right"));
        }

        [Test]
        public void PausedSimulationDoesNotObserveAgainOrMaturePendingCommands()
        {
            var scheduler = new DelayedCommandScheduler<int>(.1, .2);
            Assert.That(scheduler.TryObserve(5), Is.True);
            scheduler.Enqueue(1, 5);
            for (int renderedFrame = 0; renderedFrame < 1000; renderedFrame++)
            {
                Assert.That(scheduler.TryObserve(5), Is.False);
                Assert.That(scheduler.TryActivate(5, out _), Is.False);
            }
            Assert.That(scheduler.TryActivate(5.2, out _), Is.True);
        }

        [Test]
        public void CaptureFrameGroupingDoesNotChangePhysicsClockDecisions()
        {
            Assert.That(Simulate(4), Is.EqualTo(Simulate(1)));
        }

        private static List<int> Simulate(int physicsStepsPerCapture)
        {
            var scheduler = new DelayedCommandScheduler<int>(.1, .2);
            var activations = new List<int>();
            for (int frame = 0; frame < 120 / physicsStepsPerCapture; frame++)
            for (int substep = 0; substep < physicsStepsPerCapture; substep++)
            {
                int tick = frame * physicsStepsPerCapture + substep;
                double now = tick / 120.0;
                if (scheduler.TryObserve(now))
                    scheduler.Enqueue(tick, now);
                if (scheduler.TryActivate(now, out int command))
                {
                    Assert.That(tick - command, Is.GreaterThanOrEqualTo(24));
                    activations.Add(tick);
                }
            }
            Assert.That(activations, Is.Not.Empty);
            return activations;
        }

        [Test]
        public void StressProfileIsImmediateAndResetDiscardsPreviousLifeCommands()
        {
            var immediate = new DelayedCommandScheduler<int>(0, 0);
            Assert.That(immediate.TryObserve(1), Is.True);
            immediate.Enqueue(7, 1);
            Assert.That(immediate.TryActivate(1, out int command), Is.True);
            Assert.That(command, Is.EqualTo(7));
            var delayed = new DelayedCommandScheduler<int>(.1, .2);
            delayed.Enqueue(9, 1);
            delayed.Reset();
            Assert.That(delayed.TryActivate(2, out _), Is.False);
            Assert.That(delayed.TryObserve(0), Is.True);
        }

        [Test]
        public void ClockJumpUsesNewestMatureCommandWithoutBurstingObservations()
        {
            var scheduler = new DelayedCommandScheduler<int>(.1, .2);
            scheduler.Enqueue(1, 0);
            scheduler.Enqueue(2, .1);
            scheduler.Enqueue(3, .9);
            Assert.That(scheduler.TryActivate(1, out int command), Is.True);
            Assert.That(command, Is.EqualTo(2));
            Assert.That(scheduler.TryActivate(1, out _), Is.False);
            Assert.That(scheduler.TryObserve(1), Is.True);
            Assert.That(scheduler.TryObserve(1), Is.False);
        }
    }
}
