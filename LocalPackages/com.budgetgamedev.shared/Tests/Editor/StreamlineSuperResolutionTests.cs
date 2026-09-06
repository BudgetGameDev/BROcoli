using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class StreamlineSuperResolutionTests
    {
        [Test]
        public void ExecutionEvidenceExpiresAndAnOptionsFailureOverridesEarlierSuccess()
        {
            var status = new StreamlineNative.SuperResolutionStatus
            {
                available = 1,
                optionsResult = 0,
                evaluationResult = 0,
                evaluations = 5,
                attempts = 5,
                evaluationTick = 100,
                snapshotTick = 1000,
            };
            Assert.That(
                StreamlineDlssDiagnostics.Describe(status),
                Does.Contain("dispatch OBSERVED")
            );
            status.snapshotTick = 2000;
            Assert.That(StreamlineDlssDiagnostics.Describe(status), Does.Contain("NOT OBSERVED"));
            status.snapshotTick = 1000;
            status.optionsResult = 5;
            Assert.That(StreamlineDlssDiagnostics.Describe(status), Does.Contain("NOT OBSERVED"));
        }

        [Test]
        public void ConfigurationRemovesBothUnityDlssNamesFromFallbacks()
        {
            var settings = GlobalDynamicResolutionSettings.NewDefault();
            settings.advancedUpscalerNames = new System.Collections.Generic.List<string>
            {
                "DLSS",
                "Deep Learning Super Sampling 4",
                "STP",
            };
            var configured = StreamlineSettings.ConfigureSuperResolution(settings);
            Assert.That(
                configured.advancedUpscalerNames,
                Is.EqualTo(new[] { StreamlineUpscaler.Name, "STP" })
            );
            Assert.That(settings.advancedUpscalerNames.Count, Is.EqualTo(3));
        }

#if ENABLE_UPSCALER_FRAMEWORK
        [Test]
        public void MotionVectorsUseNormalizedCurrentToPreviousCoordinates()
        {
            var size = new Vector2Int(1920, 1080);
            Assert.That(
                StreamlineUpscaler.MotionScale(
                    UpscalingIO.MotionVectorDomain.NDC,
                    UpscalingIO.MotionVectorDirection.PreviousFrameToCurrentFrame,
                    size
                ),
                Is.EqualTo(new Vector2(-1, -1))
            );
            Assert.That(
                StreamlineUpscaler.MotionScale(
                    UpscalingIO.MotionVectorDomain.ScreenSpace,
                    UpscalingIO.MotionVectorDirection.CurrentFrameToPreviousFrame,
                    size
                ),
                Is.EqualTo(new Vector2(1f / 1920, 1f / 1080))
            );
        }

        [Test]
        public void UnsupportedEditorLeavesTheRenderResolutionUntouched()
        {
            var upscaler = new StreamlineUpscaler();
            var input = new Vector2Int(1600, 900);
            upscaler.NegotiatePreUpscaleResolution(ref input, new Vector2Int(1920, 1080));
            Assert.That(input, Is.EqualTo(new Vector2Int(1600, 900)));
            Assert.That(upscaler.supportsXR, Is.False);
        }
#endif
    }
}
