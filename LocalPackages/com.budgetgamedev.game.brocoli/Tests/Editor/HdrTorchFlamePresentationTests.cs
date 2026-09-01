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
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
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
                primaryMaterial = new Material(
                    Shader.Find("Universal Render Pipeline/Particles/Unlit")
                )
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
                Vector3 nits = AcesToneScale.DisplayNits(
                    new Vector3(material.r, material.g, material.b) * alpha,
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
