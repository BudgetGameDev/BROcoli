using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// What each scenario actually grades. The scenarios are the harness's
    /// contract with whoever reads an exit code, and they disagree with each
    /// other on purpose: surviving passes one and is beside the point in three.
    /// </summary>
    public sealed partial class RunTelemetryTests
    {
        [Test]
        public void CleanScenarioPoliciesCoverSurvivalAndProgress()
        {
            Assert.That(
                (bool)Invoke("EvaluateScenario", "duration", NothingMissing, NoFindings),
                Is.True
            );
            SetConfigScenario("survive");
            Assert.That(
                (bool)Invoke("EvaluateScenario", "duration", NothingMissing, NoFindings),
                Is.True
            );
            Assert.That(
                (bool)Invoke("EvaluateScenario", "gameover", NothingMissing, NoFindings),
                Is.False
            );
            SetConfigScenario("progress");
            SetConfigValue("MinLevel", 0);
            Assert.That(
                (bool)Invoke("EvaluateScenario", "gameover", NothingMissing, NoFindings),
                Is.True
            );
            SetConfigValue("MinLevel", 1);
            Assert.That(
                (bool)Invoke("EvaluateScenario", "gameover", NothingMissing, NoFindings),
                Is.False
            );

            SetConfigScenario("coverage");
            Assert.That(
                (bool)Invoke("EvaluateScenario", "gameover", NothingMissing, NoFindings),
                Is.True,
                "reaching every system passes even if the agent then died"
            );
            Assert.That(
                (bool)Invoke("EvaluateScenario", "duration", SomethingMissing, NoFindings),
                Is.False,
                "surviving is not enough when a system was never reached"
            );

            SetConfigScenario("balance");
            Assert.That(
                (bool)Invoke("EvaluateScenario", "gameover", SomethingMissing, NoFindings),
                Is.True,
                "a balance run is graded on difficulty, not on coverage or survival"
            );
            Assert.That(
                (bool)Invoke("EvaluateScenario", "duration", NothingMissing, OutOfBand),
                Is.False,
                "a run outside the difficulty band is the whole point of the scenario"
            );

            SetConfigScenario(AutoplayFeatures.JourneyScenario);
            Assert.That(
                (bool)Invoke("EvaluateScenario", "journey", NothingMissing, OutOfBand),
                Is.True,
                "a journey is graded on its steps, not on the difficulty it met on the way"
            );
            Assert.That(
                (bool)Invoke("EvaluateScenario", "duration", SomethingMissing, NoFindings),
                Is.False,
                "lasting the whole run is not the same as having been through the journey"
            );
        }

        /// <summary>
        /// A journey is a fixed list of things to do rather than a length of time, so
        /// the run ends on the last one. Playing on afterwards would only be the bot
        /// filling the remaining minutes -- the death the tier exists for has already
        /// happened by then.
        /// </summary>
        [Test]
        public void AJourneyRunEndsWhenItHasBeenEverywhereItSetOutToGo()
        {
            SetConfigScenario(AutoplayFeatures.JourneyScenario);
            SetConfigValue("Duration", 1000f);
            SetAutoplayActive(true);
            try
            {
                Invoke("Update");
                Assert.That(
                    File.Exists(Path.Combine(output, "summary.json")),
                    Is.False,
                    "the journey has not been anywhere yet"
                );

                foreach (string reached in AutoplayFeatures.SaveJourney)
                    AutoplayFeatureLog.Record(reached);

                LogAssert.Expect(
                    LogType.Log,
                    new Regex("^\\[Autoplay\\] Run ended \\(journey\\).+passed=True")
                );
                Invoke("Update");
            }
            finally
            {
                SetAutoplayActive(false);
                AutoplayFeatureLog.Reset();
            }

            Assert.That(
                File.ReadAllText(Path.Combine(output, "summary.json")),
                Does.Contain("\"reason\":\"journey\"")
            );
        }

        private static void SetAutoplayActive(bool active) =>
            typeof(AutoplayController)
                .GetField("<IsActive>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, active);
    }
}
