using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Authors the experience orb's glow against the display it is actually shown on, and drives
    /// the part of the effect that belongs to one orb rather than to all of them.
    ///
    /// The look is split in two on purpose. Colour is the same for every orb in the dungeon, so
    /// it lives on the shared materials and is re-solved only when the calibration changes.
    /// Intensity follows the magnet pull, which differs per orb, so it goes through a property
    /// block -- and only while an orb is actually being pulled, so an idle floor full of orbs
    /// costs nothing.
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
            GameDisplaySettings.ValuesChanged += ApplyDisplayColors;
            ApplyDisplayColors();
            appliedIntensity = 1f;
        }

        private void OnDisable()
        {
            GameDisplaySettings.ValuesChanged -= ApplyDisplayColors;
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

        /// <summary>
        /// Re-solves both shells against the current display. Idempotent, and every orb calls it,
        /// which is what keeps an orb spawned after a calibration change from being authored for
        /// the display the game started on.
        /// </summary>
        internal static void ApplyDisplayColors()
        {
            bool hdrActive = GameDisplaySettings.HdrEnabled && GameDisplaySettings.IsHdrActive;
            ApplyShellColors(PickupVisual3D.GlowShell.Core, hdrActive);
            ApplyShellColors(PickupVisual3D.GlowShell.Halo, hdrActive);
        }

        private static void ApplyShellColors(PickupVisual3D.GlowShell shell, bool hdrActive) =>
            ApplyShellColors(PickupVisual3D.GetGlowMaterial(shell), shell, hdrActive);

        internal static void ApplyShellColors(
            Material material,
            PickupVisual3D.GlowShell shell,
            bool hdrActive
        )
        {
            if (material == null)
                return;

            (Color core, Color rim) = ShellColors(shell, hdrActive);
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
            bool hdrActive
        )
        {
            bool isCoreShell = shell == PickupVisual3D.GlowShell.Core;

            if (!hdrActive)
            {
                return (
                    Opaque(CoreHue * (isCoreShell ? SdrCoreShellCore : SdrHaloShellCore)),
                    Opaque(RimHue * (isCoreShell ? SdrCoreShellRim : SdrHaloShellRim))
                );
            }

            Color rimPeak = GameDisplaySettings.HdrSceneColorAtPeakBrightness(RimHue);
            Color corePeak = GameDisplaySettings.HdrSceneColorAtPeakBrightness(CoreHue);
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
