using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class RenderPipelineProbeTests
    {
        [Test]
        public void EachPipelinesAssetTypeNamesItsPipeline()
        {
            Assert.That(
                RenderPipelineProbe.Classify(
                    "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
                ),
                Is.EqualTo(RenderPipelineKind.Universal)
            );
            Assert.That(
                RenderPipelineProbe.Classify(
                    "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset"
                ),
                Is.EqualTo(RenderPipelineKind.HighDefinition)
            );
        }

        [Test]
        public void AQualityTiersSubclassStillNamesItsPipeline()
        {
            // Both pipelines let a project subclass the asset per quality level, and the
            // tiers this project ships do exactly that.
            Assert.That(
                RenderPipelineProbe.Classify(
                    "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAssetRayTracedUltra"
                ),
                Is.EqualTo(RenderPipelineKind.HighDefinition)
            );
        }

        [Test]
        public void AnythingElseIsUnknownRatherThanGuessed()
        {
            Assert.That(RenderPipelineProbe.Classify(null), Is.EqualTo(RenderPipelineKind.Unknown));
            Assert.That(RenderPipelineProbe.Classify(""), Is.EqualTo(RenderPipelineKind.Unknown));
            Assert.That(
                RenderPipelineProbe.Classify("UnityEngine.Rendering.SomeOtherPipelineAsset"),
                Is.EqualTo(RenderPipelineKind.Unknown)
            );
        }
    }
}
