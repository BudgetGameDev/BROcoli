using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseSpray(SanitizerSpray spray, List<EnemyBase> enemies)
        {
            _ = spray.SprayRange;
            _ = spray.SprayWidth;
            _ = spray.IsSpraying;
            _ = spray.IsOnCooldown;
            spray.UpdateStatsFromPlayer();
            Assert.That(spray.GetParticleSpeed(), Is.GreaterThan(0f));
            spray.StopSpray();
            spray.StartSpray(Vector2.right);
            spray.StartSpray(Vector2.right);
            InvokeHierarchy(spray, "Update");
            spray.StopSpray();
            spray.StopSpray();
            InvokeHierarchy(spray, "OnParticleTrigger");
            Assert.That(spray.FireSprayBurstAtTarget(null), Is.False);

            EnemyBase enemy = enemies.Find(candidate => candidate != null);
            if (enemy != null)
            {
                enemy.transform.position = spray.transform.position + Vector3.right;
                SetHierarchyField(spray, "lastBurstTime", -10f);
                SetHierarchyField(spray, "currentBurstEndTime", 0f);
                SetHierarchyField(spray, "hasPendingSpray", false);
                spray.FireSprayBurstAtTarget(enemy.transform);
                SetHierarchyField(spray, "aimStartTime", Time.time - 10f);
                InvokeHierarchy(spray, "HandlePendingSpray");
                InvokeHierarchy(spray, "Update");
                SetHierarchyField(spray, "currentBurstEndTime", Time.time - 1f);
                InvokeHierarchy(spray, "Update");
            }

            SetHierarchyField(spray, "lastBurstTime", -10f);
            SetHierarchyField(spray, "currentBurstEndTime", 0f);
            SetHierarchyField(spray, "hasPendingSpray", false);
            Assert.That(spray.FireSprayBurst(Vector2.right), Is.True);
            Assert.That(spray.FireSprayBurst(Vector2.right), Is.False);
            SetHierarchyField(spray, "hasPendingSpray", false);
            SetHierarchyField(spray, "lastBurstTime", Time.time);
            Assert.That(spray.FireSprayBurst(Vector2.right), Is.False);
            SetHierarchyField(spray, "lastBurstTime", -10f);
            SetHierarchyField(spray, "currentBurstEndTime", Time.time + 10f);
            Assert.That(spray.FireSprayBurst(Vector2.right), Is.False);
            SetHierarchyField(spray, "currentBurstEndTime", 0f);
            SpraySettings.ShowHandAlways = false;
            SetHierarchyField(spray, "hasPendingSpray", true);
            InvokeHierarchy(spray, "CancelPendingSpray");
            InvokeHierarchy(spray, "ExecutePendingSpray");
            SetHierarchyField(spray, "hasPendingSpray", true);
            InvokeHierarchy(spray, "ExecutePendingSpray");
            SetHierarchyField(spray, "isInBurst", false);
            InvokeHierarchy(spray, "HideHand");
            SpraySettings.ShowHandAlways = true;

            GameObject farTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farTarget.transform.position = spray.transform.position + Vector3.right * 1000f;
            SetHierarchyField(spray, "playerTransform", spray.transform);
            SetHierarchyField(spray, "currentRange", 1f);
            SetHierarchyField(spray, "lastBurstTime", -10f);
            SetHierarchyField(spray, "currentBurstEndTime", 0f);
            SetHierarchyField(spray, "hasPendingSpray", false);
            Assert.That(spray.FireSprayBurstAtTarget(farTarget.transform), Is.False);
            Object.Destroy(farTarget);

            SprayDamageHandler damage = GetHierarchyField<SprayDamageHandler>(
                spray,
                "damageHandler"
            );
            damage.UpdateReferences(null, null);
            damage.SetParticleSpeed(0f);
            damage.SetWeaponKnockbackMultiplier(-1f);
            damage.RegisterParticleHit(null);
            if (enemy != null)
            {
                damage.RegisterParticleHit(enemy);
                damage.RegisterParticleHit(enemy);
            }
            damage.ResetDamageTick();
            if (enemy != null)
                damage.RegisterParticleHit(enemy);
            damage.ProcessDamage(Vector2.right, 20f, 360f, spray.transform.position);
            damage.ProcessDamage(Vector2.zero, 20f, 360f, spray.transform.position);
            damage.ResolveConeKnockback();
            ExerciseQueuedSprayDamage(damage, enemy);
            GameObject taggedCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            taggedCollider.name = "Coverage Non Enemy Component";
            taggedCollider.tag = "Enemy";
            taggedCollider.transform.position = spray.transform.position + Vector3.right;
            Physics.SyncTransforms();
            damage.ResetDamageTick();
            damage.ProcessDamage(Vector2.right, 20f, 360f, spray.transform.position);
            Object.Destroy(taggedCollider);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.position = spray.transform.position + Vector3.right;
            wall.transform.localScale = new Vector3(0.5f, 10f, 10f);
            GameObject obstructedEnemyObject = new("Coverage Obstructed Enemy");
            obstructedEnemyObject.tag = "Enemy";
            Rigidbody obstructedBody = obstructedEnemyObject.AddComponent<Rigidbody>();
            obstructedBody.useGravity = false;
            Collider obstructedCollider = obstructedEnemyObject.AddComponent<BoxCollider>();
            EnemyScript obstructedEnemy = obstructedEnemyObject.AddComponent<EnemyScript>();
            obstructedEnemyObject.transform.position =
                spray.transform.position + Vector3.right * 2f;
            Physics.SyncTransforms();
            Assert.That(
                SprayDamageHandler.TryGetEnemy(obstructedCollider, out EnemyBase foundEnemy),
                Is.True
            );
            Assert.That(foundEnemy, Is.SameAs(obstructedEnemy));
            Assert.That(
                ProjectileWallCollision.HasClearLine(
                    spray.transform.position,
                    obstructedCollider.bounds.center
                ),
                Is.False
            );
            damage.ResetDamageTick();
            damage.ProcessDamage(Vector2.right, 20f, 360f, spray.transform.position);
            Object.Destroy(obstructedEnemyObject);
            Object.Destroy(wall);
            damage.ProcessParticleTrigger(null);
            ParticleSystem triggerParticles = spray.GetComponentInChildren<ParticleSystem>(true);
            if (triggerParticles != null)
            {
                damage.ProcessParticleTrigger(triggerParticles);
                var triggered = new List<ParticleSystem.Particle>
                {
                    new() { position = spray.transform.position, remainingLifetime = 1f },
                };
                Assert.That(damage.ProcessTriggeredParticles(triggered, 1, _ => null), Is.False);
                if (enemy != null)
                {
                    Collider enemyCollider = enemy.GetComponent<Collider>();
                    bool killed = damage.ProcessTriggeredParticles(
                        triggered,
                        1,
                        _ => enemyCollider
                    );
                    Assert.That(killed, Is.True);
                    LogAssert.Expect(
                        LogType.Error,
                        "Assigning trigger particles to the wrong event type!"
                    );
                    SprayDamageHandler.ApplyTriggerResults(triggerParticles, triggered, true);
                }
            }
            damage.ClearHits();
            ExerciseSprayParticles(spray, enemy);
            ExerciseSanitizerEdges(spray, enemy);
        }

        private static void ExerciseSanitizerEdges(SanitizerSpray spray, EnemyBase enemy)
        {
            PlayerStats stats = GetHierarchyField<PlayerStats>(spray, "playerStats");
            SetHierarchyField(spray, "playerStats", null);
            spray.UpdateStatsFromPlayer();
            SetHierarchyField(spray, "playerStats", stats);

            if (enemy != null)
            {
                SetHierarchyField(spray, "lastBurstTime", Time.time);
                spray.FireSprayBurstAtTarget(enemy.transform);
                SetHierarchyField(spray, "lastBurstTime", -10f);
                SetHierarchyField(spray, "currentBurstEndTime", Time.time + 10f);
                spray.FireSprayBurstAtTarget(enemy.transform);
                SetHierarchyField(spray, "currentBurstEndTime", 0f);
                SetHierarchyField(spray, "hasPendingSpray", true);
                spray.FireSprayBurstAtTarget(enemy.transform);
                SetHierarchyField(spray, "hasPendingSpray", false);
                enemy.transform.position = spray.transform.position + Vector3.right * 1000f;
                spray.FireSprayBurstAtTarget(enemy.transform);
            }

            GameObject parent = new("Coverage Spray Parent");
            GameObject child = new("Coverage Alternate Spray");
            child.transform.SetParent(parent.transform, false);
            SanitizerSpray alternate = child.AddComponent<SanitizerSpray>();
            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            SetHierarchyField(alternate, "sprayParticles", particles);
            InvokeHierarchy(alternate, "InitializeComponents");
            SetHierarchyField(alternate, "sprayAudio", null);
            InvokeHierarchy(alternate, "FindReferences");
            SetHierarchyField(alternate, "playerStats", null);
            alternate.UpdateStatsFromPlayer();
            InvokeHierarchy(alternate, "Start");
            Object.Destroy(parent);
        }
    }
}
