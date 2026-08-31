using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Whether this device needs the on-screen controller. A desktop editor can
    /// never be a phone, so the decision is fed the platform values rather than
    /// reading them, and the activation it drives is checked on its own.
    /// </summary>
    public sealed class InputManagerMobileTests
    {
        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void DestroySpawned()
        {
            foreach (GameObject created in spawned)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            spawned.Clear();
        }

        private GameObject NewController()
        {
            var controller = new GameObject("VirtualController");
            controller.AddComponent<VirtualController>();
            controller.SetActive(false);
            spawned.Add(controller);
            return controller;
        }

        private InputManager NewManager()
        {
            var host = new GameObject("InputManager");
            spawned.Add(host);
            return host.AddComponent<InputManager>();
        }

        [TestCase(RuntimePlatform.IPhonePlayer, DeviceType.Desktop, false)]
        [TestCase(RuntimePlatform.Android, DeviceType.Desktop, false)]
        [TestCase(RuntimePlatform.WindowsPlayer, DeviceType.Handheld, false)]
        public void PhonesAndHandheldsAlwaysGetTouchControls(
            RuntimePlatform platform,
            DeviceType deviceType,
            bool touchSupported
        )
        {
            Assert.That(InputManager.IsMobileDevice(platform, deviceType, touchSupported), Is.True);
        }

        [TestCase(RuntimePlatform.WebGLPlayer, DeviceType.Desktop, false)]
        [TestCase(RuntimePlatform.OSXPlayer, DeviceType.Desktop, true)]
        [TestCase(RuntimePlatform.WindowsPlayer, DeviceType.Console, true)]
        public void DesktopsAreLeftWithKeyboardAndPad(
            RuntimePlatform platform,
            DeviceType deviceType,
            bool touchSupported
        )
        {
            Assert.That(
                InputManager.IsMobileDevice(platform, deviceType, touchSupported),
                Is.False
            );
        }

        [Test]
        public void ATouchCapableBrowserCountsAsMobileAndSaysWhy()
        {
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape("WebGL with touch support")));

            Assert.That(
                InputManager.IsMobileDevice(RuntimePlatform.WebGLPlayer, DeviceType.Desktop, true),
                Is.True,
                "a phone browser is the case the JavaScript probe exists to catch"
            );
        }

        [TestCase(false, DeviceType.Desktop, false, false)]
        [TestCase(true, DeviceType.Desktop, false, true)]
        [TestCase(false, DeviceType.Handheld, false, true)]
        [TestCase(false, DeviceType.Desktop, true, true)]
        public void EditorSimulationExtendsButNeverClearsMobileDetection(
            bool detected,
            DeviceType deviceType,
            bool touchSupported,
            bool expected
        )
        {
            Assert.That(
                InputManager.IncludeEditorMobileSimulation(detected, deviceType, touchSupported),
                Is.EqualTo(expected)
            );
        }

        [Test]
        public void ADesktopLeavesTheOnScreenControllerHidden()
        {
            GameObject controller = NewController();

            InputManager.ActivateVirtualController(false);

            Assert.That(controller.activeSelf, Is.False);
        }

        [Test]
        public void AMobileDeviceShowsTheOnScreenController()
        {
            GameObject controller = NewController();
            LogAssert.Expect(
                LogType.Log,
                new Regex(Regex.Escape("VirtualController found and activated"))
            );

            InputManager.ActivateVirtualController(true);

            Assert.That(controller.activeSelf, Is.True, "a phone has no other way to play");
        }

        [Test]
        public void AMobileDeviceWithNoControllerInTheSceneIsReportedLoudly()
        {
            Assert.That(
                Object.FindAnyObjectByType<VirtualController>(FindObjectsInactive.Include),
                Is.Null,
                "this test needs a scene with no on-screen controller in it"
            );
            LogAssert.Expect(
                LogType.Warning,
                new Regex(Regex.Escape("VirtualController not found in scene"))
            );

            InputManager.ActivateVirtualController(true);
        }

        [Test]
        public void TheRetryWindowClosesAfterTenFrames()
        {
            InputManager manager = NewManager();

            for (int frame = 0; frame < 12; frame++)
            {
                manager.Update();
            }

            Assert.That(
                manager.mobileCheckFrames,
                Is.EqualTo(0),
                "the late-platform check must not run for the whole game"
            );
        }

        [Test]
        public void ARetryShowsAControllerThatOnlyAppearedAfterStart()
        {
            InputManager manager = NewManager();
            GameObject controller = NewController();

            manager.RetryActivation(false);

            Assert.That(controller.activeSelf, Is.False, "a desktop retry changes nothing");

            LogAssert.Expect(
                LogType.Log,
                new Regex(Regex.Escape("VirtualController re-activated in Update frame"))
            );
            manager.RetryActivation(true);

            Assert.That(controller.activeSelf, Is.True);

            // A controller that is already showing must not be activated - or
            // announced - a second time, which the log assertions enforce.
            manager.RetryActivation(true);

            Assert.That(controller.activeSelf, Is.True);
        }
    }
}
