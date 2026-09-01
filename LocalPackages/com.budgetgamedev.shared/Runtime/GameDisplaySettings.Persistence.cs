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

        internal static bool IsNativeHdrPlatform(RuntimePlatform platform) =>
            platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.OSXPlayer;

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

        private static bool TryUseNativeDisplayCalibration()
        {
            LoadValues();
            if (
                hasSavedCalibration
                || !IsNativeHdrPlayer
                || (!HDROutputSettings.main.available && !HDROutputSettings.main.active)
            )
                return false;

            return TryApplyDetectedCalibrationDefaults(
                HDROutputSettings.main.maxToneMapLuminance,
                HDROutputSettings.main.paperWhiteNits,
                HDROutputSettings.main.minToneMapLuminance
            );
        }
    }
}
