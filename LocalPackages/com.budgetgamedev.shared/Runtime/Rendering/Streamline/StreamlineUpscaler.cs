#if ENABLE_UPSCALER_FRAMEWORK
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Scripting;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>One Streamline SR implementation for Unity 6.5's URP and HDRP upscaler framework.</summary>
    [Preserve]
    public sealed class StreamlineUpscaler : AbstractUpscaler
    {
        public const string Name = "NVIDIA Streamline DLSS";
        public override string name => Name;
        public override bool isTemporal => true;
        public override bool supportsSharpening => false;
        internal static bool Available =>
            StreamlineNative.TryGetSuperResolutionStatus(out var status) && status.available != 0;
        private Vector2 jitter;
        private int jitterPhases = 18;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private ulong previousCamera;
        private int previousFrame = -2;
        private Vector2Int previousOutput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => UpscalerRegistry.Register<StreamlineUpscaler>(Name);

        [Preserve]
        public StreamlineUpscaler() { }

        public override void CalculateJitter(
            int frameIndex,
            out Vector2 value,
            out bool allowScaling
        )
        {
            int index = (frameIndex % jitterPhases) + 1;
            value = new Vector2(
                HaltonSequence.Get(index, 2) - 0.5f,
                HaltonSequence.Get(index, 3) - 0.5f
            );
            allowScaling = false;
            jitter = value;
        }

        public override void NegotiatePreUpscaleResolution(ref Vector2Int input, Vector2Int output)
        {
            // HDRP has already built camera/depth/lighting constants for its DRS size.
            // Hardware may round DLSS's requested fraction (e.g. 66.7% to 70%).
            // Keep that actual size; DLSS supports inputs within its dynamic range.
            if (StreamlineRuntime.Pipeline?.ResolutionConfiguredBeforeUpscaler == true)
                return;
            if (!Available || !StreamlineSettings.DlssEnabled)
                return;
            if (
                StreamlineNative.BgdSL_GetOptimalResolution(
                    (uint)output.x,
                    (uint)output.y,
                    out uint x,
                    out uint y
                ) == 1
            )
            {
                input = new Vector2Int((int)x, (int)y);
                float ratio = (float)output.x / input.x;
                jitterPhases = Mathf.Max(8, Mathf.CeilToInt(8 * ratio * ratio));
            }
        }

        internal static Vector2 MotionScale(
            UpscalingIO.MotionVectorDomain domain,
            UpscalingIO.MotionVectorDirection direction,
            Vector2Int size
        )
        {
            float sign =
                direction == UpscalingIO.MotionVectorDirection.PreviousFrameToCurrentFrame ? -1 : 1;
            return domain == UpscalingIO.MotionVectorDomain.NDC
                ? new Vector2(sign, sign)
                : new Vector2(sign / Mathf.Max(1, size.x), sign / Mathf.Max(1, size.y));
        }

        private sealed class PassData
        {
            internal TextureHandle input,
                output,
                depth,
                motion;
            internal StreamlineNative.SuperResolutionData packet;
            internal bool evaluate;
            internal Vector2Int inputSize;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var io = frameData.Get<UpscalingIO>();
            var desc = io.cameraColor.GetDescriptor(graph);
            desc.sizeMode = TextureSizeMode.Explicit;
            desc.width = io.postUpscaleResolution.x;
            desc.height = io.postUpscaleResolution.y;
            desc.msaaSamples = MSAASamples.None;
            desc.useDynamicScale = false;
            desc.useDynamicScaleExplicit = false;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.enableRandomWrite = true;
            desc.name = "Streamline DLSS output";
            var output = graph.CreateTexture(desc);
            using (
                var builder = graph.AddUnsafePass<PassData>(
                    "Streamline DLSS Super Resolution",
                    out var pass
                )
            )
            {
                pass.input = io.cameraColor;
                pass.output = output;
                pass.depth = io.cameraDepth;
                pass.motion = io.motionVectorColor;
                pass.inputSize = io.preUpscaleResolution;
                var camera = StreamlineRuntime.ViewCamera;
                pass.evaluate =
                    Available
                    && StreamlineSettings.DlssEnabled
                    && camera != null
                    && io.cameraInstanceID == EntityId.ToULong(camera.GetEntityId())
                    && io.numActiveViews == 1
                    && io.eyeIndex == 0
                    && !io.jitteredMotionVectors
                    && io.cameraDepth.IsValid()
                    && io.motionVectorColor.IsValid()
                    && StreamlineRuntime.FrameToken != IntPtr.Zero;
                if (pass.evaluate)
                {
                    var projection = io.projectionMatrices[0];
                    var vp = projection * camera.worldToCameraMatrix;
                    bool reset =
                        io.resetHistory
                        || previousCamera != io.cameraInstanceID
                        || previousFrame != Time.frameCount - 1
                        || previousOutput != io.postUpscaleResolution
                        || (camera.transform.position - previousPosition).sqrMagnitude > 25
                        || Quaternion.Angle(camera.transform.rotation, previousRotation) > 45;
                    // Use absolute camera matrices in both pipelines, including camera-relative HDRP.
                    var toPrevious = reset
                        ? Matrix4x4.identity
                        : previousViewProjection * vp.inverse;
                    pass.packet.frame = new StreamlineNative.FrameData
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
                        jitter = StreamlineRuntime.Pipeline.GetJitter(camera, jitter),
                        motionScale = MotionScale(
                            io.motionVectorDomain,
                            io.motionVectorDirection,
                            io.motionVectorTextureSize
                        ),
                        nearPlane = io.nearClipPlane,
                        farPlane = io.farClipPlane,
                        fieldOfView = io.fieldOfViewDegrees * Mathf.Deg2Rad,
                        aspect = camera.aspect,
                        width = (uint)io.preUpscaleResolution.x,
                        height = (uint)io.preUpscaleResolution.y,
                        outputWidth = (uint)io.postUpscaleResolution.x,
                        outputHeight = (uint)io.postUpscaleResolution.y,
                        reset = reset ? 1u : 0u,
                        invertedDepth = io.invertedDepth ? 1u : 0u,
                    };
                    pass.packet.preExposure = Mathf.Max(0.00001f, io.preExposureValue);
                    pass.packet.hdr = io.hdrInput ? 1u : 0u;
                    pass.packet.motionWidth = (uint)io.motionVectorTextureSize.x;
                    pass.packet.motionHeight = (uint)io.motionVectorTextureSize.y;
                    previousCamera = io.cameraInstanceID;
                    previousFrame = Time.frameCount;
                    previousOutput = io.postUpscaleResolution;
                    previousViewProjection = vp;
                    previousPosition = camera.transform.position;
                    previousRotation = camera.transform.rotation;
                }
                builder.UseTexture(pass.input);
                builder.UseTexture(output, AccessFlags.Write);
                if (pass.depth.IsValid())
                    builder.UseTexture(pass.depth);
                if (pass.motion.IsValid())
                    builder.UseTexture(pass.motion);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    static (PassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        RTHandle input = data.input,
                            output = data.output;
                        // Seed a complete spatial fallback before the render-thread dispatch.
                        CoreUtils.SetRenderTarget(cmd, output);
                        Blitter.BlitTexture(
                            cmd,
                            input,
                            new Vector4(
                                (float)data.inputSize.x / input.rt.width,
                                (float)data.inputSize.y / input.rt.height,
                                0,
                                0
                            ),
                            0,
                            true
                        );
                        NvidiaDiagnosticsExport.CaptureBuffer(
                            cmd,
                            input.rt,
                            "sr-input",
                            data.inputSize
                        );
                        if (!data.evaluate || NvidiaDiagnosticsExport.SpatialOnly)
                        {
                            NvidiaDiagnosticsExport.CaptureBuffer(
                                cmd,
                                output.rt,
                                "sr-output",
                                new Vector2Int(output.rt.width, output.rt.height)
                            );
                            return;
                        }
                        RTHandle depth = data.depth,
                            motion = data.motion;
                        data.packet.input = input.rt.GetNativeTexturePtr();
                        data.packet.output = output.rt.GetNativeTexturePtr();
                        data.packet.frame.depth = depth.rt.GetNativeTexturePtr();
                        data.packet.frame.motion = motion.rt.GetNativeTexturePtr();
                        var packet = StreamlineNative.BgdSL_CopySuperResolution(
                            in data.packet,
                            (uint)Marshal.SizeOf<StreamlineNative.SuperResolutionData>()
                        );
                        if (packet != IntPtr.Zero)
                            cmd.IssuePluginEventAndData(
                                StreamlineRuntime.RenderEvent,
                                StreamlineNative.SuperResolutionEvent,
                                packet
                            );
                        NvidiaDiagnosticsExport.CaptureBuffer(
                            cmd,
                            output.rt,
                            "sr-output",
                            new Vector2Int(output.rt.width, output.rt.height)
                        );
                    }
                );
            }
            io.cameraColor = output;
        }

        private Matrix4x4 previousViewProjection;
    }
}

#else
namespace BudgetGameDev.Shared.Rendering
{
    public static class StreamlineUpscaler
    {
        public const string Name = "NVIDIA Streamline DLSS";
        internal static bool Available => false;
    }
}
#endif
