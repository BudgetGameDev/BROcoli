using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    public sealed partial class GameDisplaySettingsTests
    {
        [Test]
        public void HdrStatusPolicyCoversEveryPlatformAndTransition()
        {
            AssertStatus("WINDOWS / MACOS", false, true, false, false, false);
            AssertStatus("OUTPUT DISABLED", true, false, false, false, false);
            AssertStatus("SWITCHING TO SDR", true, false, true, true, false);
            AssertStatus("DOES NOT SUPPORT", true, false, true, false, false);
            AssertStatus("ENABLE HDR", true, true, false, false, false);
            AssertStatus("SWITCHING TO NATIVE", true, true, false, false, true);
            AssertStatus("10-BIT HDR10", true, true, true, true, true, true, false, true);
            AssertStatus("10-BIT METAL", true, true, true, true, true, false, true, true);
            AssertStatus("NATIVE METAL", true, true, true, true, true, false, true, false);
            AssertStatus("R16G16", true, true, true, true, true, false, false, false);
            Assert.That(
                GameDisplaySettings.ResolveHdrStatus(
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    false,
                    true,
                    "HDR",
                    true,
                    999f
                ),
                Does.Contain("SYSTEM DISPLAY PROFILE").And.Contain("999 NITS")
            );
        }

        [Test]
        public void RuntimeOnlyDisplayPathsHaveEditorTestSeams()
        {
            Assert.That(GameDisplaySettings.IsWindowsPlayer, Is.False);
            Assert.That(GameDisplaySettings.IsMacOSPlayer, Is.False);

            // Whether this desk's Game view is actually on an HDR swapchain is the machine's
            // business, so these say how the answers hang together rather than what they are.
            // Only the deepest one can be true on its own terms; each implies the one above it.
            Assert.That(
                GameDisplaySettings.SupportsNativeHdr,
                Is.EqualTo(GameDisplaySettings.IsWindows || GameDisplaySettings.IsMacOS)
            );
            if (GameDisplaySettings.IsHdrAvailable || GameDisplaySettings.IsHdrActive)
                Assert.That(GameDisplaySettings.SupportsNativeHdr, Is.True);
            if (GameDisplaySettings.IsTenBitHdrActive)
                Assert.That(GameDisplaySettings.IsHdrActive, Is.True);
            if (!GameDisplaySettings.SupportsNativeHdr)
            {
                Assert.That(GameDisplaySettings.CanSwitchHdrAtRuntime, Is.False);
                Assert.That(GameDisplaySettings.HdrStatus, Does.Contain("WINDOWS / MACOS"));
            }
            Assert.That(GameDisplaySettings.HdrStatus, Is.Not.Empty);

            int notifications = 0;
            GameDisplaySettings.ValuesChanged += () => notifications++;
            GameDisplaySettings.NotifyStatusChanged();
            Assert.That(notifications, Is.EqualTo(1));

            GameDisplaySettings.SetPaperWhite(250f);
            GameDisplaySettings.SetBlackLevel(0.01f);
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(250f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.EqualTo(0.01f));

            foreach (string key in PreferenceKeys)
                PlayerPrefs.DeleteKey(key);
            GameDisplaySettings.ResetStatics();
            Assert.That(
                GameDisplaySettings.TryUseNativeDisplayCalibration(
                    true,
                    true,
                    false,
                    1200f,
                    240f,
                    0.001f
                ),
                Is.True
            );
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(1200f));
        }

        [Test]
        public void BootstrapAndDriverLifecycleCoverDuplicateFocusPollingAndCleanup()
        {
            GameObject created = null;
            GameDisplaySettings.Bootstrap(true, true, value => created = (GameObject)value);
            Assert.That(created, Is.Not.Null);
            var driver = created.GetComponent<GameDisplaySettings.HdrDisplayDriver>();
            driver.Awake();
            var duplicateRoot = new GameObject("Duplicate HDR driver");
            try
            {
                var duplicate = duplicateRoot.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("Destroy may not be called from edit mode")
                );
                duplicate.Awake();
                duplicate.Apply();
                driver.OnApplicationFocus(false);
                driver.OnApplicationFocus(true);
                typeof(GameDisplaySettings.HdrDisplayDriver)
                    .GetField(
                        "nextStatusPoll",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .SetValue(driver, float.PositiveInfinity);
                driver.Update();
                typeof(GameDisplaySettings.HdrDisplayDriver)
                    .GetField(
                        "lastStatus",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .SetValue(driver, "forced status change");
                typeof(GameDisplaySettings.HdrDisplayDriver)
                    .GetField(
                        "nextStatusPoll",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .SetValue(driver, 0f);
                driver.Update();
                typeof(GameDisplaySettings.HdrDisplayDriver)
                    .GetField(
                        "nextStatusPoll",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .SetValue(driver, 0f);
                driver.Update();
                bool requested = false;
                driver.Apply(true, true, _ => requested = true);
                Assert.That(requested, Is.True);
                driver.OnDestroy(true, _ => { }, _ => { });
                driver.OnDestroy();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicateRoot);
                UnityEngine.Object.DestroyImmediate(created);
            }
        }

        private static void AssertStatus(
            string expected,
            bool native,
            bool enabled,
            bool active,
            bool canSwitch,
            bool available,
            bool windows = false,
            bool macOS = false,
            bool tenBit = false
        ) =>
            Assert.That(
                GameDisplaySettings.ResolveHdrStatus(
                    native,
                    enabled,
                    active,
                    canSwitch,
                    available,
                    windows,
                    macOS,
                    tenBit,
                    "R16G16",
                    false,
                    600f
                ),
                Does.Contain(expected)
            );
    }
}
