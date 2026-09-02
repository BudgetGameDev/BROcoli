using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// The light spec exists so a torch lights the stone to the same luminance on both front
    /// ends. These check the photometry that claim rests on, because the two pipelines reach
    /// it by different routes: High Definition is told lumens and divides by pi itself, while
    /// Universal is told a graded number that already has the pi folded in.
    /// </summary>
    public sealed class PunctualLightSpecTests
    {
        private const float PaperWhite = 200f;

        [Test]
        public void ReferenceIlluminanceInvertsTheLambertianReflection()
        {
            var spec = new PunctualLightSpec(30f, 2f, 9f, Color.white);

            // A Lambertian surface of albedo p under illuminance E has luminance E*p/pi.
            float luminance =
                spec.ReferenceIlluminanceLux * PunctualLightSpec.ReferenceAlbedo / Mathf.PI;

            Assert.That(luminance, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void LuminousIntensityFallsOffWithTheSquareOfDistance()
        {
            var near = new PunctualLightSpec(30f, 2f, 9f, Color.white);
            var far = new PunctualLightSpec(30f, 4f, 9f, Color.white);

            Assert.That(
                far.LuminousIntensityCandela,
                Is.EqualTo(near.LuminousIntensityCandela * 4f).Within(1e-2f),
                "Twice the distance for the same luminance needs four times the intensity."
            );
        }

        [Test]
        public void LumensAreTheIntensityOverTheWholeSphere()
        {
            var spec = new PunctualLightSpec(30f, 2f, 9f, Color.white);

            Assert.That(
                spec.LuminousFluxLumens,
                Is.EqualTo(spec.LuminousIntensityCandela * 4f * Mathf.PI).Within(1e-2f)
            );
        }

        [Test]
        public void BothPipelinesLightTheReferenceSurfaceToTheSameLuminance()
        {
            var spec = new PunctualLightSpec(30f, 2f, 9f, Color.white);
            float albedo = PunctualLightSpec.ReferenceAlbedo;
            float distanceSquared = spec.ReferenceDistanceMeters * spec.ReferenceDistanceMeters;

            // High Definition: lumens back to candela, then illuminance, then a Lambertian
            // surface, which is the pipeline's own physical chain.
            float candela = spec.LuminousFluxLumens / (4f * Mathf.PI);
            float highDefinitionNits = candela / distanceSquared * albedo / Mathf.PI;

            // Universal: albedo * intensity / d^2 in graded units, where 1.0 is paper white.
            float universalNits =
                albedo * spec.UniversalIntensity(PaperWhite) / distanceSquared * PaperWhite;

            Assert.That(highDefinitionNits, Is.EqualTo(30f).Within(1e-2f));
            Assert.That(universalNits, Is.EqualTo(30f).Within(1e-2f));
            Assert.That(
                universalNits,
                Is.EqualTo(highDefinitionNits).Within(1e-2f),
                "The two front ends must not drift apart; that is the whole point of the spec."
            );
        }

        [Test]
        public void TheTorchLightsStoneToTheLadderRatherThanToDiffuseWhite()
        {
            SceneLuminanceBudget budget = SceneLuminanceBudget.Dungeon;
            PunctualLightSpec torch = PunctualLightSpec.Torch(budget);

            Assert.That(torch.TargetLuminanceNits, Is.EqualTo(budget.TorchLitStoneNits));
            Assert.That(
                torch.TargetLuminanceNits,
                Is.LessThan(budget.DiffuseWhiteNits),
                "Torchlight carries the dungeon, so lit stone sits below diffuse white."
            );
            Assert.That(
                torch.TargetLuminanceNits,
                Is.LessThan(budget.FlameBodyNits),
                "The flame must read far hotter than the stone it lights."
            );
        }

        [Test]
        public void UniversalIntensityIsZeroRatherThanInfiniteWithoutAPaperWhite()
        {
            var spec = new PunctualLightSpec(30f, 2f, 9f, Color.white);

            Assert.That(spec.UniversalIntensity(0f), Is.EqualTo(0f));
        }
    }
}
