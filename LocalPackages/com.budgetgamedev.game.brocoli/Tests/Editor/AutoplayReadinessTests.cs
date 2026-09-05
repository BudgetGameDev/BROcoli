using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayReadinessTests
    {
        [TestCase(true, 0f, false, true)]
        [TestCase(true, 0f, true, false)]
        [TestCase(false, 0f, false, false)]
        [TestCase(true, 1f, false, false)]
        public void RestoringAReadinessPausePreservesMenuAndExternalPauseDecisions(
            bool owned,
            float currentScale,
            bool menuPaused,
            bool restore
        )
        {
            Assert.That(
                AutoplayReadiness.ShouldRestoreScale(owned, currentScale, menuPaused),
                Is.EqualTo(restore)
            );
        }
    }
}
