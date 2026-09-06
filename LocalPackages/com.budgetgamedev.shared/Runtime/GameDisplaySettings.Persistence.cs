using System;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    public static partial class GameDisplaySettings
    {
        internal static bool IsTenBitFormat(string formatName) =>
            !string.IsNullOrEmpty(formatName)
            && formatName.IndexOf("10", StringComparison.OrdinalIgnoreCase) >= 0
            && formatName.IndexOf("2", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Whether <paramref name="platform"/> can put a native HDR swapchain on screen. Both
        /// Editors are in: the Game view outputs HDR the same way a player does on DirectX 12,
        /// Vulkan and Metal, and being able to see the HDR grade without building is most of how
        /// it gets tuned. This promises nothing about HDR being on -- that is
        /// <c>HDROutputSettings</c>' answer, and on DirectX 11 the Editor's answer is always no,
        /// which the status line then says out loud.
        /// </summary>
        internal static bool IsNativeHdrPlatform(RuntimePlatform platform) =>
            platform == RuntimePlatform.WindowsPlayer
            || platform == RuntimePlatform.OSXPlayer
            || platform == RuntimePlatform.WindowsEditor
            || platform == RuntimePlatform.OSXEditor;

        internal static void NotifyStatusChanged() => ValuesChanged?.Invoke();

        private static void LoadValues()
        {
            if (valuesLoaded)
                return;

            hdrEnabled = PlayerPrefs.GetInt(HdrEnabledKey, DefaultHdrEnabled ? 1 : 0) != 0;
            hasSavedCalibration =
                PlayerPrefs.HasKey(PeakBrightnessKey)
                || PlayerPrefs.HasKey(PaperWhiteKey)
                || PlayerPrefs.HasKey(BlackLevelKey);
            usingSystemCalibrationDefaults =
                hasSavedCalibration
                && (
                    PlayerPrefs.GetInt(SystemCalibrationKey, 0) != 0
                    || PlayerPrefs.GetInt(LegacyWindowsCalibrationKey, 0) != 0
                );
            peakBrightnessNits = SanitizePeakBrightness(
                PlayerPrefs.GetFloat(
                    PeakBrightnessKey,
                    PlayerPrefs.GetInt(PeakBrightnessKey, (int)DefaultPeakBrightnessNits)
                )
            );
            paperWhiteNits = SanitizePaperWhite(
                PlayerPrefs.GetFloat(PaperWhiteKey, DefaultPaperWhiteNits),
                peakBrightnessNits
            );
            blackLevelNits = SanitizeBlackLevel(
                PlayerPrefs.GetFloat(BlackLevelKey, DefaultBlackLevelNits)
            );
            valuesLoaded = true;
        }

        private static void SaveAndApply()
        {
            PlayerPrefs.SetInt(HdrEnabledKey, hdrEnabled ? 1 : 0);
            if (hasSavedCalibration)
            {
                PlayerPrefs.SetFloat(PeakBrightnessKey, peakBrightnessNits);
                PlayerPrefs.SetFloat(PaperWhiteKey, paperWhiteNits);
                PlayerPrefs.SetFloat(BlackLevelKey, blackLevelNits);
                PlayerPrefs.SetInt(SystemCalibrationKey, usingSystemCalibrationDefaults ? 1 : 0);
                PlayerPrefs.DeleteKey(LegacyWindowsCalibrationKey);
            }
            else
            {
                PlayerPrefs.DeleteKey(PeakBrightnessKey);
                PlayerPrefs.DeleteKey(PaperWhiteKey);
                PlayerPrefs.DeleteKey(BlackLevelKey);
                PlayerPrefs.DeleteKey(SystemCalibrationKey);
                PlayerPrefs.DeleteKey(LegacyWindowsCalibrationKey);
            }
            PlayerPrefs.Save();
            instance?.Apply();
            ValuesChanged?.Invoke();
        }

        internal static bool TryUseNativeDisplayCalibration()
        {
            if (!SupportsNativeHdr)
                return false;

            var display = HDROutputSettings.main;
            // Unity throws when luminance metadata is read without an available HDR display.
            // Guard here: arguments are evaluated before the calibration overload can reject them.
            if (display == null || !display.available)
                return false;

            return TryUseNativeDisplayCalibration(
                true,
                true,
                display.active,
                display.maxToneMapLuminance,
                display.paperWhiteNits,
                display.minToneMapLuminance,
                display.maxFullFrameToneMapLuminance
            );
        }

        internal static bool TryUseNativeDisplayCalibration(
            bool nativeHdrPlayer,
            bool available,
            bool active,
            float peakNits,
            float paperWhite,
            float blackLevel,
            float fullFrameNits = float.NaN
        )
        {
            LoadValues();
            if (
                !nativeHdrPlayer
                || (!available && !active)
                || !TryNormalizeDetectedCalibration(
                    peakNits,
                    paperWhite,
                    blackLevel,
                    out float normalizedPeak,
                    out float normalizedPaperWhite,
                    out float normalizedBlack
                )
            )
                return false;

            float normalizedFullFrame =
                float.IsFinite(fullFrameNits) && fullFrameNits > 0f
                    ? SanitizePeakBrightness(fullFrameNits)
                    : normalizedPeak;
            bool detectedChanged =
                !hasDetectedHdrProfile
                || !Mathf.Approximately(detectedPeakBrightnessNits, normalizedPeak)
                || !Mathf.Approximately(detectedFullFrameBrightnessNits, normalizedFullFrame)
                || !Mathf.Approximately(detectedPaperWhiteNits, normalizedPaperWhite)
                || !Mathf.Approximately(detectedBlackLevelNits, normalizedBlack);
            hasDetectedHdrProfile = true;
            detectedPeakBrightnessNits = normalizedPeak;
            detectedFullFrameBrightnessNits = normalizedFullFrame;
            detectedPaperWhiteNits = normalizedPaperWhite;
            detectedBlackLevelNits = normalizedBlack;

            if (hasSavedCalibration && !usingSystemCalibrationDefaults)
            {
                if (detectedChanged)
                    ValuesChanged?.Invoke();
                return false;
            }

            return TryApplyDetectedCalibrationDefaults(
                normalizedPeak,
                normalizedPaperWhite,
                normalizedBlack
            );
        }
    }
}
