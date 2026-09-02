using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    /// <summary>
    /// Realizes the HDR grade through High Definition's volume components.
    ///
    /// High Definition spells the HDR output parameters exactly as Universal does -- the same
    /// ACES preset, paper white, and brightness limits -- so the grade transfers value for
    /// value and the dungeon reads at the same luminance on both front ends. What differs is
    /// what surrounds it: High Definition meters the scene itself, so the fixed exposure that
    /// keeps the nit ladder honest is set on the pipeline's own volume profile rather than
    /// here, and bloom likewise stays with the scene.
    /// </summary>
    public sealed class HighDefinitionHdrGradeFrontEnd : IHdrGradeFrontEnd
    {
        /// <summary>
        /// High Definition clamps paper white to 400 nits and the display peak to 5000. Values
        /// are clamped here too rather than being silently reshaped by the parameter, so a
        /// calibration that asks for more is capped at a number the grade actually used.
        /// </summary>
        private const float MaxPaperWhiteNits = 400f;
        private const float MaxDisplayPeakNits = 5000f;
        private const float MaxBlackLevelNits = 50f;

        private Volume volume;
        private VolumeProfile profile;
        private Tonemapping tonemapping;
        private ColorAdjustments colorAdjustments;
        private LiftGammaGain liftGammaGain;

        public RenderPipelineKind Pipeline => RenderPipelineKind.HighDefinition;

        public void Attach(GameObject host)
        {
            volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = float.MaxValue;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            tonemapping = profile.Add<Tonemapping>();
            colorAdjustments = profile.Add<ColorAdjustments>();
            liftGammaGain = profile.Add<LiftGammaGain>();
            volume.profile = profile;
        }

        public void Apply(in HdrGradeRequest request)
        {
            if (tonemapping == null)
                return;

            volume.enabled = request.Enabled;
            tonemapping.active = request.Enabled;

            tonemapping.mode.Override(TonemappingMode.ACES);
            tonemapping.acesPreset.Override((HDRACESPreset)request.AcesPreset);
            tonemapping.hueShiftAmount.Override(0f);

            // useFullACES swaps the tone map for the full academy transform, which is graded
            // for a reference projector rather than a desktop panel and lands the dungeon
            // roughly a stop darker. The scene is authored against the same approximation
            // Universal uses, so it stays off.
            tonemapping.useFullACES.Override(false);

            tonemapping.detectPaperWhite.Override(false);
            tonemapping.paperWhite.Override(
                Mathf.Clamp(request.PaperWhiteNits, 0f, MaxPaperWhiteNits)
            );
            tonemapping.detectBrightnessLimits.Override(request.DetectBrightnessLimits);
            tonemapping.minNits.Override(Mathf.Clamp(request.MinNits, 0f, MaxBlackLevelNits));
            tonemapping.maxNits.Override(Mathf.Clamp(request.MaxNits, 0f, MaxDisplayPeakNits));

            colorAdjustments.active = request.Enabled;
            colorAdjustments.saturation.Override(request.SaturationLift);
            colorAdjustments.contrast.Override(request.ContrastLift);

            // Lift's fourth channel is a scene-linear offset in both pipelines: the shared
            // formula zeroes the colour channels and adds w, so a black-only lift is a pure
            // offset. That offset is the floor the SDR transform has and the HDR one does not.
            liftGammaGain.active = request.Enabled;
            liftGammaGain.lift.Override(new Vector4(0f, 0f, 0f, request.BlackFloor));
        }

        public void Detach(
            bool isPlaying,
            Action<UnityEngine.Object> destroyDeferred,
            Action<UnityEngine.Object> destroyImmediate
        )
        {
            if (profile == null)
                return;

            if (isPlaying)
                destroyDeferred(profile);
            else
                destroyImmediate(profile);
            profile = null;
        }
    }
}
