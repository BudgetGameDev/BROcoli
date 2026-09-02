using NUnit.Framework;

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
        }
    }
}
