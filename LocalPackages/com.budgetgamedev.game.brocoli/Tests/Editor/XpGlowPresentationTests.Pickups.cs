using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public partial class XpGlowPresentationTests
    {
        [Test]
        public void TheOrbBurnsBrighterWhileTheMagnetHasHoldOfIt()
        {
            Assert.That(XpGlowPresentation.IntensityForAttraction(0f), Is.EqualTo(1f));
            Assert.That(XpGlowPresentation.IntensityForAttraction(1f), Is.GreaterThan(1.5f));
            Assert.That(
                XpGlowPresentation.IntensityForAttraction(0.5f),
                Is.GreaterThan(XpGlowPresentation.IntensityForAttraction(0f))
            );
            Assert.That(
                XpGlowPresentation.IntensityForAttraction(4f),
                Is.EqualTo(XpGlowPresentation.IntensityForAttraction(1f)),
                "a blend past one is still a fully pulled orb"
            );
        }

        [Test]
        public void ABoostPickupIsLeftAlone()
        {
            GameObject pickup = new("Boost");
            try
            {
                PickupVisual3D visual = pickup.AddComponent<PickupVisual3D>();
                typeof(PickupVisual3D)
                    .GetMethod(
                        "Initialize",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .Invoke(visual, new object[] { PickupVisual3D.ModelKind.Health });

                Assert.That(FindChild(pickup.transform, PickupVisual3D.GlowCoreName), Is.Null);
                Assert.That(pickup.GetComponent<XpGlowPresentation>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(pickup);
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name)
                    return candidate;
            return null;
        }
    }
}
