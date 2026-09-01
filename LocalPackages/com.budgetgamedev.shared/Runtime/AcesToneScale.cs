using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// A CPU model of the ACES tone map URP applies when native HDR output is active, so content
    /// can be authored against a wanted display luminance instead of a guessed multiplier.
    /// URP tone maps the graded frame with the ACES reference rendering transform followed by an
    /// HDR output device transform, scaling the scene by paper white first: scene value 1 lands
    /// near paper white and the output device transform's shoulder carries highlights up to the
    /// preset's maximum. Nothing else in the pipeline reduces that range on a PQ swapchain, so
    /// these transforms alone decide how many nits a scene value is displayed at.
    /// </summary>
    public static partial class AcesToneScale
    {
        /// <summary>
        /// Scene values are read through URP's LogC grading LUT, which saturates just under 59.
        /// Anything brighter is indistinguishable from this on screen.
        /// </summary>
        public const float MaximumSceneValue = 58.8f;

        /// <summary>
        /// How much of an output device transform's range is left above the calibrated peak.
        /// The shoulder flattens as it approaches its maximum, so a display calibrated close to
        /// the preset's ceiling would need an implausible scene value to reach its own peak.
        /// </summary>
        private const float PresetHeadroom = 1.25f;

        private const int SolverIterations = 40;

        public static float PresetPeakNits(HDRACESPreset preset) =>
            preset switch
            {
                HDRACESPreset.ACES2000Nits => 2000f,
                HDRACESPreset.ACES4000Nits => 4000f,
                _ => 1000f,
            };

        /// <summary>
        /// The smallest output device transform that still leaves shoulder headroom above the
        /// display's calibrated peak. All three share a tone scale below diffuse white, so the
        /// choice changes how far highlights reach, never how the dark scene is rendered.
        /// </summary>
        public static HDRACESPreset SelectPreset(float peakNits)
        {
            if (!float.IsFinite(peakNits) || peakNits <= 0f)
                return HDRACESPreset.ACES1000Nits;
            if (peakNits * PresetHeadroom <= 1000f)
                return HDRACESPreset.ACES1000Nits;
            return peakNits * PresetHeadroom <= 2000f
                ? HDRACESPreset.ACES2000Nits
                : HDRACESPreset.ACES4000Nits;
        }

        /// <summary>The display luminance a neutral scene value is tone mapped to.</summary>
        public static float DisplayNits(
            float sceneValue,
            float paperWhiteNits,
            HDRACESPreset preset
        )
        {
            Vector3 nits = DisplayNits(
                new Vector3(sceneValue, sceneValue, sceneValue),
                paperWhiteNits,
                preset
            );
            return Mathf.Max(nits.x, Mathf.Max(nits.y, nits.z));
        }

        /// <summary>
        /// The per-primary display luminance a scene-linear colour is tone mapped to. Peak
        /// brightness is a per-primary limit, so a saturated colour reaches the display's peak
        /// on one primary long before its luminance would suggest.
        /// </summary>
        public static Vector3 DisplayNits(
            Vector3 sceneColor,
            float paperWhiteNits,
            HDRACESPreset preset
        )
        {
            Vector3 aces =
                Transform(Ap1ToAp0, Transform(SRgbToAp1, Positive(sceneColor)))
                * (paperWhiteNits * 0.01f);
            Vector3 rgbPre = Transform(Ap0ToAp1, ReferenceRenderingTransform(aces));
            Vector3 ap1Nits = new(
                OutputDeviceTransform(rgbPre.x, preset),
                OutputDeviceTransform(rgbPre.y, preset),
                OutputDeviceTransform(rgbPre.z, preset)
            );
            return Transform(XyzToRec709, Transform(D60ToD65, Transform(Ap1ToXyz, ap1Nits)));
        }

        /// <summary>
        /// The neutral scene value that is tone mapped to <paramref name="nits"/>. Used to draw
        /// calibration patterns at a known luminance.
        /// </summary>
        public static float SceneValueForNits(
            float nits,
            float paperWhiteNits,
            HDRACESPreset preset
        )
        {
            if (!float.IsFinite(nits) || nits <= 0f)
                return 0f;

            return SolveSceneScale(Vector3.one, nits, paperWhiteNits, preset);
        }

        /// <summary>
        /// <paramref name="hue"/> scaled so its brightest primary is tone mapped to exactly
        /// <paramref name="peakNits"/>. Highlights authored this way sit at the top of the
        /// display's range without asking it for luminance it cannot show.
        /// </summary>
        public static Color SceneColorForPeakNits(
            Color hue,
            float peakNits,
            float paperWhiteNits,
            HDRACESPreset preset
        )
        {
            Vector3 direction = new(hue.r, hue.g, hue.b);
            float longest = Mathf.Max(direction.x, Mathf.Max(direction.y, direction.z));
            if (longest <= 0f || !float.IsFinite(peakNits) || peakNits <= 0f)
                return Color.black;

            direction /= longest;
            float scale = SolveSceneScale(direction, peakNits, paperWhiteNits, preset);
            return new Color(direction.x * scale, direction.y * scale, direction.z * scale, 1f);
        }

        /// <summary>
        /// Bisects the tone scale, which is monotonic, for the scale that puts the brightest
        /// primary of <paramref name="direction"/> at <paramref name="nits"/>.
        /// </summary>
        private static float SolveSceneScale(
            Vector3 direction,
            float nits,
            float paperWhiteNits,
            HDRACESPreset preset
        )
        {
            if (BrightestPrimaryNits(direction * MaximumSceneValue, paperWhiteNits, preset) <= nits)
                return MaximumSceneValue;

            float low = 0f;
            float high = MaximumSceneValue;
            for (int iteration = 0; iteration < SolverIterations; iteration++)
            {
                float middle = 0.5f * (low + high);
                if (BrightestPrimaryNits(direction * middle, paperWhiteNits, preset) < nits)
                    low = middle;
                else
                    high = middle;
            }
            return 0.5f * (low + high);
        }

        private static float BrightestPrimaryNits(
            Vector3 sceneColor,
            float paperWhiteNits,
            HDRACESPreset preset
        )
        {
            Vector3 nits = DisplayNits(sceneColor, paperWhiteNits, preset);
            return Mathf.Max(nits.x, Mathf.Max(nits.y, nits.z));
        }
    }
}
