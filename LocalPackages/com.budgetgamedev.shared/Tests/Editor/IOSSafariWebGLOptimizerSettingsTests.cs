using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// What the optimizer actually relaxes. The whole point is that it buys frame
    /// time without touching the light budget: the dungeon is lit by two additional
    /// URP lights, and dropping either leaves most of it black on iOS.
    /// </summary>
    public sealed class IOSSafariWebGLOptimizerSettingsTests : IOSSafariWebGLOptimizerTestBase
    {
        [Test]
        public void TheQualityReductionsSpareTheLightingBudget()
        {
            int lightsBefore = QualitySettings.pixelLightCount;
            int levelBefore = QualitySettings.GetQualityLevel();
            QualitySettings.antiAliasing = 4;
            ExpectQualityReport();

            iOSSafariWebGLOptimizer.ApplyLightingSafeQualitySettings();

            Assert.That(QualitySettings.antiAliasing, Is.EqualTo(0));
            Assert.That(QualitySettings.softParticles, Is.False);
            Assert.That(QualitySettings.softVegetation, Is.False);
            Assert.That(QualitySettings.billboardsFaceCameraPosition, Is.False);
            Assert.That(QualitySettings.lodBias, Is.EqualTo(0.5f));
            Assert.That(QualitySettings.maximumLODLevel, Is.EqualTo(2));
            Assert.That(QualitySettings.particleRaycastBudget, Is.EqualTo(16));
            Assert.That(
                QualitySettings.pixelLightCount,
                Is.EqualTo(lightsBefore),
                "dropping pixel lights would leave the dungeon black"
            );
            Assert.That(
                QualitySettings.GetQualityLevel(),
                Is.EqualTo(levelBefore),
                "the quality level is deliberately left alone"
            );
        }

        [Test]
        public void AProjectWithNoPipelineAssetIsReported()
        {
            ExpectPipelineReport(null);

            iOSSafariWebGLOptimizer.ApplyLightingSafeURPSettings(null);
        }

        [Test]
        public void ThePipelineLosesMsaaAndUpscalingButKeepsItsLights()
        {
            ScratchPipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            ScratchPipeline.msaaSampleCount = 4;
            ScratchPipeline.renderScale = 0.5f;
            int lightsBefore = ScratchPipeline.maxAdditionalLightsCount;
            float shadowsBefore = ScratchPipeline.shadowDistance;
            ExpectPipelineReport(ScratchPipeline);

            iOSSafariWebGLOptimizer.ApplyLightingSafeURPSettings(ScratchPipeline);

            Assert.That(ScratchPipeline.msaaSampleCount, Is.EqualTo(1));
            Assert.That(
                ScratchPipeline.renderScale,
                Is.EqualTo(1f),
                "the template already caps the retina buffer, so no pixel is thrown away"
            );
            Assert.That(
                ScratchPipeline.maxAdditionalLightsCount,
                Is.EqualTo(lightsBefore),
                "both dungeon lights are additional URP lights"
            );
            Assert.That(ScratchPipeline.shadowDistance, Is.EqualTo(shadowsBefore));
        }
    }
}
