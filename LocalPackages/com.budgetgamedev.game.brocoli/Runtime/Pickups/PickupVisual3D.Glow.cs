using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class PickupVisual3D
    {
        internal const string GlowShaderResource = "Brocoli/Shaders/XpEnergyGlow";
        internal const string GlowCoreName = "XP Glow Core";
        internal const string GlowHaloName = "XP Glow Halo";

        /// <summary>
        /// Which shell of the effect a renderer is. The two are the same shader with the same
        /// animation; they differ in how much of the display's range they are allowed to spend,
        /// which is the whole reason they are separate objects.
        /// </summary>
        internal enum GlowShell
        {
            /// <summary>The crystal's own surface, just clear of it.</summary>
            Core,

            /// <summary>The wide, inside-out shell that stands in for a volumetric halo.</summary>
            Halo,
        }

        private static readonly Dictionary<GlowShell, Material> GlowMaterials = new();

        /// <summary>
        /// Just clear of the crystal, in the crystal's own frame. The core glow is drawn on a
        /// copy of the gem rather than on a sphere so its silhouette is the crystal's: a sphere
        /// wide enough to contain the gem also hides it, and the orb stops reading as a cut
        /// stone. The facets are what the rim then breaks over.
        /// </summary>
        private static readonly Quaternion CoreShellFrame = Quaternion.Euler(0f, 0f, 22.5f);
        private static readonly Vector3 CoreShellScale = new(0.6f, 0.6f, 0.85f);

        /// <summary>
        /// The halo stands well outside the crystal and is round rather than faceted, so it
        /// reads as light gathered around the orb instead of a second crystal.
        /// </summary>
        private static readonly Vector3 HaloShellScale = new(1.25f, 1.25f, 1.25f);

        /// <summary>
        /// Wraps the crystal in its two glow shells and gives the pickup the component that
        /// colours them. Safe to call on an orb that already has them, which is what a pooled
        /// orb -- and a prefab baked before the glow existed -- comes back as.
        ///
        /// Both shells are siblings of the crystal rather than children of it, so neither
        /// inherits the crystal's own scale on top of its authored one.
        /// </summary>
        private void EnsureExperienceGlow()
        {
            if (Kind != ModelKind.Experience || modelRoot == null)
                return;

            if (modelRoot.Find(GlowCoreName) == null)
            {
                CreateGlowPart(
                    modelRoot,
                    GlowCoreName,
                    GlowShell.Core,
                    GetGemMesh(),
                    CoreShellFrame,
                    CoreShellScale
                );
            }

            if (modelRoot.Find(GlowHaloName) == null)
            {
                CreateGlowPart(
                    modelRoot,
                    GlowHaloName,
                    GlowShell.Halo,
                    GetGlowSphereMesh(),
                    Quaternion.identity,
                    HaloShellScale
                );
            }

            if (GetComponent<XpGlowPresentation>() == null)
                gameObject.AddComponent<XpGlowPresentation>();
        }

        /// <summary>
        /// The halo is drawn from the inside. Culling its near faces leaves only the far wall of
        /// the shell, whose falloff then reads as light gathered around the crystal instead of a
        /// second gem hanging in front of it.
        /// </summary>
        internal static CullMode GlowCullMode(GlowShell shell) =>
            shell == GlowShell.Core ? CullMode.Back : CullMode.Front;

        private void CreateGlowPart(
            Transform parent,
            string partName,
            GlowShell shell,
            Mesh mesh,
            Quaternion rotation,
            Vector3 scale
        )
        {
            GameObject part = new(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.layer = gameObject.layer;
            Transform partTransform = part.transform;
            partTransform.SetParent(parent, false);
            partTransform.localPosition = Vector3.zero;
            partTransform.localRotation = rotation;
            partTransform.localScale = scale;

            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            // The gem is split into three facet groups. One material per submesh, all the same,
            // or a renderer given a single material would draw only the first third of it.
            renderer.sharedMaterials = GlowMaterialsFor(shell, mesh.subMeshCount);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private static Material[] GlowMaterialsFor(GlowShell shell, int subMeshCount)
        {
            Material material = GetGlowMaterial(shell);
            Material[] materials = new Material[Mathf.Max(subMeshCount, 1)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            return materials;
        }

        internal static Material GetGlowMaterial(GlowShell shell) =>
            GetGlowMaterial(shell, Resources.Load<Shader>, Shader.Find);

        internal static Material GetGlowMaterial(
            GlowShell shell,
            System.Func<string, Shader> loadResource,
            System.Func<string, Shader> findShader
        )
        {
            if (GlowMaterials.TryGetValue(shell, out Material cached) && cached != null)
                return cached;

            Shader shader = FindGlowShader(loadResource, findShader);
            if (shader == null)
                return null;

            Material material = new(shader) { name = $"XpEnergyGlow {shell}" };
            ApplyGlowShape(material, shell);
            GlowMaterials[shell] = material;
            return material;
        }

        /// <summary>
        /// The animation each shell is authored with. Colour is deliberately absent: it belongs
        /// to <see cref="XpGlowPresentation"/>, which solves it against the display every time
        /// the calibration changes.
        /// </summary>
        internal static void ApplyGlowShape(Material material, GlowShell shell)
        {
            material.SetFloat("_Cull", (float)GlowCullMode(shell));

            if (shell == GlowShell.Core)
            {
                // A rim: hot along the crystal's silhouette, thin over its face, so the gem's
                // own facets stay visible through it.
                material.SetFloat("_FalloffInverted", 0f);
                material.SetFloat("_FresnelPower", 2.4f);
                material.SetFloat("_FresnelBias", 0.03f);
                material.SetFloat("_BandScale", 5.5f);
                material.SetFloat("_BandSpeed", 1.9f);
                material.SetFloat("_BandSharpness", 5f);
                material.SetFloat("_BandStrength", 0.6f);
                material.SetFloat("_PulseSpeed", 4.1f);
                material.SetFloat("_PulseAmount", 0.22f);
                material.SetFloat("_FlickerSpeed", 19f);
                material.SetFloat("_FlickerAmount", 0.2f);
                return;
            }

            // The halo is broad, so its bands are stretched and slowed and its flicker is
            // halved. Fast detail over that much of the screen reads as noise, and on an OLED
            // it is also the part of the effect a player's eye tracks in the dark.
            //
            // Its falloff is inverted, and no bias is added under it: a glow has to reach zero
            // at its own edge or it draws a disc with a visible rim instead of light.
            material.SetFloat("_FalloffInverted", 1f);
            material.SetFloat("_FresnelPower", 2.2f);
            material.SetFloat("_FresnelBias", 0f);
            material.SetFloat("_BandScale", 2.2f);
            material.SetFloat("_BandSpeed", 0.85f);
            material.SetFloat("_BandSharpness", 3f);
            material.SetFloat("_BandStrength", 0.35f);
            material.SetFloat("_PulseSpeed", 2.6f);
            material.SetFloat("_PulseAmount", 0.3f);
            material.SetFloat("_FlickerSpeed", 11f);
            material.SetFloat("_FlickerAmount", 0.1f);
        }

        /// <summary>
        /// The glow shader, by resource path first so an unloaded game cannot leave the name
        /// resolving to another package's shader, then by name for the edit-time case where the
        /// resource has not been imported yet.
        /// </summary>
        internal static Shader FindGlowShader(
            System.Func<string, Shader> load,
            System.Func<string, Shader> find
        )
        {
            Shader shader = load(GlowShaderResource);
            return shader != null ? shader : find("BROcoli/XP Energy Glow");
        }
    }
}
