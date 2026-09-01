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
        /// <paramref name="hue"/> scaled so the HDR tone map displays its brightest primary at
        /// the calibrated peak brightness. Emissive highlights are authored through this so they
        /// reach the top of the display's range without being asked for luminance it cannot show.
        /// </summary>
        public static Color HdrSceneColorAtPeakBrightness(Color hue) =>
            AcesToneScale.SceneColorForPeakNits(
                hue,
                PeakBrightnessNits,
                PaperWhiteNits,
                HdrToneMapPreset
            );
    }
}
