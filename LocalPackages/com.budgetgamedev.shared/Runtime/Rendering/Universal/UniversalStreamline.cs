using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Rendering.Universal
{
    internal sealed class UniversalStreamline : IStreamlinePipeline
    {
#if ENABLE_UPSCALER_FRAMEWORK
        private UniversalRenderPipelineAsset asset;
        private string originalUpscaler;
        private int originalMsaa;
        private float originalScale;
#endif
        private EventInfo inputHook,
            finalHook,
            activeHook;
        private Func<Camera, bool> active;
        private Action<RenderGraph, ContextContainer, Matrix4x4, Vector2> inputs;
        private Action<RenderGraph, ContextContainer, Action<TextureHandle>> final;
        private UniversalStreamlineCapture capture;
        public bool IsActive =>
            GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
        public bool CanCapture => capture != null;

        public bool SupportsCamera(Camera camera) =>
            camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)
            && data.renderType == CameraRenderType.Base
            && data.cameraStack.Count == 0
            && data.scriptableRenderer is UniversalRenderer;

        public Vector2 GetJitter(Camera camera, Vector2 requested) => -requested;

        private readonly System.Collections.Generic.Dictionary<
            Camera,
            (bool post, bool dynamic)
        > cameras = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => StreamlineRuntime.Register(new UniversalStreamline());

        public void Attach(GameObject host)
        {
            var hook = typeof(UniversalRenderPipeline).Assembly.GetType(
                "UnityEngine.Rendering.Universal.SharedStreamlineHooks"
            );
            activeHook = hook?.GetEvent("Active");
            inputHook = hook?.GetEvent("Inputs");
            finalHook = hook?.GetEvent("FinalFrame");
            if (inputHook == null || finalHook == null)
            {
                Debug.LogWarning(
                    "[Streamline] URP capture hooks unavailable; frame generation disabled."
                );
                return;
            }
            active = camera =>
                StreamlineRuntime.CaptureEnabled && camera == StreamlineRuntime.ViewCamera;
            activeHook?.AddEventHandler(null, active);
            capture = new UniversalStreamlineCapture();
            inputs = capture.Inputs;
            final = capture.FinalFrame;
            inputHook.AddEventHandler(null, inputs);
            finalHook.AddEventHandler(null, final);
        }

        public void Configure(bool superResolution)
        {
#if ENABLE_UPSCALER_FRAMEWORK
            var next = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != next)
            {
                Restore();
                asset = next;
                originalUpscaler = asset.upscalerName;
                originalMsaa = asset.msaaSampleCount;
                originalScale = asset.renderScale;
            }
            asset.upscalerName = superResolution
                ? StreamlineUpscaler.Name
                : FallbackName(originalUpscaler);
            asset.msaaSampleCount = superResolution ? 1 : originalMsaa;
            asset.renderScale = superResolution ? 1 : originalScale;
#endif
        }

        internal static string FallbackName(string name) =>
            name == StreamlineUpscaler.Name || name == "Deep Learning Super Sampling 4"
                ? "Bilinear"
                : name;

        public void ConfigureCamera(Camera camera, bool superResolution)
        {
            if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                return;
            if (superResolution && SupportsCamera(camera))
            {
                if (!cameras.ContainsKey(camera))
                    cameras.Add(camera, (data.renderPostProcessing, camera.allowDynamicResolution));
                data.renderPostProcessing = true;
                camera.allowDynamicResolution = false;
            }
            else if (cameras.TryGetValue(camera, out var original))
            {
                data.renderPostProcessing = original.post;
                camera.allowDynamicResolution = original.dynamic;
                cameras.Remove(camera);
            }
        }

        private void Restore()
        {
#if ENABLE_UPSCALER_FRAMEWORK
            if (asset == null)
                return;
            asset.upscalerName = originalUpscaler;
            asset.msaaSampleCount = originalMsaa;
            asset.renderScale = originalScale;
            asset = null;
#endif
        }

        public void Dispose()
        {
            Restore();
            foreach (var pair in cameras)
                if (
                    pair.Key != null
                    && pair.Key.TryGetComponent<UniversalAdditionalCameraData>(out var data)
                )
                {
                    data.renderPostProcessing = pair.Value.post;
                    pair.Key.allowDynamicResolution = pair.Value.dynamic;
                }
            cameras.Clear();
            if (active != null)
                activeHook?.RemoveEventHandler(null, active);
            if (inputs != null)
                inputHook?.RemoveEventHandler(null, inputs);
            if (final != null)
                finalHook?.RemoveEventHandler(null, final);
            capture?.Dispose();
            capture = null;
        }
    }
}
