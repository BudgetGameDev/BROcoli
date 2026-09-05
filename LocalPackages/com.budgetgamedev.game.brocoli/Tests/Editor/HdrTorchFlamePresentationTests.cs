using BudgetGameDev.Games.Brocoli.Rendering;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class HdrTorchFlamePresentationTests
    {
        [Test]
        public void HdrBoostAppliesOnlyToPrimaryFlameRenderer()
        {
            GameObject root = new("Torch");
            Material primaryMaterial = null;
            Material secondaryMaterial = null;
            try
            {
                Shader shader = BrocoliShaders.Resolve(BrocoliShaders.Flame);
                Assert.That(shader, Is.Not.Null);
                primaryMaterial = new Material(shader)
                {
                    name = HdrTorchFlamePresentation.PrimaryMaterialName,
                };
                secondaryMaterial = new Material(shader) { name = "DungeonTorchFireSecondary" };

                ParticleSystemRenderer primaryRenderer = CreateFlameRenderer(
                    root.transform,
                    "Flames",
                    primaryMaterial
                );
                ParticleSystemRenderer secondaryRenderer = CreateFlameRenderer(
                    root.transform,
                    "Flames Secondary",
                    secondaryMaterial
                );
                var presentation = root.AddComponent<HdrTorchFlamePresentation>();

                presentation.SetHdrPresentation(true);

                var propertyBlock = new MaterialPropertyBlock();
                primaryRenderer.GetPropertyBlock(propertyBlock);
                Color primaryColor = propertyBlock.GetColor("_BaseColor");
                Assert.That(primaryColor.r, Is.GreaterThan(1f), "the flame outruns diffuse white");
                Assert.That(primaryColor.r, Is.GreaterThan(primaryColor.g));
                Assert.That(primaryColor.g, Is.GreaterThan(primaryColor.b));
                Assert.That(primaryColor.a, Is.EqualTo(1f).Within(0.001f));
                propertyBlock.Clear();
                secondaryRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.clear));

                presentation.SetHdrPresentation(false);
                propertyBlock.Clear();
                primaryRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.clear));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(primaryMaterial);
                Object.DestroyImmediate(secondaryMaterial);
            }
        }

        [Test]
        public void FlameIsAuthoredSoTheHottestParticlesLandOnTheCalibratedPeak()
        {
            GameObject root = new("Torch");
            Material primaryMaterial = null;
            try
            {
                primaryMaterial = new Material(BrocoliShaders.Resolve(BrocoliShaders.Flame))
                {
                    name = HdrTorchFlamePresentation.PrimaryMaterialName,
                };
                ParticleSystemRenderer flameRenderer = CreateFlameRenderer(
                    root.transform,
                    "Flames",
                    primaryMaterial
                );
                ParticleSystem particles = flameRenderer.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.72f, 0.25f, 0.5f),
                    new Color(1f, 0.4f, 0.1f, 0.5f)
                );

                root.AddComponent<HdrTorchFlamePresentation>().SetHdrPresentation(true);

                var propertyBlock = new MaterialPropertyBlock();
                flameRenderer.GetPropertyBlock(propertyBlock);
                Color material = propertyBlock.GetColor("_BaseColor");

                // Additive particles contribute the material colour through their own alpha.
                float alpha = HdrTorchFlamePresentation.PeakParticleAlpha(particles);
                Assert.That(alpha, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(GameDisplaySettings.HighlightOvershoot, Is.GreaterThan(1f));
                // The grade applies its contrast before the tone map, and the material is
                // authored to survive it, so the whole path has to be walked to land on the peak:
                // the white-hot texture sample, then particle alpha, then the grade.
                Vector3 emitted = new(material.r, material.g, material.b);
                Vector3 graded = AcesToneScale.ApplyContrast(
                    emitted * alpha,
                    (GameDisplaySettings.HdrContrastLift / 100f) + 1f
                );
                Vector3 nits = AcesToneScale.DisplayNits(
                    graded,
                    GameDisplaySettings.PaperWhiteNits,
                    GameDisplaySettings.HdrToneMapPreset
                );
                float target =
                    GameDisplaySettings.PeakBrightnessNits * GameDisplaySettings.HighlightOvershoot;
                Assert.That(
                    Mathf.Max(nits.x, Mathf.Max(nits.y, nits.z)),
                    Is.EqualTo(target).Within(target * 0.02f),
                    "the flame is driven past the peak so the panel clips it flat"
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(primaryMaterial);
            }
        }

        [Test]
        public void PeakParticleAlphaFoldsInTheColourOverLifetimeFade()
        {
            GameObject root = new("Torch");
            try
            {
                var particles = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f));

                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                Gradient gradient = new();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f) },
                    new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                fade.color = new ParticleSystem.MinMaxGradient(gradient);

                Assert.That(
                    HdrTorchFlamePresentation.PeakParticleAlpha(particles),
                    Is.EqualTo(0.45f).Within(0.001f)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReapplyingHdrCalibrationDoesNotRepeatedlyCrushTheFlameFade()
        {
            GameObject root = new("Torch");
            try
            {
                var particles = root.AddComponent<ParticleSystem>();
                var presentation = root.AddComponent<HdrTorchFlamePresentation>();
                var fade = particles.colorOverLifetime;
                fade.enabled = true;
                Gradient gradient = new();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                fade.color = new ParticleSystem.MinMaxGradient(gradient);

                presentation.SteepenFade(particles, 4f);
                float first = fade.color.gradient.Evaluate(0.5f).a;
                presentation.SteepenFade(particles, 4f);
                Assert.That(fade.color.gradient.Evaluate(0.5f).a, Is.EqualTo(first).Within(1e-6f));
                presentation.SteepenFade(particles, 2f);
                Assert.That(fade.color.gradient.Evaluate(0.5f).a, Is.GreaterThan(first));
                presentation.SteepenFade(particles, 1f);
                Assert.That(fade.color.gradient.Evaluate(0.5f).a, Is.EqualTo(0.5f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MaterialColourCompensatesForTheParticleAlpha()
        {
            Color material = HdrTorchFlamePresentation.MaterialColorForPeak(
                new Color(4f, 2f, 0.5f, 1f),
                0.5f
            );

            Assert.That(material.r, Is.EqualTo(8f).Within(0.001f));
            Assert.That(material.g, Is.EqualTo(4f).Within(0.001f));
            Assert.That(material.b, Is.EqualTo(1f).Within(0.001f));
            Assert.That(material.a, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SteepeningTheFadeLeavesThePeakAloneAndDropsTheTail()
        {
            Gradient authored = new();
            authored.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );

            // A four times boost should be cancelled by the time a particle is half faded.
            Gradient steepened = HdrTorchFlamePresentation.Steepen(authored, 3f);

            Assert.That(steepened.Evaluate(0f).a, Is.EqualTo(0.9f).Within(0.01f));
            Assert.That(
                steepened.Evaluate(0.5f).a,
                Is.EqualTo(authored.Evaluate(0.5f).a / 4f).Within(0.02f),
                "the faded tail that reads as an orb falls back to its SDR brightness"
            );
            Assert.That(steepened.Evaluate(1f).a, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void BoostIsMeasuredAgainstTheAuthoredMaterial()
        {
            Material authored = new(BrocoliShaders.Resolve(BrocoliShaders.Flame));
            try
            {
                authored.SetColor("_BaseColor", new Color(6f, 1.5f, 0.08f, 1f));

                float boost = HdrTorchFlamePresentation.BoostOver(
                    authored,
                    new Color(24f, 10f, 1f, 1f)
                );

                Assert.That(boost, Is.EqualTo(4f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(authored);
            }
        }

        private static ParticleSystemRenderer CreateFlameRenderer(
            Transform parent,
            string name,
            Material material
        )
        {
            GameObject flame = new(name);
            flame.transform.SetParent(parent);
            var particles = flame.AddComponent<ParticleSystem>();
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
