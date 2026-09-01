using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared
{
    public static partial class GameDisplaySettings
    {
        [DefaultExecutionOrder(-32000)]
        internal sealed partial class HdrDisplayDriver : MonoBehaviour
        {
            private const float StatusPollInterval = 0.5f;

            private Volume volume;
            private VolumeProfile profile;
            private Tonemapping tonemapping;
            private ColorAdjustments colorAdjustments;
            private Bloom bloom;
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
                CreateTonemappingOverride();
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
                if (profile != null)
                {
                    if (isPlaying)
                        destroyDeferred(profile);
                    else
                        destroyImmediate(profile);
                }
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
                ConfigureTonemapping(enabled);
                ConfigureCanvasComposition(enabled);
                if (switchable && displayDetected)
                    requestHdrMode(HdrEnabled);
            }

            private void CreateTonemappingOverride()
            {
                volume = gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = float.MaxValue;
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                tonemapping = profile.Add<Tonemapping>();
                colorAdjustments = profile.Add<ColorAdjustments>();
                bloom = profile.Add<Bloom>();
                volume.profile = profile;
            }

            private void ConfigureTonemapping(bool enabled)
            {
                if (tonemapping == null)
                    return;

                volume.enabled = enabled;
                tonemapping.active = enabled;
                tonemapping.mode.Override(TonemappingMode.Neutral);
                tonemapping.neutralHDRRangeReductionMode.Override(NeutralRangeReductionMode.BT2390);
                tonemapping.hueShiftAmount.Override(0f);
                // Paper white is an OS/display preference, not a content capability. Keep it
                // automatic and calibrate only the physical black and peak limits.
                tonemapping.detectPaperWhite.Override(true);
                tonemapping.paperWhite.Override(PaperWhiteNits);
                tonemapping.detectBrightnessLimits.Override(
                    calibrationPreviewActive || UsingSystemCalibrationDefaults
                );
                tonemapping.minNits.Override(BlackLevelNits);
                tonemapping.maxNits.Override(PeakBrightnessNits);

                // Keep ordinary HDR scene values below SDR reference white so the display's
                // highlight range remains available to compact emissive details such as flames.
                // SDR retains the authored ACES exposure and bloom from the scene profile.
                colorAdjustments.active = enabled;
                colorAdjustments.postExposure.Override(-0.65f);
                colorAdjustments.contrast.Override(8f);

                // The scene bloom is intentionally generous for SDR. In HDR that produces a
                // broad halo and makes the glow brighter than the visible flame, so admit only
                // the hottest pixels and keep their spread tight.
                bloom.active = enabled;
                bloom.threshold.Override(4f);
                bloom.intensity.Override(0.2f);
                bloom.scatter.Override(0.2f);
            }
        }
    }
}
