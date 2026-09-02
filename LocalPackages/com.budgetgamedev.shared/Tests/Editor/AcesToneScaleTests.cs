using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Tests
{
    public class AcesToneScaleTests
    {
        private const float PaperWhite = 200f;

        [Test]
        public void DiffuseWhiteIsDisplayedNearPaperWhite()
        {
            float nits = AcesToneScale.DisplayNits(1f, PaperWhite, HDRRangeReduction.ACES1000Nits);

            Assert.That(nits, Is.EqualTo(PaperWhite).Within(PaperWhite * 0.15f));
        }

        [Test]
        public void ShadowsStayFarBelowTheLinearScaleNeutralToneMappingWouldApply()
        {
            // The dungeon's dark surfaces sit around here. Scaling them straight into nits, as
            // neutral tone mapping does on an HDR swapchain, is what washed the picture out.
            float nits = AcesToneScale.DisplayNits(
                0.02f,
                PaperWhite,
                HDRRangeReduction.ACES1000Nits
            );

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
                    HDRRangeReduction.ACES1000Nits
                );
                Assert.That(
                    AcesToneScale.DisplayNits(
                        sceneValue,
                        PaperWhite,
                        HDRRangeReduction.ACES4000Nits
                    ),
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
                    HDRRangeReduction.ACES1000Nits
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
                HDRRangeReduction.ACES1000Nits
            );
            float atFourHundred = AcesToneScale.SceneValueForNits(
                600f,
                400f,
                HDRRangeReduction.ACES1000Nits
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
                    HDRRangeReduction.ACES1000Nits
                );
                Assert.That(
                    AcesToneScale.DisplayNits(
                        sceneValue,
                        PaperWhite,
                        HDRRangeReduction.ACES1000Nits
                    ),
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
                HDRRangeReduction.ACES1000Nits
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
                HDRRangeReduction.ACES1000Nits
            );

            Vector3 nits = AcesToneScale.DisplayNits(
                new Vector3(scene.r, scene.g, scene.b),
                PaperWhite,
                HDRRangeReduction.ACES1000Nits
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
                HDRRangeReduction.ACES1000Nits
            );

            Assert.That(scene.g / scene.r, Is.EqualTo(hue.g).Within(0.001f));
            Assert.That(scene.b / scene.r, Is.EqualTo(hue.b).Within(0.001f));
            Assert.That(scene.a, Is.EqualTo(1f));
        }

        [Test]
        public void SceneColorForDisplayNitsInvertsTheToneMap()
        {
            // The emerald the menus are built from, and the surface behind it.
            foreach (Color authored in new[] { new Color(0.06f, 0.35f, 0.07f), Color.white })
            {
                Vector3 target = new Vector3(authored.r, authored.g, authored.b) * PaperWhite;

                Vector3 scene = AcesToneScale.SceneColorForDisplayNits(
                    target,
                    PaperWhite,
                    HDRRangeReduction.ACES1000Nits
                );
                Vector3 rendered = AcesToneScale.DisplayNits(
                    scene,
                    PaperWhite,
                    HDRRangeReduction.ACES1000Nits
                );

                Assert.That(
                    rendered.x,
                    Is.EqualTo(target.x).Within(Mathf.Max(target.x, 1f) * 0.01f)
                );
                Assert.That(
                    rendered.y,
                    Is.EqualTo(target.y).Within(Mathf.Max(target.y, 1f) * 0.01f)
                );
                Assert.That(
                    rendered.z,
                    Is.EqualTo(target.z).Within(Mathf.Max(target.z, 1f) * 0.01f)
                );
            }
        }

        [Test]
        public void CompensatingAColourBrightensItRatherThanLeavingItToTheToe()
        {
            Vector3 target = new Vector3(0.06f, 0.35f, 0.07f) * PaperWhite;

            Vector3 scene = AcesToneScale.SceneColorForDisplayNits(
                target,
                PaperWhite,
                HDRRangeReduction.ACES1000Nits
            );

            Assert.That(scene.y, Is.GreaterThan(0.35f), "the toe has to be undone, not reinforced");
        }

        [Test]
        public void ContrastPivotsOnMiddleGreyAndInvertsCleanly()
        {
            Vector3 shadow = new(0.02f, 0.02f, 0.02f);
            Vector3 highlight = new(1f, 1f, 1f);

            Vector3 darkened = AcesToneScale.ApplyContrast(shadow, 1.17f);
            Vector3 brightened = AcesToneScale.ApplyContrast(highlight, 1.17f);

            Assert.That(darkened.x, Is.LessThan(shadow.x), "contrast crushes below middle grey");
            Assert.That(brightened.x, Is.GreaterThan(highlight.x), "and lifts above it");

            // Content authored for a display luminance is pre-compensated with the inverse, so
            // the two have to cancel.
            foreach (Vector3 sample in new[] { shadow, highlight, new Vector3(0.18f, 0.4f, 0.05f) })
            {
                Vector3 undone = AcesToneScale.ApplyContrast(sample, 1f / 1.17f);
                Vector3 roundTripped = AcesToneScale.ApplyContrast(undone, 1.17f);
                Assert.That(roundTripped.x, Is.EqualTo(sample.x).Within(sample.x * 0.02f + 1e-4f));
                Assert.That(roundTripped.y, Is.EqualTo(sample.y).Within(sample.y * 0.02f + 1e-4f));
                Assert.That(roundTripped.z, Is.EqualTo(sample.z).Within(sample.z * 0.02f + 1e-4f));
            }
        }

        [Test]
        public void MiddleGreyIsTheContrastPivot()
        {
            Vector3 grey = new(0.18f, 0.18f, 0.18f);

            Vector3 graded = AcesToneScale.ApplyContrast(grey, 1.4f);

            Assert.That(graded.x, Is.EqualTo(0.18f).Within(0.01f));
        }

        [Test]
        public void SelectPresetLeavesShoulderHeadroomAboveTheCalibratedPeak()
        {
            Assert.That(
                AcesToneScale.SelectPreset(600f),
                Is.EqualTo(HDRRangeReduction.ACES1000Nits)
            );
            Assert.That(
                AcesToneScale.SelectPreset(800f),
                Is.EqualTo(HDRRangeReduction.ACES1000Nits)
            );
            Assert.That(
                AcesToneScale.SelectPreset(900f),
                Is.EqualTo(HDRRangeReduction.ACES2000Nits)
            );
            Assert.That(
                AcesToneScale.SelectPreset(2000f),
                Is.EqualTo(HDRRangeReduction.ACES4000Nits)
            );
        }

        [Test]
        public void EveryPresetCanReachThePeakItIsSelectedFor()
        {
            foreach (float peak in new[] { 200f, 600f, 1000f, 1500f, 2000f })
            {
                HDRRangeReduction preset = AcesToneScale.SelectPreset(peak);
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
