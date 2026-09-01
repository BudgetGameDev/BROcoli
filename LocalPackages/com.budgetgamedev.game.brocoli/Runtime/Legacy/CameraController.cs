using System.Runtime.InteropServices;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Diablo 3: Reaper of Souls style camera.
    /// - Fixed 45-degree yaw over the grid-aligned world, so rooms, bridges,
    ///   and the platform's cliff edges all read diagonally on screen
    /// - Smooth follow without centering/reset
    /// - Subtle drift in movement direction that stays
    /// - Responsive zoom for portrait/landscape
    /// - Extra zoom on mobile devices
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        /// <summary>
        /// How far the view is yawed against the world grid. The dungeon's
        /// layout maths stays axis-aligned; only the camera (and the input
        /// mapping, see <see cref="PlayerInputHandler"/>) turns, which is
        /// exactly how Diablo gets its diagonal look out of square tiles.
        /// Screen "up" is world north-east.
        /// </summary>
        public const float WorldYawDegrees = 45f;

        /// <summary>
        /// The ground direction this camera reads as straight up the screen.
        /// Presentation cheats that used to displace a sprite up the screen (the
        /// hop bounce) must aim along this, not along ground north: since the rig
        /// is yawed, ground north now reads as up-and-left.
        /// </summary>
        public static Vector2 ScreenUpGround => Vector2.up.RotatedByYaw(WorldYawDegrees);

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int IsMobileBrowser();
#endif

        [Header("Target")]
        public Transform target;

        [Header("Follow Smoothing")]
        [Tooltip("How quickly camera follows target")]
        public float followSpeed = 5f;

        [Header("Look Ahead (Diablo style - no reset)")]
        [Tooltip("How much camera drifts in movement direction")]
        public float lookAheadAmount = 1f;

        [Tooltip("How quickly drift builds up")]
        public float driftSpeed = 1f;

        [Header("Responsive Zoom")]
        [Tooltip("Base field of view for landscape mode")]
        public float landscapeFOV = 35f;

        [Tooltip("Field of view for portrait mode (higher = more zoomed out)")]
        public float portraitFOV = 60f;

        [Tooltip("How quickly camera zooms between sizes")]
        public float zoomSpeed = 5f;

        [Header("Mobile Zoom")]
        [Tooltip("Extra zoom percentage on mobile (25 = 25% more zoom)")]
        public float mobileZoomPercent = 25f;

        [Tooltip("Force mobile zoom in editor for testing")]
        public bool forceMobileZoomInEditor = false;

        private Vector3 offset;
        private Vector3 currentDrift;
        private bool initialized;
        private Camera cam;
        private float targetFOV;
        private bool isMobile;

        void Start()
        {
            cam = GetComponent<Camera>();

            // The scene authors the rig looking due north; the diagonal view is
            // applied here so the one yaw constant also governs input mapping.
            transform.rotation = Quaternion.Euler(0f, WorldYawDegrees, 0f) * transform.rotation;

            // Check if we're on mobile
            isMobile = CheckIsMobile();

            // Store current FOV as landscape FOV if not set
            if (cam != null && landscapeFOV <= 0)
            {
                landscapeFOV = cam.fieldOfView;
            }

            if (target != null)
            {
                CaptureYawedOffset();
                initialized = true;
            }

            // Initialize zoom immediately
            UpdateTargetZoom();
            if (cam != null)
            {
                cam.fieldOfView = targetFOV;
            }
        }

        /// <summary>
        /// Preserves the follow distance authored in the scene, swung around
        /// the target by the world yaw so the camera sits south-west of the
        /// player looking north-east.
        /// </summary>
        private void CaptureYawedOffset()
        {
            offset =
                Quaternion.Euler(0f, WorldYawDegrees, 0f) * (transform.position - target.position);
            transform.position = target.position + offset;
        }

        private bool CheckIsMobile()
        {
#if UNITY_EDITOR
            return IsMobileEnvironment(
                UnityEngine.Device.SystemInfo.deviceType,
                forceMobileZoomInEditor,
                SystemInfo.deviceType
            );
#elif UNITY_WEBGL
            bool result = IsMobileBrowser() == 1;
            Debug.Log($"[CameraController] IsMobileBrowser returned: {result}");
            return result;
#elif UNITY_IOS || UNITY_ANDROID
            return true;
#else
            return false;
#endif
        }

        internal static bool IsMobileEnvironment(
            DeviceType simulatedDevice,
            bool forceInEditor,
            DeviceType fallbackDevice
        ) =>
            simulatedDevice == DeviceType.Handheld
            || forceInEditor
            || fallbackDevice == DeviceType.Handheld;

        void LateUpdate()
        {
            if (target == null)
                return;

            if (!initialized)
            {
                CaptureYawedOffset();
                initialized = true;
            }

            // Update responsive zoom for portrait/landscape
            UpdateTargetZoom();
            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(
                    cam.fieldOfView,
                    targetFOV,
                    zoomSpeed * Time.deltaTime
                );
            }

            // Diablo-style: very subtle drift that doesn't reset
            Vector3 targetDrift = Vector3.zero;
            if (target.TryGetComponent<PlayerController>(out var pc))
            {
                Vector2 move = pc.RawInput;
                if (move.sqrMagnitude > 0.1f)
                {
                    // Only drift while moving, but don't reset when stopping
                    targetDrift = move.ToWorld() * lookAheadAmount;
                }
                else
                {
                    // Keep current drift when stopped (Diablo style)
                    targetDrift = currentDrift;
                }
            }

            // Slowly drift towards target (or stay if not moving)
            currentDrift = Vector3.Lerp(currentDrift, targetDrift, driftSpeed * Time.deltaTime);

            // Smoothly follow target + offset + drift
            Vector3 desiredPosition = target.position + offset + currentDrift;
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                followSpeed * Time.deltaTime
            );
        }

        void UpdateTargetZoom()
        {
            bool isPortrait = Screen.height > Screen.width;
            targetFOV = CalculateTargetFov(
                isPortrait,
                isMobile,
                landscapeFOV,
                portraitFOV,
                mobileZoomPercent
            );
        }

        internal static float CalculateTargetFov(
            bool isPortrait,
            bool mobile,
            float landscape,
            float portrait,
            float mobileZoom
        )
        {
            float fieldOfView = isPortrait ? portrait : landscape;
            return mobile && mobileZoom > 0f ? fieldOfView * (1f - mobileZoom / 100f) : fieldOfView;
        }
    }
}
