using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Rendering.Universal
{
    /// <summary>
    /// Applies native HDR calibration or SDR highlight shaping through Universal's volumes.
    /// HDR inherits the scene's authored bloom. SDR gives bright cores a stronger shoulder
    /// and a tighter glow while retaining the scene's exposure, contrast and saturation.
    /// </summary>
    public sealed class UniversalHdrGradeFrontEnd : IHdrGradeFrontEnd
    {
        private Volume volume;
        private VolumeProfile profile;
        private Tonemapping tonemapping;
        private ColorAdjustments colorAdjustments;
        private LiftGammaGain liftGammaGain;
        private ShadowsMidtonesHighlights sdrHighlights;
        private Bloom sdrBloom;

        public RenderPipelineKind Pipeline => RenderPipelineKind.Universal;

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
            sdrHighlights = profile.Add<ShadowsMidtonesHighlights>();
            sdrBloom = profile.Add<Bloom>();
            volume.profile = profile;
        }

        public void Apply(in HdrGradeRequest request)
        {
            if (tonemapping == null)
                return;

            volume.enabled = true;
            sdrHighlights.active = !request.Enabled;
            // Positive wheel weight adds four times w to the linear RGB gain: 1.35 here.
            sdrHighlights.highlights.Override(new Vector4(1f, 1f, 1f, 0.0875f));
            sdrHighlights.highlightsStart.Override(0.5f);
            sdrHighlights.highlightsEnd.Override(1f);
            sdrBloom.active = !request.Enabled;
            sdrBloom.threshold.Override(1f);
            sdrBloom.scatter.Override(0.62f);
            tonemapping.active = request.Enabled;

            // The scene is graded for ACES in SDR. Neutral tone mapping has no filmic curve at
            // all on an HDR swapchain: it scales the scene straight into nits, which lifts the
            // dungeon's shadows several times above what SDR shows and leaves the picture milky.
            // ACES keeps the SDR tone curve below diffuse white and spends the display's extra
            // range on the highlights instead.
            tonemapping.mode.Override(TonemappingMode.ACES);
            tonemapping.acesPreset.Override((HDRACESPreset)request.AcesPreset);
            tonemapping.hueShiftAmount.Override(0f);

            // Paper white decides where diffuse white lands, and therefore how bright the whole
            // picture is. The calibration seeds itself from the operating system, so this is the
            // system's value until the player overrides it; taking it from the request keeps it
            // in step with the luminance the torches solve for.
            tonemapping.detectPaperWhite.Override(false);
            tonemapping.paperWhite.Override(request.PaperWhiteNits);
            tonemapping.detectBrightnessLimits.Override(request.DetectBrightnessLimits);
            tonemapping.minNits.Override(request.MinNits);
            tonemapping.maxNits.Override(request.MaxNits);

            // Chroma and the toe. Exposure stays where the scene sets it, so the HDR picture is
            // the SDR one re-seated on the HDR transform rather than a second grade.
            colorAdjustments.active = request.Enabled;
            colorAdjustments.saturation.Override(request.SaturationLift);
            colorAdjustments.contrast.Override(request.ContrastLift);

            // Universal folds lift's fourth channel straight into a scene-linear offset, and the
            // grade clamps at zero afterwards, which is the black floor the SDR transform has and
            // the HDR one does not.
            liftGammaGain.active = request.Enabled;
            liftGammaGain.lift.Override(new Vector4(0f, 0f, 0f, request.BlackFloor));
        }

        public void Detach(
            bool isPlaying,
            Action<UnityEngine.Object> destroyDeferred,
            Action<UnityEngine.Object> destroyImmediate
        )
        {
            Action<UnityEngine.Object> destroy = isPlaying ? destroyDeferred : destroyImmediate;
            if (volume != null)
            {
                // Destroy is deferred in play mode. Stop contributing to the stack immediately
                // when a quality change moves the driver to the other pipeline.
                volume.enabled = false;
                volume.sharedProfile = null;
                destroy(volume);
                volume = null;
            }
            if (profile != null)
            {
                foreach (VolumeComponent component in profile.components)
                    destroy(component);
                destroy(profile);
                profile = null;
            }
            tonemapping = null;
            colorAdjustments = null;
            liftGammaGain = null;
            sdrHighlights = null;
            sdrBloom = null;
        }
    }
}
