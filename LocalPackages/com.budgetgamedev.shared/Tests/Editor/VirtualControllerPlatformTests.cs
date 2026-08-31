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
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex(
                    "^\\[VirtualController\\] Runtime mobile detection:"
                )
            );
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex(
                    "^\\[VirtualController\\] No preference set"
                )
            );
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex("^\\[VirtualController\\] Awake -")
            );
            LogAssert.Expect(
                LogType.Log,
                "[VirtualController] Hiding joystick controls, keeping pause button"
            );

            Set("isMobileCacheSet", false);
            Invoke("Awake");
            Set("isMobileCacheSet", false);
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex(
                    "^\\[VirtualController\\] Runtime mobile detection:"
                )
            );
            Assert.That((bool)Invoke("IsMobilePlatform"), Is.False);
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
