using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Rendering.Universal
{
    internal sealed partial class UniversalStreamlineCapture : IDisposable
    {
        private readonly Material alphaMaterial = CoreUtils.CreateEngineMaterial(
            Resources.Load<Shader>("Streamline/UIAlpha")
        );
        private Camera previousCamera;
        private int previousFrame = -2;
        private Matrix4x4 previousViewProjection;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector2Int previousSize;

        private sealed class InputData
        {
            internal TextureHandle depth,
                motion;
            internal StreamlineNative.FrameData packet;
        }

        internal void Inputs(
            RenderGraph graph,
            ContextContainer context,
            Matrix4x4 projection,
            Vector2 jitter
        )
        {
            var cameraData = context.Get<UniversalCameraData>();
            var resources = context.Get<UniversalResourceData>();
            var camera = cameraData.camera;
            if (!resources.cameraDepthTexture.IsValid() || !resources.motionVectorColor.IsValid())
                return;
            var size = new Vector2Int(
                cameraData.cameraTargetDescriptor.width,
                cameraData.cameraTargetDescriptor.height
            );
            var vp = projection * camera.worldToCameraMatrix;
            bool reset =
                camera != previousCamera
                || previousFrame != Time.frameCount - 1
                || size != previousSize
                || (camera.transform.position - previousPosition).sqrMagnitude > 25
                || Quaternion.Angle(camera.transform.rotation, previousRotation) > 45;
            var toPrevious = reset ? Matrix4x4.identity : previousViewProjection * vp.inverse;
            using (
                var builder = graph.AddUnsafePass<InputData>(
                    "Streamline URP depth and motion",
                    out var pass
                )
            )
            {
                pass.depth = resources.cameraDepthTexture;
                pass.motion = resources.motionVectorColor;
                pass.packet = new StreamlineNative.FrameData
                {
                    token = StreamlineRuntime.FrameToken,
                    viewToClip = projection,
                    clipToView = projection.inverse,
                    clipToPrevious = toPrevious,
                    previousToClip = toPrevious.inverse,
                    position = camera.transform.position,
                    up = camera.transform.up,
                    right = camera.transform.right,
                    forward = camera.transform.forward,
                    jitter = jitter,
                    motionScale = new Vector2(-1, -1),
                    nearPlane = camera.nearClipPlane,
                    farPlane = camera.farClipPlane,
                    fieldOfView = camera.fieldOfView * Mathf.Deg2Rad,
                    aspect = camera.aspect,
                    width = (uint)size.x,
                    height = (uint)size.y,
                    outputWidth = (uint)camera.pixelWidth,
                    outputHeight = (uint)camera.pixelHeight,
                    reset = reset ? 1u : 0u,
                    invertedDepth = SystemInfo.usesReversedZBuffer ? 1u : 0u,
                };
                builder.UseTexture(pass.depth);
                builder.UseTexture(pass.motion);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    static (InputData data, UnsafeGraphContext ctx) =>
                    {
                        RTHandle depth = data.depth,
                            motion = data.motion;
                        data.packet.depth = depth.rt.GetNativeTexturePtr();
                        data.packet.motion = motion.rt.GetNativeTexturePtr();
                        Submit(CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd), data.packet);
                    }
                );
            }
            previousCamera = camera;
            previousFrame = Time.frameCount;
            previousSize = size;
            previousPosition = camera.transform.position;
            previousRotation = camera.transform.rotation;
            previousViewProjection = vp;
        }

        private static void Submit(CommandBuffer cmd, StreamlineNative.FrameData frame)
        {
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

        public void Dispose() => CoreUtils.Destroy(alphaMaterial);
    }
}
