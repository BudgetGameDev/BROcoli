using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    [Serializable]
    internal sealed class StreamlineInputsPass : CustomPass
    {
        private Camera previousCamera;
        private int previousFrame = -2;
        private Matrix4x4 previousViewProjection;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector2Int previousSize;
        protected override bool executeInSceneView => false;

        protected override void Execute(CustomPassContext ctx)
        {
            var camera = ctx.hdCamera.camera;
            if (
                !StreamlineRuntime.CaptureEnabled
                || camera != StreamlineRuntime.ViewCamera
                || StreamlineRuntime.FrameToken == IntPtr.Zero
                || ctx.cameraDepthBuffer?.rt == null
                || ctx.cameraMotionVectorsBuffer?.rt == null
            )
                return;
            var view = ctx.hdCamera.mainViewConstants;
            if (!HDROutputSettings.main.active)
                StreamlineRuntime.CaptureSdrUi(ctx);
            // Unity's public Camera view matrix includes world translation, unlike
            // HDRP's camera-relative view constants. Keep consecutive frames absolute.
            var projection = view.nonJitteredProjMatrix;
            var viewProjection = projection * camera.worldToCameraMatrix;
            var size = new Vector2Int(ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight);
            bool reset =
                camera != previousCamera
                || previousFrame != Time.frameCount - 1
                || size != previousSize
                || (camera.transform.position - previousPosition).sqrMagnitude > 25
                || Quaternion.Angle(camera.transform.rotation, previousRotation) > 45;
            var toPrevious = reset
                ? Matrix4x4.identity
                : previousViewProjection * viewProjection.inverse;
            var data = new StreamlineNative.FrameData
            {
                token = StreamlineRuntime.FrameToken,
                depth = ctx.cameraDepthBuffer.rt.GetNativeTexturePtr(),
                motion = ctx.cameraMotionVectorsBuffer.rt.GetNativeTexturePtr(),
                viewToClip = projection,
                clipToView = projection.inverse,
                clipToPrevious = toPrevious,
                previousToClip = toPrevious.inverse,
                position = camera.transform.position,
                up = camera.transform.up,
                right = camera.transform.right,
                forward = camera.transform.forward,
                jitter = new Vector2(-ctx.hdCamera.taaJitter.x, -ctx.hdCamera.taaJitter.y),
                // HDRP stores normalized previous-to-current motion; SL consumes
                // current-to-previous normalized motion (not DLSS SR's pixel scale).
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
            var packet = StreamlineNative.BgdSL_CopyFrame(
                in data,
                (uint)Marshal.SizeOf<StreamlineNative.FrameData>()
            );
            if (packet != IntPtr.Zero)
                ctx.cmd.IssuePluginEventAndData(
                    StreamlineRuntime.RenderEvent,
                    StreamlineNative.CaptureEvent,
                    packet
                );
            previousCamera = camera;
            previousFrame = Time.frameCount;
            previousSize = size;
            previousPosition = camera.transform.position;
            previousRotation = camera.transform.rotation;
            previousViewProjection = viewProjection;
        }
    }
}
