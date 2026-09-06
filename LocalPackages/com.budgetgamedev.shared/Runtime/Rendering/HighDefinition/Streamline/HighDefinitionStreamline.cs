using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    internal sealed class HighDefinitionStreamline : IStreamlinePipeline
    {
        private HDRenderPipelineAsset asset;
        private GlobalDynamicResolutionSettings original;
        private StreamlineFinalFrame finalFrame;
        private CustomPassVolume volume;
        internal static HighDefinitionStreamline Instance { get; private set; }
        public bool IsActive => GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset;
        public bool CanCapture => finalFrame != null;

        public bool SupportsCamera(Camera camera) =>
            camera.TryGetComponent<HDAdditionalCameraData>(out _);

        public Vector2 GetJitter(Camera camera, Vector2 requested)
        {
            var actual = HDCamera.GetOrCreate(camera).taaJitter;
            return new Vector2(-actual.x, -actual.y);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() =>
            StreamlineRuntime.Register(new HighDefinitionStreamline());

        public void Attach(GameObject host)
        {
            Instance = this;
            finalFrame = new StreamlineFinalFrame();
            if (!finalFrame.Attach())
            {
                finalFrame.Dispose();
                finalFrame = null;
                Debug.LogWarning(
                    "[Streamline] HDRP final-color hook unavailable; frame generation disabled."
                );
                return;
            }
            volume = host.AddComponent<CustomPassVolume>();
            volume.isGlobal = true;
            volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            volume.customPasses.Add(new StreamlineInputsPass());
        }

        internal void CaptureSdrUi(CustomPassContext context) => finalFrame?.CaptureSdrUi(context);

        public void Configure(bool superResolution)
        {
            var next = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (asset != next)
            {
                Restore();
                asset = next;
                original = asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings;
            }
            var settings = asset.currentPlatformRenderPipelineSettings;
            settings.dynamicResolutionSettings = superResolution
                ? StreamlineSettings.ConfigureSuperResolution(original)
                : original;
            // Never select Unity's separate NGX integration, even as a fallback.
            var priority = new System.Collections.Generic.List<string>(
                settings.dynamicResolutionSettings.advancedUpscalerNames ?? new()
            );
            priority.RemoveAll(name => name == "DLSS" || name == "Deep Learning Super Sampling 4");
            settings.dynamicResolutionSettings.advancedUpscalerNames = priority;
            asset.currentPlatformRenderPipelineSettings = settings;
        }

        public void ConfigureCamera(Camera camera, bool superResolution)
        {
            if (!camera.TryGetComponent<HDAdditionalCameraData>(out var data))
                return;
            data.allowDeepLearningSuperSampling = false;
            if (superResolution)
                camera.allowDynamicResolution = true;
        }

        private void Restore()
        {
            if (asset == null)
                return;
            var settings = asset.currentPlatformRenderPipelineSettings;
            settings.dynamicResolutionSettings = original;
            asset.currentPlatformRenderPipelineSettings = settings;
            asset = null;
        }

        public void Dispose()
        {
            Restore();
            finalFrame?.Dispose();
            finalFrame = null;
            if (volume != null)
                Object.Destroy(volume);
            Instance = null;
        }
    }
}
