using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public partial class HdrTorchFlamePresentationTests
    {
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
        public void GradientVariantsAndInvalidFadesUseSafeAlphaFallbacks()
        {
            GameObject root = new("Torch gradient variants");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                Gradient low = new();
                low.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f) },
                    new[] { new GradientAlphaKey(0.2f, 0f) }
                );
                Gradient high = new();
                high.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f) },
                    new[] { new GradientAlphaKey(0.8f, 0f) }
                );
                ParticleSystem.MainModule main = particles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(low, high);
                Assert.That(
                    HdrTorchFlamePresentation.PeakParticleAlpha(particles),
                    Is.EqualTo(0.8f).Within(0.001f)
                );

                ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
                fade.enabled = true;
                fade.color = new ParticleSystem.MinMaxGradient(Color.black, Color.white);
                root.AddComponent<HdrTorchFlamePresentation>().SteepenFade(particles, 2f);

                Gradient empty = new();
                empty.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f) },
                    new[] { new GradientAlphaKey(0f, 0f) }
                );
                Assert.That(HdrTorchFlamePresentation.Steepen(empty, 2f), Is.SameAs(empty));
                Assert.That(
                    HdrTorchFlamePresentation.PeakAlpha(
                        main.startColor,
                        (ParticleSystemGradientMode)999
                    ),
                    Is.EqualTo(1f)
                );
                Assert.That(
                    HdrTorchFlamePresentation.TryReadGradientForTests(
                        new ParticleSystem.MinMaxGradient(low, high),
                        out Gradient selected
                    ),
                    Is.True
                );
                Assert.That(selected, Is.SameAs(high));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
