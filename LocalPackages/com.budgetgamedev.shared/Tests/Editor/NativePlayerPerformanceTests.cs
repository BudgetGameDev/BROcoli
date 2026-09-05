using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class NativePlayerPerformanceTests
    {
        [TestCase(RuntimePlatform.WindowsPlayer, true)]
        [TestCase(RuntimePlatform.OSXPlayer, true)]
        [TestCase(RuntimePlatform.LinuxPlayer, true)]
        [TestCase(RuntimePlatform.WindowsEditor, false)]
        [TestCase(RuntimePlatform.WebGLPlayer, false)]
        [TestCase(RuntimePlatform.IPhonePlayer, false)]
        [TestCase(RuntimePlatform.Android, false)]
        public void OnlyNativePlayersReceiveDesktopTuning(
            RuntimePlatform platform,
            bool expected
        ) => Assert.That(NativePlayerPerformance.IsDesktopPlayer(platform), Is.EqualTo(expected));

        [Test]
        public void RestoresLowLatencySettingsWithoutChangingQualityOrPipeline()
        {
            int vsync = QualitySettings.vSyncCount;
            int queue = QualitySettings.maxQueuedFrames;
            int target = Application.targetFrameRate;
            int interval = OnDemandRendering.renderFrameInterval;
            float fixedStep = Time.fixedDeltaTime;
            float maximumStep = Time.maximumDeltaTime;
            float polling = InputSystem.pollingFrequency;
            var inputMode = InputSystem.settings.updateMode;
            int quality = QualitySettings.GetQualityLevel();
            var pipeline = QualitySettings.renderPipeline;
            try
            {
                QualitySettings.vSyncCount = 1;
                QualitySettings.maxQueuedFrames = 2;
                Application.targetFrameRate = 60;
                OnDemandRendering.renderFrameInterval = 2;
                Time.fixedDeltaTime = 0.02f;
                NativePlayerPerformance.Apply();
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(QualitySettings.maxQueuedFrames, Is.EqualTo(1));
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                Assert.That(OnDemandRendering.renderFrameInterval, Is.EqualTo(1));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 120f).Within(0.000001f));
                Assert.That(Time.maximumDeltaTime, Is.EqualTo(1f / 30f).Within(0.000001f));
                Assert.That(
                    InputSystem.settings.updateMode,
                    Is.EqualTo(InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                );
                Assert.That(InputSystem.pollingFrequency, Is.EqualTo(240f));
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(quality));
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(pipeline));

                // Frame pacing can be re-applied after changing quality without resetting
                // the autoplay harness's simulation clock.
                Time.fixedDeltaTime = 0.01f;
                QualitySettings.vSyncCount = 1;
                NativePlayerPerformance.ApplyFramePacing();
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.01f).Within(0.000001f));
            }
            finally
            {
                QualitySettings.vSyncCount = vsync;
                QualitySettings.maxQueuedFrames = queue;
                Application.targetFrameRate = target;
                OnDemandRendering.renderFrameInterval = interval;
                Time.fixedDeltaTime = fixedStep;
                Time.maximumDeltaTime = maximumStep;
                InputSystem.settings.updateMode = inputMode;
                InputSystem.pollingFrequency = polling;
            }
        }
    }
}
