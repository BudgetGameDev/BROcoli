using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class TorchFireVfxTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TorchReplacesLegacyLayersWithBoundedIndoorFireAndHeat(bool hasWickAnchor)
        {
            var root = new GameObject("Torch");
            var authored = new Material(BrocoliShaders.Resolve(BrocoliShaders.Flame))
            {
                name = HdrTorchFlamePresentation.PrimaryMaterialName,
            };
            try
            {
                var old = new GameObject("Legacy flame");
                old.transform.SetParent(root.transform);
                old.transform.localPosition = new Vector3(0.1f, 2f, 0.4f);
                Vector3 expectedOrigin = old.transform.localPosition;
                if (hasWickAnchor)
                {
                    var anchor = new GameObject("Flame");
                    anchor.transform.SetParent(root.transform, false);
                    anchor.transform.localPosition = new Vector3(0.1f, 2.14f, 0.22f);
                    expectedOrigin = anchor.transform.localPosition;
                }
                var legacyParticles = old.AddComponent<ParticleSystem>();
                var legacyRenderer = old.GetComponent<ParticleSystemRenderer>();
                legacyRenderer.sharedMaterial = authored;
                var fire = root.AddComponent<TorchFireVfx>();
                fire.Initialize();
                fire.Initialize();
                Assert.That(legacyRenderer.enabled, Is.False);
                Assert.That(legacyParticles.isPlaying, Is.False);
                Assert.That(
                    legacyRenderer.sharedMaterial,
                    Is.SameAs(authored),
                    "shared prefab assets stay intact"
                );
                var systems = root.GetComponentsInChildren<ParticleSystem>();
                Assert.That(
                    systems.Length,
                    Is.EqualTo(6),
                    "initialization must not duplicate layers"
                );
                int budget = 0;
                int flames = 0,
                    smoke = 0,
                    embers = 0,
                    heat = 0;
                foreach (var system in systems)
                {
                    if (system == legacyParticles)
                        continue;
                    budget += system.main.maxParticles;
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    Assert.That(
                        renderer.sharedMaterial.shader.name,
                        Is.EqualTo(BrocoliShaders.TorchFire)
                    );
                    var streams = new List<ParticleSystemVertexStream>();
                    renderer.GetActiveVertexStreams(streams);
                    Assert.That(streams, Does.Contain(ParticleSystemVertexStream.StableRandomX));
                    int layer = Mathf.RoundToInt(renderer.sharedMaterial.GetFloat("_Layer"));
                    Assert.That(
                        system.main.simulationSpace,
                        Is.EqualTo(
                            layer == 2
                                ? ParticleSystemSimulationSpace.World
                                : ParticleSystemSimulationSpace.Local
                        )
                    );
                    Assert.That(
                        system.velocityOverLifetime.space,
                        Is.EqualTo(ParticleSystemSimulationSpace.World)
                    );
                    Assert.That(system.main.startColor.colorMax.a, Is.LessThan(1f));
                    switch (layer)
                    {
                        case 0:
                            flames++;
                            Assert.That(
                                renderer.renderMode,
                                Is.EqualTo(ParticleSystemRenderMode.Mesh)
                            );
                            Assert.That(
                                renderer.alignment,
                                Is.EqualTo(ParticleSystemRenderSpace.Local)
                            );
                            Assert.That(
                                renderer.mesh.vertexCount,
                                Is.GreaterThan(12),
                                "A curved flame needs vertical subdivisions, not a pitched camera card."
                            );
                            Assert.That(
                                system.main.startSizeX.constantMin,
                                Is.GreaterThan(0.6f),
                                "The flame covers the half-metre fuel head."
                            );
                            break;
                        case 1:
                            smoke++;
                            break;
                        case 2:
                            embers++;
                            Assert.That(
                                system.main.gravityModifier.constantMin,
                                Is.GreaterThan(0f)
                            );
                            Assert.That(system.collision.enabled, Is.True);
                            Assert.That(system.collision.enableDynamicColliders, Is.False);
                            Assert.That(system.emission.enabled, Is.False);
                            Assert.That(system.main.prewarm, Is.False);
                            break;
                        case 3:
                            heat++;
                            Assert.That(renderer.sortingOrder, Is.LessThan(5));
                            break;
                    }
                }
                Assert.That(budget, Is.EqualTo(TorchFireVfx.ParticleBudget));
                Assert.That(flames, Is.EqualTo(2));
                Assert.That(smoke, Is.EqualTo(1));
                Assert.That(embers, Is.EqualTo(1));
                Assert.That(heat, Is.EqualTo(1));
                var core = root.transform.Find("Fire Core").GetComponent<ParticleSystem>();
                Assert.That(
                    core.velocityOverLifetime.y.constantMax,
                    Is.LessThan(0.02f),
                    "The blue ignition zone stays seated at the wick throughout a flame's life."
                );
                Assert.That(core.noise.strength.constantMax, Is.LessThan(0.01f));
                Assert.That(core.main.startLifetime.constantMin, Is.GreaterThan(2f));
                Assert.That(
                    root.transform.Find("Fire Core").localPosition,
                    Is.EqualTo(expectedOrigin)
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(authored);
            }
        }

        [Test]
        public void AnUnrelatedParticleEffectIsNotReplaced()
        {
            var root = new GameObject("Fireball");
            try
            {
                root.AddComponent<ParticleSystem>();
                root.AddComponent<TorchFireVfx>().Initialize();
                Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponent<ParticleSystemRenderer>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
