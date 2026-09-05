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
        public void EveryBoostHasItsOwnColoredRimAndHalo([Values] PickupVisual3D.ModelKind kind)
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
                    .Invoke(visual, new object[] { kind });

                Transform core = FindChild(pickup.transform, PickupVisual3D.GlowCoreName);
                Transform halo = FindChild(pickup.transform, PickupVisual3D.GlowHaloName);
                Assert.That(core, Is.Not.Null);
                Assert.That(halo, Is.Not.Null);
                Assert.That(pickup.GetComponent<XpGlowPresentation>(), Is.Not.Null);
                Assert.That(pickup.GetComponentsInChildren<Light>(), Is.Empty);
                Material material = core.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(
                    material,
                    Is.SameAs(PickupVisual3D.GetGlowMaterial(PickupVisual3D.GlowShell.Core, kind))
                );
                if (kind != PickupVisual3D.ModelKind.Experience)
                {
                    Assert.That(core.parent.name, Is.EqualTo("Token Face"));
                    Assert.That(
                        core.GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(
                            core.parent.Find("Token Rim").GetComponent<MeshFilter>().sharedMesh
                        )
                    );
                    Assert.That(
                        material,
                        Is.Not.SameAs(PickupVisual3D.GetGlowMaterial(PickupVisual3D.GlowShell.Core))
                    );
                    (_, Color accent, _) = PickupVisual3D.GetPalette(kind);
                    (Color color, Color rim) = XpGlowPresentation.ShellColors(
                        PickupVisual3D.GlowShell.Core,
                        false,
                        kind
                    );
                    Assert.That(
                        color.r / color.maxColorComponent,
                        Is.EqualTo(accent.r / accent.maxColorComponent).Within(0.001f)
                    );
                    Assert.That(
                        color.g / color.maxColorComponent,
                        Is.EqualTo(accent.g / accent.maxColorComponent).Within(0.001f)
                    );
                    Assert.That(rim.maxColorComponent, Is.GreaterThan(1f));
                }
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
