using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Installing the optimizer and deciding whether it has any work to do. It is
    /// platform policy that has to hold from the first frame, so it installs itself
    /// before any scene loads and runs exactly once per session.
    /// </summary>
    public sealed class IOSSafariWebGLOptimizerTests : IOSSafariWebGLOptimizerTestBase
    {
        [Test]
        public void OffIOSNothingIsChangedAndTheSkipIsRecorded()
        {
            iOSSafariWebGLOptimizer optimizer = NewOptimizer();
            QualitySettings.antiAliasing = 4;
            ExpectLog("Not an iOS/iPadOS WebGL device - skipping optimizations");

            optimizer.Awake();

            Assert.That(iOSSafariWebGLOptimizer._optimizationsApplied, Is.False);
            Assert.That(QualitySettings.antiAliasing, Is.EqualTo(4), "a desktop keeps its MSAA");
        }

        [Test]
        public void ASecondOptimizerRemovesItselfWithoutRedoingTheWork()
        {
            GameObject removed = null;
            iOSSafariWebGLOptimizer.RemoveSelf = target => removed = target;
            iOSSafariWebGLOptimizer._optimizationsApplied = true;
            iOSSafariWebGLOptimizer optimizer = NewOptimizer();
            QualitySettings.antiAliasing = 4;

            optimizer.Awake();

            Assert.That(removed, Is.SameAs(optimizer.gameObject), "the duplicate takes itself out");
            Assert.That(
                QualitySettings.antiAliasing,
                Is.EqualTo(4),
                "and bows out before touching anything"
            );
        }

        [Test]
        public void TheOptimizerInstallsItselfExactlyOnceAndOutlivesSceneLoads()
        {
            // DontDestroyOnLoad throws outside play mode, so the request is recorded.
            GameObject kept = null;
            iOSSafariWebGLOptimizer.KeepAcrossScenes = target => kept = target;

            iOSSafariWebGLOptimizer.Install();
            iOSSafariWebGLOptimizer[] hosts = FindOptimizers();

            Assert.That(hosts.Length, Is.EqualTo(1));
            Track(hosts[0].gameObject);
            Assert.That(
                kept,
                Is.SameAs(hosts[0].gameObject),
                "the launcher boots first, so the policy has to survive its scene load"
            );

            iOSSafariWebGLOptimizer._optimizationsApplied = true;
            iOSSafariWebGLOptimizer.Install();

            Assert.That(
                FindOptimizers().Length,
                Is.EqualTo(1),
                "once the work is done there is nothing left to install"
            );
        }

        [Test]
        public void ApplyingTheOptimizationsLatchesSoItOnlyEverHappensOnce()
        {
            iOSSafariWebGLOptimizer optimizer = NewOptimizer();
            ScratchPipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            ScratchPipeline.msaaSampleCount = 4;
            QualitySettings.antiAliasing = 4;

            ExpectLog("applying optimizations");
            ExpectQualityReport();
            ExpectPipelineReport(ScratchPipeline);
            ExpectLog("All optimizations applied");

            optimizer.ApplyOptimizations(ScratchPipeline);

            Assert.That(iOSSafariWebGLOptimizer._optimizationsApplied, Is.True);
            Assert.That(QualitySettings.antiAliasing, Is.EqualTo(0));
            Assert.That(ScratchPipeline.msaaSampleCount, Is.EqualTo(1));
        }

        [Test]
        public void TheDefaultRouteReachesForTheProjectsOwnPipeline()
        {
            iOSSafariWebGLOptimizer optimizer = NewOptimizer();
            QualitySettings.antiAliasing = 4;

            ExpectLog("applying optimizations");
            ExpectQualityReport();
            ExpectPipelineReport(LivePipeline);
            ExpectLog("All optimizations applied");

            optimizer.ApplyOptimizations();

            Assert.That(QualitySettings.antiAliasing, Is.EqualTo(0));
            Assert.That(iOSSafariWebGLOptimizer._optimizationsApplied, Is.True);
        }

        private static iOSSafariWebGLOptimizer[] FindOptimizers()
        {
            return Object.FindObjectsByType<iOSSafariWebGLOptimizer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        }
    }
}
