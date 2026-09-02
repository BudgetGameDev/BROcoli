using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The ledger is what turns "the bot played for a while" into "these systems
    /// were reached", so it has to stay inert during ordinary play and honest about
    /// what a run missed.
    /// </summary>
    public sealed class AutoplayFeatureLedgerTests
    {
        [SetUp]
        [TearDown]
        public void ClearLedger()
        {
            SetAutoplayActive(false);
            AutoplayFeatureLog.Reset();
        }

        private static void SetAutoplayActive(bool active) =>
            typeof(AutoplayController)
                .GetField("<IsActive>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, active);

        [Test]
        public void RecordingIsInertUntilARunIsDriving()
        {
            AutoplayFeatureLog.Record(AutoplayFeatures.EnemyKilled);
            Assert.That(AutoplayFeatureLog.Count(AutoplayFeatures.EnemyKilled), Is.Zero);

            SetAutoplayActive(true);
            AutoplayFeatureLog.Record(AutoplayFeatures.EnemyKilled);
            AutoplayFeatureLog.Record(AutoplayFeatures.EnemyKilled);

            Assert.That(AutoplayFeatureLog.Count(AutoplayFeatures.EnemyKilled), Is.EqualTo(2));
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.EnemyKilled), Is.True);
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.ChestOpened), Is.False);
        }

        [Test]
        public void AnEmptyFeatureNameIsIgnored()
        {
            SetAutoplayActive(true);

            AutoplayFeatureLog.Record(null);
            AutoplayFeatureLog.Record(string.Empty);

            Assert.That(AutoplayFeatureLog.Missing(), Is.EqualTo(AutoplayFeatures.Required));
        }

        [Test]
        public void ConditionalRecordingFollowsItsCondition()
        {
            SetAutoplayActive(true);

            AutoplayFeatureLog.RecordIf(false, AutoplayFeatures.EliteKilled);
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.EliteKilled), Is.False);

            AutoplayFeatureLog.RecordIf(true, AutoplayFeatures.EliteKilled);
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.EliteKilled), Is.True);
        }

        [Test]
        public void EveryRequiredFeatureIsMissingUntilItActuallyHappens()
        {
            SetAutoplayActive(true);
            Assert.That(AutoplayFeatureLog.Missing(), Is.EqualTo(AutoplayFeatures.Required));

            foreach (string feature in AutoplayFeatures.Required)
                AutoplayFeatureLog.Record(feature);

            Assert.That(AutoplayFeatureLog.Missing(), Is.Empty);
        }

        [Test]
        public void TheRenderedLedgerCoversTheWholeCatalogue()
        {
            SetAutoplayActive(true);
            AutoplayFeatureLog.Record(AutoplayFeatures.MapPanned);

            string json = AutoplayFeatureLog.ToJson();

            Assert.That(json, Does.StartWith("{").And.EndWith("}"));
            Assert.That(json, Does.Contain($"\"{AutoplayFeatures.MapPanned}\":1"));
            foreach (string feature in Catalogue())
                Assert.That(json, Does.Contain($"\"{feature}\":"), feature);
        }

        private static IEnumerable<string> Catalogue()
        {
            foreach (string feature in AutoplayFeatures.Required)
                yield return feature;
            foreach (string feature in AutoplayFeatures.Optional)
                yield return feature;
        }
    }
}
