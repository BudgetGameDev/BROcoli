using System;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class PickupVisualCoverageTests
    {
        [Test]
        public void EveryBoostSoundMapsToAVisualAndPalette()
        {
            foreach (
                ProceduralBoostAudio.BoostSoundType sound in Enum.GetValues(
                    typeof(ProceduralBoostAudio.BoostSoundType)
                )
            )
            {
                PickupVisual3D.ModelKind kind = PickupVisual3D.ModelKindForSound(sound);
                _ = PickupVisual3D.GetPalette(kind);
            }
            Assert.That(
                PickupVisual3D.ModelKindForSound((ProceduralBoostAudio.BoostSoundType)999),
                Is.EqualTo(PickupVisual3D.ModelKind.ExperienceBoost)
            );
        }

        [Test]
        public void ExistingModelHierarchyIsReusedAfterComponentRecreation()
        {
            GameObject host = new("Coverage reused pickup model");
            try
            {
                PickupVisual3D first = PickupVisual3D.AttachExperience(host);
                Transform root = first.ModelRoot;
                UnityEngine.Object.DestroyImmediate(first);

                PickupVisual3D second = PickupVisual3D.AttachExperience(host);
                Assert.That(second.ModelRoot, Is.SameAs(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ShaderFallbackChecksSimpleLitAndSpriteShaders()
        {
            Shader sprite = Shader.Find("Sprites/Default");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(
                PickupVisual3D.FindPickupShader(name =>
                    name == "Universal Render Pipeline/Simple Lit" ? sprite : null
                ),
                Is.SameAs(sprite)
            );
            Assert.That(
                PickupVisual3D.FindPickupShader(name => name == "Sprites/Default" ? sprite : null),
                Is.SameAs(sprite)
            );
        }
    }
}
