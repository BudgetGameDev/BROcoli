using BudgetGameDev.Shared.Rendering;
using BudgetGameDev.Shared.Rendering.Universal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Tests
{
    public sealed partial class GameDisplaySettingsTests
    {
        [Test]
        public void RuntimeDriverAppliesCalibratedValuesToHighestPriorityGlobalVolume()
        {
            GameObject root = new("HDR Display Driver Test");
            // These assertions target Universal volume components, independently of the Editor pipeline.
            RenderPipelineFrontEnd.OverrideForTests(new UniversalHdrGradeFrontEnd());
            try
            {
                var driver = root.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                driver.Awake();
                driver.Apply(false, true, _ => { });

                Volume volume = root.GetComponent<Volume>();
                Assert.That(volume, Is.Not.Null);
                Assert.That(volume.isGlobal, Is.True);
                Assert.That(volume.priority, Is.EqualTo(float.MaxValue));
                Assert.That(volume.enabled, Is.True);
                Assert.That(volume.profile.TryGet(out Tonemapping tonemapping), Is.True);
                Assert.That(
                    volume.profile.TryGet(out Bloom bloom) && bloom.active,
                    Is.False,
                    "HDR inherits the scene's bloom rather than overriding it"
                );
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(colorAdjustments.active, Is.True);
                Assert.That(colorAdjustments.saturation.value, Is.EqualTo(12f));
                Assert.That(colorAdjustments.contrast.value, Is.EqualTo(17f));
                Assert.That(volume.profile.TryGet(out LiftGammaGain liftGammaGain), Is.True);
                Assert.That(liftGammaGain.active, Is.True);
                Assert.That(
                    liftGammaGain.lift.value.w,
                    Is.LessThan(0f),
                    "the grade needs a floor to reach true black the way SDR does"
                );
                Assert.That(liftGammaGain.lift.value.w, Is.EqualTo(-0.0008f).Within(1e-6f));
                Assert.That(
                    colorAdjustments.postExposure.overrideState,
                    Is.False,
                    "exposure stays with the scene; the HDR grade only reshapes it"
                );
                Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));
                Assert.That(tonemapping.acesPreset.value, Is.EqualTo(HDRACESPreset.ACES1000Nits));
                Assert.That(tonemapping.detectPaperWhite.value, Is.False);
                Assert.That(tonemapping.paperWhite.value, Is.EqualTo(200f));
                Assert.That(tonemapping.detectBrightnessLimits.value, Is.False);
                Assert.That(tonemapping.minNits.value, Is.EqualTo(0.0005f));
                Assert.That(tonemapping.maxNits.value, Is.EqualTo(600f));

                GameDisplaySettings.BeginHdrCalibrationPreview();
                Assert.That(tonemapping.detectBrightnessLimits.value, Is.True);
                GameDisplaySettings.EndHdrCalibrationPreview();
                Assert.That(tonemapping.detectBrightnessLimits.value, Is.False);

                GameDisplaySettings.SetCalibration(775f, 225f, 0.003f);
                Assert.That(tonemapping.maxNits.value, Is.EqualTo(775f));
                Assert.That(tonemapping.paperWhite.value, Is.EqualTo(225f));
                Assert.That(tonemapping.minNits.value, Is.EqualTo(0.003f));
                Assert.That(tonemapping.acesPreset.value, Is.EqualTo(HDRACESPreset.ACES2000Nits));

                GameDisplaySettings.SetCalibration(1200f, 225f, 0.003f);
                Assert.That(
                    tonemapping.acesPreset.value,
                    Is.EqualTo(HDRACESPreset.ACES2000Nits),
                    "a peak the 1000 nit shoulder cannot reach moves up a preset"
                );
                GameDisplaySettings.SetHdrEnabled(false);
                Assert.That(volume.enabled, Is.True);
                Assert.That(tonemapping.active, Is.False);
                Assert.That(bloom.active, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                RenderPipelineFrontEnd.OverrideForTests((IHdrGradeFrontEnd)null);
            }
        }

        [Test]
        public void SdrLeavesTheSceneAtItsOwnContrastAndSaturation()
        {
            GameObject root = new("SDR Display Driver Test");
            // These assertions target Universal volume components, independently of the Editor pipeline.
            RenderPipelineFrontEnd.OverrideForTests(new UniversalHdrGradeFrontEnd());
            try
            {
                var driver = root.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                driver.Awake();
                GameDisplaySettings.SetHdrEnabled(false);
                driver.Apply(false, true, _ => { });

                Volume volume = root.GetComponent<Volume>();
                Assert.That(volume.enabled, Is.True);
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(
                    colorAdjustments.active,
                    Is.False,
                    "SDR renders the scene's own grade, which is flat: no contrast, no saturation"
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                RenderPipelineFrontEnd.OverrideForTests((IHdrGradeFrontEnd)null);
            }
        }

        [Test]
        public void RuntimeDriverFollowsPipelineChangesAndReattachesWhenSwitchingBack()
        {
            var universal = new RecordingHdrGrade(RenderPipelineKind.Universal);
            var highDefinition = new RecordingHdrGrade(RenderPipelineKind.HighDefinition);
            GameObject root = new("Switchable HDR Display Driver Test");
            RenderPipelineFrontEnd.OverrideForTests(universal);
            GameDisplaySettings.HdrDisplayDriver driver = null;
            try
            {
                driver = root.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                driver.Awake();
                driver.Apply(false, true, _ => { });
                Assert.That(universal.AttachCount, Is.EqualTo(1));

                RenderPipelineFrontEnd.OverrideForTests(highDefinition);
                driver.Update();
                Assert.That(universal.DetachCount, Is.EqualTo(1));
                Assert.That(highDefinition.AttachCount, Is.EqualTo(1));
                driver.Apply(false, true, _ => { });
                Assert.That(highDefinition.LastRequest.Enabled, Is.True);
                Assert.That(highDefinition.LastRequest.MaxNits, Is.EqualTo(600f));
                Assert.That(highDefinition.AttachCount, Is.EqualTo(1));

                RenderPipelineFrontEnd.OverrideForTests(universal);
                driver.Apply(false, true, _ => { });
                Assert.That(highDefinition.DetachCount, Is.EqualTo(1));
                Assert.That(universal.AttachCount, Is.EqualTo(2));
                Assert.That(universal.LastRequest.Enabled, Is.True);
            }
            finally
            {
                // EditMode tests invoke lifecycle methods explicitly, just as Awake above.
                driver?.OnDestroy();
                Object.DestroyImmediate(root);
                RenderPipelineFrontEnd.OverrideForTests((IHdrGradeFrontEnd)null);
            }
            Assert.That(universal.DetachCount, Is.EqualTo(2));
        }

        private sealed class RecordingHdrGrade : IHdrGradeFrontEnd
        {
            public RenderPipelineKind Pipeline { get; }
            public int AttachCount { get; private set; }
            public int DetachCount { get; private set; }
            public HdrGradeRequest LastRequest { get; private set; }

            public RecordingHdrGrade(RenderPipelineKind pipeline) => Pipeline = pipeline;

            public void Attach(GameObject host) => AttachCount++;

            public void Apply(in HdrGradeRequest request) => LastRequest = request;

            public void Detach(
                bool isPlaying,
                System.Action<Object> destroyDeferred,
                System.Action<Object> destroyImmediate
            ) => DetachCount++;
        }
    }
}
