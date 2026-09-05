using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

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

        [Test]
        public void CurrentPipelinePropertiesAndNullAssetLookupAreSafe()
        {
            RenderPipelineKind current = RenderPipelineProbe.Current;
            Assert.That(
                RenderPipelineProbe.IsUniversal,
                Is.EqualTo(current == RenderPipelineKind.Universal)
            );
            Assert.That(
                RenderPipelineProbe.IsHighDefinition,
                Is.EqualTo(current == RenderPipelineKind.HighDefinition)
            );
            Assert.That(RenderPipelineProbe.AssetTypeName(null), Is.Null);
            Assert.That(
                RenderPipelineProbe.AssetTypeName(GraphicsSettings.currentRenderPipeline),
                Is.Not.Empty
            );
            TestPipelineAsset unknown = ScriptableObject.CreateInstance<TestPipelineAsset>();
            try
            {
                Assert.That(
                    RenderPipelineProbe.AssetTypeName(unknown),
                    Does.Contain("TestPipelineAsset")
                );
            }
            finally
            {
                Object.DestroyImmediate(unknown);
            }
        }

        private sealed class TestPipelineAsset : RenderPipelineAsset
        {
            protected override RenderPipeline CreatePipeline() => null;
        }
    }
}
