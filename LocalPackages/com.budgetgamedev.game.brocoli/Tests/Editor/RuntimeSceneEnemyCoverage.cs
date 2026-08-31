using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseEnemyCombat(EnemyBase enemy)
        {
            ExerciseEnemyAudio(enemy);
            Rigidbody body = enemy.rb;
            Transform target = enemy.player;
            enemy.player = null;
            if (enemy is EnemyScript || enemy is HydraEnemyScript)
                InvokeHierarchy(enemy, "RefreshAttackAim");
            InvokeHierarchy(enemy, "Update");
            enemy.player = target;

            Assert.That(enemy.TryApplyDamageKnockback(1f, Vector2.zero), Is.False);
            enemy.StrengthenActiveDamageKnockback(5f, Vector2.right);
            SetHierarchyField(enemy, "nextKnockbackTime", 0f);
            SetHierarchyField(enemy, "minimumDamageFractionForKnockback", 0.1f);
            Assert.That(enemy.TryApplyDamageKnockback(0f, Vector2.right), Is.False);
            SetHierarchyField(enemy, "nextKnockbackTime", 0f);
            enemy.TryApplyDamageKnockback(enemy.MaxHealth, Vector2.right, 1f);
            SetHierarchyField(enemy, "activeKnockbackForce", 0f);
            enemy.StrengthenActiveDamageKnockback(enemy.MaxHealth, Vector2.up, 1f);
            enemy.StrengthenActiveDamageKnockback(enemy.MaxHealth, Vector2.up, 2f);
            enemy.StrengthenActiveDamageKnockback(enemy.MaxHealth, Vector2.up, 1f);
            SetHierarchyField(enemy, "nextKnockbackTime", Time.time + 10f);
            Assert.That(
                enemy.TryApplyDamageKnockback(enemy.MaxHealth, Vector2.right, 1f),
                Is.False
            );
            InvokeHierarchy(enemy, "LockBodyForAttack");
            InvokeHierarchy(enemy, "LockBodyForAttack");
            SetHierarchyField(enemy, "nextKnockbackTime", 0f);
            enemy.ApplyKnockback(Vector2.left, 1f);
            enemy.ApplyKnockback(Vector2.right, 2f);
            InvokeHierarchy(enemy, "UnlockBodyAfterAttack", true);
            InvokeHierarchy(enemy, "LockBodyForAttack");
            InvokeHierarchy(enemy, "UnlockBodyAfterAttack", false);
            SetHierarchyField(enemy, "rb", null);
            Assert.That(enemy.TryApplyDamageKnockback(10f, Vector2.right), Is.False);
            InvokeHierarchy(enemy, "ApplySeparation");
            SetHierarchyField(enemy, "rb", body);
            enemy.ApplyKnockback(Vector2.right);
            enemy.ApplyKnockback(Vector2.zero, 1f);
            SetHierarchyField(enemy, "nextKnockbackTime", Time.time + 10f);
            enemy.ApplyKnockback(Vector2.right, 1f);
            SetHierarchyField(enemy, "nextKnockbackTime", 0f);
            SetHierarchyField(enemy, "isDying", true);
            enemy.TakeDamage(1f);
            InvokeHierarchy(enemy, "Die");
            SetHierarchyField(enemy, "isDying", false);
            InvokeHierarchy(enemy, "OnApplicationQuit");
            InvokeHierarchy(enemy, "Die");
            SetHierarchyField(enemy, "isQuitting", false);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                Physics.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
                InvokeHierarchy(enemy, "ConfigureSolidBody");
                Physics.IgnoreLayerCollision(enemyLayer, enemyLayer, false);
            }

            InvokeHierarchy(enemy, "CalculateDamageKnockbackForce", enemy.MaxHealth, 0f);
            InvokeHierarchy(enemy, "CalculateDamageKnockbackForce", enemy.MaxHealth * 0.5f, 1f);
            InvokeHierarchy(enemy, "CalculateDamageKnockbackForce", enemy.MaxHealth * 0.5f, 0.2f);
            InvokeHierarchy(enemy, "GetPlayerColliderGap");
            InvokeHierarchy(enemy, "IsPlayerWithinAttackContact", 10f, Vector2.zero, 0.5f);
            ExerciseEnemyColliderEdges(enemy, target);
            enemy.ResetForPool();

            if (enemy is EnemyScript melee)
                ExerciseMeleeStateMachine(melee);
            else if (enemy is HydraEnemyScript hydra)
                ExerciseMeleeStateMachine(hydra);
        }

        private static void ExerciseEnemyVariants(Vector3 playerPosition, List<EnemyBase> enemies)
        {
            EnemyScript spriteEnemy = CreateEnemyVariant("Coverage Sprite Enemy", true, false);
            EnemyScript meshEnemy = CreateEnemyVariant("Coverage Mesh Enemy", false, true);
            EnemyScript bareEnemy = CreateEnemyVariant("Coverage Bare Enemy", false, false);
            GameObject healthBar = new("HealthBar");
            healthBar.transform.SetParent(bareEnemy.transform, false);
            healthBar.AddComponent<Bar>();
            SetHierarchyField(bareEnemy, "healthBar", null);
            InvokeHierarchy(bareEnemy, "DisableWorldHealthBar");
            bareEnemy.gameObject.AddComponent<EliteEnemyEffects>();
            foreach (EnemyScript enemy in new[] { spriteEnemy, meshEnemy, bareEnemy })
            {
                enemy.transform.position = playerPosition + Vector3.right * 0.1f;
                enemy.player = PlayerStats.Resolve().transform;
                ExerciseEnemyCombat(enemy);
                ExerciseMeleeIntermediateStates(enemy);
                SetHierarchyField(enemy, "separationRadius", 100f);
                SetHierarchyField(enemy, "separationForce", 100f);
                SetHierarchyField(enemy, "maxSeparationSpeed", 100f);
                SetHierarchyField(enemy, "enemyKnockbackForce", 100f);
                SetHierarchyField(enemy, "enemyKnockbackDuration", 100f);
                SetHierarchyField(enemy, "enemyKnockbackCooldown", 0f);
                InvokeHierarchy(enemy, "EnforceSafePhysicsLimits");
                SetHierarchyField(enemy, "bodyCollider", null);
                InvokeHierarchy(enemy, "ConfigureSolidBody");
                enemy.MakeElite();
                enemy.MakeElite();
                enemy.OnEliteDeath += _ => { };
                enemy.OnDeath += _ => { };
                Drain((IEnumerator)InvokeHierarchy(enemy, "HitFlash"));
                SetHierarchyField(enemy, "isKnockedBack", true);
                SetHierarchyField(enemy, "knockbackTimer", 0f);
                enemy.Update();
                enemy.TakeDamage(0f);
                enemies.Add(enemy);
            }
            bareEnemy.Health = 1f;
            bareEnemy.TakeDamage(10f);
            bareEnemy.SetPooled(true);
            InvokeHierarchy(bareEnemy, "CompleteDeath");
            ExerciseEnemyVisualEffects();
            ExerciseShootingEnemy(playerPosition, enemies);
            ExerciseHydraVisualVariants(playerPosition, enemies);
        }

        private static void ExerciseHydraVisualVariants(
            Vector3 playerPosition,
            List<EnemyBase> enemies
        )
        {
            foreach (
                (bool sprite, bool mesh) in new[] { (true, false), (false, true), (false, false) }
            )
            {
                GameObject root = new($"Coverage Hydra {sprite} {mesh}");
                root.AddComponent<Rigidbody>();
                root.AddComponent<BoxCollider>();
                if (sprite)
                {
                    GameObject visual = new("Hydra Sprite Visual");
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localScale = Vector3.zero;
                    visual.AddComponent<SpriteRenderer>();
                }
                if (mesh)
                {
                    GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.name = "Hydra Mesh Visual";
                    visual.transform.SetParent(root.transform, false);
                }

                HydraEnemyScript hydra = root.AddComponent<HydraEnemyScript>();
                hydra.transform.position = playerPosition + Vector3.left * 0.1f;
                hydra.player = PlayerStats.Resolve().transform;
                Transform attackPlayer = hydra.player;
                hydra.player = null;
                InvokeHierarchy(hydra, "PerformMeleeAttack");
                hydra.player = attackPlayer;
                Vector3 savedPlayerPosition = attackPlayer.position;
                attackPlayer.position = hydra.transform.position;
                hydra.rb.position = hydra.transform.position;
                InvokeHierarchy(hydra, "FixedUpdate");
                attackPlayer.position = savedPlayerPosition;
                PlayerDamageHandler playerDamage = attackPlayer.GetComponent<PlayerDamageHandler>();
                if (playerDamage != null)
                    SetHierarchyField(playerDamage, "_lastDamageTime", -100f);
                SetHierarchyField(hydra, "activeAttackReach", 100f);
                SetHierarchyField(
                    hydra,
                    "attackDirection",
                    attackPlayer.position.ToGround() - hydra.transform.position.ToGround()
                );
                InvokeHierarchy(hydra, "PerformMeleeAttack");
                SetHierarchyField(hydra, "maxGenerations", 0);
                ExerciseEnemyCombat(hydra);

                SetHierarchyField(hydra, "attackWindupDuration", 1f);
                SetHierarchyField(hydra, "attackStrikeDuration", 1f);
                SetHierarchyField(hydra, "attackRecoverDuration", 1f);
                SetHierarchyField(hydra, "meleeRange", 10f);
                InvokeHierarchy(hydra, "StartAttackAnimation");
                SetHierarchyField(hydra, "attackPhase", 1);
                SetHierarchyField(hydra, "attackTimer", 0.25f);
                InvokeHierarchy(hydra, "UpdateAttackAnimation");
                SetHierarchyField(hydra, "attackPhase", 2);
                SetHierarchyField(hydra, "attackTimer", 0.25f);
                InvokeHierarchy(hydra, "FixedUpdate");
                InvokeHierarchy(hydra, "UpdateAttackAnimation");
                SetHierarchyField(hydra, "attackPhase", 3);
                SetHierarchyField(hydra, "attackTimer", 0.25f);
                InvokeHierarchy(hydra, "UpdateAttackAnimation");
                InvokeHierarchy(hydra, "PerformMeleeAttack");

                InvokeHierarchy(hydra, "StartAttackAnimation");
                SetHierarchyField(hydra, "isAttacking", true);
                InvokeHierarchy(hydra, "PrepareForIncomingKnockback");
                SetHierarchyField(hydra, "isAttacking", true);
                SetHierarchyField(hydra, "nextKnockbackTime", 0f);
                hydra.ApplyKnockback(Vector2.right, 1f);
                hydra.ResetForPool();
                enemies.Add(hydra);
            }
        }

        private static EnemyScript CreateEnemyVariant(string name, bool sprite, bool mesh)
        {
            GameObject root = new(name);
            root.AddComponent<Rigidbody>();
            root.AddComponent<BoxCollider>();
            if (sprite)
            {
                GameObject visual = new("Sprite Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.zero;
                visual.AddComponent<SpriteRenderer>();
            }
            if (mesh)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Mesh Visual";
                visual.transform.SetParent(root.transform, false);
            }
            return root.AddComponent<EnemyScript>();
        }

        private static void ReturnEnemyCatalog(List<EnemyBase> enemies)
        {
            foreach (EnemyBase enemy in enemies)
            {
                if (enemy != null && enemy.gameObject.activeSelf)
                    PoolManager.Instance.ReturnEnemy(enemy);
            }
        }

        private static object InvokeHierarchy(object target, string name, params object[] arguments)
        {
            Type type = target.GetType();
            while (type != null)
            {
                foreach (
                    MethodInfo method in type.GetMethods(
                        PrivateInstance | BindingFlags.Public | BindingFlags.Static
                    )
                )
                {
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
                }
                type = type.BaseType;
            }
            Assert.Fail($"Method {name} was not found on {target.GetType().Name}");
            return null;
        }

        private static void SetHierarchyField(object target, string name, object value)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    PrivateInstance | BindingFlags.Public | BindingFlags.Static
                );
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            Assert.Fail($"Field {name} was not found on {target.GetType().Name}");
        }

        private static T GetHierarchyField<T>(object target, string name)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    PrivateInstance | BindingFlags.Public | BindingFlags.Static
                );
                if (field != null)
                    return (T)field.GetValue(target);
                type = type.BaseType;
            }
            Assert.Fail($"Field {name} was not found on {target.GetType().Name}");
            return default;
        }
    }
}
