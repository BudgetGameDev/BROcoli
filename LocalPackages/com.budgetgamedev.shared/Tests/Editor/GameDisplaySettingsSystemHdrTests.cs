using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Tests
{
    public sealed partial class GameDisplaySettingsTests
    {
        [Test]
        public void HdrOutputFollowsTheOperatingSystemSwitchInBothDirections()
        {
            Assert.That(GameDisplaySettings.SystemHdrState, Is.EqualTo(SystemHdrState.Unknown));
            Assert.That(GameDisplaySettings.HdrFollowsSystem, Is.False);
            Assert.That(GameDisplaySettings.CanToggleHdr, Is.True);

            GameDisplaySettings.SetHdrEnabled(false);
            GameDisplaySettings.systemHdrStateProvider = () => SystemHdrState.Enabled;
            Assert.That(GameDisplaySettings.RefreshSystemHdrState(), Is.True);
            Assert.That(GameDisplaySettings.RefreshSystemHdrState(), Is.False);
            Assert.That(GameDisplaySettings.HdrEnabled, Is.True);
            Assert.That(GameDisplaySettings.HdrPreferred, Is.False);
            Assert.That(GameDisplaySettings.HdrFollowsSystem, Is.True);
            Assert.That(GameDisplaySettings.CanToggleHdr, Is.False);

            GameDisplaySettings.ToggleHdr();
            Assert.That(GameDisplaySettings.HdrEnabled, Is.True);
            Assert.That(GameDisplaySettings.HdrPreferred, Is.False);

            GameDisplaySettings.SetHdrEnabled(true);
            GameDisplaySettings.systemHdrStateProvider = () => SystemHdrState.Disabled;
            Assert.That(GameDisplaySettings.RefreshSystemHdrState(), Is.True);
            Assert.That(GameDisplaySettings.HdrEnabled, Is.False);
            Assert.That(GameDisplaySettings.HdrPreferred, Is.True);

            GameDisplaySettings.systemHdrStateProvider = () => SystemHdrState.Unknown;
            Assert.That(GameDisplaySettings.RefreshSystemHdrState(), Is.True);
            Assert.That(GameDisplaySettings.HdrEnabled, Is.True);
            Assert.That(GameDisplaySettings.CanToggleHdr, Is.True);
            GameDisplaySettings.ToggleHdr();
            Assert.That(GameDisplaySettings.HdrEnabled, Is.False);
        }

        [Test]
        public void OutputPolicyOnlyDefersToThePreferenceWhenTheSystemSwitchIsUnknown()
        {
            Assert.That(
                GameDisplaySettings.ResolveHdrOutputEnabled(SystemHdrState.Enabled, false),
                Is.True
            );
            Assert.That(
                GameDisplaySettings.ResolveHdrOutputEnabled(SystemHdrState.Disabled, true),
                Is.False
            );
            Assert.That(
                GameDisplaySettings.ResolveHdrOutputEnabled(SystemHdrState.Unknown, true),
                Is.True
            );
            Assert.That(
                GameDisplaySettings.ResolveHdrOutputEnabled(SystemHdrState.Unknown, false),
                Is.False
            );
        }

        [Test]
        public void NativeSystemSwitchQueryAnswersOnWindowsAndIsSilentElsewhere()
        {
            // The query reads the operating system, so what it answers on Windows -- Editor
            // included -- belongs to whoever is running the tests. Everywhere else it has no
            // switch to read and says so.
            SystemHdrState queried = WindowsDisplayHdrState.Query();
            Assert.That(System.Enum.IsDefined(typeof(SystemHdrState), queried), Is.True);
            if (!GameDisplaySettings.IsWindows)
            {
                Assert.That(queried, Is.EqualTo(SystemHdrState.Unknown));
                Assert.That(
                    GameDisplaySettings.QuerySystemHdrState(),
                    Is.EqualTo(SystemHdrState.Unknown)
                );
            }

            GameDisplaySettings.systemHdrStateProvider = () => SystemHdrState.Enabled;
            Assert.That(
                GameDisplaySettings.QuerySystemHdrState(),
                Is.EqualTo(SystemHdrState.Enabled),
                "the provider seam stands in for the operating system"
            );
            GameDisplaySettings.systemHdrStateProvider = null;
            Assert.That(
                WindowsDisplayHdrState.ResolveAdvancedColorMode(2),
                Is.EqualTo(SystemHdrState.Enabled)
            );
            Assert.That(
                WindowsDisplayHdrState.ResolveAdvancedColorMode(1),
                Is.EqualTo(SystemHdrState.Disabled)
            );
            Assert.That(
                WindowsDisplayHdrState.ResolveLegacyAdvancedColor(0x3),
                Is.EqualTo(SystemHdrState.Enabled)
            );
            Assert.That(
                WindowsDisplayHdrState.ResolveLegacyAdvancedColor(0x1),
                Is.EqualTo(SystemHdrState.Disabled)
            );
        }

        [Test]
        public void NativeDisplayModePreservesFractionalRefreshRates()
        {
            NativeDisplayMode mode = new(
                2560,
                1440,
                new RefreshRate { numerator = 240083, denominator = 1000 }
            );

            Assert.That(mode.IsValid, Is.True);
            Assert.That(mode.Width, Is.EqualTo(2560));
            Assert.That(mode.Height, Is.EqualTo(1440));
            Assert.That(mode.RefreshRate.numerator, Is.EqualTo(240083));
            Assert.That(mode.RefreshRate.denominator, Is.EqualTo(1000));
            Assert.That(mode.ToString(), Is.EqualTo("2560x1440 @ 240083/1000 Hz"));
        }

        [TestCase(0, 1440, 240083u, 1000u)]
        [TestCase(2560, 0, 240083u, 1000u)]
        [TestCase(2560, 1440, 0u, 1000u)]
        [TestCase(2560, 1440, 240083u, 0u)]
        public void NativeDisplayModeRejectsIncompleteMonitorData(
            int width,
            int height,
            uint numerator,
            uint denominator
        )
        {
            NativeDisplayMode mode = new(
                width,
                height,
                new RefreshRate { numerator = numerator, denominator = denominator }
            );

            Assert.That(mode.IsValid, Is.False);
        }

        [Test]
        public void HdrGradeNeedsBothTheSwitchAndADetectedHdrDisplay()
        {
            GameObject root = new("HDR Display Driver Policy Test");
            try
            {
                var driver = root.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                driver.Awake();
                Volume volume = root.GetComponent<Volume>();

                bool? requested = null;
                driver.Apply(true, false, value => requested = value);
                Assert.That(volume.enabled, Is.False);
                Assert.That(requested, Is.Null);

                GameDisplaySettings.systemHdrStateProvider = () => SystemHdrState.Disabled;
                GameDisplaySettings.RefreshSystemHdrState();
                driver.Apply(true, true, value => requested = value);
                Assert.That(volume.enabled, Is.False);
                Assert.That(requested, Is.False);

                GameDisplaySettings.SetHdrEnabled(false);
                GameDisplaySettings.systemHdrStateProvider = () => SystemHdrState.Enabled;
                GameDisplaySettings.RefreshSystemHdrState();
                driver.Apply(true, true, value => requested = value);
                Assert.That(volume.enabled, Is.True);
                Assert.That(requested, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DriverPollReappliesAndNotifiesWhenTheSystemSwitchChanges()
        {
            GameObject root = new("HDR Display Driver Poll Test");
            try
            {
                SystemHdrState state = SystemHdrState.Disabled;
                GameDisplaySettings.systemHdrStateProvider = () => state;
                var driver = root.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                driver.Awake();
                int notifications = 0;
                GameDisplaySettings.ValuesChanged += () => notifications++;

                SetDriverField(driver, "nextStatusPoll", 0f);
                driver.Update();
                Assert.That(notifications, Is.EqualTo(0));

                state = SystemHdrState.Enabled;
                SetDriverField(driver, "nextStatusPoll", 0f);
                driver.Update();
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(GameDisplaySettings.HdrEnabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HdrStatusExplainsWhenTheOperatingSystemOwnsTheSwitch()
        {
            Assert.That(
                Status(false, false, false, true, SystemHdrState.Disabled),
                Is.EqualTo("FOLLOWS WINDOWS • HDR IS OFF IN WINDOWS DISPLAY SETTINGS")
            );
            Assert.That(
                Status(false, false, false, false, SystemHdrState.Disabled),
                Does.StartWith("FOLLOWS SYSTEM")
            );
            Assert.That(
                Status(false, false, false, true, SystemHdrState.Unknown),
                Is.EqualTo("NATIVE HDR OUTPUT DISABLED")
            );
            Assert.That(
                Status(true, false, false, true, SystemHdrState.Enabled),
                Does.Contain("NO HDR OUTPUT WAS DETECTED")
            );
            Assert.That(
                Status(true, false, false, true, SystemHdrState.Unknown),
                Is.EqualTo("ENABLE HDR IN WINDOWS DISPLAY SETTINGS")
            );
            Assert.That(
                Status(true, true, true, true, SystemHdrState.Enabled),
                Does.StartWith("FOLLOWS WINDOWS • 10-BIT HDR10 ACTIVE")
            );
            Assert.That(
                Status(true, true, true, true, SystemHdrState.Unknown),
                Does.StartWith("10-BIT HDR10 ACTIVE")
            );
        }

        private static string Status(
            bool enabled,
            bool active,
            bool available,
            bool windows,
            SystemHdrState systemState
        ) =>
            GameDisplaySettings.ResolveHdrStatus(
                true,
                enabled,
                active,
                true,
                available,
                windows,
                false,
                true,
                "R10G10B10A2",
                true,
                600f,
                systemState
            );

        private static void SetDriverField(
            GameDisplaySettings.HdrDisplayDriver driver,
            string name,
            object value
        ) =>
            typeof(GameDisplaySettings.HdrDisplayDriver)
                .GetField(
                    name,
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )
                .SetValue(driver, value);
    }
}
