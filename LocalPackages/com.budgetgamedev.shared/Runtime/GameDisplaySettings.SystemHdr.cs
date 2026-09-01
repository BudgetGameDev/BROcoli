using System;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Whether the operating system has HDR switched on for the display showing the game.
    /// </summary>
    public enum SystemHdrState
    {
        /// <summary>The platform does not expose an HDR switch; the in-game preference rules.</summary>
        Unknown,
        Disabled,
        Enabled,
    }

    public static partial class GameDisplaySettings
    {
        private static SystemHdrState systemHdrState;

        /// <summary>Test seam replacing the native operating-system query.</summary>
        internal static Func<SystemHdrState> systemHdrStateProvider;

        /// <summary>
        /// The operating system's HDR switch as of the last poll. While it is known, HDR output
        /// follows it: the game never renders its HDR grade onto an SDR desktop and never stays
        /// in SDR on an HDR one.
        /// </summary>
        public static SystemHdrState SystemHdrState => systemHdrState;

        public static bool HdrFollowsSystem => systemHdrState != SystemHdrState.Unknown;

        public static bool CanToggleHdr => !HdrFollowsSystem;

        /// <summary>
        /// Whether HDR output is in effect: the operating-system switch when the platform
        /// exposes one, otherwise the saved in-game preference.
        /// </summary>
        public static bool HdrEnabled
        {
            get
            {
                LoadValues();
                return ResolveHdrOutputEnabled(systemHdrState, hdrEnabled);
            }
        }

        public static void ToggleHdr()
        {
            if (CanToggleHdr)
                SetHdrEnabled(!HdrEnabled);
        }

        /// <summary>
        /// The saved in-game preference, regardless of whether the operating system overrides it.
        /// </summary>
        public static bool HdrPreferred
        {
            get
            {
                LoadValues();
                return hdrEnabled;
            }
        }

        internal static bool ResolveHdrOutputEnabled(SystemHdrState state, bool preferred) =>
            state switch
            {
                SystemHdrState.Enabled => true,
                SystemHdrState.Disabled => false,
                _ => preferred,
            };

        internal static SystemHdrState QuerySystemHdrState()
        {
            if (systemHdrStateProvider != null)
                return systemHdrStateProvider();
            return IsWindowsPlayer ? WindowsDisplayHdrState.Query() : SystemHdrState.Unknown;
        }

        /// <summary>Re-reads the operating-system switch. Returns true when it changed.</summary>
        internal static bool RefreshSystemHdrState()
        {
            SystemHdrState state = QuerySystemHdrState();
            if (state == systemHdrState)
                return false;

            systemHdrState = state;
            return true;
        }
    }
}
