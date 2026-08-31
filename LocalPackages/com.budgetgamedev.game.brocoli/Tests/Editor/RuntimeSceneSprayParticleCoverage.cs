using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseSprayParticles(SanitizerSpray spray, EnemyBase enemy)
        {
            SprayParticleController controller = GetHierarchyField<SprayParticleController>(
                spray,
                "particleController"
            );
            controller.UpdateForStats(12f, 45f);
            ParticleSystem legacy = GetHierarchyField<ParticleSystem>(controller, "sprayParticles");
            if (legacy != null)
            {
                legacy.gameObject.SetActive(true);
                controller.SetSprayDirectionAndPosition(
                    Vector2.right,
                    spray.transform.position,
                    12f,
                    45f
                );
                controller.SetSprayDirectionAndPosition(
                    Vector2.zero,
                    spray.transform.position,
                    12f,
                    45f
                );
                controller.ApplyVelocityCompensation(Vector2.right, 12f, 45f);
                controller.UpdateNozzlePosition();
                controller.PlayBurst();
                controller.Play();
                controller.Stop();
                Assert.That(controller.HasParticles, Is.True);
            }

            var emptyController = new SprayParticleController(spray.transform);
            _ = emptyController.Particles;
            Assert.That(emptyController.GetParticleSpeed(), Is.GreaterThan(0f));
            emptyController.UpdateForStats(1f, 1f);
            emptyController.ApplyVelocityCompensation(Vector2.right, 1f, 1f);
            emptyController.UpdateNozzlePosition();
            emptyController.SetParticleSystem(null);
            Assert.That(emptyController.HasParticles, Is.False);

            foreach (
                SprayParticleCollisionHandler collision in spray.GetComponentsInChildren<SprayParticleCollisionHandler>(
                    true
                )
            )
            {
                collision.SetDamageParams(PlayerStats.Resolve(), 100f, 1f);
                collision.SetSprayDirection(Vector2.up);
                SetHierarchyField(collision, "weaponKnockbackMultiplier", 1f);
                Assert.That(collision.ConsumeParticlesNear(null), Is.Zero);
                collision.ClearCooldowns();

                ParticleSystem particles = collision.GetComponent<ParticleSystem>();
                if (particles == null)
                    continue;
                var emitted = new ParticleSystem.Particle
                {
                    position = Vector3.zero,
                    startLifetime = 10f,
                    remainingLifetime = 10f,
                    startSize = 1f,
                };
                particles.SetParticles(new[] { emitted }, 1);
                ParticleSystem.MainModule particleMain = particles.main;
                particleMain.simulationSpace = ParticleSystemSimulationSpace.Local;
                collision.ConsumeParticlesNear(new[] { particles.transform.position });
                particles.SetParticles(new[] { emitted }, 1);
                GameObject customSpace = new("Coverage Particle Custom Space");
                particleMain.simulationSpace = ParticleSystemSimulationSpace.Custom;
                particleMain.customSimulationSpace = customSpace.transform;
                collision.ConsumeParticlesNear(new[] { customSpace.transform.position });
                particleMain.simulationSpace = ParticleSystemSimulationSpace.World;
                Object.Destroy(customSpace);
                InvokeHierarchy(collision, "FindNearestParticle", Vector3.zero, 0);
                InvokeHierarchy(collision, "OnParticleCollision", enemy?.gameObject);
                if (enemy != null)
                {
                    enemy.enabled = true;
                    enemy.MaxHealth = 100f;
                    enemy.Health = 1000f;
                    SetHierarchyField(enemy, "isDying", false);
                    SetHierarchyField(enemy, "minimumDamageFractionForKnockback", 0.1f);
                    SetHierarchyField(enemy, "nextKnockbackTime", 0f);
                    var contact = new ParticleCollisionEvent();
                    collision.ProcessCollision(enemy.gameObject, new[] { contact }, 1);
                    collision.ProcessCollision(enemy.gameObject, new[] { contact }, 1);
                    collision.ClearCooldowns();
                    SetHierarchyField(collision, "damageParamsExplicitlySet", false);
                    collision.ProcessCollision(enemy.gameObject, new[] { contact }, 1);
                    collision.ProcessCollision(null, null, 0);
                    collision.ClearCooldowns();
                    enemy.enabled = false;
                    collision.SetDamageParams(PlayerStats.Resolve(), 0f, 1f);
                    collision.ProcessCollision(enemy.gameObject, new[] { contact }, 1);
                    enemy.enabled = true;
                }
                InvokeHierarchy(collision, "OnDisable");
            }

            GameObject emptyObject = new("Coverage Empty Particle Collision");
            SprayParticleCollisionHandler empty =
                emptyObject.AddComponent<SprayParticleCollisionHandler>();
            InvokeHierarchy(empty, "Awake");
            InvokeHierarchy(empty, "OnParticleCollision", (object)null);
            Assert.That(empty.ConsumeParticlesNear(new[] { Vector3.zero }), Is.Zero);
            Object.Destroy(emptyObject);
        }
    }
}
