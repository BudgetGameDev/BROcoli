using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Persistent native-display settings. On Windows and macOS this drives Unity's HDR output
    /// and a high-priority URP override configured by the in-game calibration screen.
    /// </summary>
    public static partial class GameDisplaySettings
    {
        public const bool DefaultHdrEnabled = true;
        public const float DefaultPeakBrightnessNits = 600f;
        public const float DefaultPaperWhiteNits = 200f;
        public const float DefaultBlackLevelNits = 0.0005f;
        public const float MinimumPeakBrightnessNits = 200f;
        public const float MaximumPeakBrightnessNits = 2000f;
        public const float MinimumPaperWhiteNits = 80f;
        public const float MaximumPaperWhiteNits = 400f;
        public const float MinimumBlackLevelNits = 0f;
        public const float MaximumBlackLevelNits = 0.05f;

        internal const string HdrEnabledKey = "Display.HdrEnabled";
        internal const string PeakBrightnessKey = "Display.HdrPeakNits";
        internal const string PaperWhiteKey = "Display.HdrPaperWhiteNits";
        internal const string BlackLevelKey = "Display.HdrBlackLevelNits";
        internal const string SystemCalibrationKey = "Display.HdrUsesSystemCalibration";
        internal const string LegacyWindowsCalibrationKey = "Display.HdrUsesWindowsCalibration";

        private static HdrDisplayDriver instance;
        private static bool valuesLoaded;
        private static bool hasSavedCalibration;
        private static bool usingSystemCalibrationDefaults;
        private static bool hdrEnabled;
        private static float peakBrightnessNits;
        private static float paperWhiteNits;
        private static float blackLevelNits;

        public static event Action ValuesChanged;

        public static bool HdrEnabled
        {
            get
            {
                LoadValues();
                return hdrEnabled;
            }
        }

        public static float PeakBrightnessNits
        {
            get
            {
                LoadValues();
                return peakBrightnessNits;
            }
        }

        public static float PaperWhiteNits
        {
            get
            {
                LoadValues();
                return paperWhiteNits;
            }
        }

        public static float BlackLevelNits
        {
            get
            {
                LoadValues();
                return blackLevelNits;
            }
        }

        public static bool IsWindowsPlayer => Application.platform == RuntimePlatform.WindowsPlayer;

        public static bool IsMacOSPlayer => Application.platform == RuntimePlatform.OSXPlayer;

        public static bool IsNativeHdrPlayer => IsNativeHdrPlatform(Application.platform);

        public static bool IsHdrAvailable =>
            IsNativeHdrPlayer
            && SystemInfo.hdrDisplaySupportFlags.HasFlag(HDRDisplaySupportFlags.Supported)
            && HDROutputSettings.main.available;

        public static bool IsHdrActive => IsNativeHdrPlayer && HDROutputSettings.main.active;

        public static bool CanSwitchHdrAtRuntime =>
            IsNativeHdrPlayer
            && SystemInfo.hdrDisplaySupportFlags.HasFlag(HDRDisplaySupportFlags.RuntimeSwitchable);

        public static bool IsTenBitHdrActive =>
            IsHdrActive && IsTenBitFormat(HDROutputSettings.main.graphicsFormat.ToString());

        public static bool UsingSystemCalibrationDefaults
        {
            get
            {
                LoadValues();
                return usingSystemCalibrationDefaults;
            }
        }

        public static string HdrStatus
        {
            get
            {
                if (!IsNativeHdrPlayer)
                    return "NATIVE HDR • WINDOWS / MACOS BUILDS ONLY";
                if (!HdrEnabled)
                {
                    if (!IsHdrActive)
                        return "NATIVE HDR OUTPUT DISABLED";
                    return CanSwitchHdrAtRuntime
                        ? "SWITCHING TO SDR…"
                        : "HDR ACTIVE • PLATFORM DOES NOT SUPPORT LIVE SWITCHING";
                }
                if (!IsHdrAvailable && !IsHdrActive)
                    return "ENABLE HDR IN SYSTEM DISPLAY SETTINGS";
                if (!IsHdrActive)
                    return "SWITCHING TO NATIVE HDR…";
                string output =
                    IsWindowsPlayer && IsTenBitHdrActive ? "10-BIT HDR10 ACTIVE"
                    : IsMacOSPlayer && IsTenBitHdrActive ? "10-BIT METAL HDR ACTIVE"
                    : IsMacOSPlayer ? "NATIVE METAL HDR ACTIVE"
                    : $"HDR ACTIVE • {HDROutputSettings.main.graphicsFormat}";
                string source = UsingSystemCalibrationDefaults
                    ? "SYSTEM DISPLAY PROFILE"
                    : "IN-GAME CALIBRATION";
                return $"{output} • {Mathf.RoundToInt(PeakBrightnessNits)} NITS • {source}";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            instance = null;
            valuesLoaded = false;
            ValuesChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Bootstrap()
        {
            LoadValues();
            if (!IsNativeHdrPlayer || instance != null)
                return;

            GameObject root = new("Game Display Settings");
            instance = root.AddComponent<HdrDisplayDriver>();
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(root);
        }

        public static void SetHdrEnabled(bool enabled)
        {
            LoadValues();
            if (hdrEnabled == enabled)
                return;

            hdrEnabled = enabled;
            SaveAndApply();
        }

        public static void ToggleHdr() => SetHdrEnabled(!HdrEnabled);

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

        public static void ResetToDefault()
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
            if (TryUseNativeDisplayCalibration())
                return;
            SaveAndApply();
        }

        internal static bool TryApplyDetectedCalibrationDefaults(
            float peakNits,
            float paperWhite,
            float blackLevel
        )
        {
            LoadValues();
            if (
                hasSavedCalibration
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

            peakBrightnessNits = peakNits;
            paperWhiteNits = paperWhite;
            blackLevelNits = blackLevel;
            hasSavedCalibration = true;
            usingSystemCalibrationDefaults = true;
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
