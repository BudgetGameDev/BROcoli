using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Rendering.Universal
{
    internal sealed partial class UniversalStreamlineCapture
    {
        private sealed class UiData
        {
            internal RendererListHandle list;
        }

        private sealed class FinalData
        {
            internal TextureHandle color,
                ui,
                alpha;
            internal Material material;
            internal int width,
                height;
            internal IntPtr token;
        }

        internal void FinalFrame(
            RenderGraph graph,
            ContextContainer context,
            Action<TextureHandle> renderHudless
        )
        {
            var camera = context.Get<UniversalCameraData>();
            var resources = context.Get<UniversalResourceData>();
            int width = camera.camera.pixelWidth,
                height = camera.camera.pixelHeight;
            var desc = new TextureDesc(width, height)
            {
                colorFormat = graph.GetRenderTargetInfo(resources.backBufferColor).format,
                name = "Streamline URP HUD-less final output",
            };
            var hudless = graph.CreateTexture(desc);
            // Replay Unity's final compositor with identical encoding/filtering and UI disabled.
            renderHudless(hudless);
            var ui = resources.overlayUITexture;
            if (!camera.isHDROutputActive)
            {
                desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                desc.name = "Streamline URP SDR overlay";
                ui = graph.CreateTexture(desc);
                var depthDesc = new TextureDesc(width, height)
                {
                    depthBufferBits = DepthBits.Depth24,
                    clearBuffer = true,
                    name = "Streamline URP overlay stencil",
                };
                var depth = graph.CreateTexture(depthDesc);
                using (
                    var builder = graph.AddRasterRenderPass<UiData>(
                        "Streamline URP overlay capture",
                        out var pass
                    )
                )
                {
                    pass.list = graph.CreateUIOverlayRendererList(
                        camera.camera,
                        UISubset.UIToolkit_UGUI
                    );
                    builder.UseAllGlobalTextures(true);
                    builder.UseRendererList(pass.list);
                    builder.SetRenderAttachment(ui, 0);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(
                        static (UiData data, RasterGraphContext ctx) =>
                            ctx.cmd.DrawRendererList(data.list)
                    );
                }
            }
            if (!ui.IsValid() || alphaMaterial == null)
                return;
            desc.colorFormat = GraphicsFormat.R8_UNorm;
            desc.name = "Streamline URP UI alpha";
            var alpha = graph.CreateTexture(desc);
            using (
                var builder = graph.AddUnsafePass<FinalData>(
                    "Streamline URP final tags",
                    out var pass
                )
            )
            {
                pass.color = hudless;
                pass.ui = ui;
                pass.alpha = alpha;
                pass.material = alphaMaterial;
                pass.width = width;
                pass.height = height;
                pass.token = StreamlineRuntime.FrameToken;
                builder.UseTexture(hudless);
                builder.UseTexture(ui);
                builder.UseTexture(alpha, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    static (FinalData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        RTHandle color = data.color,
                            ui = data.ui,
                            alpha = data.alpha;
                        var properties = new MaterialPropertyBlock();
                        properties.SetTexture("_UITexture", ui.rt);
                        properties.SetVector(
                            "_ViewportScale",
                            new Vector4(
                                (float)data.width / ui.rt.width,
                                (float)data.height / ui.rt.height,
                                0,
                                0
                            )
                        );
                        CoreUtils.SetRenderTarget(cmd, alpha);
                        CoreUtils.DrawFullScreen(cmd, data.material, properties);
                        Submit(
                            cmd,
                            new StreamlineNative.FrameData
                            {
                                token = data.token,
                                hudless = color.rt.GetNativeTexturePtr(),
                                ui = alpha.rt.GetNativeTexturePtr(),
                                outputWidth = (uint)data.width,
                                outputHeight = (uint)data.height,
                            }
                        );
                    }
                );
            }
        }
    }
}
