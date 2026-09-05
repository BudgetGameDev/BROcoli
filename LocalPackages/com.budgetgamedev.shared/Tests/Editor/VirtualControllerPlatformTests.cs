using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BudgetGameDev.Shared.Tests
{
    public sealed partial class VirtualControllerTests
    {
        [Test]
        public void AutomaticPreferenceUsesTheTestablePlatformDecision()
        {
            bool expected = VirtualController.IsMobileDevice(
                Application.platform,
                SystemInfo.deviceType,
                Input.touchSupported,
                UnityEngine.Device.SystemInfo.deviceType,
                UnityEngine.Device.Application.isMobilePlatform
            );
            LogAssert.Expect(
                LogType.Log,
                $"[VirtualController] Runtime mobile detection: {expected}"
            );
            LogAssert.Expect(
                LogType.Log,
                $"[VirtualController] No preference set, auto-detecting: {(expected ? "mobile" : "desktop")}"
            );
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex("^\\[VirtualController\\] Awake -")
            );
            if (expected)
            {
                LogAssert.Expect(LogType.Log, "[VirtualController] EnhancedTouchSupport enabled");
                LogAssert.Expect(LogType.Log, "[VirtualController] Visible and ready");
            }
            else
                LogAssert.Expect(
                    LogType.Log,
                    "[VirtualController] Hiding joystick controls, keeping pause button"
                );

            Set("isMobileCacheSet", false);
            Invoke("Awake");
            Assert.That(joystick.gameObject.activeSelf, Is.EqualTo(expected));
            Set("isMobileCacheSet", false);
            LogAssert.Expect(
                LogType.Log,
                $"[VirtualController] Runtime mobile detection: {expected}"
            );
            Assert.That((bool)Invoke("IsMobilePlatform"), Is.EqualTo(expected));
        }

        [TestCase(
            RuntimePlatform.IPhonePlayer,
            DeviceType.Desktop,
            false,
            DeviceType.Desktop,
            false,
            true
        )]
        [TestCase(
            RuntimePlatform.Android,
            DeviceType.Desktop,
            false,
            DeviceType.Desktop,
            false,
            true
        )]
        [TestCase(
            RuntimePlatform.WebGLPlayer,
            DeviceType.Desktop,
            true,
            DeviceType.Desktop,
            false,
            true
        )]
        [TestCase(
            RuntimePlatform.OSXPlayer,
            DeviceType.Handheld,
            false,
            DeviceType.Desktop,
            false,
            true
        )]
        [TestCase(
            RuntimePlatform.OSXPlayer,
            DeviceType.Desktop,
            false,
            DeviceType.Handheld,
            false,
            true
        )]
        [TestCase(
            RuntimePlatform.OSXPlayer,
            DeviceType.Desktop,
            false,
            DeviceType.Console,
            false,
            true
        )]
        [TestCase(
            RuntimePlatform.OSXPlayer,
            DeviceType.Desktop,
            false,
            DeviceType.Desktop,
            true,
            true
        )]
        [TestCase(
            RuntimePlatform.OSXPlayer,
            DeviceType.Desktop,
            false,
            DeviceType.Desktop,
            false,
            false
        )]
        public void PlatformDecisionCoversNativeBrowserAndSimulatorDevices(
            RuntimePlatform platform,
            DeviceType deviceType,
            bool touchSupported,
            DeviceType simulatedDeviceType,
            bool simulatedMobile,
            bool expected
        )
        {
            Assert.That(
                VirtualController.IsMobileDevice(
                    platform,
                    deviceType,
                    touchSupported,
                    simulatedDeviceType,
                    simulatedMobile
                ),
                Is.EqualTo(expected)
            );
        }

        [Test]
        public void PointerTransitionsDriveAndReleaseTheJoystick()
        {
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, joystick.position);
            LogAssert.Expect(LogType.Log, "[VirtualController] Touch began on joystick, finger: 4");
            controller.ProcessTouch(TouchPhase.Began, 4, center);
            controller.ProcessTouch(TouchPhase.Moved, 8, center);
            controller.ProcessTouch(TouchPhase.Moved, 4, center + Vector2.one * 40f);
            Assert.That(controller.JoystickInput, Is.Not.EqualTo(Vector2.zero));
            controller.ProcessTouch(TouchPhase.Stationary, 4, center + Vector2.one * 30f);
            controller.ProcessTouch(TouchPhase.Ended, 4, center);
            Assert.That(controller.JoystickInput, Is.EqualTo(Vector2.zero));

            LogAssert.Expect(LogType.Log, "[VirtualController] Touch began on joystick, finger: 5");
            controller.ProcessTouch(TouchPhase.Began, 5, center);
            controller.ProcessTouch(TouchPhase.Canceled, 5, center);
            controller.ProcessMouse(true, true, false, center + Vector2.one * 30f);
            Assert.That(controller.JoystickInput, Is.Not.EqualTo(Vector2.zero));
            controller.ProcessMouse(false, false, true, center);
            Assert.That(controller.JoystickInput, Is.EqualTo(Vector2.zero));
        }
    }
}
