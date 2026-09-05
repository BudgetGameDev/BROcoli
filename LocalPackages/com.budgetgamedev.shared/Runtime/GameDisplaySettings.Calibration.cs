using UnityEngine;

namespace BudgetGameDev.Shared
{
    public static partial class GameDisplaySettings
    {
        public static void SetHdrEnabled(bool enabled)
        {
            LoadValues();
            if (hdrEnabled == enabled)
                return;

            hdrEnabled = enabled;
            SaveAndApply();
        }

        public static void SetCalibration(float peakNits, float paperWhite, float blackLevel)
        {
            LoadValues();
            peakNits = SanitizePeakBrightness(peakNits);
            paperWhite = SanitizePaperWhite(paperWhite, peakNits);
            blackLevel = SanitizeBlackLevel(blackLevel);
            if (
                Mathf.Approximately(peakBrightnessNits, peakNits)
                && Mathf.Approximately(paperWhiteNits, paperWhite)
                && Mathf.Approximately(blackLevelNits, blackLevel)
            )
                return;

            peakBrightnessNits = peakNits;
            paperWhiteNits = paperWhite;
            blackLevelNits = blackLevel;
            hasSavedCalibration = true;
            usingSystemCalibrationDefaults = false;
            SaveAndApply();
        }

        public static void SetPeakBrightness(float nits) =>
            SetCalibration(nits, PaperWhiteNits, BlackLevelNits);

        public static void SetPaperWhite(float nits) =>
            SetCalibration(PeakBrightnessNits, nits, BlackLevelNits);

        public static void SetBlackLevel(float nits) =>
            SetCalibration(PeakBrightnessNits, PaperWhiteNits, nits);

        public static bool ResetToDetectedHdrProfile()
        {
            LoadValues();
            if (!hasDetectedHdrProfile)
                return false;

            peakBrightnessNits = detectedPeakBrightnessNits;
            paperWhiteNits = detectedPaperWhiteNits;
            blackLevelNits = detectedBlackLevelNits;
            hasSavedCalibration = true;
            usingSystemCalibrationDefaults = true;
            SaveAndApply();
            return true;
        }

        public static void ResetToDefault()
        {
            ResetToSystemCalibration();
        }

        /// <summary>
        /// Discards the player's manual HDR limits and returns to the values reported by the
        /// operating system and display. Defaults are used only when the platform reports none.
        /// </summary>
        public static void ResetToSystemCalibration()
        {
            LoadValues();
            hdrEnabled = DefaultHdrEnabled;
            peakBrightnessNits = DefaultPeakBrightnessNits;
            paperWhiteNits = DefaultPaperWhiteNits;
            blackLevelNits = DefaultBlackLevelNits;
            hasSavedCalibration = false;
            usingSystemCalibrationDefaults = false;
            PlayerPrefs.DeleteKey(PeakBrightnessKey);
            PlayerPrefs.DeleteKey(PaperWhiteKey);
            PlayerPrefs.DeleteKey(BlackLevelKey);
            PlayerPrefs.DeleteKey(SystemCalibrationKey);
            PlayerPrefs.DeleteKey(LegacyWindowsCalibrationKey);
            if (!TryUseNativeDisplayCalibration())
                SaveAndApply();
        }

        /// <summary>
        /// Temporarily lets URP use the display's native luminance limits while calibration
        /// patterns are shown, so moving a slider changes the pattern instead of its tone map.
        /// </summary>
        public static void BeginHdrCalibrationPreview()
        {
            if (calibrationPreviewActive)
                return;

            calibrationPreviewActive = true;
            instance?.Apply();
        }

        public static void EndHdrCalibrationPreview()
        {
            if (!calibrationPreviewActive)
                return;

            calibrationPreviewActive = false;
            instance?.Apply();
        }

        internal static bool TryApplyDetectedCalibrationDefaults(
            float peakNits,
            float paperWhite,
            float blackLevel
        )
        {
            LoadValues();
            if (
                (hasSavedCalibration && !usingSystemCalibrationDefaults)
                || !TryNormalizeDetectedCalibration(
                    peakNits,
                    paperWhite,
                    blackLevel,
                    out peakNits,
                    out paperWhite,
                    out blackLevel
                )
            )
                return false;

            bool changed =
                !Mathf.Approximately(peakBrightnessNits, peakNits)
                || !Mathf.Approximately(paperWhiteNits, paperWhite)
                || !Mathf.Approximately(blackLevelNits, blackLevel)
                || !hasSavedCalibration
                || !usingSystemCalibrationDefaults;
            peakBrightnessNits = peakNits;
            paperWhiteNits = paperWhite;
            blackLevelNits = blackLevel;
            hasSavedCalibration = true;
            usingSystemCalibrationDefaults = true;
            if (changed)
                SaveAndApply();
            return true;
        }

        internal static bool TryNormalizeDetectedCalibration(
            float peakNits,
            float paperWhite,
            float blackLevel,
            out float normalizedPeakNits,
            out float normalizedPaperWhite,
            out float normalizedBlackLevel
        )
        {
            normalizedPeakNits = DefaultPeakBrightnessNits;
            normalizedPaperWhite = DefaultPaperWhiteNits;
            normalizedBlackLevel = DefaultBlackLevelNits;
            if (
                !float.IsFinite(peakNits)
                || peakNits <= 0f
                || !float.IsFinite(paperWhite)
                || paperWhite <= 0f
                || !float.IsFinite(blackLevel)
                || blackLevel < 0f
            )
                return false;

            normalizedPeakNits = SanitizePeakBrightness(peakNits);
            normalizedPaperWhite = SanitizePaperWhite(paperWhite, normalizedPeakNits);
            normalizedBlackLevel = SanitizeBlackLevel(blackLevel);
            return true;
        }

        internal static float SanitizePeakBrightness(float value) =>
            Mathf.Clamp(
                float.IsFinite(value) ? value : DefaultPeakBrightnessNits,
                MinimumPeakBrightnessNits,
                MaximumPeakBrightnessNits
            );

        internal static float SanitizePaperWhite(float value, float peakNits) =>
            Mathf.Clamp(
                float.IsFinite(value) ? value : DefaultPaperWhiteNits,
                MinimumPaperWhiteNits,
                Mathf.Min(MaximumPaperWhiteNits, SanitizePeakBrightness(peakNits))
            );

        internal static float SanitizeBlackLevel(float value) =>
            Mathf.Clamp(
                float.IsFinite(value) ? value : DefaultBlackLevelNits,
                MinimumBlackLevelNits,
                MaximumBlackLevelNits
            );
    }
}
