using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// The HDR grade a scene wants, stated in display terms rather than in either pipeline's
    /// volume components. The calibration solves for nits and for how far the grade should
    /// reshape the picture; turning that into <c>Tonemapping</c>, <c>ColorAdjustments</c> and
    /// <c>LiftGammaGain</c> overrides is the front end's job, because the two pipelines spell
    /// those differently and one of them is absent from any given build.
    /// </summary>
    public readonly struct HdrGradeRequest
    {
        /// <summary>Whether the HDR grade should be applied at all.</summary>
        public bool Enabled { get; }

        /// <summary>The ACES output device transform to tone map through.</summary>
        public HDRRangeReduction AcesPreset { get; }

        /// <summary>Where diffuse white lands, and so how bright the whole picture is.</summary>
        public float PaperWhiteNits { get; }

        /// <summary>The display's black level.</summary>
        public float MinNits { get; }

        /// <summary>The display's calibrated peak.</summary>
        public float MaxNits { get; }

        /// <summary>
        /// Whether the pipeline should read the display's own limits instead of
        /// <see cref="MinNits"/> and <see cref="MaxNits"/>.
        /// </summary>
        public bool DetectBrightnessLimits { get; }

        /// <summary>Chroma given back after the ACES shoulder desaturates it, in percent.</summary>
        public float SaturationLift { get; }

        /// <summary>Contrast added to match the SDR toe, in percent.</summary>
        public float ContrastLift { get; }

        /// <summary>The scene-linear offset that drops the darkest picture to true black.</summary>
        public float BlackFloor { get; }

        public HdrGradeRequest(
            bool enabled,
            HDRRangeReduction acesPreset,
            float paperWhiteNits,
            float minNits,
            float maxNits,
            bool detectBrightnessLimits,
            float saturationLift,
            float contrastLift,
            float blackFloor
        )
        {
            Enabled = enabled;
            AcesPreset = acesPreset;
            PaperWhiteNits = paperWhiteNits;
            MinNits = minNits;
            MaxNits = maxNits;
            DetectBrightnessLimits = detectBrightnessLimits;
            SaturationLift = saturationLift;
            ContrastLift = contrastLift;
            BlackFloor = blackFloor;
        }
    }
}
