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

            /// <summary>
            /// Only the tone map is overridden for HDR. In particular the scene's bloom is left
            /// alone: it is deliberately blown out, admitting everything above 0.85 and adding it
            /// back at 135%, and that glow around the torches is most of the dungeon's
            /// atmosphere rather than an artefact of SDR clipping. Inheriting it makes the HDR
            /// picture the SDR one with its highlights carried past display white instead of
            /// pinned to it.
            /// </summary>
            private void CreateTonemappingOverride()
            {
                volume = gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = float.MaxValue;
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                tonemapping = profile.Add<Tonemapping>();
                volume.profile = profile;
            }

            private void ConfigureTonemapping(bool enabled)
            {
                if (tonemapping == null)
                    return;

                volume.enabled = enabled;
                tonemapping.active = enabled;

                // The scene is graded for ACES in SDR. Neutral tone mapping has no filmic curve
                // at all on an HDR swapchain: it scales the scene straight into nits, which lifts
                // the dungeon's shadows several times above what SDR shows and leaves the picture
                // milky. ACES keeps the SDR tone curve below diffuse white and spends the
                // display's extra range on the highlights instead.
                tonemapping.mode.Override(TonemappingMode.ACES);
                tonemapping.acesPreset.Override(HdrToneMapPreset);
                tonemapping.hueShiftAmount.Override(0f);

                // Paper white decides where diffuse white lands, and therefore how bright the
                // whole picture is. The calibration seeds itself from the operating system, so
                // this is the system's value until the player overrides it; reading it from the
                // saved calibration keeps it in step with the luminance the torches solve for.
                tonemapping.detectPaperWhite.Override(false);
                tonemapping.paperWhite.Override(PaperWhiteNits);
                tonemapping.detectBrightnessLimits.Override(
                    calibrationPreviewActive || UsingSystemCalibrationDefaults
                );
                tonemapping.minNits.Override(BlackLevelNits);
                tonemapping.maxNits.Override(PeakBrightnessNits);
            }
        }
    }
}
