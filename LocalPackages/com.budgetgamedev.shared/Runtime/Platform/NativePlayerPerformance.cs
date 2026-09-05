using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared
{
    /// <summary>Native frame pacing and input tuning, independent of scenes and render pipelines.</summary>
    public sealed class NativePlayerPerformance : MonoBehaviour
    {
        internal const float PhysicsStep = 1f / 120f;
        internal const float MaximumStep = 1f / 30f;
        internal const float InputPollingRate = 240f;
        private int qualityLevel;
        private bool reportTiming;
        private double sampleStart;
        private int sampleFrames;
        private double longestFrame;
        private bool sampleFocused;

        internal static bool IsDesktopPlayer(RuntimePlatform platform) =>
            platform
                is RuntimePlatform.WindowsPlayer
                    or RuntimePlatform.OSXPlayer
                    or RuntimePlatform.LinuxPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!IsDesktopPlayer(Application.platform))
                return;

            Apply();
            var root = new GameObject("Native Player Performance");
            DontDestroyOnLoad(root);
            var driver = root.AddComponent<NativePlayerPerformance>();
            driver.qualityLevel = QualitySettings.GetQualityLevel();
            driver.reportTiming = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument =>
                    string.Equals(
                        argument,
                        "-frameTimingReport",
                        StringComparison.OrdinalIgnoreCase
                    )
            );
            driver.ResetSample();
            Debug.Log(
                $"[NativePerformance] quality={driver.qualityLevel} "
                    + $"({QualitySettings.names[driver.qualityLevel]}), targetFPS={Application.targetFrameRate}, "
                    + $"vSync={QualitySettings.vSyncCount}, queue={QualitySettings.maxQueuedFrames}, "
                    + $"renderInterval={OnDemandRendering.renderFrameInterval}, physicsHz={1f / Time.fixedDeltaTime:F0}, "
                    + $"input={InputSystem.settings.updateMode}, pollingHz={InputSystem.pollingFrequency:F0}, "
                    + $"displayHz={Screen.currentResolution.refreshRateRatio.value:F2}, api={SystemInfo.graphicsDeviceType}"
            );
        }

        internal static void Apply()
        {
            ApplyFramePacing();
            Time.fixedDeltaTime = PhysicsStep;
            Time.maximumDeltaTime = MaximumStep;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.pollingFrequency = InputPollingRate;
        }

        internal static void ApplyFramePacing()
        {
            // A quality preset must not reintroduce a wait for VSync. There is no fixed
            // 60/120 FPS software cap, so a 240 Hz display can use the available GPU budget.
            QualitySettings.vSyncCount = 0;
            QualitySettings.maxQueuedFrames = 1;
            Application.targetFrameRate = -1;
            OnDemandRendering.renderFrameInterval = 1;
        }

        private void LateUpdate()
        {
            int currentQuality = QualitySettings.GetQualityLevel();
            if (currentQuality != qualityLevel)
            {
                qualityLevel = currentQuality;
                ApplyFramePacing();
            }

            if (!reportTiming)
                return;
            sampleFrames++;
            longestFrame = Math.Max(longestFrame, Time.unscaledDeltaTime);
            sampleFocused &= Application.isFocused;
            double elapsed = Time.realtimeSinceStartupAsDouble - sampleStart;
            if (elapsed < 5d)
                return;
            Debug.Log(
                $"[NativePerformance] measuredFPS={sampleFrames / elapsed:F1}, "
                    + $"meanFrameMs={elapsed * 1000d / sampleFrames:F2}, maxFrameMs={longestFrame * 1000d:F2}, "
                    + $"focused={sampleFocused}, batch={Application.isBatchMode}, "
                    + $"quality={qualityLevel}, targetFPS={Application.targetFrameRate}, vSync={QualitySettings.vSyncCount}"
            );
            ResetSample();
        }

        private void ResetSample()
        {
            sampleStart = Time.realtimeSinceStartupAsDouble;
            sampleFrames = 0;
            longestFrame = 0d;
            sampleFocused = Application.isFocused;
        }
    }
}
