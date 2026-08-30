using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class EnemyMaterialSafetyTests
    {
        private const string EnemyMaterialGuid = "5ddd954e98d44fd4696e95bf079d20f1";

        [Test]
        public void RuntimeColorDoesNotModifyTheSharedEnemyMaterial()
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(EnemyMaterialGuid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null, "The enemy-0 material must remain loadable.");

            string serializedBefore = EditorJsonUtility.ToJson(material);
            bool wasDirty = EditorUtility.IsDirty(material);
            var enemy = new GameObject("Enemy material safety test");

            try
            {
                MeshRenderer renderer = enemy.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var properties = new MaterialPropertyBlock();

                EnemyRendererColor.Set(renderer, properties, Color.magenta);

                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(serializedBefore));
                Assert.That(EditorUtility.IsDirty(material), Is.EqualTo(wasDirty));

                renderer.GetPropertyBlock(properties);
                Assert.That(
                    properties.GetColor(Shader.PropertyToID("_BaseColor")),
                    Is.EqualTo(Color.magenta)
                );
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }
    }
}
