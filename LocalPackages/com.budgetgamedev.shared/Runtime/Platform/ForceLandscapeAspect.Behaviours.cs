using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared
{
    public static partial class ForceLandscapeAspect
    {
        /// <summary>
        /// Helper MonoBehaviour that runs the update loop and clears letterbox areas
        /// </summary>
        internal class AspectRatioUpdater : MonoBehaviour
        {
            private Camera _clearCamera;
            private bool _initialFocusChecked = false;

            internal void Start()
            {
                // Create a camera specifically for clearing the letterbox/pillarbox areas to black
                var clearCamObj = new GameObject("[LetterboxClearCamera]");
                clearCamObj.transform.SetParent(transform);
                _clearCamera = clearCamObj.AddComponent<Camera>();
                _clearCamera.depth = -100; // Render first (behind everything)
                _clearCamera.clearFlags = CameraClearFlags.SolidColor;
                _clearCamera.backgroundColor = Color.black;
                _clearCamera.cullingMask = 0; // Don't render any layers
                _clearCamera.rect = new Rect(0, 0, 1, 1); // Full screen to clear letterbox areas

                // Initial update
                UpdateAllCameras();
            }

            internal void Update() => Tick(Application.isFocused);

            /// <summary>
            /// The per-frame work with focus supplied by the caller: an editor test
            /// cannot take focus away from the editor itself.
            /// </summary>
            internal void Tick(bool isFocused)
            {
                CheckForScreenChange();

                // Check initial focus state after a short delay (let Unity settle)
                if (!_initialFocusChecked)
                {
                    _initialFocusChecked = true;
                    // Check if we started without focus
                    if (!isFocused)
                    {
                        if (DEBUG_MODE)
                            Debug.Log(
                                "[ForceLandscapeAspect] Game started without focus - pausing"
                            );
                        OnFocusLost();
                    }
                }
            }

            internal void OnApplicationFocus(bool hasFocus)
            {
                if (!hasFocus)
                {
                    OnFocusLost();
                }
                else
                {
                    OnFocusRegained();
                }
            }

            internal void OnApplicationPause(bool pauseStatus)
            {
                // Also handle app pause (mobile backgrounding)
                if (pauseStatus)
                {
                    OnFocusLost();
                }
                else
                {
                    OnFocusRegained();
                }
            }

            // Called from JavaScript via SendMessage for WebGL
            public void OnVisibilityLost()
            {
                OnFocusLost();
            }

            // Called from JavaScript via SendMessage for WebGL
            public void OnVisibilityRegained()
            {
                OnFocusRegained();
            }

            internal void OnDestroy()
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        /// <summary>
        /// Simple animator for the rotate icon.
        /// Designed to be resilient to enable/disable cycles that can happen on iOS Safari offline.
        /// </summary>
        internal class RotateAnimator : MonoBehaviour
        {
            // Static state survives enable/disable cycles
            internal static float _persistedAngle = 0f;
            internal static float _persistedTargetAngle = -90f;
            internal static float _persistedPauseTimer = 0f;
            private static bool _hasInitialized = false;

            private const float ANIM_SPEED = 2f;

            internal static void ResetStatics()
            {
                _persistedAngle = 0f;
                _persistedTargetAngle = -90f;
                _persistedPauseTimer = 0f;
                _hasInitialized = false;
            }

            internal void OnEnable()
            {
                // Restore persisted state (survives rapid enable/disable)
                if (_hasInitialized)
                {
                    transform.localRotation = Quaternion.Euler(0, 0, _persistedAngle);
                }
            }

            internal void Update() => Step(Time.unscaledDeltaTime);

            /// <summary>
            /// One animation step against a caller-supplied delta, so the sweep and
            /// its pauses can be stepped exactly rather than at editor frame rate.
            /// </summary>
            internal void Step(float unscaledDeltaTime)
            {
                _hasInitialized = true;

                // Use unscaled time since game is paused
                if (_persistedPauseTimer > 0f)
                {
                    _persistedPauseTimer -= unscaledDeltaTime;
                    return;
                }

                _persistedAngle = Mathf.MoveTowards(
                    _persistedAngle,
                    _persistedTargetAngle,
                    unscaledDeltaTime * 90f * ANIM_SPEED
                );
                transform.localRotation = Quaternion.Euler(0, 0, _persistedAngle);

                if (Mathf.Approximately(_persistedAngle, _persistedTargetAngle))
                {
                    // Swap between portrait (0) and landscape (-90)
                    if (_persistedTargetAngle == -90f)
                    {
                        _persistedPauseTimer = 1f;
                        _persistedTargetAngle = 0f;
                    }
                    else
                    {
                        _persistedPauseTimer = 0.5f;
                        _persistedTargetAngle = -90f;
                    }
                }
            }
        }
    }
}
