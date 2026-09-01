using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExercisePlayerCombat(PlayerStats stats, List<EnemyBase> enemies)
        {
            PlayerCombat combat = stats.GetComponent<PlayerCombat>();
            PlayerMovement movement = stats.GetComponent<PlayerMovement>();
            Assert.That(combat, Is.Not.Null);
            Assert.That(movement, Is.Not.Null);
            ProceduralGunAudio gunAudio = GetHierarchyField<ProceduralGunAudio>(
                combat,
                "_gunAudio"
            );
            if (gunAudio != null)
            {
                SetHierarchyField(gunAudio, "cachedClips", null);
                gunAudio.PlayGunSound(0f);
            }
            combat.CurrentWeapon = PlayerCombat.WeaponType.SanitizerSpray;
            Assert.That(combat.CurrentWeapon, Is.EqualTo(PlayerCombat.WeaponType.SanitizerSpray));
            combat.EnemyDetectionRadius = 50f;

            SetHierarchyField(combat, "_nextAllowedAttack", Time.time + 100f);
            combat.HandleCombat();
            SetHierarchyField(combat, "_nextAllowedAttack", 0f);
            SetHierarchyField(combat, "_playerMovement", null);
            combat.HandleCombat();
            SetHierarchyField(combat, "_playerMovement", movement);

            var colliders = new List<Collider>();
            foreach (EnemyBase enemy in enemies)
            {
                if (enemy == null)
                    continue;
                Collider collider = enemy.GetComponent<Collider>();
                if (collider != null)
                    colliders.Add(collider);
            }
            Collider[] hits = colliders.ToArray();
            Vector2 playerPosition = stats.transform.position.ToGround();
            InvokeHierarchy(combat, "FindBestSprayTarget", null, playerPosition, 10f);
            InvokeHierarchy(combat, "FindBestSprayTarget", new Collider[0], playerPosition, 10f);
            InvokeHierarchy(combat, "FindClosestEnemy", new Collider[] { null }, playerPosition);
            InvokeHierarchy(combat, "FindTarget", new Collider[] { null }, playerPosition, 10f);
            InvokeHierarchy(combat, "FindClosestEnemy", hits, playerPosition);
            if (enemies.Count > 1 && enemies[0] != null && enemies[1] != null)
            {
                SanitizerSpray targetingSpray = GetHierarchyField<SanitizerSpray>(
                    combat,
                    "_sanitizerSpray"
                );
                SetHierarchyField(targetingSpray, "currentWidth", 70f);
                enemies[0].transform.position =
                    stats.transform.position + new Vector3(5f, 0f, 2.5f);
                enemies[1].transform.position =
                    stats.transform.position + new Vector3(5f, 0f, -2.5f);
                Physics.SyncTransforms();
                InvokeHierarchy(combat, "FindBestSprayTarget", hits, playerPosition, 50f);
            }
            InvokeHierarchy(combat, "FindBestSprayTarget", hits, playerPosition, 50f);
            InvokeHierarchy(combat, "FindTarget", hits, playerPosition, 50f);
            InvokeHierarchy(combat, "CanShootTarget", null, playerPosition);

            var candidates = new List<(Transform, EnemyBase, Vector2, float)>();
            candidates.Add((stats.transform, null, playerPosition, 0f));
            candidates.Add((stats.transform, null, playerPosition + Vector2.right * 100f, 100f));
            candidates.Add((stats.transform, null, playerPosition + Vector2.right, 1f));
            InvokeHierarchy(
                combat,
                "CalculateSprayDamage",
                candidates,
                playerPosition,
                Vector2.right,
                45f,
                10f
            );

            EnemyBase target = enemies.Find(enemy => enemy != null);
            if (target == null)
                return;
            ExerciseLegacyProjectileHit(target);
            target.transform.position = stats.transform.position + Vector3.right * 2f;
            Collider targetCollider = target.GetComponent<Collider>();
            Physics.SyncTransforms();
            InvokeHierarchy(
                combat,
                "FindBestSprayTarget",
                new[] { targetCollider },
                playerPosition,
                50f
            );
            InvokeHierarchy(combat, "CanShootTarget", targetCollider, playerPosition);
            InvokeHierarchy(combat, "GetPredictedEnemyPosition", target, Vector2.right, 0.1f, 10f);
            Rigidbody body = target.rb;
            SetHierarchyField(target, "rb", null);
            InvokeHierarchy(combat, "GetPredictedEnemyPosition", target, Vector2.right, 5f, 10f);
            SetHierarchyField(target, "rb", body);
            body.SetGroundVelocity(Vector2.zero);
            InvokeHierarchy(combat, "GetPredictedEnemyPosition", target, Vector2.right, 5f, 10f);
            body.SetGroundVelocity(Vector2.up * 4f);
            InvokeHierarchy(combat, "GetPredictedEnemyPosition", target, Vector2.right, 5f, 10f);

            InvokeHierarchy(combat, "FireSprayAt", (object)null);
            InvokeHierarchy(combat, "AttackTarget", target.transform);
            combat.CurrentWeapon = PlayerCombat.WeaponType.Projectile;
            InvokeHierarchy(combat, "AttackTarget", target.transform);
            InvokeHierarchy(combat, "FireProjectileAt", (object)null);
            GameObject noCollider = new("Coverage Projectile Target Without Collider");
            InvokeHierarchy(combat, "FireProjectileAt", noCollider.transform);
            Object.Destroy(noCollider);
            combat.CurrentWeapon = PlayerCombat.WeaponType.SanitizerSpray;
            object playerStats = GetHierarchyField<object>(combat, "_playerStats");
            SetHierarchyField(combat, "_playerStats", null);
            SetHierarchyField(combat, "_nextAllowedAttack", 0f);
            LogAssert.Expect(LogType.Warning, "PlayerCombat: PlayerStats is null - cannot attack!");
            combat.HandleCombat();
            SetHierarchyField(combat, "_playerStats", playerStats);
        }

        private static void ExerciseLegacyProjectileHit(EnemyBase target)
        {
            GameObject projectileObject = new("Coverage Legacy Projectile Hit");
            projectileObject.AddComponent<Rigidbody>();
            SphereCollider collider = projectileObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            projectile.Init(Vector2.right, 1f, 1f);
            InvokeHierarchy(projectile, "OnTriggerEnter", target.GetComponent<Collider>());
        }
    }
}
