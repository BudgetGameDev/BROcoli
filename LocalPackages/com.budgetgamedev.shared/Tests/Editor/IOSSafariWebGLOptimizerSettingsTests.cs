using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// What the relief valve gives up and what it refuses to give up. The dungeon is
    /// lit almost entirely by the pixel-light budget and by two additional
    /// URP lights, and dropping either leaves most of it black on iOS.
    /// </summary>
    public sealed class IOSSafariWebGLOptimizerSettingsTests : IOSSafariWebGLOptimizerTestBase
    {
        [Test]
        public void TheQualityReductionsSpareTheLightingBudget()
        {
            int lightsBefore = QualitySettings.pixelLightCount;
            int levelBefore = QualitySettings.GetQualityLevel();
            ExpectQualityReport();

            iOSSafariWebGLOptimizer.ApplyLightingSafeQualitySettings();

            iOSSafariWebGLOptimizer.QualitySnapshot applied = WrittenQuality.Value;
            Assert.That(applied.AntiAliasing, Is.EqualTo(0));
            Assert.That(applied.SoftParticles, Is.False);
            Assert.That(applied.SoftVegetation, Is.False);
            Assert.That(applied.BillboardsFaceCameraPosition, Is.False);
            Assert.That(applied.LodBias, Is.EqualTo(0.5f));
            Assert.That(applied.MaximumLodLevel, Is.EqualTo(2));
            Assert.That(applied.ParticleRaycastBudget, Is.EqualTo(16));
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

        /// <summary>
        /// The one check that lets the reductions through to the project settings for
        /// real. It hands over what the project already holds, so the write it makes
        /// is the write the project would make to itself and no run, finished or
        /// interrupted, can leave the editor's MSAA switched off.
        /// </summary>
        [Test]
        public void TheDefaultRouteWritesTheSnapshotStraightToTheProject()
        {
            iOSSafariWebGLOptimizer.ResetStatics();
            iOSSafariWebGLOptimizer.QualitySnapshot unchanged = iOSSafariWebGLOptimizer
                .QualitySnapshot
                .Current;

            iOSSafariWebGLOptimizer.WriteQuality(unchanged);

            Assert.That(
                iOSSafariWebGLOptimizer.QualitySnapshot.Current.ToString(),
                Is.EqualTo(unchanged.ToString())
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
