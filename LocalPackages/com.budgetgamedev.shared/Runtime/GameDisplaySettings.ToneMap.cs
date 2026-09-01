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
        /// Chroma given back to the HDR grade. ACES desaturates as it climbs its shoulder, and
        /// HDR lights the scene harder than SDR does -- the light on the player alone is scaled
        /// 2.2x -- so the same surface sits further up that shoulder and renders paler: measured
        /// against the SDR grade, greens lose 7 to 11 percent of their saturation. This is the
        /// lift that puts them back, in URP's units where zero is no change.
        /// </summary>
        public const float HdrSaturationLift = 12f;

        /// <summary>
        /// Contrast given to the HDR grade, in URP's units where zero is no change. The HDR
        /// output transform has a shallower toe than the SDR one, so the dark end of the picture
        /// renders one and a half to two and a half times brighter than SDR shows it: fogged
        /// distance that reads as black there stays visible here, which both flattens the dungeon
        /// and lets the player see further. This is the fit that puts the fogged range back on
        /// SDR, within a sixth of a stop from a fiftieth of middle grey up to a third of it.
        /// </summary>
        public const float HdrContrastLift = 17f;

        /// <summary>The contrast multiplier the grade applies, as URP forms it.</summary>
        private static float ContrastMultiplier => (HdrContrastLift / 100f) + 1f;

        /// <summary>
        /// <paramref name="hue"/> scaled so the HDR tone map drives its brightest primary past
        /// the calibrated peak by <see cref="HighlightOvershoot"/>. Emissive highlights are
        /// authored through this so the display clips them flat instead of rolling them off.
        /// </summary>
        public static Color HdrSceneColorAtPeakBrightness(Color hue)
        {
            Color solved = AcesToneScale.SceneColorForPeakNits(
                hue,
                PeakBrightnessNits * HighlightOvershoot,
                PaperWhiteNits,
                HdrToneMapPreset
            );
            return UndoGradeContrast(solved);
        }

        /// <summary>
        /// Undoes the contrast the grade is about to apply. Both the flame and the interface are
        /// authored for a luminance the tone map produces, and contrast runs before it, so
        /// without this the grade would move them off the values they were solved for.
        /// </summary>
        private static Color UndoGradeContrast(Color color)
        {
            Vector3 undone = AcesToneScale.ApplyContrast(
                new Vector3(color.r, color.g, color.b),
                1f / ContrastMultiplier
            );
            return new Color(undone.x, undone.y, undone.z, color.a);
        }

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
            return UndoGradeContrast(new Color(scene.x, scene.y, scene.z, color.a));
        }
    }
}
