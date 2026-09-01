using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Tests
{
    public class AcesToneScaleTests
    {
        private const float PaperWhite = 200f;

        [Test]
        public void DiffuseWhiteIsDisplayedNearPaperWhite()
        {
            float nits = AcesToneScale.DisplayNits(1f, PaperWhite, HDRACESPreset.ACES1000Nits);

            Assert.That(nits, Is.EqualTo(PaperWhite).Within(PaperWhite * 0.15f));
        }

        [Test]
        public void ShadowsStayFarBelowTheLinearScaleNeutralToneMappingWouldApply()
        {
            // The dungeon's dark surfaces sit around here. Scaling them straight into nits, as
            // neutral tone mapping does on an HDR swapchain, is what washed the picture out.
            float nits = AcesToneScale.DisplayNits(0.02f, PaperWhite, HDRACESPreset.ACES1000Nits);

            Assert.That(nits, Is.LessThan(0.02f * PaperWhite * 0.5f));
        }

        [Test]
        public void PresetsShareTheirToneScaleThroughTheDarkScene()
        {
            // Only the shoulder differs between presets, so raising the preset for a brighter
            // display cannot brighten the dungeon.
            foreach (float sceneValue in new[] { 0.02f, 0.05f, 0.18f })
            {
                float reference = AcesToneScale.DisplayNits(
                    sceneValue,
                    PaperWhite,
                    HDRACESPreset.ACES1000Nits
                );
                Assert.That(
                    AcesToneScale.DisplayNits(sceneValue, PaperWhite, HDRACESPreset.ACES4000Nits),
                    Is.EqualTo(reference).Within(reference * 0.02f),
                    $"scene value {sceneValue}"
                );
            }
        }

        [Test]
        public void ToneScaleIsMonotonic()
        {
            float previous = -1f;
            for (float sceneValue = 0f; sceneValue < 58f; sceneValue += 0.25f)
            {
                float nits = AcesToneScale.DisplayNits(
                    sceneValue,
                    PaperWhite,
                    HDRACESPreset.ACES1000Nits
                );
                Assert.That(nits, Is.GreaterThan(previous), $"scene value {sceneValue}");
                previous = nits;
            }
        }

        [Test]
        public void PaperWhiteScalesTheSceneRatherThanTheCurve()
        {
            // Paper white multiplies the scene before the tone scale, so twice the paper white
            // needs half the scene value for the same luminance.
            float atTwoHundred = AcesToneScale.SceneValueForNits(
                600f,
                200f,
                HDRACESPreset.ACES1000Nits
            );
            float atFourHundred = AcesToneScale.SceneValueForNits(
                600f,
                400f,
                HDRACESPreset.ACES1000Nits
            );

            Assert.That(
                atFourHundred,
                Is.EqualTo(atTwoHundred * 0.5f).Within(atTwoHundred * 0.02f)
            );
        }

        [Test]
        public void SceneValueForNitsRoundTripsThroughTheToneScale()
        {
            foreach (float nits in new[] { 1f, 50f, 200f, 600f })
            {
                float sceneValue = AcesToneScale.SceneValueForNits(
                    nits,
                    PaperWhite,
                    HDRACESPreset.ACES1000Nits
                );
                Assert.That(
                    AcesToneScale.DisplayNits(sceneValue, PaperWhite, HDRACESPreset.ACES1000Nits),
                    Is.EqualTo(nits).Within(nits * 0.01f),
                    $"{nits} nits"
                );
            }
        }

        [Test]
        public void SceneValueForNitsSaturatesRatherThanExceedingTheGradingRange()
        {
            float sceneValue = AcesToneScale.SceneValueForNits(
                999f,
                PaperWhite,
                HDRACESPreset.ACES1000Nits
            );

            Assert.That(sceneValue, Is.EqualTo(AcesToneScale.MaximumSceneValue));
        }

        [Test]
        public void SceneColorForPeakNitsPutsTheBrightestPrimaryOnThePeak()
        {
            Color hue = new(1f, 0.451f, 0.053f);

            Color scene = AcesToneScale.SceneColorForPeakNits(
                hue,
                600f,
                PaperWhite,
                HDRACESPreset.ACES1000Nits
            );

            Vector3 nits = AcesToneScale.DisplayNits(
                new Vector3(scene.r, scene.g, scene.b),
                PaperWhite,
                HDRACESPreset.ACES1000Nits
            );
            Assert.That(nits.x, Is.EqualTo(600f).Within(6f));
            Assert.That(nits.y, Is.LessThan(nits.x), "the flame keeps its hue at the peak");
            Assert.That(nits.z, Is.LessThan(nits.y));
            Assert.That(scene.r, Is.GreaterThan(scene.g));
        }

        [Test]
        public void SceneColorForPeakNitsKeepsTheHueDirection()
        {
            Color hue = new(1f, 0.451f, 0.053f);

            Color scene = AcesToneScale.SceneColorForPeakNits(
                hue,
                600f,
                PaperWhite,
                HDRACESPreset.ACES1000Nits
            );

            Assert.That(scene.g / scene.r, Is.EqualTo(hue.g).Within(0.001f));
            Assert.That(scene.b / scene.r, Is.EqualTo(hue.b).Within(0.001f));
            Assert.That(scene.a, Is.EqualTo(1f));
        }

        [Test]
        public void SelectPresetLeavesShoulderHeadroomAboveTheCalibratedPeak()
        {
            Assert.That(AcesToneScale.SelectPreset(600f), Is.EqualTo(HDRACESPreset.ACES1000Nits));
            Assert.That(AcesToneScale.SelectPreset(800f), Is.EqualTo(HDRACESPreset.ACES1000Nits));
            Assert.That(AcesToneScale.SelectPreset(900f), Is.EqualTo(HDRACESPreset.ACES2000Nits));
            Assert.That(AcesToneScale.SelectPreset(2000f), Is.EqualTo(HDRACESPreset.ACES4000Nits));
        }

        [Test]
        public void EveryPresetCanReachThePeakItIsSelectedFor()
        {
            foreach (float peak in new[] { 200f, 600f, 1000f, 1500f, 2000f })
            {
                HDRACESPreset preset = AcesToneScale.SelectPreset(peak);
                float sceneValue = AcesToneScale.SceneValueForNits(peak, PaperWhite, preset);

                Assert.That(
                    sceneValue,
                    Is.LessThan(AcesToneScale.MaximumSceneValue),
                    $"{peak} nits"
                );
                Assert.That(
                    AcesToneScale.DisplayNits(sceneValue, PaperWhite, preset),
                    Is.EqualTo(peak).Within(peak * 0.01f),
                    $"{peak} nits"
                );
            }
        }
    }
}
