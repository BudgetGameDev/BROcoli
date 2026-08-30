using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Applies lighting-safe performance optimizations for iOS WebGL builds only.
    /// Does NOT affect native iOS builds or other platforms.
    ///
    /// Settings applied:
    /// - Preserve the normal WebGL quality level and scene-light budget
    /// - Capped 2x Retina rendering (paired with the WebGL template's iOS DPR policy)
    /// - MSAA disabled
    /// - Non-lighting quality reductions
    ///
    /// Note: this does not change the frame rate; it only relaxes iOS Safari's cost.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // Run very early
    public class iOSSafariWebGLOptimizer : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int IsiOSMobile();

        [DllImport("__Internal")]
        private static extern void ReportIOSLightingSettings(
            int qualityLevel,
            int pixelLightCount,
            int additionalLightCount,
            int shadowsEnabled
        );
#endif

        private static bool _optimizationsApplied = false;

        /// <summary>
        /// Installs itself before the first scene loads.
        /// </summary>
        /// <remarks>
        /// This is platform policy, not game content: it has to hold from the
        /// very first frame no matter which scene boots. The hub launcher boots
        /// first and is deliberately empty of game objects, so relying on a
        /// component placed in a game's scene would leave the launcher, and any
        /// game that forgot to add it, running unoptimised on iOS Safari.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_optimizationsApplied)
                return;

            var host = new GameObject(nameof(iOSSafariWebGLOptimizer));
            DontDestroyOnLoad(host);
            host.AddComponent<iOSSafariWebGLOptimizer>();
        }

        private void Awake()
        {
            if (_optimizationsApplied)
            {
                Destroy(gameObject);
                return;
            }

            ApplyOptimizationsIfNeeded();
        }

        private void ApplyOptimizationsIfNeeded()
        {
            bool isiOSWebGL = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                bool isiOS = IsiOSMobile() == 1;
                isiOSWebGL = isiOS;

                Debug.Log($"[iOSSafariOptimizer] Detection - iOS/iPadOS device: {isiOS}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[iOSSafariOptimizer] JS detection failed: {e.Message}");
                return;
            }
#endif

            if (!isiOSWebGL)
            {
                Debug.Log(
                    "[iOSSafariOptimizer] Not an iOS/iPadOS WebGL device - skipping optimizations"
                );
                return;
            }

            Debug.Log("[iOSSafariOptimizer] iOS Safari WebGL detected - applying optimizations");
            _optimizationsApplied = true;

            ApplyLightingSafeQualitySettings();
            ApplyLightingSafeURPSettings();
            ReportLightingSettings();
            Debug.Log("[iOSSafariOptimizer] All optimizations applied");
        }

        private void ReportLightingSettings()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            ReportIOSLightingSettings(
                QualitySettings.GetQualityLevel(),
                QualitySettings.pixelLightCount,
                urpAsset == null ? 0 : urpAsset.maxAdditionalLightsCount,
                QualitySettings.shadows == UnityEngine.ShadowQuality.Disable ? 0 : 1
            );
#endif
        }

        private void ApplyLightingSafeQualitySettings()
        {
            // Do not switch quality levels here. The WebGL default preserves the pixel-light
            // budget used by the world and player-proximity lights. The Very Low profile has
            // zero pixel lights, which leaves most of the Dungeon scene black on iOS.
            Debug.Log(
                $"[iOSSafariOptimizer] Preserving quality level {QualitySettings.GetQualityLevel()}"
            );

            // Note: VSync and target frame rate are left at their project defaults.

            // Disable MSAA via quality settings
            QualitySettings.antiAliasing = 0;
            Debug.Log("[iOSSafariOptimizer] MSAA disabled (QualitySettings)");

            // Reduce effects that do not alter the scene's light selection or shadow budget.
            QualitySettings.softParticles = false;
            QualitySettings.softVegetation = false;
            QualitySettings.billboardsFaceCameraPosition = false;
            QualitySettings.lodBias = 0.5f;
            QualitySettings.maximumLODLevel = 2;
            QualitySettings.particleRaycastBudget = 16;
            Debug.Log("[iOSSafariOptimizer] Additional quality reductions applied");
        }

        private void ApplyLightingSafeURPSettings()
        {
            // Try to modify URP asset settings at runtime
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                Debug.LogWarning("[iOSSafariOptimizer] URP asset not found");
                return;
            }

            // Disable MSAA in URP
            urpAsset.msaaSampleCount = 1;
            Debug.Log("[iOSSafariOptimizer] URP MSAA disabled");

            // Preserve every pixel supplied by the template's capped Retina buffer.
            urpAsset.renderScale = 1.0f;
            Debug.Log("[iOSSafariOptimizer] URP render scale set to 1.0 (full buffer resolution)");

            // Disable HDR
            // Note: HDR property might not be directly settable at runtime
            // urpAsset.supportsHDR = false;

            // Do not change shadowDistance or maxAdditionalLightsCount. Dungeon uses two
            // point lights (world + player proximity), and both are additional URP lights.
            Debug.Log(
                $"[iOSSafariOptimizer] Preserving {urpAsset.maxAdditionalLightsCount} additional lights "
                    + $"and {urpAsset.shadowDistance} shadow distance"
            );
        }
    }
}
