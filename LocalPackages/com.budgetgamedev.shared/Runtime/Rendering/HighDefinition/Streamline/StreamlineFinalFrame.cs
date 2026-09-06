using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    /// <summary>Uses HDRP's actual final pass, including its HDR encoding and dithering.</summary>
    internal sealed class StreamlineFinalFrame : IDisposable
    {
        private EventInfo hook;
        private Action<HDCamera, CommandBuffer, Material, RTHandle, Rect, bool> callback;
        private RTHandle hudless,
            uiAlpha;
        private RTHandle sdrUi;
        private Material alphaMaterial;
        private Texture2D transparent;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        internal bool Attach()
        {
            var type = typeof(HDRenderPipeline).Assembly.GetType(
                "UnityEngine.Rendering.HighDefinition.SharedFrameGenerationHooks"
            );
            hook = type?.GetEvent("FinalFrame", BindingFlags.Public | BindingFlags.Static);
            var shader = Resources.Load<Shader>("Streamline/UIAlpha");
            if (hook == null || shader == null)
                return false;
            alphaMaterial = CoreUtils.CreateEngineMaterial(shader);
            transparent = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            transparent.SetPixel(0, 0, Color.clear);
            transparent.Apply(false, true);
            callback = Capture;
            hook.AddEventHandler(null, callback);
            return true;
        }

        private void Capture(
            HDCamera camera,
            CommandBuffer cmd,
            Material finalMaterial,
            RTHandle ui,
            Rect viewport,
            bool finalOutput
        )
        {
            if (
                !StreamlineRuntime.CaptureEnabled
                || camera.camera != StreamlineRuntime.ViewCamera
                || StreamlineRuntime.FrameToken == IntPtr.Zero
                || !finalOutput
            )
                return;
            bool hdr = HDROutputSettings.main.active;
            if (hdr && ui?.rt == null)
                return;
            int width = (int)viewport.width;
            int height = (int)viewport.height;
            var format = hdr ? HDROutputSettings.main.graphicsFormat : GraphicsFormat.R8G8B8A8_SRGB;
            if (
                hudless == null
                || hudless.rt.width != width
                || hudless.rt.height != height
                || hudless.rt.graphicsFormat != format
            )
            {
                hudless?.Release();
                hudless = RTHandles.Alloc(
                    width,
                    height,
                    colorFormat: format,
                    name: "Streamline HUD-less final color"
                );
            }
            properties.Clear();
            properties.SetTexture("_UITexture", transparent);
            // Keep after-post scene geometry: only screen-space overlay UI is removed.
            // The same final material supplies all tonemapping/encoding/flip parameters.
            HDUtils.DrawFullScreen(cmd, viewport, finalMaterial, hudless, properties);
            if (hdr)
                ExtractAlpha(cmd, ui, width, height);
            var frame = new StreamlineNative.FrameData
            {
                token = StreamlineRuntime.FrameToken,
                hudless = hudless.rt.GetNativeTexturePtr(),
                ui = hdr ? uiAlpha.rt.GetNativeTexturePtr() : IntPtr.Zero,
                outputWidth = (uint)width,
                outputHeight = (uint)height,
            };
            var packet = StreamlineNative.BgdSL_CopyFrame(
                in frame,
                (uint)Marshal.SizeOf<StreamlineNative.FrameData>()
            );
            if (packet != IntPtr.Zero)
                cmd.IssuePluginEventAndData(
                    StreamlineRuntime.RenderEvent,
                    StreamlineNative.CaptureEvent,
                    packet
                );
        }

        internal void CaptureSdrUi(CustomPassContext context)
        {
            int width = context.hdCamera.camera.pixelWidth;
            int height = context.hdCamera.camera.pixelHeight;
            if (sdrUi == null || sdrUi.rt.width != width || sdrUi.rt.height != height)
            {
                sdrUi?.Release();
                sdrUi = RTHandles.Alloc(
                    width,
                    height,
                    depthBufferBits: DepthBits.Depth24,
                    colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
                    name: "Streamline SDR overlay copy"
                );
            }
            // SDR overlay is drawn after HDRP's final color. Render its real renderer
            // list into a separate target to obtain alpha, including stencil masks.
            var list = context.renderContext.CreateUIOverlayRendererList(context.hdCamera.camera);
            CoreUtils.SetRenderTarget(context.cmd, sdrUi, ClearFlag.All, Color.clear);
            context.cmd.SetViewport(new Rect(0, 0, width, height));
            context.cmd.DrawRendererList(list);
            ExtractAlpha(context.cmd, sdrUi, width, height);
            var frame = new StreamlineNative.FrameData
            {
                token = StreamlineRuntime.FrameToken,
                ui = uiAlpha.rt.GetNativeTexturePtr(),
                outputWidth = (uint)width,
                outputHeight = (uint)height,
            };
            var packet = StreamlineNative.BgdSL_CopyFrame(
                in frame,
                (uint)Marshal.SizeOf<StreamlineNative.FrameData>()
            );
            if (packet != IntPtr.Zero)
                context.cmd.IssuePluginEventAndData(
                    StreamlineRuntime.RenderEvent,
                    StreamlineNative.CaptureEvent,
                    packet
                );
            CoreUtils.SetRenderTarget(
                context.cmd,
                context.cameraColorBuffer,
                context.cameraDepthBuffer
            );
            context.cmd.SetViewport(
                new Rect(0, 0, context.hdCamera.actualWidth, context.hdCamera.actualHeight)
            );
        }

        private void ExtractAlpha(CommandBuffer cmd, RTHandle source, int width, int height)
        {
            if (uiAlpha == null || uiAlpha.rt.width != width || uiAlpha.rt.height != height)
            {
                uiAlpha?.Release();
                uiAlpha = RTHandles.Alloc(
                    width,
                    height,
                    colorFormat: GraphicsFormat.R8_UNorm,
                    name: "Streamline UI alpha"
                );
            }
            properties.Clear();
            properties.SetTexture("_UITexture", source.rt);
            properties.SetVector(
                "_ViewportScale",
                new Vector4((float)width / source.rt.width, (float)height / source.rt.height, 0, 0)
            );
            HDUtils.DrawFullScreen(
                cmd,
                new Rect(0, 0, width, height),
                alphaMaterial,
                uiAlpha,
                properties
            );
        }

        public void Dispose()
        {
            if (callback != null)
                hook?.RemoveEventHandler(null, callback);
            hudless?.Release();
            uiAlpha?.Release();
            sdrUi?.Release();
            CoreUtils.Destroy(alphaMaterial);
            CoreUtils.Destroy(transparent);
        }
    }
}
