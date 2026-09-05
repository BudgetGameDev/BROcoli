using System;
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
    public partial class iOSSafariWebGLOptimizer : MonoBehaviour
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

        internal static bool _optimizationsApplied = false;

        /// <summary>
        /// How the host is kept alive across scene loads, and how a duplicate host
        /// removes itself. Both engine calls are play-mode only - they throw or
        /// complain outside it - so each is reached through a field an editor
        /// context can substitute.
        /// </summary>
        internal static Action<GameObject> KeepAcrossScenes = DontDestroyOnLoad;

        /// <inheritdoc cref="KeepAcrossScenes"/>
        internal static Action<GameObject> RemoveSelf = Destroy;

        /// <summary>
        /// How the quality reductions reach the project, and how the pipeline asset
        /// the project renders with is found. Both cross into state an open Editor
        /// owns and flushes to disk when it quits: a check that turned the project's
        /// MSAA off for real leaves it off when the run is interrupted before it can
        /// put it back. Each is reached through a field an editor context can
        /// substitute, so the policy is checked without the project being written.
        /// </summary>
        internal static Action<QualitySnapshot> WriteQuality = ApplyQuality;

        /// <inheritdoc cref="WriteQuality"/>
        internal static Func<UniversalRenderPipelineAsset> ResolveLivePipeline = LivePipeline;

        private static void ApplyQuality(QualitySnapshot snapshot) => snapshot.ApplyToProject();

        private static UniversalRenderPipelineAsset LivePipeline() =>
            GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>
        /// Clears the once-only latch. Statics survive a play session when domain
        /// reloading is off, so without this the second run would skip the work.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            _optimizationsApplied = false;
            KeepAcrossScenes = DontDestroyOnLoad;
            RemoveSelf = Destroy;
            WriteQuality = ApplyQuality;
            ResolveLivePipeline = LivePipeline;
        }

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
        internal static void Install()
        {
            if (_optimizationsApplied)
                return;

            var host = new GameObject(nameof(iOSSafariWebGLOptimizer));
            KeepAcrossScenes(host);
            host.AddComponent<iOSSafariWebGLOptimizer>();
        }

        internal void Awake()
        {
            if (_optimizationsApplied)
            {
                RemoveSelf(gameObject);
                return;
            }

            ApplyOptimizationsIfNeeded();
        }

        internal void ApplyOptimizationsIfNeeded()
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

            ApplyOptimizationsIfNeeded(isiOSWebGL);
        }

        internal void ApplyOptimizationsIfNeeded(bool isiOSWebGL)
        {
            if (!isiOSWebGL)
            {
                Debug.Log(
                    "[iOSSafariOptimizer] Not an iOS/iPadOS WebGL device - skipping optimizations"
                );
                return;
            }

            ApplyOptimizations();
        }

        internal void ApplyOptimizations() => ApplyOptimizations(ResolveLivePipeline());

        /// <summary>
        /// The optimizations themselves, split from the detection so they can be
        /// applied - and checked - on a platform that is not iOS Safari, and against
        /// a pipeline asset that is not the one the project is rendering with.
        /// </summary>
        internal void ApplyOptimizations(UniversalRenderPipelineAsset urpAsset)
        {
            Debug.Log("[iOSSafariOptimizer] iOS Safari WebGL detected - applying optimizations");
            _optimizationsApplied = true;

            ApplyLightingSafeQualitySettings();
            ApplyLightingSafeURPSettings(urpAsset);
            ReportLightingSettings();
            Debug.Log("[iOSSafariOptimizer] All optimizations applied");
        }

        internal void ReportLightingSettings()
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

        internal static void ApplyLightingSafeQualitySettings()
        {
            // Do not switch quality levels here. The WebGL default preserves the pixel-light
            // budget used by the world and player-proximity lights. The Very Low profile has
            // zero pixel lights, which leaves most of the Dungeon scene black on iOS.
            Debug.Log(
                $"[iOSSafariOptimizer] Preserving quality level {QualitySettings.GetQualityLevel()}"
            );

            // Note: VSync and target frame rate are left at their project defaults.
            // MSAA goes, and so do the effects that do not alter the scene's light
            // selection or shadow budget.
            WriteQuality(QualitySnapshot.LightingSafe);
            Debug.Log("[iOSSafariOptimizer] MSAA disabled (QualitySettings)");
            Debug.Log("[iOSSafariOptimizer] Additional quality reductions applied");
        }

        /// <summary>
        /// Relaxes the pipeline's per-pixel cost. The caller supplies the asset so
        /// both the "no URP asset" and the "URP asset present" paths can be
        /// exercised without swapping the project's active render pipeline.
        /// </summary>
        internal static void ApplyLightingSafeURPSettings(UniversalRenderPipelineAsset urpAsset)
        {
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
