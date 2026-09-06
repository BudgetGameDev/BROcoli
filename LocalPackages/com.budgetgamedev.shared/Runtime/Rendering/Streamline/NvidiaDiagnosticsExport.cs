using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>Opt-in player log capture for reproducing native failures outside the menu.</summary>
    internal sealed class NvidiaDiagnosticsExport : MonoBehaviour
    {
        private string output;
        private static string bufferPrefix;
        private static int captureFrame = -1;
        internal static bool SpatialOnly { get; private set; }

        internal static void CaptureBuffer(
            CommandBuffer cmd,
            RenderTexture texture,
            string label,
            Vector2Int size
        )
        {
            if (bufferPrefix == null || Time.frameCount < 300)
                return;
            if (captureFrame < 0)
                captureFrame = Time.frameCount;
            if (Time.frameCount != captureFrame)
                return;
            string path = bufferPrefix + "-" + label + ".png";
            cmd.RequestAsyncReadback(
                texture,
                0,
                0,
                size.x,
                0,
                size.y,
                0,
                1,
                TextureFormat.RGBA32,
                request =>
                {
                    if (request.hasError)
                    {
                        Debug.LogWarning("NVIDIA buffer readback failed: " + label);
                        return;
                    }
                    File.WriteAllBytes(
                        path,
                        ImageConversion.EncodeArrayToPNG(
                            request.GetData<byte>().ToArray(),
                            GraphicsFormat.R8G8B8A8_UNorm,
                            (uint)request.width,
                            (uint)request.height
                        )
                    );
                    Debug.Log("[NVIDIA diagnostics] Buffer capture: " + path);
                }
            );
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-nvidiaDiagnostics");
            if (Application.isEditor || index < 0 || index + 1 >= args.Length)
                return;
            var host = new GameObject("NVIDIA diagnostics export");
            DontDestroyOnLoad(host);
            host.AddComponent<NvidiaDiagnosticsExport>().output = Path.GetFullPath(args[index + 1]);
            if (Array.IndexOf(args, "-nvidiaDiagnosticsBuffers") >= 0)
                bufferPrefix = Path.GetFullPath(args[index + 1]);
            SpatialOnly = Array.IndexOf(args, "-nvidiaDiagnosticsSpatialOnly") >= 0;
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int scene = Array.IndexOf(args, "-nvidiaDiagnosticsScene");
            if (scene >= 0 && scene + 1 < args.Length)
            {
                yield return new WaitForSecondsRealtime(5);
                if (Application.CanStreamedLevelBeLoaded(args[scene + 1]))
                {
                    Debug.Log("[NVIDIA diagnostics] Loading scene: " + args[scene + 1]);
                    SceneManager.LoadScene(args[scene + 1]);
                }
                else
                    Debug.LogError(
                        "[NVIDIA diagnostics] Scene is not included in this player: "
                            + args[scene + 1]
                    );
            }
            // Continue collecting while the settings menu pauses gameplay.
            while (true)
            {
                yield return new WaitForSecondsRealtime(5);
                Write();
                if (Array.IndexOf(args, "-nvidiaDiagnosticsScreenshot") >= 0)
                    ScreenCapture.CaptureScreenshot(Path.ChangeExtension(output, ".png"));
            }
        }

        private void OnApplicationQuit() => Write();

        private void Write()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, NvidiaRendering.CaptureForCopy());
            }
            catch (Exception error)
                when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("NVIDIA diagnostics export failed: " + error.Message);
                enabled = false;
                StopAllCoroutines();
            }
        }
    }
}
