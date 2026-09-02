using BudgetGameDev.Shared;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public class PointerRevealPolicyTests
    {
        [Test]
        public void AScreenThatIsClickedHoldsThePointerHoweverLongItIsOpen()
        {
            Assert.That(PointerRevealPolicy.ShouldShowPointer(true, 0f), Is.True);
            Assert.That(
                PointerRevealPolicy.ShouldShowPointer(
                    true,
                    PointerRevealPolicy.RevealSeconds * 20f
                ),
                Is.True,
                "an inventory left open must not lose its pointer to a timer"
            );
        }

        [Test]
        public void MovingTheMouseRevealsThePointerForExactlyTheRevealWindow()
        {
            Assert.That(PointerRevealPolicy.ShouldShowPointer(false, 0f), Is.True);
            Assert.That(
                PointerRevealPolicy.ShouldShowPointer(
                    false,
                    PointerRevealPolicy.RevealSeconds - 0.01f
                ),
                Is.True
            );
            Assert.That(
                PointerRevealPolicy.ShouldShowPointer(false, PointerRevealPolicy.RevealSeconds),
                Is.False,
                "the reveal ends at five seconds, not after them"
            );
            Assert.That(PointerRevealPolicy.ShouldShowPointer(false, 60f), Is.False);
        }

        [Test]
        public void TheRevealIsTheFiveSecondsTheGameWasAskedFor()
        {
            Assert.That(PointerRevealPolicy.RevealSeconds, Is.EqualTo(5f));
            Assert.That(
                PointerRevealPolicy.MovementThresholdPixels,
                Is.GreaterThan(0f),
                "a threshold of zero would let a still mouse's own jitter hold the pointer on"
            );
        }
    }
}
