using BudgetGameDev.Games.Brocoli.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class TorchCombustionTests
    {
        private GameObject root;
        private Material authored;
        private TorchFireVfx fire;
        private ParticleSystem embers;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Torch combustion test");
            root.transform.SetPositionAndRotation(
                new Vector3(100f, 100f, 100f),
                Quaternion.Euler(0f, 133f, 0f)
            );
            authored = new Material(BrocoliShaders.Resolve(BrocoliShaders.Flame))
            {
                name = HdrTorchFlamePresentation.PrimaryMaterialName,
            };
            var legacy = new GameObject("Legacy flame");
            legacy.transform.SetParent(root.transform, false);
            legacy.AddComponent<ParticleSystem>();
            legacy.GetComponent<ParticleSystemRenderer>().sharedMaterial = authored;
            var anchor = new GameObject("Flame");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(0.01f, 1.82f, 0.53f);
            fire = root.AddComponent<TorchFireVfx>();
            fire.Initialize();
            embers = root.transform.Find("Fire Embers").GetComponent<ParticleSystem>();
            // Initialize Unity's simulation state without advancing or emitting anything.
            // Runtime CreateLayer calls Play; EditMode fixtures deliberately do not.
            embers.Simulate(0f, false, true, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(authored);
        }

        [Test]
        public void EmbersBeginInsideFuelAndCrackleSeesTheSameEmission()
        {
            embers.Simulate(3f, false, true, false);
            Assert.That(embers.particleCount, Is.Zero, "No prewarmed or automatic stray sparks.");
            int crackles = 0;
            int emitted = fire.EmitCrackle(
                1f,
                strength =>
                {
                    crackles++;
                    Assert.That(strength, Is.EqualTo(1f));
                    Assert.That(
                        embers.particleCount,
                        Is.EqualTo(3),
                        "Sound follows the actual visual event."
                    );
                }
            );
            Assert.That(emitted, Is.EqualTo(3));
            Assert.That(crackles, Is.EqualTo(1));
            var particles = new ParticleSystem.Particle[12];
            int count = embers.GetParticles(particles);
            Vector3 fuel = root.transform.Find("Flame").position;
            for (int i = 0; i < count; i++)
            {
                Assert.That(Vector3.Distance(particles[i].position, fuel), Is.LessThan(0.085f));
                Assert.That(particles[i].velocity.y, Is.InRange(0.9f, 1.65f));
                Assert.That(
                    Vector3.ProjectOnPlane(particles[i].velocity, Vector3.up).magnitude,
                    Is.LessThan(0.25f)
                );
                Assert.That(
                    particles[i].GetCurrentColor(embers).a,
                    Is.GreaterThan(0.35f),
                    "Visible at the fuel, without a delayed fade-in."
                );
            }
        }

        [Test]
        public void EmberArcsStayCloseAndFallInsteadOfDriftingAway()
        {
            fire.EmitCrackle(1f);
            Vector3 fuel = embers.transform.position;
            embers.Simulate(0.5f, false, false, true);
            var particles = new ParticleSystem.Particle[12];
            int count = embers.GetParticles(particles);
            Assert.That(count, Is.EqualTo(3));
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = particles[i].position - fuel;
                Assert.That(
                    Vector3.ProjectOnPlane(offset, Vector3.up).magnitude,
                    Is.LessThan(0.2f)
                );
                Assert.That(offset.y, Is.LessThan(0.3f));
                Assert.That(
                    particles[i].velocity.y,
                    Is.LessThan(0f),
                    "Gravity has turned the short hop downward."
                );
            }
        }

        [Test]
        public void LongFrameDoesNotReplayABacklogAndDisabledFireIsSilent()
        {
            int crackles = 0;
            fire.AdvanceCombustion(60f, _ => crackles++);
            Assert.That(crackles, Is.EqualTo(1));
            fire.AdvanceCombustion(0f, _ => crackles++);
            Assert.That(crackles, Is.EqualTo(1));
            fire.enabled = false;
            fire.AdvanceCombustion(60f, _ => crackles++);
            Assert.That(fire.EmitCrackle(1f, _ => crackles++), Is.Zero);
            Assert.That(crackles, Is.EqualTo(1));
        }

        [Test]
        public void FullParticleBudgetDoesNotProduceAnUnmatchedCrackle()
        {
            for (int i = 0; i < 4; i++)
                fire.EmitCrackle(1f);
            Assert.That(embers.particleCount, Is.EqualTo(12));
            int crackles = 0;
            Assert.That(fire.EmitCrackle(1f, _ => crackles++), Is.Zero);
            Assert.That(crackles, Is.Zero);
        }

        [Test]
        public void FloorContactBouncesBrieflyButCliffVoidDoesNot()
        {
            var layout = new DungeonLayout(12345);
            var particle = new ParticleSystem.Particle
            {
                position = new Vector3(0f, -0.03f, 0f),
                velocity = new Vector3(0.2f, -2f, 0.1f),
                remainingLifetime = 1f,
            };
            Assert.That(TorchFireVfx.ResolveEmberGroundContact(ref particle, layout), Is.True);
            Assert.That(particle.position.y, Is.EqualTo(0.015f));
            Assert.That(particle.velocity.y, Is.InRange(0.3f, 0.4f));
            Assert.That(particle.remainingLifetime, Is.EqualTo(0.22f));
            particle.position = new Vector3(0f, -0.03f, 1000f);
            particle.velocity = Vector3.down;
            Assert.That(TorchFireVfx.ResolveEmberGroundContact(ref particle, layout), Is.False);
            Assert.That(particle.position.y, Is.LessThan(0f));
        }
    }
}
