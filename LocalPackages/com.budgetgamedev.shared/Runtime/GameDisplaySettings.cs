using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared
{
    internal readonly struct NativeDisplayMode
    {
        internal readonly int Width;
        internal readonly int Height;
        internal readonly RefreshRate RefreshRate;

        internal NativeDisplayMode(int width, int height, RefreshRate refreshRate)
        {
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
        }

        internal bool IsValid =>
            Width > 0 && Height > 0 && RefreshRate.numerator > 0 && RefreshRate.denominator > 0;

        public override string ToString() =>
            $"{Width}x{Height} @ {RefreshRate.numerator}/{RefreshRate.denominator} Hz";
    }

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
        private static bool calibrationPreviewActive;
        private static bool hasDetectedHdrProfile;
        private static bool hdrEnabled;
        private static float peakBrightnessNits;
        private static float paperWhiteNits;
        private static float blackLevelNits;
        private static float detectedPeakBrightnessNits;
        private static float detectedFullFrameBrightnessNits;
        private static float detectedPaperWhiteNits;
        private static float detectedBlackLevelNits;

        public static event Action ValuesChanged;

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

        /// <summary>Windows, whether the game is a player or the Editor playing it.</summary>
        public static bool IsWindows =>
            IsWindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

        /// <summary>The same, for macOS.</summary>
        public static bool IsMacOS =>
            IsMacOSPlayer || Application.platform == RuntimePlatform.OSXEditor;

        public static bool SupportsNativeHdr => IsNativeHdrPlatform(Application.platform);

        public static bool IsHdrAvailable =>
            SupportsNativeHdr
            && SystemInfo.hdrDisplaySupportFlags.HasFlag(HDRDisplaySupportFlags.Supported)
            && HDROutputSettings.main.available;

        public static bool IsHdrActive => SupportsNativeHdr && HDROutputSettings.main.active;

        public static bool CanSwitchHdrAtRuntime =>
            SupportsNativeHdr
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

        public static bool HasDetectedHdrProfile => hasDetectedHdrProfile;

        public static float DetectedPeakBrightnessNits => detectedPeakBrightnessNits;

        public static float DetectedFullFrameBrightnessNits => detectedFullFrameBrightnessNits;

        public static float DetectedPaperWhiteNits => detectedPaperWhiteNits;

        public static float DetectedBlackLevelNits => detectedBlackLevelNits;

        public static string HdrStatus
        {
            get =>
                ResolveHdrStatus(
                    SupportsNativeHdr,
                    HdrEnabled,
                    IsHdrActive,
                    CanSwitchHdrAtRuntime,
                    IsHdrAvailable,
                    IsWindows,
                    IsMacOS,
                    IsTenBitHdrActive,
                    IsHdrActive ? HDROutputSettings.main.graphicsFormat.ToString() : string.Empty,
                    UsingSystemCalibrationDefaults,
                    PeakBrightnessNits,
                    systemHdrState
                );
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        internal static void ConfigureNativeWindowsDisplay()
        {
            if (!IsWindowsPlayer)
                return;

            NativeDisplayMode mode = DetectNativeWindowsDisplayMode();
            if (!mode.IsValid)
            {
                Debug.LogWarning(
                    "[GameDisplaySettings] Could not detect a valid native Windows display mode."
                );
                return;
            }

            // Unity persists the previous player's fullscreen mode and refresh rate. Override
            // those values before the first splash-screen present so upgrades from an exclusive
            // D3D12 build cannot recreate a stale swapchain and fail with DXGI_ERROR_INVALID_CALL.
            Screen.SetResolution(
                mode.Width,
                mode.Height,
                FullScreenMode.FullScreenWindow,
                mode.RefreshRate
            );
            Debug.Log($"[GameDisplaySettings] Native borderless display mode: {mode}");
        }

        internal static NativeDisplayMode DetectNativeWindowsDisplayMode()
        {
            if (WindowsDisplayHdrState.TryQueryActiveDisplayMode(out NativeDisplayMode mode))
                return mode;

            Resolution current = Screen.currentResolution;
            Display display = Display.main;
            int width =
                display != null && display.systemWidth > 0 ? display.systemWidth : current.width;
            int height =
                display != null && display.systemHeight > 0 ? display.systemHeight : current.height;
            return new NativeDisplayMode(width, height, current.refreshRateRatio);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            instance = null;
            valuesLoaded = false;
            calibrationPreviewActive = false;
            systemHdrState = SystemHdrState.Unknown;
            systemHdrStateProvider = null;
            hasDetectedHdrProfile = false;
            detectedPeakBrightnessNits = 0f;
            detectedFullFrameBrightnessNits = 0f;
            detectedPaperWhiteNits = 0f;
            detectedBlackLevelNits = 0f;
            ValuesChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Bootstrap()
        {
            Bootstrap(
                SupportsNativeHdr,
                Application.isPlaying,
                UnityEngine.Object.DontDestroyOnLoad
            );
        }

        internal static void Bootstrap(
            bool nativeHdrPlayer,
            bool isPlaying,
            Action<UnityEngine.Object> keepAlive
        )
        {
            LoadValues();
            if (!nativeHdrPlayer || instance != null)
                return;

            GameObject root = new("Game Display Settings");
            instance = root.AddComponent<HdrDisplayDriver>();
            if (isPlaying)
                keepAlive(root);
        }
    }
}
