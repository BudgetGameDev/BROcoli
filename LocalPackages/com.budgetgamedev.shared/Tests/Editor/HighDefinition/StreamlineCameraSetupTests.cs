using BudgetGameDev.Shared.Rendering.HighDefinition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class StreamlineCameraSetupTests
    {
        [Test]
        public void UnchangedDynamicResolutionDoesNotRebuildHdrpEveryFrame()
        {
            var asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
            try
            {
                var original = asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings;
                var desired = BudgetGameDev.Shared.Rendering.StreamlineSettings.ConfigureSuperResolution(original);
                desired.forceResolution = true;
                desired.forcedPercentage = 66.66667f;
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, desired), Is.True);
                for (int frame = 0; frame < 120; ++frame)
                {
                    desired.advancedUpscalerNames = new System.Collections.Generic.List<string>(desired.advancedUpscalerNames);
                    Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, desired), Is.False);
                }
                desired.forcedPercentage = 50;
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, desired), Is.True);
                Assert.That(asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings.forcedPercentage, Is.EqualTo(50));
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, desired), Is.False);
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, original), Is.True);
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, original), Is.False);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void UpscalerPriorityChangesAreAppliedWithoutMutatingOtherPipelineSettings()
        {
            var asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
            try
            {
                var before = asset.currentPlatformRenderPipelineSettings;
                var desired = before.dynamicResolutionSettings;
                desired.advancedUpscalerNames = new System.Collections.Generic.List<string> { "NVIDIA Streamline DLSS", "STP" };
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, desired), Is.True);
                desired.advancedUpscalerNames = new System.Collections.Generic.List<string> { "STP", "NVIDIA Streamline DLSS" };
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, desired), Is.True);
                Assert.That(asset.currentPlatformRenderPipelineSettings.supportRayTracing, Is.EqualTo(before.supportRayTracing));
                Assert.That(HighDefinitionStreamline.ApplyDynamicResolutionSettings(asset, desired), Is.False);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void CommonSceneCameraGetsHdrpDataBeforeFrameSettingsAreBuilt()
        {
            var host = new GameObject("Common scene camera");
            try
            {
                var camera = host.AddComponent<Camera>();
                var urp = host.AddComponent<UniversalAdditionalCameraData>();
                var adapter = new HighDefinitionStreamline();
                Assert.That(adapter.SupportsCamera(camera), Is.True);
                Assert.That(adapter.SupportsCamera(camera), Is.True);
                Assert.That(host.GetComponents<HDAdditionalCameraData>(), Has.Length.EqualTo(1));
                Assert.That(host.GetComponent<UniversalAdditionalCameraData>(), Is.SameAs(urp));
                adapter.ConfigureCamera(camera, true);
                Assert.That(camera.allowDynamicResolution, Is.True);
                Assert.That(
                    host.GetComponent<HDAdditionalCameraData>().allowDynamicResolution,
                    Is.True
                );
                Assert.That(
                    host.GetComponent<HDAdditionalCameraData>().allowDeepLearningSuperSampling,
                    Is.False
                );
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
