using System;
using BudgetGameDev.Shared.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared
{
    public static partial class GameDisplaySettings
    {
        [DefaultExecutionOrder(-32000)]
        internal sealed partial class HdrDisplayDriver : MonoBehaviour
        {
            private const float StatusPollInterval = 0.5f;

            private IHdrGradeFrontEnd grade;
            private string lastStatus;
            private float nextStatusPoll;

            internal void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                instance = this;
                InitializeCanvasComposition();
                AttachGrade();
                RefreshSystemHdrState();
                TryUseNativeDisplayCalibration();
                Apply();
                lastStatus = HdrStatus;
            }

            internal void OnApplicationFocus(bool focused)
            {
                if (focused)
                {
                    RefreshSystemHdrState();
                    TryUseNativeDisplayCalibration();
                    Apply();
                }
            }

            internal void Update()
            {
                if (Time.unscaledTime < nextStatusPoll)
                    return;

                nextStatusPoll = Time.unscaledTime + StatusPollInterval;
                bool systemChanged = RefreshSystemHdrState();
                if (systemChanged)
                    Apply();
                TryUseNativeDisplayCalibration();

                string status = HdrStatus;
                if (!systemChanged && string.Equals(status, lastStatus, StringComparison.Ordinal))
                    return;

                lastStatus = status;
                NotifyStatusChanged();
            }

            internal void OnDestroy()
            {
                OnDestroy(Application.isPlaying, Destroy, DestroyImmediate);
            }

            internal void OnDestroy(
                bool isPlaying,
                Action<UnityEngine.Object> destroyDeferred,
                Action<UnityEngine.Object> destroyImmediate
            )
            {
                ShutdownCanvasComposition();
                if (instance == this)
                    instance = null;
                grade?.Detach(isPlaying, destroyDeferred, destroyImmediate);
            }

            internal void Apply()
            {
                HDRDisplaySupportFlags flags = SystemInfo.hdrDisplaySupportFlags;
                bool switchable = flags.HasFlag(HDRDisplaySupportFlags.RuntimeSwitchable);
                Apply(
                    switchable,
                    HDROutputSettings.main.available || HDROutputSettings.main.active,
                    HDROutputSettings.main.RequestHDRModeChange
                );
            }

            internal void Apply(bool switchable, bool displayDetected, Action<bool> requestHdrMode)
            {
                // The HDR grade re-exposes the scene for a wide-luminance swapchain. On an SDR
                // desktop it only flattens the picture, so it needs a detected HDR display too.
                bool enabled = HdrEnabled && displayDetected;
                grade?.Apply(BuildGradeRequest(enabled));
                ConfigureCanvasComposition(enabled);
                if (switchable && displayDetected)
                    requestHdrMode(HdrEnabled);
            }

            /// <summary>
            /// States the grade the calibration is asking for, in display terms. Which volume
            /// components carry it is the active pipeline's business, not this driver's: the
            /// same request produces the same luminance whether Universal or High Definition
            /// renders the frame.
            /// </summary>
            private static HdrGradeRequest BuildGradeRequest(bool enabled) =>
                new HdrGradeRequest(
                    enabled,
                    HdrToneMapPreset,
                    PaperWhiteNits,
                    BlackLevelNits,
                    PeakBrightnessNits,
                    calibrationPreviewActive || UsingSystemCalibrationDefaults,
                    HdrSaturationLift,
                    HdrContrastLift,
                    HdrBlackFloor
                );

            /// <summary>
            /// Builds the active pipeline's grade volume on this object. A build whose pipeline
            /// registers no front end -- the web build, which has no native HDR swapchain to
            /// grade for -- simply runs without one.
            /// </summary>
            private void AttachGrade()
            {
                grade = RenderPipelineFrontEnd.HdrGrade;
                grade?.Attach(gameObject);
            }
        }
    }
}
