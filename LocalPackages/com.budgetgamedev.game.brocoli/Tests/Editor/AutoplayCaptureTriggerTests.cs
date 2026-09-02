using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Capture triggers are how an agent asks a batch run a specific question, so
    /// the spec has to read the way it is documented and a trigger that never fired
    /// has to be reported rather than quietly producing no picture.
    /// </summary>
    public sealed class AutoplayCaptureTriggerTests
    {
        [SetUp]
        [TearDown]
        public void ClearTriggers()
        {
            AutoplayCaptureTriggers.Reset();
            AutoplayFeatureLog.Reset();
            SetAutoplayActive(false);
        }

        private static void SetAutoplayActive(bool active) =>
            typeof(AutoplayController)
                .GetField("<IsActive>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, active);

        private static AutoplayCaptureTrigger Parse(string spec)
        {
            Assert.That(
                AutoplayCaptureTrigger.TryParse(spec, out AutoplayCaptureTrigger trigger),
                Is.True,
                spec
            );
            return trigger;
        }

        [Test]
        public void ABareEventNameMeansItsFirstOccurrence()
        {
            AutoplayCaptureTrigger trigger = Parse(" pickup.experience-dropped ");

            Assert.That(trigger.Event, Is.EqualTo("pickup.experience-dropped"));
            Assert.That(trigger.Occurrence, Is.EqualTo(1));
            Assert.That(trigger.Delay, Is.Zero);
            Assert.That(trigger.Matches("pickup.experience-dropped", 1), Is.True);
            Assert.That(trigger.Matches("pickup.experience-dropped", 2), Is.False);
            Assert.That(trigger.Matches("combat.enemy-killed", 1), Is.False);
        }

        [Test]
        public void AnOccurrenceAStarAndADelayAreAllReadable()
        {
            AutoplayCaptureTrigger third = Parse("levelup.upgrade-chosen#3");
            AutoplayCaptureTrigger every = Parse("combat.enemy-killed*");
            AutoplayCaptureTrigger delayed = Parse("pickup.experience-dropped+0.5");
            AutoplayCaptureTrigger both = Parse("dungeon.chest-opened#2+1.25");

            Assert.That(third.Occurrence, Is.EqualTo(3));
            Assert.That(every.Occurrence, Is.EqualTo(AutoplayCaptureTrigger.EveryOccurrence));
            Assert.That(every.Matches("combat.enemy-killed", 97), Is.True);
            Assert.That(delayed.Delay, Is.EqualTo(0.5f));
            Assert.That(delayed.Occurrence, Is.EqualTo(1));
            Assert.That(both.Event, Is.EqualTo("dungeon.chest-opened"));
            Assert.That(both.Occurrence, Is.EqualTo(2));
            Assert.That(both.Delay, Is.EqualTo(1.25f));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("*")]
        [TestCase("+1")]
        [TestCase("event#0")]
        [TestCase("event#-2")]
        [TestCase("event#two")]
        [TestCase("event+later")]
        [TestCase("event+-1")]
        public void AMalformedSpecIsRefused(string spec)
        {
            Assert.That(AutoplayCaptureTrigger.TryParse(spec, out _), Is.False, spec);
        }

        [Test]
        public void ArmingReportsASpecItCannotRead()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Ignoring capture trigger 'event#0'"));

            AutoplayCaptureTriggers.Arm(new[] { "combat.enemy-killed", "event#0" });

            Assert.That(AutoplayCaptureTriggers.Any, Is.True);
            Assert.That(
                AutoplayCaptureTriggers.Unfired(),
                Is.EqualTo(new[] { "combat.enemy-killed" }),
                "a spec that could not be read is not a trigger waiting to fire"
            );
        }

        [Test]
        public void NoTriggersMeansNothingToCapture()
        {
            AutoplayCaptureTriggers.Arm(null);

            AutoplayCaptureTriggers.Notify("combat.enemy-killed", 1);
            AutoplayCaptureTriggers.Tick(1f);

            Assert.That(AutoplayCaptureTriggers.Any, Is.False);
            Assert.That(AutoplayCaptureTriggers.TryTakeReady(out _), Is.False);
            Assert.That(AutoplayCaptureTriggers.ToJson(), Is.EqualTo("[]"));
            Assert.That(AutoplayCaptureTriggers.Unfired(), Is.Empty);
        }

        [Test]
        public void TheRequestedOccurrenceIsTheOneQueued()
        {
            AutoplayCaptureTriggers.Arm(new[] { "combat.enemy-killed#2" });

            AutoplayCaptureTriggers.Notify("combat.enemy-killed", 1);
            AutoplayCaptureTriggers.Notify("dungeon.chest-opened", 2);
            Assert.That(AutoplayCaptureTriggers.TryTakeReady(out _), Is.False);

            AutoplayCaptureTriggers.Notify("combat.enemy-killed", 2);

            Assert.That(
                AutoplayCaptureTriggers.TryTakeReady(out AutoplayCaptureTriggers.Request request),
                Is.True
            );
            Assert.That(request.Occurrence, Is.EqualTo(2));
            Assert.That(request.Event, Is.EqualTo("combat.enemy-killed"));
        }

        [Test]
        public void ADelayedTriggerWaitsOutItsDelayBeforeItIsReady()
        {
            AutoplayCaptureTriggers.Arm(new[] { "pickup.experience-dropped+0.5" });
            AutoplayCaptureTriggers.Notify("pickup.experience-dropped", 1);

            AutoplayCaptureTriggers.Tick(0.2f);
            Assert.That(AutoplayCaptureTriggers.TryTakeReady(out _), Is.False, "still falling");

            AutoplayCaptureTriggers.Tick(0.4f);

            Assert.That(
                AutoplayCaptureTriggers.TryTakeReady(out AutoplayCaptureTriggers.Request request),
                Is.True
            );
            string entry = AutoplayCaptureTriggers.Record(request, "events/orb.png");
            Assert.That(entry, Does.Contain("\"t\":0.6"), "stamped with the run clock");
            Assert.That(entry, Does.Contain("\"event\":\"pickup.experience-dropped\""));
            Assert.That(entry, Does.Contain("\"trigger\":\"pickup.experience-dropped+0.5\""));
            Assert.That(entry, Does.Contain("\"file\":\"events/orb.png\""));
            Assert.That(AutoplayCaptureTriggers.ToJson(), Is.EqualTo($"[{entry}]"));
            Assert.That(AutoplayCaptureTriggers.Unfired(), Is.Empty);
        }

        [Test]
        public void EveryOccurrenceIsCappedSoALongRunStaysReadable()
        {
            AutoplayCaptureTriggers.Arm(new[] { "combat.enemy-killed*" });

            for (int kill = 1; kill <= AutoplayCaptureTriggers.EveryLimit + 10; kill++)
                AutoplayCaptureTriggers.Notify("combat.enemy-killed", kill);

            int taken = 0;
            while (
                AutoplayCaptureTriggers.TryTakeReady(out AutoplayCaptureTriggers.Request request)
            )
            {
                AutoplayCaptureTriggers.Record(request, $"events/kill-{request.Occurrence}.png");
                taken++;
            }

            Assert.That(taken, Is.EqualTo(AutoplayCaptureTriggers.EveryLimit));
        }

        [Test]
        public void RecordingAFeatureIsWhatArmsTheCamera()
        {
            AutoplayCaptureTriggers.Arm(new[] { AutoplayFeatures.ExperienceDropped });
            SetAutoplayActive(true);

            AutoplayFeatureLog.Record(AutoplayFeatures.ExperienceDropped);

            Assert.That(
                AutoplayCaptureTriggers.TryTakeReady(out AutoplayCaptureTriggers.Request request),
                Is.True
            );
            Assert.That(request.Event, Is.EqualTo(AutoplayFeatures.ExperienceDropped));
            Assert.That(
                AutoplayFeatureLog.ToJson(),
                Does.Contain($"\"{AutoplayFeatures.ExperienceDropped}\":1"),
                "an observed moment is counted in the ledger like any other"
            );
        }

        [Test]
        public void ATriggerThatNeverFiredIsNamedRatherThanForgotten()
        {
            AutoplayCaptureTriggers.Arm(
                new[] { "combat.elite-killed", "combat.enemy-killed", "combat.enemy-killed" }
            );

            AutoplayCaptureTriggers.Notify("combat.enemy-killed", 1);
            Assert.That(
                AutoplayCaptureTriggers.TryTakeReady(out AutoplayCaptureTriggers.Request request),
                Is.True
            );
            AutoplayCaptureTriggers.Record(request, "events/kill.png");

            Assert.That(
                AutoplayCaptureTriggers.Unfired(),
                Is.EqualTo(new[] { "combat.elite-killed" })
            );
        }
    }
}
