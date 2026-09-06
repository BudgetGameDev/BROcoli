using System.Linq;
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
        public bool ResolutionConfiguredBeforeUpscaler => true;

        public bool SupportsCamera(Camera camera)
        {
            // Common scenes are authored with a regular/URP camera. HDRP can render
            // those, but per-camera upscaler settings need its own additional data.
            // This runs at beginContextRendering, before HDRP builds frame settings.
            if (camera == null)
                return false;
            if (!camera.TryGetComponent<HDAdditionalCameraData>(out _))
                camera.gameObject.AddComponent<HDAdditionalCameraData>();
            return true;
        }

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
            var camera = StreamlineRuntime.ViewCamera;
            if (
                superResolution
                && camera != null
                && StreamlineNative.BgdSL_GetOptimalResolution(
                    (uint)camera.pixelWidth,
                    (uint)camera.pixelHeight,
                    out uint width,
                    out uint height
                ) == 1
            )
            {
                // HDRP builds lighting/depth/screen constants before IUpscaler negotiates
                // its resolution in HDCamera.SetReferenceSize. Set DRS first so those
                // constants and the later render viewport describe the same image.
                settings.dynamicResolutionSettings.forceResolution = true;
                settings.dynamicResolutionSettings.forcedPercentage =
                    100f
                    * Mathf.Min(
                        (float)width / camera.pixelWidth,
                        (float)height / camera.pixelHeight
                    );
            }
            // Never select Unity's separate NGX integration, even as a fallback.
            var priority = new System.Collections.Generic.List<string>(
                settings.dynamicResolutionSettings.advancedUpscalerNames ?? new()
            );
            priority.RemoveAll(name => name == "DLSS" || name == "Deep Learning Super Sampling 4");
            settings.dynamicResolutionSettings.advancedUpscalerNames = priority;
            ApplyDynamicResolutionSettings(asset, settings.dynamicResolutionSettings);
        }

        internal static bool ApplyDynamicResolutionSettings(
            HDRenderPipelineAsset target,
            GlobalDynamicResolutionSettings desired
        )
        {
            var settings = target.currentPlatformRenderPipelineSettings;
            var current = settings.dynamicResolutionSettings;
            // ConfigureSuperResolution creates a priority list. Equal contents must not
            // make otherwise identical settings compare unequal by list identity.
            if (current.advancedUpscalerNames != null && desired.advancedUpscalerNames != null
                && current.advancedUpscalerNames.SequenceEqual(desired.advancedUpscalerNames))
                desired.advancedUpscalerNames = current.advancedUpscalerNames;
            if (current.Equals(desired))
                return false;

            // HDRP's setter invalidates and rebuilds the entire pipeline, including GPU
            // resources and temporal histories. Only actual setting changes may call it.
            settings.dynamicResolutionSettings = desired;
            target.currentPlatformRenderPipelineSettings = settings;
            return true;
        }

        public void ConfigureCamera(Camera camera, bool superResolution)
        {
            if (!camera.TryGetComponent<HDAdditionalCameraData>(out var data))
                return;
            data.allowDeepLearningSuperSampling = false;
            if (superResolution)
            {
                data.allowDynamicResolution = true;
                camera.allowDynamicResolution = true;
            }
        }

        private void Restore()
        {
            if (asset == null)
                return;
            ApplyDynamicResolutionSettings(asset, original);
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
