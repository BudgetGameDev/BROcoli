using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Calibrates glow colors for XP and boost pickups and brightens them during magnet pull.
    /// Each pickup kind shares its materials; only attraction intensity varies per instance.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class XpGlowPresentation : MonoBehaviour
    {
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        /// <summary>
        /// The two hues the effect is built from. Only their direction matters under HDR, where
        /// the calibration decides the length; under SDR they are scaled by the constants below.
        /// </summary>
        private static readonly Color RimHue = new(0.42f, 0.88f, 1f);
        private static readonly Color CoreHue = new(0.05f, 0.45f, 1f);

        /// <summary>
        /// How much of the display's peak each part of the effect is allowed to spend. The rim of
        /// the inner shell is a silhouette a few pixels wide and takes the whole peak, the same
        /// budget the dungeon's torches are given. Everything else is a fraction of it, because
        /// area is what makes a highlight uncomfortable on an OLED and what makes its automatic
        /// brightness limiter pull the rest of the frame down.
        /// </summary>
        private const float CoreShellRimFraction = 1f;
        private const float CoreShellCoreFraction = 0.28f;
        private const float HaloShellRimFraction = 0.3f;
        private const float HaloShellCoreFraction = 0.08f;

        /// <summary>
        /// The SDR grade has no calibration to solve against, so the shells are authored directly
        /// in scene-linear units. Above one on purpose: the tone map's shoulder is what turns the
        /// rim into a hot edge rather than a flat blue line.
        /// </summary>
        private const float SdrCoreShellRim = 3.2f;
        private const float SdrCoreShellCore = 1.5f;
        private const float SdrHaloShellRim = 1.1f;
        private const float SdrHaloShellCore = 0.35f;

        /// <summary>
        /// How much brighter an orb burns while the magnet has hold of it. The pull already
        /// shrinks and wobbles the crystal; lighting it up as well is what makes a collected orb
        /// read as spent rather than as having simply left the screen.
        /// </summary>
        private const float AttractionIntensityGain = 0.9f;

        /// <summary>Below this the property block is cleared rather than rewritten.</summary>
        private const float IntensityEpsilon = 0.002f;

        private PickupVisual3D visual;
        private MeshRenderer[] glowRenderers;
        private MaterialPropertyBlock propertyBlock;
        private float appliedIntensity = 1f;

        private void Awake()
        {
            visual = GetComponent<PickupVisual3D>();
            propertyBlock = new MaterialPropertyBlock();
            CacheRenderers();
        }

        private void OnEnable()
        {
            // A pooled pickup may have been disabled while it was still being attracted.
            // Clear the old override immediately, even before the next animation update.
            if (glowRenderers != null)
                foreach (MeshRenderer renderer in glowRenderers)
                    if (renderer != null)
                        renderer.SetPropertyBlock(null);
            appliedIntensity = 1f;
        }

        private void LateUpdate()
        {
            if (glowRenderers == null)
                return;

            float intensity = IntensityForAttraction(visual == null ? 0f : visual.AttractionBlend);
            if (Mathf.Abs(intensity - appliedIntensity) < IntensityEpsilon)
                return;

            appliedIntensity = intensity;
            bool atRest = Mathf.Abs(intensity - 1f) < IntensityEpsilon;
            foreach (MeshRenderer glowRenderer in glowRenderers)
            {
                if (glowRenderer == null)
                    continue;

                if (atRest)
                {
                    glowRenderer.SetPropertyBlock(null);
                    continue;
                }

                glowRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(IntensityId, intensity);
                glowRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        internal static float IntensityForAttraction(float attractionBlend) =>
            1f + Mathf.Clamp01(attractionBlend) * AttractionIntensityGain;

        internal static void ApplyShellColors(
            Material material,
            PickupVisual3D.GlowShell shell,
            PickupVisual3D.ModelKind kind
        ) =>
            ApplyShellColors(
                material,
                shell,
                GameDisplaySettings.HdrEnabled && GameDisplaySettings.IsHdrActive,
                kind
            );

        internal static void ApplyShellColors(
            Material material,
            PickupVisual3D.GlowShell shell,
            bool hdrActive,
            PickupVisual3D.ModelKind kind = PickupVisual3D.ModelKind.Experience
        )
        {
            if (material == null)
                return;
            (Color core, Color rim) = ShellColors(shell, hdrActive, kind);
            material.SetColor(CoreColorId, core);
            material.SetColor(RimColorId, rim);
            material.SetFloat(IntensityId, 1f);
            material.SetFloat(FadeId, 1f);
        }

        /// <summary>
        /// The scene-linear colours a shell is drawn with. Under HDR both are solved through the
        /// ACES model so the rim lands on the calibrated peak and everything else is a known
        /// fraction of it; under SDR they are the authored values, since there is no peak to
        /// solve against and the output transform clips rather than rolls off.
        /// </summary>
        internal static (Color core, Color rim) ShellColors(
            PickupVisual3D.GlowShell shell,
            bool hdrActive,
            PickupVisual3D.ModelKind kind = PickupVisual3D.ModelKind.Experience
        )
        {
            bool isCoreShell = shell == PickupVisual3D.GlowShell.Core;
            Color coreHue = CoreHue;
            Color rimHue = RimHue;
            if (kind != PickupVisual3D.ModelKind.Experience)
            {
                (_, Color accent, _) = PickupVisual3D.GetPalette(kind);
                coreHue = accent;
                rimHue = Color.Lerp(accent, Color.white, 0.18f);
            }

            if (!hdrActive)
            {
                return (
                    Opaque(coreHue * (isCoreShell ? SdrCoreShellCore : SdrHaloShellCore)),
                    Opaque(rimHue * (isCoreShell ? SdrCoreShellRim : SdrHaloShellRim))
                );
            }

            Color rimPeak = GameDisplaySettings.HdrSceneColorAtPeakBrightness(rimHue);
            Color corePeak = GameDisplaySettings.HdrSceneColorAtPeakBrightness(coreHue);
            return (
                Opaque(corePeak * (isCoreShell ? CoreShellCoreFraction : HaloShellCoreFraction)),
                Opaque(rimPeak * (isCoreShell ? CoreShellRimFraction : HaloShellRimFraction))
            );
        }

        /// <summary>
        /// Scaling a colour scales its alpha too. The pass blends additively and ignores alpha,
        /// but a colour that reads as transparent in the inspector invites someone to fix the
        /// wrong thing later.
        /// </summary>
        private static Color Opaque(Color color) => new(color.r, color.g, color.b, 1f);

        private void CacheRenderers()
        {
            glowRenderers = new[]
            {
                FindShell(PickupVisual3D.GlowCoreName),
                FindShell(PickupVisual3D.GlowHaloName),
            };
        }

        private MeshRenderer FindShell(string shellName)
        {
            foreach (MeshRenderer candidate in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (candidate.gameObject.name == shellName)
                    return candidate;
            }

            return null;
        }
    }
}
