using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseMeleeIntermediateStates(EnemyScript enemy)
        {
            Transform player = enemy.player;
            Vector3 originalPlayerPosition = player.position;
            SetHierarchyField(enemy, "attackWindupDuration", 1f);
            SetHierarchyField(enemy, "attackStrikeDuration", 1f);
            SetHierarchyField(enemy, "attackRecoverDuration", 1f);
            SetHierarchyField(enemy, "meleeRange", 10f);
            SetHierarchyField(enemy, "attackLungeDistance", 0.42f);

            InvokeHierarchy(enemy, "StartAttackAnimation");
            SetHierarchyField(enemy, "attackPhase", 1);
            SetHierarchyField(enemy, "attackTimer", 0.2f);
            InvokeHierarchy(enemy, "UpdateAttackAnimation");
            SetHierarchyField(enemy, "attackPhase", 2);
            SetHierarchyField(enemy, "attackTimer", 0.2f);
            InvokeHierarchy(enemy, "UpdateAttackAnimation");
            SetHierarchyField(enemy, "attackPhase", 3);
            SetHierarchyField(enemy, "attackTimer", 0.2f);
            InvokeHierarchy(enemy, "UpdateAttackAnimation");

            SetHierarchyField(enemy, "activeAttackReach", 0f);
            SetHierarchyField(enemy, "attackDirection", Vector2.left);
            InvokeHierarchy(enemy, "PerformMeleeAttack");

            SetHierarchyField(enemy, "isAttacking", false);
            SetHierarchyField(enemy, "isKnockedBack", false);
            player.position = enemy.transform.position;
            InvokeHierarchy(enemy, "FixedUpdate");
            player.position = enemy.transform.position + Vector3.right * 20f;
            InvokeHierarchy(enemy, "FixedUpdate");
            player.position = enemy.transform.position + Vector3.right * 0.1f;
            InvokeHierarchy(enemy, "FixedUpdate");

            SetHierarchyField(enemy, "isAttacking", true);
            SetHierarchyField(enemy, "attackPhase", 2);
            InvokeHierarchy(enemy, "FixedUpdate");
            SetHierarchyField(enemy, "isAttacking", false);
            SetHierarchyField(enemy, "isKnockedBack", true);
            InvokeHierarchy(enemy, "FixedUpdate");

            SetHierarchyField(enemy, "baseLocalScale", Vector3.zero);
            enemy.ResetForPool();
            player.position = originalPlayerPosition;
        }

        private static void ExerciseShootingEnemy(
            Vector3 position,
            System.Collections.Generic.List<EnemyBase> enemies
        )
        {
            GameObject root = new("Coverage Shooting Enemy");
            root.AddComponent<Rigidbody>();
            root.AddComponent<BoxCollider>();
            var enemy = root.AddComponent<ShootingEnemyScript>();
            enemy.transform.position = position;
            enemy.player = PlayerStats.Resolve().transform;
            InvokeHierarchy(enemy, "Start");

            Transform player = enemy.player;
            enemy.player = null;
            InvokeHierarchy(enemy, "FixedUpdate");
            InvokeHierarchy(enemy, "TryShoot");
            enemy.player = player;
            SetHierarchyField(enemy, "isKnockedBack", true);
            InvokeHierarchy(enemy, "FixedUpdate");
            SetHierarchyField(enemy, "isKnockedBack", false);

            player.position = root.transform.position + Vector3.right * 20f;
            InvokeHierarchy(enemy, "FixedUpdate");
            player.position = root.transform.position + Vector3.right * 0.1f;
            InvokeHierarchy(enemy, "FixedUpdate");
            player.position = root.transform.position + Vector3.right * 4f;
            InvokeHierarchy(enemy, "FixedUpdate");
            player.position = root.transform.position;
            enemy.rb.position = root.transform.position;
            InvokeHierarchy(enemy, "FixedUpdate");

            GameObject projectile = new("Coverage Enemy Projectile");
            projectile.SetActive(false);
            projectile.AddComponent<Rigidbody>();
            projectile.AddComponent<SphereCollider>();
            EnemyProjectile projectileComponent = projectile.AddComponent<EnemyProjectile>();
            InvokeHierarchy(projectileComponent, "Awake");
            enemy.projectilePrefab = projectile;
            InvokeHierarchy(enemy, "Start");
            enemy.player = null;
            InvokeHierarchy(enemy, "TryShoot");
            enemy.player = player;
            enemy.fireRate = 0f;
            InvokeHierarchy(enemy, "TryShoot");
            enemy.fireRate = 1f;
            SetHierarchyField(enemy, "nextShootTime", Time.time + 10f);
            InvokeHierarchy(enemy, "TryShoot");
            SetHierarchyField(enemy, "nextShootTime", -10f);
            player.position = root.transform.position;
            InvokeHierarchy(enemy, "TryShoot");
            player.position = root.transform.position + Vector3.right * 3f;
            projectile.SetActive(true);
            SetHierarchyField(enemy, "_cachedProjectilePrefab", null);
            InvokeHierarchy(enemy, "TryShoot");
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.position = (root.transform.position + player.position) * 0.5f;
            wall.transform.localScale = new Vector3(0.5f, 4f, 4f);
            Physics.SyncTransforms();
            SetHierarchyField(enemy, "nextShootTime", -10f);
            InvokeHierarchy(enemy, "TryShoot");
            Object.Destroy(wall);

            GameObject barePlayer = new("Coverage Bare Shooting Target");
            Transform realPlayer = enemy.player;
            enemy.player = barePlayer.transform;
            InvokeHierarchy(enemy, "GetPlayerGroundVelocity");
            enemy.player = realPlayer;
            Object.Destroy(barePlayer);

            ShootingEnemyScript.CalculateAimDirection(
                Vector2.zero,
                Vector2.zero,
                Vector2.one,
                1f,
                1f,
                1f
            );
            ShootingEnemyScript.CalculateAimDirection(
                Vector2.zero,
                Vector2.right,
                Vector2.right,
                1f,
                1f,
                10f
            );
            ShootingEnemyScript.CalculateAimDirection(
                Vector2.zero,
                Vector2.right,
                Vector2.right * 5f,
                1f,
                1f,
                10f
            );
            ShootingEnemyScript.CalculateAimDirection(
                Vector2.zero,
                Vector2.right,
                Vector2.left,
                1f,
                1f,
                10f
            );
            ShootingEnemyScript.CalculateAimDirection(
                Vector2.zero,
                Vector2.right,
                Vector2.left * 2f,
                1f,
                1f,
                10f
            );
            enemies.Add(enemy);
        }
    }
}
