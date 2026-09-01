using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class HdrTorchFlamePresentationTests
    {
        [Test]
        public void HdrBoostAppliesOnlyToPrimaryFlameRenderer()
        {
            GameObject root = new("Torch");
            Material primaryMaterial = null;
            Material secondaryMaterial = null;
            try
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                Assert.That(shader, Is.Not.Null);
                primaryMaterial = new Material(shader)
                {
                    name = HdrTorchFlamePresentation.PrimaryMaterialName,
                };
                secondaryMaterial = new Material(shader) { name = "DungeonTorchFireSecondary" };

                ParticleSystemRenderer primaryRenderer = CreateFlameRenderer(
                    root.transform,
                    "Flames",
                    primaryMaterial
                );
                ParticleSystemRenderer secondaryRenderer = CreateFlameRenderer(
                    root.transform,
                    "Flames Secondary",
                    secondaryMaterial
                );
                var presentation = root.AddComponent<HdrTorchFlamePresentation>();

                presentation.SetHdrPresentation(true);

                var propertyBlock = new MaterialPropertyBlock();
                primaryRenderer.GetPropertyBlock(propertyBlock);
                Color primaryColor = propertyBlock.GetColor("_BaseColor");
                Assert.That(primaryColor.r, Is.EqualTo(30f).Within(0.001f));
                Assert.That(primaryColor.g, Is.EqualTo(15f).Within(0.001f));
                Assert.That(primaryColor.b, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(primaryColor.a, Is.EqualTo(1f).Within(0.001f));
                propertyBlock.Clear();
                secondaryRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.clear));

                presentation.SetHdrPresentation(false);
                propertyBlock.Clear();
                primaryRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(Color.clear));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(primaryMaterial);
                Object.DestroyImmediate(secondaryMaterial);
            }
        }

        private static ParticleSystemRenderer CreateFlameRenderer(
            Transform parent,
            string name,
            Material material
        )
        {
            GameObject flame = new(name);
            flame.transform.SetParent(parent);
            var particles = flame.AddComponent<ParticleSystem>();
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
