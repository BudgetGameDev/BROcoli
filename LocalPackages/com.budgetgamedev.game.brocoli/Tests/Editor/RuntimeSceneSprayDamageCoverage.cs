using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseQueuedSprayDamage(SprayDamageHandler damage, EnemyBase enemy)
        {
            if (enemy == null)
                return;

            SetHierarchyField(enemy, "isDying", false);
            enemy.enabled = true;
            enemy.MaxHealth = 100f;
            enemy.Health = 1000f;
            SetHierarchyField(enemy, "nextKnockbackTime", 0f);
            SetHierarchyField(enemy, "minimumDamageFractionForKnockback", 0.1f);
            damage.SetWeaponKnockbackMultiplier(1f);
            SetHierarchyField(damage, "playerTransform", enemy.transform);
            SetHierarchyField(damage, "lastConeDirection", Vector2.up);
            var hits = GetHierarchyField<Dictionary<EnemyBase, int>>(damage, "particleHitCounts");
            hits[enemy] = 1;
            damage.ApplyQueuedDamage(enemy.MaxHealth, 1f);
            var resolved = GetHierarchyField<HashSet<EnemyBase>>(damage, "coneKnockbackResolved");
            Assert.That(resolved.Contains(enemy), Is.True);
            hits[enemy] = 1;
            damage.ApplyQueuedDamage(enemy.MaxHealth * 0.1f, 1f);

            var totals = GetHierarchyField<Dictionary<EnemyBase, float>>(
                damage,
                "coneDamageTotals"
            );
            resolved.Clear();
            totals[enemy] = enemy.MaxHealth;
            enemy.enabled = false;
            damage.ResolveConeKnockback();
            enemy.enabled = true;
            totals[enemy] = enemy.MaxHealth;
            SetHierarchyField(enemy, "nextKnockbackTime", 0f);
            damage.ResolveConeKnockback();

            enemy.Health = 100f;
            enemy.enabled = false;
            hits[enemy] = 1;
            damage.ApplyQueuedDamage(0f, 1f);
            enemy.enabled = true;
        }
    }
}
