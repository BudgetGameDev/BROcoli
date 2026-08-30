using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class SprayParticleQualityTests
    {
        [Test]
        public void SprayEmissionIsDenseEnoughToReadAsAContinuousPlume()
        {
            Assert.That(SpraySettings.VisualEmissionRate, Is.GreaterThanOrEqualTo(500));
            Assert.That(
                SpraySettings.VisualEmissionRate,
                Is.GreaterThan(SpraySettings.EmissionRate * 10)
            );
        }

        [Test]
        public void VisualDensityDoesNotIncreaseDamagingCoreParticleRate()
        {
            GameObject root = new GameObject("Layered Spray Rate Test");
            try
            {
                var layers = new SprayParticleLayers(root.transform);
                layers.CreateAllLayers();
                int visualCount = Mathf.RoundToInt(
                    SpraySettings.VisualEmissionRate * SpraySettings.BurstDuration * 1.5f
                );

                layers.PlayBurst(visualCount);

                int coreCount = Mathf.RoundToInt(
                    SpraySettings.EmissionRate * SpraySettings.BurstDuration * 1.5f
                );
                int initialCount = Mathf.Clamp(Mathf.RoundToInt(coreCount * 0.03f), 1, 4);
                float expectedRate =
                    (coreCount - initialCount) / SpraySettings.BurstDuration;
                Assert.That(
                    layers.CoreSpray.emission.rateOverTime.constant,
                    Is.EqualTo(expectedRate).Within(0.001f)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MistMaterialPreservesTheSoftParticleAlphaMask()
        {
            Material material = SprayMaterialCreator.GetSprayMistMaterial();
            Assert.That(
                material.GetInt("_SrcBlend"),
                Is.EqualTo((int)UnityEngine.Rendering.BlendMode.SrcAlpha)
            );
            Assert.That(
                material.GetInt("_DstBlend"),
                Is.EqualTo((int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
            );
        }

        [Test]
        public void CoreCollisionUsesExplicitHighQualityParticleConsumption()
        {
            GameObject root = new GameObject("Spray Test Root");
            try
            {
                ParticleSystem core = SprayLayerCore.Create(root.transform, Texture2D.whiteTexture);
                ParticleSystem.CollisionModule collision = core.collision;

                Assert.That(collision.enabled, Is.True);
                Assert.That(collision.sendCollisionMessages, Is.True);
                Assert.That(collision.quality, Is.EqualTo(ParticleSystemCollisionQuality.High));
                Assert.That(collision.enableDynamicColliders, Is.True);
                Assert.That(collision.lifetimeLoss.constant, Is.Zero.Within(0.0001f));
                Assert.That(core.GetComponent<SprayParticleCollisionHandler>(), Is.Not.Null);
                Assert.That(core.shape.radius, Is.Zero.Within(0.0001f));
                Assert.That(
                    core.GetComponent<ParticleSystemRenderer>().renderMode,
                    Is.EqualTo(ParticleSystemRenderMode.Billboard)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CollisionHandlerRemovesOneNearestParticlePerContact()
        {
            GameObject root = new GameObject("Spray Collision Test");
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 1f;
                main.maxParticles = 8;
                var emission = particles.emission;
                emission.enabled = false;

                SprayParticleCollisionHandler handler =
                    root.AddComponent<SprayParticleCollisionHandler>();
                particles.Play();
                EmitAt(particles, new Vector3(1f, 0f, 0f));
                EmitAt(particles, new Vector3(4f, 0f, 0f));
                particles.Simulate(0.01f, true, false, true);

                int removed = handler.ConsumeParticlesNear(
                    new List<Vector3> { new Vector3(1f, 0f, 0f) }
                );

                Assert.That(removed, Is.EqualTo(1));
                Assert.That(particles.particleCount, Is.EqualTo(1));
                var remaining = new ParticleSystem.Particle[1];
                particles.GetParticles(remaining);
                Assert.That(remaining[0].position.x, Is.EqualTo(4f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DropletLayerUsesHighQualityCollisionWithoutProjectileStreaks()
        {
            GameObject root = new GameObject("Droplet Test Root");
            try
            {
                ParticleSystem droplets = SprayLayerDroplet.Create(
                    root.transform,
                    Texture2D.whiteTexture
                );

                Assert.That(
                    droplets.collision.quality,
                    Is.EqualTo(ParticleSystemCollisionQuality.High)
                );
                Assert.That(
                    droplets.collision.lifetimeLoss.constant,
                    Is.EqualTo(1f).Within(0.0001f)
                );
                Assert.That(droplets.trails.enabled, Is.False);
                Assert.That(
                    droplets.GetComponent<ParticleSystemRenderer>().renderMode,
                    Is.EqualTo(ParticleSystemRenderMode.Billboard)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EmitAt(ParticleSystem particles, Vector3 position)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = 1f,
                startSize = 0.05f,
            };
            particles.Emit(emit, 1);
        }
    }
}
