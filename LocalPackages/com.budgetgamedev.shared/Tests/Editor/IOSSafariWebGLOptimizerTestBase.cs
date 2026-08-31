using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Shared fixture for the iOS Safari relief valve. Everything the optimizer
    /// touches is global - quality settings and the render pipeline asset - so the
    /// fixture records the previous values and puts every one of them back.
    /// </summary>
    public abstract class IOSSafariWebGLOptimizerTestBase
    {
        private readonly List<GameObject> spawned = new();

        /// <summary>A pipeline asset owned by the test rather than the project.</summary>
        protected UniversalRenderPipelineAsset ScratchPipeline;

        private int savedAntiAliasing;
        private bool savedSoftParticles;
        private bool savedSoftVegetation;
        private bool savedBillboards;
        private float savedLodBias;
        private int savedMaximumLodLevel;
        private int savedParticleRaycastBudget;
        private int savedLivePipelineMsaa;
        private float savedLivePipelineRenderScale;

        protected static UniversalRenderPipelineAsset LivePipeline =>
            GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        [SetUp]
        public void SaveGlobalSettings()
        {
            foreach (
                iOSSafariWebGLOptimizer existing in Object.FindObjectsByType<iOSSafariWebGLOptimizer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
                Object.DestroyImmediate(existing.gameObject);
            iOSSafariWebGLOptimizer.ResetStatics();
            savedAntiAliasing = QualitySettings.antiAliasing;
            savedSoftParticles = QualitySettings.softParticles;
            savedSoftVegetation = QualitySettings.softVegetation;
            savedBillboards = QualitySettings.billboardsFaceCameraPosition;
            savedLodBias = QualitySettings.lodBias;
            savedMaximumLodLevel = QualitySettings.maximumLODLevel;
            savedParticleRaycastBudget = QualitySettings.particleRaycastBudget;

            UniversalRenderPipelineAsset live = LivePipeline;
            if (live != null)
            {
                savedLivePipelineMsaa = live.msaaSampleCount;
                savedLivePipelineRenderScale = live.renderScale;
            }
        }

        [TearDown]
        public void RestoreGlobalSettings()
        {
            QualitySettings.antiAliasing = savedAntiAliasing;
            QualitySettings.softParticles = savedSoftParticles;
            QualitySettings.softVegetation = savedSoftVegetation;
            QualitySettings.billboardsFaceCameraPosition = savedBillboards;
            QualitySettings.lodBias = savedLodBias;
            QualitySettings.maximumLODLevel = savedMaximumLodLevel;
            QualitySettings.particleRaycastBudget = savedParticleRaycastBudget;

            UniversalRenderPipelineAsset live = LivePipeline;
            if (live != null)
            {
                live.msaaSampleCount = savedLivePipelineMsaa;
                live.renderScale = savedLivePipelineRenderScale;
            }

            if (ScratchPipeline != null)
            {
                Object.DestroyImmediate(ScratchPipeline);
                ScratchPipeline = null;
            }

            foreach (GameObject created in spawned)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            spawned.Clear();
            iOSSafariWebGLOptimizer.ResetStatics();
        }

        protected iOSSafariWebGLOptimizer NewOptimizer()
        {
            var host = new GameObject(nameof(iOSSafariWebGLOptimizer));
            spawned.Add(host);
            return host.AddComponent<iOSSafariWebGLOptimizer>();
        }

        /// <summary>Registers an object the code under test created itself.</summary>
        protected void Track(GameObject created)
        {
            spawned.Add(created);
        }

        protected static void ExpectLog(string fragment)
        {
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(fragment)));
        }

        protected static void ExpectQualityReport()
        {
            ExpectLog("Preserving quality level");
            ExpectLog("MSAA disabled (QualitySettings)");
            ExpectLog("Additional quality reductions applied");
        }

        /// <summary>
        /// Whether the pipeline is reported as relaxed or as missing depends on how
        /// the project is configured, and both are correct behaviour.
        /// </summary>
        protected static void ExpectPipelineReport(UniversalRenderPipelineAsset urpAsset)
        {
            if (urpAsset == null)
            {
                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("URP asset not found")));
                return;
            }

            ExpectLog("URP MSAA disabled");
            ExpectLog("URP render scale set to 1.0");
            ExpectLog("additional lights");
        }
    }
}
