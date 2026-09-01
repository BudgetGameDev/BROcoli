using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared
{
    public static partial class GameDisplaySettings
    {
        /// <summary>
        /// The output device transform native HDR output tone maps with. It follows the
        /// calibrated peak so the display's whole range stays reachable.
        /// </summary>
        public static HDRACESPreset HdrToneMapPreset =>
            AcesToneScale.SelectPreset(PeakBrightnessNits);

        /// <summary>
        /// The neutral scene value that the HDR tone map displays at <paramref name="nits"/>,
        /// so calibration patterns can be drawn at a known luminance.
        /// </summary>
        public static float HdrSceneValueForNits(float nits) =>
            AcesToneScale.SceneValueForNits(nits, PaperWhiteNits, HdrToneMapPreset);

        /// <summary>
        /// How far past the calibrated peak the brightest highlights are driven. SDR authors the
        /// flame past display white and lets the clip flatten it, and that blown, flat core is
        /// the look; asking the display for more than it can show reproduces it, because the
        /// panel clips what it cannot reach while bloom carries the spill back down around it.
        /// </summary>
        public const float HighlightOvershoot = 1.3f;

        /// <summary>
        /// <paramref name="hue"/> scaled so the HDR tone map drives its brightest primary past
        /// the calibrated peak by <see cref="HighlightOvershoot"/>. Emissive highlights are
        /// authored through this so the display clips them flat instead of rolling them off.
        /// </summary>
        public static Color HdrSceneColorAtPeakBrightness(Color hue) =>
            AcesToneScale.SceneColorForPeakNits(
                hue,
                PeakBrightnessNits * HighlightOvershoot,
                PaperWhiteNits,
                HdrToneMapPreset
            );

        /// <summary>
        /// <paramref name="color"/> re-authored so the HDR grade renders it as the colour it was
        /// picked as. Interface colours are display referred, but HDR output draws the interface
        /// through the camera, so the grade's toe would otherwise darken and muddy them: an
        /// emerald picked as #43A047 reaches the panel as #259132. Returns the colour untouched
        /// while HDR output is not in effect, where the interface composites after the grade.
        /// </summary>
        public static Color HdrUiColor(Color color)
        {
            if (!IsNativeHdrPlayer || !HdrEnabled || !IsHdrActive)
                return color;

            Vector3 scene = AcesToneScale.SceneColorForDisplayNits(
                new Vector3(color.r, color.g, color.b) * PaperWhiteNits,
                PaperWhiteNits,
                HdrToneMapPreset
            );
            return new Color(scene.x, scene.y, scene.z, color.a);
        }
    }
}
