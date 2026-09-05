using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class XpGlowPresentationTests
    {
        [Test]
        public void AnExperienceOrbIsWrappedInBothGlowShells()
        {
            GameObject pickup = new("XP");
            try
            {
                PickupVisual3D.AttachExperience(pickup);

                Transform core = FindChild(pickup.transform, PickupVisual3D.GlowCoreName);
                Transform halo = FindChild(pickup.transform, PickupVisual3D.GlowHaloName);
                Assert.That(core, Is.Not.Null, "the crystal has no rim");
                Assert.That(halo, Is.Not.Null, "the crystal has no halo");

                Assert.That(
                    pickup.GetComponent<XpGlowPresentation>(),
                    Is.Not.Null,
                    "without this the shells keep whatever colour the shader defaults to"
                );
            }
            finally
            {
                Object.DestroyImmediate(pickup);
            }
        }

        [Test]
        public void BothShellsUseTheGlowShaderAndQueueAsTransparent()
        {
            GameObject pickup = new("XP");
            try
            {
                PickupVisual3D.AttachExperience(pickup);

                foreach (
                    string shellName in new[]
                    {
                        PickupVisual3D.GlowCoreName,
                        PickupVisual3D.GlowHaloName,
                    }
                )
                {
                    Transform shell = FindChild(pickup.transform, shellName);
                    Material material = shell.GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.That(material, Is.Not.Null, shellName);
                    Assert.That(material.shader.name, Is.EqualTo("BROcoli/XP Energy Glow"));
                    Assert.That(
                        material.renderQueue,
                        Is.GreaterThanOrEqualTo(3000),
                        $"{shellName} has to be drawn after the dungeon it glows over"
                    );

                    MeshRenderer renderer = shell.GetComponent<MeshRenderer>();
                    Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                    Assert.That(renderer.receiveShadows, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(pickup);
            }
        }

        /// <summary>
        /// The one property of this effect that a screenshot cannot show and a player would
        /// notice on an OLED: it only ever adds light. Blending it any other way would paint a
        /// faintly lit rectangle over pixels the panel is otherwise keeping switched off, and
        /// writing depth would make the orbs cut holes in each other.
        /// </summary>
        [Test]
        public void EveryPipelinesPassIsAdditiveAndWritesNoDepth()
        {
            string source = System.IO.File.ReadAllText(
                "Packages/com.budgetgamedev.game.brocoli/Resources/Brocoli/Shaders/"
                    + "XpEnergyGlow.shader"
            );

            int passes = System
                .Text.RegularExpressions.Regex.Matches(source, @"Name\s+""XpEnergyGlow""")
                .Count;
            Assert.That(
                passes,
                Is.EqualTo(2),
                "one pass for Universal and one for High Definition"
            );
            Assert.That(
                System.Text.RegularExpressions.Regex.Matches(source, @"Blend\s+One\s+One").Count,
                Is.EqualTo(passes)
            );
            Assert.That(
                System.Text.RegularExpressions.Regex.Matches(source, @"ZWrite\s+Off").Count,
                Is.EqualTo(passes)
            );
            Assert.That(source, Does.Contain("\"RenderPipeline\" = \"UniversalPipeline\""));
            Assert.That(source, Does.Contain("\"RenderPipeline\" = \"HDRenderPipeline\""));
            Assert.That(
                source,
                Does.Not.Contain("com.unity.render-pipelines.high-definition"),
                "including High Definition's headers would fail to compile in this project, "
                    + "which does not have that package"
            );
        }

        [Test]
        public void TheShaderCompilesForTheActivePipeline()
        {
            Shader glow = Resources.Load<Shader>("Brocoli/Shaders/XpEnergyGlow");
            Assert.That(glow, Is.Not.Null, "the shader is loaded by resource path at runtime");
            Assert.That(glow.isSupported, Is.True);
            Assert.That(
                UnityEditor.ShaderUtil.GetShaderMessageCount(glow),
                Is.EqualTo(0),
                "a subshader that cannot compile leaves a permanent error in the console"
            );
        }

        [Test]
        public void EveryShellTriangleIsCoveredByAMaterial()
        {
            GameObject pickup = new("XP");
            try
            {
                PickupVisual3D.AttachExperience(pickup);

                foreach (
                    string shellName in new[]
                    {
                        PickupVisual3D.GlowCoreName,
                        PickupVisual3D.GlowHaloName,
                    }
                )
                {
                    Transform shell = FindChild(pickup.transform, shellName);
                    Mesh mesh = shell.GetComponent<MeshFilter>().sharedMesh;
                    MeshRenderer renderer = shell.GetComponent<MeshRenderer>();
                    Assert.That(
                        renderer.sharedMaterials.Length,
                        Is.EqualTo(mesh.subMeshCount),
                        $"{shellName} would draw only part of itself: the gem is split across "
                            + "facet groups and a renderer draws one submesh per material"
                    );
                    foreach (Material material in renderer.sharedMaterials)
                        Assert.That(material, Is.Not.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(pickup);
            }
        }

        [Test]
        public void TheHaloIsSmoothAndRoundWhileTheRimFollowsTheCrystalsFacets()
        {
            GameObject pickup = new("XP");
            try
            {
                PickupVisual3D.AttachExperience(pickup);

                Mesh halo = FindChild(pickup.transform, PickupVisual3D.GlowHaloName)
                    .GetComponent<MeshFilter>()
                    .sharedMesh;
                Mesh core = FindChild(pickup.transform, PickupVisual3D.GlowCoreName)
                    .GetComponent<MeshFilter>()
                    .sharedMesh;

                Assert.That(halo.subMeshCount, Is.EqualTo(1));
                Assert.That(
                    halo.vertexCount,
                    Is.GreaterThan(core.vertexCount),
                    "the halo is a sphere; a faceted halo renders as visible triangles"
                );

                Vector3[] normals = halo.normals;
                Vector3[] vertices = halo.vertices;
                for (int i = 0; i < vertices.Length; i += 17)
                {
                    Assert.That(
                        Vector3.Dot(normals[i], vertices[i].normalized),
                        Is.EqualTo(1f).Within(0.001f),
                        "a shell's normals have to be its own directions, or the falloff "
                            + "breaks at every seam"
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(pickup);
            }
        }

        [Test]
        public void TheRimIsAuthoredAgainstTheDisplaysPeakAndEverythingElseBelowIt()
        {
            (Color sdrCore, Color sdrRim) = XpGlowPresentation.ShellColors(
                PickupVisual3D.GlowShell.Core,
                false
            );
            Assert.That(sdrRim.b, Is.GreaterThan(1f), "the rim outruns diffuse white");
            Assert.That(sdrRim.b, Is.GreaterThan(sdrRim.g));
            Assert.That(sdrRim.g, Is.GreaterThan(sdrRim.r), "the orb reads blue, not white");
            Assert.That(sdrCore.b, Is.LessThan(sdrRim.b));
            Assert.That(sdrCore.a, Is.EqualTo(1f).Within(0.001f));

            (Color haloCore, Color haloRim) = XpGlowPresentation.ShellColors(
                PickupVisual3D.GlowShell.Halo,
                false
            );
            Assert.That(
                haloRim.b,
                Is.LessThan(sdrRim.b),
                "the halo covers far more of the screen than the rim and must not be as hot"
            );
            Assert.That(haloCore.b, Is.LessThan(sdrCore.b));
        }

        [Test]
        public void UnderHdrTheShellsAreSolvedAgainstTheCalibrationRatherThanGuessed()
        {
            float peak = GameDisplaySettings.PeakBrightnessNits;
            try
            {
                GameDisplaySettings.SetPeakBrightness(1000f);
                (_, Color brightRim) = XpGlowPresentation.ShellColors(
                    PickupVisual3D.GlowShell.Core,
                    true
                );

                GameDisplaySettings.SetPeakBrightness(400f);
                (_, Color dimRim) = XpGlowPresentation.ShellColors(
                    PickupVisual3D.GlowShell.Core,
                    true
                );

                Assert.That(
                    brightRim.b,
                    Is.GreaterThan(dimRim.b),
                    "a display calibrated brighter has to be asked for more, or the orb is "
                        + "authored for a screen nobody is using"
                );
                Assert.That(dimRim.b, Is.GreaterThan(0f));
                Assert.That(brightRim.a, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                GameDisplaySettings.SetPeakBrightness(peak);
            }
        }

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
