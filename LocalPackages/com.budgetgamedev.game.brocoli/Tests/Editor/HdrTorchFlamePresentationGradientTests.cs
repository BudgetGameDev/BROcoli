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
    }
}
