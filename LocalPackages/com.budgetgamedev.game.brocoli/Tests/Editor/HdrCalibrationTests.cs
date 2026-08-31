using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class HdrCalibrationTests
    {
        [TestCase(0f)]
        [TestCase(0.0001f)]
        [TestCase(0.0005f)]
        [TestCase(0.005f)]
        [TestCase(0.05f)]
        public void BlackLevelSliderRoundTrips(float nits)
        {
            float slider = ResponsiveMainMenuLayout.BlackLevelToSlider(nits);
            float result = ResponsiveMainMenuLayout.SliderToBlackLevel(slider);

            Assert.That(result, Is.EqualTo(nits).Within(0.000001f));
        }

        [Test]
        public void BlackLevelSliderUsesLogarithmicRange()
        {
            float oledBlack = ResponsiveMainMenuLayout.BlackLevelToSlider(0.0005f);
            float raisedBlack = ResponsiveMainMenuLayout.BlackLevelToSlider(0.01f);

            Assert.That(oledBlack, Is.InRange(0f, 1f));
            Assert.That(raisedBlack, Is.GreaterThan(oledBlack));
            Assert.That(ResponsiveMainMenuLayout.SliderToBlackLevel(0f), Is.Zero);
            Assert.That(
                ResponsiveMainMenuLayout.SliderToBlackLevel(1f),
                Is.EqualTo(0.05f).Within(0.000001f)
            );
        }
    }
}
