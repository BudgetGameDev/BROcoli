using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class EnemyBaseAssetGuardCoverageTests
    {
        [Test]
        public void PrefabAssetGuardsIgnoreDisableAndDeathMessages()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Packages/com.budgetgamedev.game.brocoli/Resources" }
            );
            EnemyBase enemy = null;
            foreach (string guid in guids)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );
                enemy = prefab == null ? null : prefab.GetComponentInChildren<EnemyBase>(true);
                if (enemy != null)
                    break;
            }
            Assert.That(enemy, Is.Not.Null);
            Assert.That(enemy.gameObject.scene.isLoaded, Is.False);

            enemy
                .GetType()
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(enemy, null);
            enemy.Die();
            Assert.That(enemy, Is.Not.Null);
        }
    }
}
