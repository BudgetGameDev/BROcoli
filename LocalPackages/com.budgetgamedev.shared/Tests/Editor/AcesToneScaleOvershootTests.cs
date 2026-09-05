using NUnit.Framework;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Tests
{
    public partial class AcesToneScaleTests
    {
        [TestCase(600f, 200f, HDRRangeReduction.ACES1000Nits)]
        [TestCase(775f, 200f, HDRRangeReduction.ACES2000Nits)]
        [TestCase(800f, 80f, HDRRangeReduction.ACES2000Nits)]
        [TestCase(1500f, 200f, HDRRangeReduction.ACES4000Nits)]
        [TestCase(2000f, 200f, HDRRangeReduction.ACES4000Nits)]
        public void CalibratedPresetCanReachTheAuthoredHighlightOvershoot(
            float peakNits,
            float paperWhiteNits,
            HDRRangeReduction expected
        )
        {
            float target = peakNits * GameDisplaySettings.HighlightOvershoot;
            HDRRangeReduction preset = AcesToneScale.SelectPreset(
                peakNits,
                paperWhiteNits,
                GameDisplaySettings.HighlightOvershoot
            );
            float scene = AcesToneScale.SceneValueForNits(target, paperWhiteNits, preset);

            Assert.That(preset, Is.EqualTo(expected));
            Assert.That(scene, Is.LessThan(AcesToneScale.MaximumSceneValue));
            Assert.That(
                AcesToneScale.DisplayNits(scene, paperWhiteNits, preset),
                Is.EqualTo(target).Within(target * 0.01f),
                "validate scene-linear values in nits, without an SDR screenshot or 8-bit clamp"
            );
        }

        [Test]
        public void UnreachableOvershootUsesTheLargestPresetWithoutExceedingTheLutRange()
        {
            HDRRangeReduction preset = AcesToneScale.SelectPreset(2000f, 80f, 1.3f);
            float scene = AcesToneScale.SceneValueForNits(2600f, 80f, preset);

            Assert.That(preset, Is.EqualTo(HDRRangeReduction.ACES4000Nits));
            Assert.That(scene, Is.EqualTo(AcesToneScale.MaximumSceneValue));
            Assert.That(AcesToneScale.DisplayNits(scene, 80f, preset), Is.GreaterThan(2000f));
        }
    }
}
