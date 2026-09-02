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
    /// touches is global - quality settings and the render pipeline asset - so every
    /// check reaches them through the optimizer's seams, and this fixture verifies
    /// the project itself came through untouched.
    /// </summary>
    public abstract class IOSSafariWebGLOptimizerTestBase
    {
        private readonly List<GameObject> spawned = new();

        /// <summary>A pipeline asset owned by the test rather than the project.</summary>
        protected UniversalRenderPipelineAsset ScratchPipeline;

        /// <summary>What the optimizer last asked the project to apply.</summary>
        internal iOSSafariWebGLOptimizer.QualitySnapshot? WrittenQuality;

        private iOSSafariWebGLOptimizer.QualitySnapshot savedQuality;
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
            savedQuality = iOSSafariWebGLOptimizer.QualitySnapshot.Current;
            WrittenQuality = null;
            iOSSafariWebGLOptimizer.WriteQuality = snapshot => WrittenQuality = snapshot;

            UniversalRenderPipelineAsset live = LivePipeline;
            if (live != null)
            {
                savedLivePipelineMsaa = live.msaaSampleCount;
                savedLivePipelineRenderScale = live.renderScale;
            }
        }

        /// <summary>
        /// Checks that the fixture left the project exactly as it found it. Restoring
        /// afterwards would not be enough: an Editor flushes whatever these settings
        /// hold when it quits, and a run interrupted partway through never reaches
        /// its restore, so the project would keep the reductions meant for iOS.
        /// </summary>
        [TearDown]
        public void RestoreGlobalSettings()
        {
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

            Assert.That(
                iOSSafariWebGLOptimizer.QualitySnapshot.Current.ToString(),
                Is.EqualTo(savedQuality.ToString()),
                "the project's own quality settings are not this fixture's to change"
            );

            UniversalRenderPipelineAsset live = LivePipeline;
            if (live != null)
            {
                Assert.That(
                    live.msaaSampleCount,
                    Is.EqualTo(savedLivePipelineMsaa),
                    "nor is the pipeline asset the project renders with"
                );
                Assert.That(live.renderScale, Is.EqualTo(savedLivePipelineRenderScale));
            }
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
