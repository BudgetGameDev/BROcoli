using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared
{
    public static partial class GameDisplaySettings
    {
        internal sealed partial class HdrDisplayDriver
        {
            private const int CanvasRefreshFrameCount = 3;
            private readonly List<CanvasState> hdrCanvasStates = new();
            private int canvasRefreshFrames;
            private bool canvasCompositionEnabled;
            private bool canvasCompositionInitialized;

            private sealed class CanvasState
            {
                internal Canvas Canvas;
                internal RenderMode RenderMode;
                internal Camera WorldCamera;
                internal float PlaneDistance;
            }

            private void InitializeCanvasComposition()
            {
                if (canvasCompositionInitialized)
                    return;

                canvasCompositionInitialized = true;
                SceneManager.sceneLoaded += OnSceneLoadedForCanvasComposition;
            }

            private void ShutdownCanvasComposition()
            {
                if (canvasCompositionInitialized)
                {
                    SceneManager.sceneLoaded -= OnSceneLoadedForCanvasComposition;
                    canvasCompositionInitialized = false;
                }

                RestoreCanvasComposition();
            }

            private void OnSceneLoadedForCanvasComposition(Scene scene, LoadSceneMode mode)
            {
                if (canvasCompositionEnabled)
                    canvasRefreshFrames = CanvasRefreshFrameCount;
            }

            internal void LateUpdate()
            {
                if (!canvasCompositionEnabled || canvasRefreshFrames <= 0)
                    return;

                canvasRefreshFrames--;
                UpdateCanvasComposition(
                    true,
                    FindObjectsByType<Canvas>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    ),
                    FindOutputCamera()
                );
            }

            private void ConfigureCanvasComposition(bool enabled)
            {
                canvasCompositionEnabled = enabled;
                if (!enabled)
                {
                    RestoreCanvasComposition();
                    return;
                }

                // URP composites Screen Space Overlay canvases from an SDR offscreen texture
                // after HDR output is enabled. Some native HDR swapchains turn that texture black,
                // so render root UI through the output camera before tone mapping instead.
                canvasRefreshFrames = CanvasRefreshFrameCount;
                UpdateCanvasComposition(
                    true,
                    FindObjectsByType<Canvas>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    ),
                    FindOutputCamera()
                );
            }

            internal void UpdateCanvasComposition(bool enabled, Canvas[] canvases, Camera camera)
            {
                if (!enabled)
                {
                    RestoreCanvasComposition();
                    return;
                }

                RemoveDestroyedCanvasStates();
                if (camera == null)
                    return;

                foreach (CanvasState state in hdrCanvasStates)
                {
                    state.Canvas.worldCamera = camera;
                    state.Canvas.planeDistance = HdrCanvasPlaneDistance(camera);
                }

                if (canvases == null)
                    return;

                foreach (Canvas canvas in canvases)
                {
                    if (
                        canvas == null
                        || !canvas.gameObject.scene.isLoaded
                        || !canvas.isRootCanvas
                        || canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    )
                    {
                        continue;
                    }

                    hdrCanvasStates.Add(
                        new CanvasState
                        {
                            Canvas = canvas,
                            RenderMode = canvas.renderMode,
                            WorldCamera = canvas.worldCamera,
                            PlaneDistance = canvas.planeDistance,
                        }
                    );
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = HdrCanvasPlaneDistance(camera);
                }
            }

            private void RestoreCanvasComposition()
            {
                foreach (CanvasState state in hdrCanvasStates)
                {
                    if (state.Canvas == null)
                        continue;

                    state.Canvas.renderMode = state.RenderMode;
                    state.Canvas.worldCamera = state.WorldCamera;
                    state.Canvas.planeDistance = state.PlaneDistance;
                }
                hdrCanvasStates.Clear();
                canvasRefreshFrames = 0;
            }

            private void RemoveDestroyedCanvasStates()
            {
                for (int index = hdrCanvasStates.Count - 1; index >= 0; index--)
                    if (hdrCanvasStates[index].Canvas == null)
                        hdrCanvasStates.RemoveAt(index);
            }

            private static Camera FindOutputCamera()
            {
                Camera main = Camera.main;
                if (IsUsableOutputCamera(main))
                    return main;

                foreach (Camera camera in Camera.allCameras)
                    if (IsUsableOutputCamera(camera))
                        return camera;
                return null;
            }

            private static bool IsUsableOutputCamera(Camera camera) =>
                camera != null
                && camera.isActiveAndEnabled
                && camera.targetTexture == null
                && camera.targetDisplay == 0;

            internal static float HdrCanvasPlaneDistance(Camera camera)
            {
                if (camera == null)
                    return 1f;

                float minimum = camera.nearClipPlane + 0.01f;
                float maximum = camera.farClipPlane - 0.01f;
                return maximum > minimum ? Mathf.Clamp(1f, minimum, maximum) : minimum;
            }
        }
    }
}
