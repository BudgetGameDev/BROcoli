using System;
using System.Runtime.InteropServices;
using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class StreamlineTests
    {
        [Test]
        public void SuperResolutionUsesStreamlineWithoutMutatingThePriorityList()
        {
            var original = GlobalDynamicResolutionSettings.NewDefault();
            var previous = original.advancedUpscalerNames.ToArray();
            var configured = StreamlineSettings.ConfigureSuperResolution(original);
            Assert.That(configured.enabled, Is.True);
            Assert.That(configured.advancedUpscalerNames[0], Is.EqualTo(StreamlineUpscaler.Name));
            Assert.That(original.advancedUpscalerNames, Is.EqualTo(previous));
            Assert.That(
                StreamlineSettings.ConfigureSuperResolution(configured).advancedUpscalerNames,
                Is.EqualTo(configured.advancedUpscalerNames)
            );
        }

        [Test]
        public void NativePacketsMatchTheWindowsBridgeAbi()
        {
            Assert.That(Marshal.SizeOf<StreamlineNative.FrameData>(), Is.EqualTo(400));
            Assert.That(
                Marshal.OffsetOf<StreamlineNative.FrameData>("viewToClip").ToInt32(),
                Is.EqualTo(40)
            );
            Assert.That(
                Marshal.OffsetOf<StreamlineNative.FrameData>("width").ToInt32(),
                Is.EqualTo(376)
            );
            Assert.That(Marshal.SizeOf<StreamlineNative.Status>(), Is.EqualTo(48));
            Assert.That(Marshal.SizeOf<StreamlineNative.SuperResolutionData>(), Is.EqualTo(432));
            Assert.That(Marshal.SizeOf<StreamlineNative.SuperResolutionStatus>(), Is.EqualTo(64));
        }

        [Test]
        public void EditorDoesNotAttemptToLoadTheWindowsPlugin()
        {
            Assert.That(StreamlineNative.TryGetStatus(out var status), Is.False);
            Assert.That(status.initialized, Is.Zero);
        }

        [Test]
        public void OnlyASingleFullScreenPerspectiveOutputCanFeedFrameGeneration()
        {
            var host = new GameObject("Streamline Camera Test");
            var texture = new RenderTexture(64, 64, 0);
            try
            {
                var camera = host.AddComponent<Camera>();
                Assert.That(StreamlineRuntime.IsEligibleCamera(camera), Is.True);
                camera.targetTexture = texture;
                Assert.That(StreamlineRuntime.IsEligibleCamera(camera), Is.False);
                camera.targetTexture = null;
                camera.orthographic = true;
                Assert.That(StreamlineRuntime.IsEligibleCamera(camera), Is.False);
                camera.orthographic = false;
                camera.rect = new Rect(0, 0, 0.5f, 1);
                Assert.That(StreamlineRuntime.IsEligibleCamera(camera), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AnAdditionalOutputCameraDisablesFrameGenerationEvenWhenOrthographic()
        {
            var scene = new GameObject("Streamline Scene Camera");
            var overlay = new GameObject("Streamline Overlay Camera");
            try
            {
                var camera = scene.AddComponent<Camera>();
                var other = overlay.AddComponent<Camera>();
                other.orthographic = true;
                Assert.That(
                    StreamlineRuntime.SelectViewCamera(new[] { camera }),
                    Is.SameAs(camera)
                );
                Assert.That(StreamlineRuntime.SelectViewCamera(new[] { camera, other }), Is.Null);
                Assert.That(StreamlineRuntime.SelectViewCamera(new[] { other }), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scene);
                UnityEngine.Object.DestroyImmediate(overlay);
            }
        }

        [Test]
        public void FrameGenerationKeepsReflexOnAndPreferencesClampInvalidValues()
        {
            const string dlssKey = "Rendering.Streamline.Dlss";
            bool hadDlss = PlayerPrefs.HasKey(dlssKey);
            int dlss = PlayerPrefs.GetInt(dlssKey);
            const string framesKey = "Rendering.Streamline.GeneratedFrames";
            const string reflexKey = "Rendering.Streamline.Reflex";
            bool hadFrames = PlayerPrefs.HasKey(framesKey),
                hadReflex = PlayerPrefs.HasKey(reflexKey);
            int frames = PlayerPrefs.GetInt(framesKey),
                reflex = PlayerPrefs.GetInt(reflexKey);
            try
            {
                StreamlineSettings.ResetDefaults();
                Assert.That(StreamlineSettings.GeneratedFrames, Is.EqualTo(3));
                Assert.That(
                    StreamlineSettings.EffectiveReflex,
                    Is.EqualTo(StreamlineSettings.ReflexMode.OnWithBoost)
                );
                StreamlineSettings.Reflex = StreamlineSettings.ReflexMode.Off;
                Assert.That(
                    StreamlineSettings.EffectiveReflex,
                    Is.EqualTo(StreamlineSettings.ReflexMode.On)
                );
                StreamlineSettings.GeneratedFrames = 0;
                Assert.That(
                    StreamlineSettings.EffectiveReflex,
                    Is.EqualTo(StreamlineSettings.ReflexMode.Off)
                );
                StreamlineSettings.GeneratedFrames = int.MaxValue;
                Assert.That(StreamlineSettings.GeneratedFrames, Is.EqualTo(3));
                StreamlineSettings.GeneratedFrames = -1;
                Assert.That(StreamlineSettings.GeneratedFrames, Is.Zero);
            }
            finally
            {
                if (hadDlss)
                    PlayerPrefs.SetInt(dlssKey, dlss);
                else
                    PlayerPrefs.DeleteKey(dlssKey);
                if (hadFrames)
                    PlayerPrefs.SetInt(framesKey, frames);
                else
                    PlayerPrefs.DeleteKey(framesKey);
                if (hadReflex)
                    PlayerPrefs.SetInt(reflexKey, reflex);
                else
                    PlayerPrefs.DeleteKey(reflexKey);
                PlayerPrefs.Save();
            }
        }
    }
}
