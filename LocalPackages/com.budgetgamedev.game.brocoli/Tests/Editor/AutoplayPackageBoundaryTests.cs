using BudgetGameDev.Autoplay;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayPackageBoundaryTests
    {
        [Test]
        public void ProductionGameDoesNotReferenceAutoplayAndTheCoreReferencesNoGame()
        {
            foreach (var reference in typeof(PlayerInputHandler).Assembly.GetReferencedAssemblies())
                Assert.That(reference.Name, Does.Not.StartWith("BudgetGameDev.Autoplay"));
            foreach (var reference in typeof(UtilitySelection).Assembly.GetReferencedAssemblies())
            {
                Assert.That(reference.Name, Does.Not.StartWith("BudgetGameDev.Games"));
                Assert.That(reference.Name, Is.Not.EqualTo("BudgetGameDev.Hub"));
            }
        }
    }
}
