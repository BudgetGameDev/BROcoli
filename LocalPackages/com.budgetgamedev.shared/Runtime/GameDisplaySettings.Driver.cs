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
                TryUseNativeDisplayCalibration();
                Apply();
                lastStatus = HdrStatus;
            }

            internal void OnApplicationFocus(bool focused)
            {
                if (focused)
                {
                    TryUseNativeDisplayCalibration();
                    Apply();
                }
            }

            internal void Update()
            {
                if (Time.unscaledTime < nextStatusPoll)
                    return;

                nextStatusPoll = Time.unscaledTime + StatusPollInterval;
                TryUseNativeDisplayCalibration();

                string status = HdrStatus;
                if (string.Equals(status, lastStatus, StringComparison.Ordinal))
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
                ConfigureTonemapping(HdrEnabled);
                ConfigureCanvasComposition(HdrEnabled && displayDetected);
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
                volume.profile = profile;
            }

            private void ConfigureTonemapping(bool enabled)
            {
                if (tonemapping == null)
                    return;

                volume.enabled = enabled;
                tonemapping.active = enabled;
                tonemapping.mode.Override(TonemappingMode.Neutral);
                tonemapping.neutralHDRRangeReductionMode.Override(
                    NeutralRangeReductionMode.BT2390
                );
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

                // SDR uses the scene's ACES curve. A small HDR-only contrast expansion keeps
                // midtones from looking washed out after switching to the calibratable Neutral
                // output path without lifting the whole image or sacrificing highlight headroom.
                colorAdjustments.active = enabled;
                colorAdjustments.contrast.Override(12f);
            }
        }
    }
}
