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
        public void ShaderResolvesTheSurfaceGraphByResourcePathFirst()
        {
            Shader sprite = Shader.Find("Sprites/Default");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(
                PickupVisual3D.FindPickupShader(
                    path => path == "Brocoli/Shaders/Surface" ? sprite : null,
                    _ => null
                ),
                Is.SameAs(sprite),
                "The surface graph is a game resource, so its path must win over any name."
            );
        }

        [Test]
        public void ShaderFallsBackToTheGraphNameThenToSprites()
        {
            Shader sprite = Shader.Find("Sprites/Default");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(
                PickupVisual3D.FindPickupShader(
                    _ => null,
                    name => name == "BROcoli/Surface" ? sprite : null
                ),
                Is.SameAs(sprite),
                "Before the resource is imported the graph is still reachable by name."
            );
            Assert.That(
                PickupVisual3D.FindPickupShader(
                    _ => null,
                    name => name == "Sprites/Default" ? sprite : null
                ),
                Is.SameAs(sprite),
                "With the graph missing entirely a pickup still renders, rather than magenta."
            );
        }
    }
}
