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
                if (systemChanged || !ReferenceEquals(grade, RenderPipelineFrontEnd.HdrGrade))
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
                grade = null;
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
                if (instance != null && instance != this)
                    return;

                AttachGrade();
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
            /// Follows quality-level pipeline changes as well as startup. The old volume must
            /// be detached before the replacement is attached, otherwise switching back leaves
            /// a stale maximum-priority grade participating in the stack.
            /// </summary>
            private void AttachGrade()
            {
                IHdrGradeFrontEnd active = RenderPipelineFrontEnd.HdrGrade;
                if (ReferenceEquals(grade, active))
                    return;

                grade?.Detach(Application.isPlaying, Destroy, DestroyImmediate);
                grade = active;
                grade?.Attach(gameObject);
            }
        }
    }
}
