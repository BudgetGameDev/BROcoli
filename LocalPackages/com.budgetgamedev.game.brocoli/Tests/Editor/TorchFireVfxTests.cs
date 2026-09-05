using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class TorchFireVfxTests
    {
        [Test]
        public void TorchReplacesLegacyLayersWithBoundedIndependentFlamesSmokeAndEmbers()
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
                    Is.EqualTo(5),
                    "initialization must not duplicate layers"
                );
                int budget = 0;
                int flames = 0,
                    smoke = 0,
                    embers = 0;
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
                    Assert.That(
                        system.main.simulationSpace,
                        Is.EqualTo(ParticleSystemSimulationSpace.Local)
                    );
                    Assert.That(
                        system.velocityOverLifetime.space,
                        Is.EqualTo(ParticleSystemSimulationSpace.World)
                    );
                    Assert.That(system.main.startColor.colorMax.a, Is.LessThan(1f));
                    switch (Mathf.RoundToInt(renderer.sharedMaterial.GetFloat("_Layer")))
                    {
                        case 0:
                            flames++;
                            break;
                        case 1:
                            smoke++;
                            break;
                        case 2:
                            embers++;
                            break;
                    }
                }
                Assert.That(budget, Is.EqualTo(TorchFireVfx.ParticleBudget));
                Assert.That(flames, Is.EqualTo(2));
                Assert.That(smoke, Is.EqualTo(1));
                Assert.That(embers, Is.EqualTo(1));
                Assert.That(
                    root.transform.Find("Fire Core").localPosition,
                    Is.EqualTo(old.transform.localPosition)
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
