using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>Owns one Streamline token per simulated frame in the Windows player.</summary>
    public sealed class StreamlineRuntime : MonoBehaviour
    {
        internal static IntPtr FrameToken { get; private set; }
        internal static IntPtr RenderEvent { get; private set; }
        internal static Camera ViewCamera { get; private set; }
        private static StreamlineRuntime instance;
        private bool simulationEnded;
        internal static IStreamlinePipeline Pipeline { get; private set; }
        private static readonly List<IStreamlinePipeline> pipelines =
            new List<IStreamlinePipeline>();

        internal static void Register(IStreamlinePipeline pipeline)
        {
            pipelines.RemoveAll(item => item.GetType() == pipeline.GetType());
            pipelines.Add(pipeline);
        }

        private float nextReport;
        internal static bool FrameGenerationSupported { get; private set; }
        internal static bool CaptureEnabled =>
            FrameGenerationSupported
            && Pipeline?.CanCapture == true
            && StreamlineSettings.GeneratedFrames > 0
            && Application.isFocused
            && !StreamlineOptionsPanel.Visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (
                Application.platform != RuntimePlatform.WindowsPlayer
                || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12
            )
                return;
            if (instance != null)
                return;
            var host = new GameObject("Streamline Rendering");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<StreamlineRuntime>();
        }

        private void Awake()
        {
            RenderPipelineManager.beginCameraRendering += ConfigureCamera;
            if (!StreamlineNative.TryGetStatus(out var status) || status.initialized == 0)
            {
                Debug.LogWarning(
                    "[Streamline] Native initialization unavailable. Ordinary pipeline rendering remains active."
                );
                return;
            }
            FrameGenerationSupported = status.frameGenerationAvailable != 0;
            RenderEvent = StreamlineNative.BgdSL_GetRenderEvent();
            ConfigurePipeline();
            gameObject.AddComponent<StreamlineOptionsPanel>();
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            InsertEarlyUpdate(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
            RenderPipelineManager.beginContextRendering += BeginRendering;
            RenderPipelineManager.endContextRendering += EndRendering;
            StreamlineSettings.Changed += ApplyOptions;
            ApplyOptions();
        }

        private void ConfigurePipeline()
        {
            var next = pipelines.FirstOrDefault(pipeline => pipeline.IsActive);
            if (next != Pipeline)
            {
                Pipeline?.Dispose();
                Pipeline = next;
                Pipeline?.Attach(gameObject);
                ApplyOptions();
            }
            Pipeline?.Configure(
                ViewCamera != null && StreamlineUpscaler.Available && StreamlineSettings.DlssEnabled
            );
        }

        private void ConfigureCamera(ScriptableRenderContext context, Camera camera) =>
            Pipeline?.ConfigureCamera(
                camera,
                camera == ViewCamera
                    && StreamlineSettings.DlssEnabled
                    && StreamlineUpscaler.Available
            );

        internal static bool IsEligibleCamera(Camera camera) =>
            camera != null
            && camera.cameraType == CameraType.Game
            && camera.targetTexture == null
            && camera.targetDisplay == 0
            && !camera.stereoEnabled
            && !camera.orthographic
            && camera.rect == new Rect(0, 0, 1, 1);

        internal static Camera SelectViewCamera(IEnumerable<Camera> cameras)
        {
            var outputs = cameras
                .Where(camera =>
                    camera != null
                    && camera.cameraType == CameraType.Game
                    && camera.targetTexture == null
                    && camera.targetDisplay == 0
                )
                .ToArray();
            return outputs.Length == 1 && IsEligibleCamera(outputs[0]) ? outputs[0] : null;
        }

        private static void BeginSimulation()
        {
            if (instance == null || RenderEvent == IntPtr.Zero)
                return;
            FrameToken = StreamlineNative.BgdSL_BeginFrame();
            instance.simulationEnded = false;
            ViewCamera = null;
        }

        private void BeginRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (FrameToken == IntPtr.Zero || simulationEnded)
                return;
            simulationEnded = true;
            // FG supports one viewport here. Multiple output cameras would describe
            // conflicting histories for the same present, so skip FG for that frame.
            ViewCamera = SelectViewCamera(cameras);
            if (ViewCamera != null && Pipeline?.SupportsCamera(ViewCamera) != true)
                ViewCamera = null;
            Pipeline?.Configure(
                ViewCamera != null && StreamlineSettings.DlssEnabled && StreamlineUpscaler.Available
            );
            StreamlineNative.BgdSL_EndSimulation(FrameToken);
            var cmd = CommandBufferPool.Get("Streamline render submission start");
            cmd.IssuePluginEventAndData(RenderEvent, StreamlineNative.SubmitStartEvent, FrameToken);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void EndRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (FrameToken == IntPtr.Zero)
                return;
            var cmd = CommandBufferPool.Get("Streamline render submission end");
            cmd.IssuePluginEventAndData(RenderEvent, StreamlineNative.SubmitEndEvent, FrameToken);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            CommandBufferPool.Release(cmd);
        }

        private void Update()
        {
            if (RenderEvent == IntPtr.Zero)
                return;
            ConfigurePipeline();
            if (Time.unscaledTime < nextReport || RenderEvent == IntPtr.Zero)
                return;
            nextReport = Time.unscaledTime + 10;
            if (StreamlineNative.TryGetStatus(out var state))
                Debug.Log(
                    $"[Streamline] proxy={state.swapchainHooked}, reflex={state.reflexAvailable}, "
                        + $"fgSupported={state.frameGenerationAvailable}, generated={state.generatedFrames}, "
                        + $"maxGenerated={state.maxGeneratedFrames}, status={state.frameGenerationStatus}, error={state.lastError}, "
                        + $"requirements={state.requirementsResult}, support={state.featureSupportResult}, warnings={state.integrationWarnings}"
                );
        }

        internal void ApplyOptions()
        {
            if (RenderEvent == IntPtr.Zero)
                return;
            StreamlineNative.BgdSL_Configure(
                Pipeline?.CanCapture == true && !StreamlineOptionsPanel.Visible
                    ? (uint)StreamlineSettings.GeneratedFrames
                    : 0,
                (uint)StreamlineSettings.EffectiveReflex,
                Application.isFocused ? 1u : 0u
            );
        }

        private void OnApplicationFocus(bool hasFocus) => ApplyOptions();

        private static void InsertEarlyUpdate(ref PlayerLoopSystem system)
        {
            if (system.type == typeof(EarlyUpdate))
            {
                var children = (system.subSystemList ?? Array.Empty<PlayerLoopSystem>())
                    .Where(child => child.type != typeof(StreamlineRuntime))
                    .ToList();
                children.Insert(
                    0,
                    new PlayerLoopSystem
                    {
                        type = typeof(StreamlineRuntime),
                        updateDelegate = BeginSimulation,
                    }
                );
                system.subSystemList = children.ToArray();
                return;
            }
            if (system.subSystemList == null)
                return;
            for (int i = 0; i < system.subSystemList.Length; i++)
                InsertEarlyUpdate(ref system.subSystemList[i]);
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= ConfigureCamera;
            RenderPipelineManager.beginContextRendering -= BeginRendering;
            RenderPipelineManager.endContextRendering -= EndRendering;
            StreamlineSettings.Changed -= ApplyOptions;
            if (RenderEvent != IntPtr.Zero)
                StreamlineNative.BgdSL_Configure(0, 0, 0);
            Pipeline?.Dispose();
            Pipeline = null;
            FrameGenerationSupported = false;
            FrameToken = RenderEvent = IntPtr.Zero;
            ViewCamera = null;
            instance = null;
        }
    }
}
