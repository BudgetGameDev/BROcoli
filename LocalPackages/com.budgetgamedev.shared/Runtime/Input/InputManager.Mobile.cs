using System.Runtime.InteropServices;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Decides whether this device needs the on-screen controller, and switches it
    /// on. Every branch that a desktop editor can never be - phone, tablet, touch
    /// browser - is reached through parameters rather than ambient device state,
    /// so the decision stays testable off-device.
    /// </summary>
    public partial class InputManager
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int IsiOSMobile();

        [DllImport("__Internal")]
        private static extern int IsAndroidMobile();

        [DllImport("__Internal")]
        private static extern int IsMobileBrowser();
#endif

        // Also try activating in Update for first few frames in case of race conditions
        internal int mobileCheckFrames = 10; // Increased for WebGL which may have delayed JS init

        private void ActivateVirtualControllerIfMobile()
        {
            bool isMobile = false;

            // For WebGL builds, use JavaScript-based detection (critical for iOS Safari)
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                bool isMobileWebGL = IsMobileBrowser() == 1;
                bool isiOS = IsiOSMobile() == 1;
                bool isAndroid = IsAndroidMobile() == 1;

                Debug.Log(
                    $"[InputManager] WebGL JS detection - isMobile: {isMobileWebGL}, iOS: {isiOS}, Android: {isAndroid}"
                );

                if (isMobileWebGL || isiOS || isAndroid)
                {
                    isMobile = true;
                    Debug.Log("[InputManager] Mobile browser detected via JavaScript");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[InputManager] JavaScript mobile detection failed: {e.Message}");
            }
#endif
            // Preprocessor directives for native mobile builds
#if UNITY_IOS && !UNITY_EDITOR
            isMobile = true;
            Debug.Log("[InputManager] iOS build detected via preprocessor");
#elif UNITY_ANDROID && !UNITY_EDITOR
            isMobile = true;
            Debug.Log("[InputManager] Android build detected via preprocessor");
#endif
            // Runtime checks as backup
            if (!isMobile)
            {
                isMobile = IsMobileDevice(
                    Application.platform,
                    SystemInfo.deviceType,
                    Input.touchSupported
                );
            }

#if UNITY_EDITOR
            isMobile = IncludeEditorMobileSimulation(
                isMobile,
                UnityEngine.Device.SystemInfo.deviceType,
                Input.touchSupported
            );
#endif
            Debug.Log(
                $"[InputManager] Platform: {Application.platform}, DeviceType: {SystemInfo.deviceType}, isMobile: {isMobile}"
            );

            ActivateVirtualController(isMobile);
        }

        internal static bool IncludeEditorMobileSimulation(
            bool detected,
            DeviceType simulatedDeviceType,
            bool touchSupported
        ) => detected || simulatedDeviceType == DeviceType.Handheld || touchSupported;

        /// <summary>
        /// Whether the given device needs touch controls. A handheld, an iPhone or
        /// an Android player always does; a WebGL player only does when the browser
        /// reports touch, because desktop browsers also run the WebGL build.
        /// </summary>
        internal static bool IsMobileDevice(
            RuntimePlatform platform,
            DeviceType deviceType,
            bool touchSupported
        )
        {
            if (
                platform == RuntimePlatform.IPhonePlayer
                || platform == RuntimePlatform.Android
                || deviceType == DeviceType.Handheld
            )
            {
                return true;
            }

            // For WebGL, also check touch support as fallback
            if (platform == RuntimePlatform.WebGLPlayer && touchSupported)
            {
                Debug.Log("[InputManager] WebGL with touch support - enabling mobile controls");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Shows the scene's on-screen controller, which ships inactive. A mobile
        /// build without one has no input at all, so its absence is reported.
        /// </summary>
        internal static void ActivateVirtualController(bool isMobile)
        {
            if (!isMobile)
                return;

            // Find the VirtualController (it starts inactive in scene)
            var vc = FindAnyObjectByType<VirtualController>(FindObjectsInactive.Include);
            if (vc != null)
            {
                vc.gameObject.SetActive(true);
                Debug.Log(
                    $"[InputManager] VirtualController found and activated. Active: {vc.gameObject.activeInHierarchy}"
                );
            }
            else
            {
                Debug.LogWarning("[InputManager] VirtualController not found in scene!");
            }
        }

        internal void Update()
        {
            if (mobileCheckFrames > 0)
            {
                mobileCheckFrames--;

                bool shouldActivate = false;

#if UNITY_WEBGL && !UNITY_EDITOR
                // For WebGL, check via JavaScript
                try
                {
                    shouldActivate =
                        IsMobileBrowser() == 1 || IsiOSMobile() == 1 || IsAndroidMobile() == 1;
                }
                catch
                {
                    // JavaScript not ready yet, try again next frame
                }
#elif UNITY_IOS || UNITY_ANDROID
                shouldActivate = true;
#endif
                RetryActivation(shouldActivate);
            }
        }

        /// <summary>
        /// Catches a controller that only became findable after Start ran: mobile
        /// browsers can report their platform a few frames late.
        /// </summary>
        internal void RetryActivation(bool shouldActivate)
        {
            if (!shouldActivate)
                return;

            var vc = FindAnyObjectByType<VirtualController>(FindObjectsInactive.Include);
            if (vc != null && !vc.gameObject.activeInHierarchy)
            {
                vc.gameObject.SetActive(true);
                Debug.Log(
                    $"[InputManager] VirtualController re-activated in Update frame {10 - mobileCheckFrames}"
                );
            }
        }
    }
}
