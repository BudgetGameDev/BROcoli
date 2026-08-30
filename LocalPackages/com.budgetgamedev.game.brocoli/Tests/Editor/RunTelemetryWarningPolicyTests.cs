using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class RunTelemetryWarningPolicyTests
    {
        [TestCase(0, 0, 0, true)]
        [TestCase(1, 0, 0, false)]
        [TestCase(0, 1, 0, false)]
        [TestCase(0, 0, 1, false)]
        public void AutoplayRequiresCleanRuntimeLogs(
            int warnings,
            int errors,
            int exceptions,
            bool expected
        )
        {
            Assert.That(
                RunTelemetry.LogsAreClean(warnings, errors, exceptions),
                Is.EqualTo(expected)
            );
        }
    }
}
