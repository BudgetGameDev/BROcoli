using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared.Tests
{
    public sealed partial class GameDisplaySettingsTests
    {
        private static readonly string[] PreferenceKeys =
        {
            GameDisplaySettings.HdrEnabledKey,
            GameDisplaySettings.PeakBrightnessKey,
            GameDisplaySettings.PaperWhiteKey,
            GameDisplaySettings.BlackLevelKey,
            GameDisplaySettings.SystemCalibrationKey,
            GameDisplaySettings.LegacyWindowsCalibrationKey,
        };

        private readonly Dictionary<string, SavedPreference> savedPreferences = new();

        [SetUp]
        public void ClearDisplaySettings()
        {
            savedPreferences.Clear();
            foreach (string key in PreferenceKeys)
            {
                savedPreferences[key] = SavedPreference.Capture(key);
                PlayerPrefs.DeleteKey(key);
            }
            GameDisplaySettings.ResetStatics();
        }

        [TearDown]
        public void RestoreDisplaySettings()
        {
            GameDisplaySettings.ResetStatics();
            foreach (string key in PreferenceKeys)
            {
                PlayerPrefs.DeleteKey(key);
                savedPreferences[key].Restore(key);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void DefaultsTargetSixHundredNits()
        {
            Assert.That(GameDisplaySettings.HdrEnabled, Is.True);
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(600f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(200f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.EqualTo(0.0005f));
        }

        [Test]
        public void ContinuousCalibrationPersists()
        {
            GameDisplaySettings.SetCalibration(725f, 235f, 0.004f);

            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(725f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(235f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.EqualTo(0.004f));
            Assert.That(
                PlayerPrefs.GetFloat(GameDisplaySettings.PeakBrightnessKey),
                Is.EqualTo(725f)
            );
            Assert.That(PlayerPrefs.GetFloat(GameDisplaySettings.PaperWhiteKey), Is.EqualTo(235f));
            Assert.That(
                PlayerPrefs.GetFloat(GameDisplaySettings.BlackLevelKey),
                Is.EqualTo(0.004f)
            );
        }

        [Test]
        public void HdrTogglePersistsIndependentlyFromCalibration()
        {
            GameDisplaySettings.SetCalibration(850f, 210f, 0.002f);
            GameDisplaySettings.SetHdrEnabled(false);

            Assert.That(GameDisplaySettings.HdrEnabled, Is.False);
            Assert.That(PlayerPrefs.GetInt(GameDisplaySettings.HdrEnabledKey), Is.Zero);
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(850f));
        }

        [Test]
        public void ValidNativeDisplayMetadataBecomesTheFirstRunDefault()
        {
            bool applied = GameDisplaySettings.TryApplyDetectedCalibrationDefaults(1300f, 250f, 0f);

            Assert.That(applied, Is.True);
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(1300f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(250f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.Zero);
            Assert.That(GameDisplaySettings.UsingSystemCalibrationDefaults, Is.True);
            Assert.That(
                PlayerPrefs.GetInt(GameDisplaySettings.SystemCalibrationKey),
                Is.EqualTo(1)
            );

            GameDisplaySettings.ResetStatics();
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(1300f));
            Assert.That(GameDisplaySettings.UsingSystemCalibrationDefaults, Is.True);
        }

        [Test]
        public void NativeDisplayMetadataNeverOverwritesSavedPlayerCalibration()
        {
            GameDisplaySettings.SetCalibration(725f, 210f, 0.001f);

            bool applied = GameDisplaySettings.TryUseNativeDisplayCalibration(
                true,
                true,
                true,
                1300f,
                250f,
                0f,
                450f
            );

            Assert.That(applied, Is.False);
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(725f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(210f));
            Assert.That(GameDisplaySettings.UsingSystemCalibrationDefaults, Is.False);
            Assert.That(GameDisplaySettings.HasDetectedHdrProfile, Is.True);
            Assert.That(GameDisplaySettings.DetectedPeakBrightnessNits, Is.EqualTo(1300f));
            Assert.That(GameDisplaySettings.DetectedFullFrameBrightnessNits, Is.EqualTo(450f));
            Assert.That(GameDisplaySettings.DetectedPaperWhiteNits, Is.EqualTo(250f));

            Assert.That(GameDisplaySettings.ResetToDetectedHdrProfile(), Is.True);
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(1300f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(250f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.Zero);
            Assert.That(GameDisplaySettings.UsingSystemCalibrationDefaults, Is.True);
        }

        [Test]
        public void InvalidNativeDisplayMetadataFallsBackWithoutBeingSaved()
        {
            bool applied = GameDisplaySettings.TryApplyDetectedCalibrationDefaults(0f, 0f, -1f);

            Assert.That(applied, Is.False);
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(600f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(200f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.EqualTo(0.0005f));
            Assert.That(PlayerPrefs.HasKey(GameDisplaySettings.PeakBrightnessKey), Is.False);
        }

        [Test]
        public void EditingSystemDefaultsTurnsThemIntoPlayerCalibration()
        {
            GameDisplaySettings.TryApplyDetectedCalibrationDefaults(1300f, 250f, 0f);

            GameDisplaySettings.SetPeakBrightness(1275f);

            Assert.That(GameDisplaySettings.UsingSystemCalibrationDefaults, Is.False);
            Assert.That(PlayerPrefs.GetInt(GameDisplaySettings.SystemCalibrationKey), Is.Zero);
        }

        [Test]
        public void SystemCalibrationRefreshesWhenTheDisplayProfileChanges()
        {
            GameDisplaySettings.TryApplyDetectedCalibrationDefaults(800f, 200f, 0.001f);

            bool applied = GameDisplaySettings.TryApplyDetectedCalibrationDefaults(1200f, 240f, 0f);

            Assert.That(applied, Is.True);
            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(1200f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(240f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.Zero);
            Assert.That(GameDisplaySettings.UsingSystemCalibrationDefaults, Is.True);
        }

        [Test]
        public void LegacyWindowsCalibrationSourceLoadsAsSystemCalibration()
        {
            PlayerPrefs.SetFloat(GameDisplaySettings.PeakBrightnessKey, 1000f);
            PlayerPrefs.SetFloat(GameDisplaySettings.PaperWhiteKey, 200f);
            PlayerPrefs.SetFloat(GameDisplaySettings.BlackLevelKey, 0.001f);
            PlayerPrefs.SetInt(GameDisplaySettings.LegacyWindowsCalibrationKey, 1);
            GameDisplaySettings.ResetStatics();

            Assert.That(GameDisplaySettings.UsingSystemCalibrationDefaults, Is.True);
        }

        [Test]
        public void NativeHdrPlatformsIncludeWindowsAndMacOSPlayersOnly()
        {
            Assert.That(
                GameDisplaySettings.IsNativeHdrPlatform(RuntimePlatform.WindowsPlayer),
                Is.True
            );
            Assert.That(
                GameDisplaySettings.IsNativeHdrPlatform(RuntimePlatform.OSXPlayer),
                Is.True
            );
            Assert.That(
                GameDisplaySettings.IsNativeHdrPlatform(RuntimePlatform.OSXEditor),
                Is.False
            );
            Assert.That(
                GameDisplaySettings.IsNativeHdrPlatform(RuntimePlatform.WebGLPlayer),
                Is.False
            );
        }

        [Test]
        public void CalibrationValuesAreClampedAndNonFiniteValuesUseDefaults()
        {
            GameDisplaySettings.SetCalibration(float.PositiveInfinity, 999f, -1f);

            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(600f));
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(400f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.Zero);

            GameDisplaySettings.SetCalibration(200f, 400f, float.NaN);
            Assert.That(GameDisplaySettings.PaperWhiteNits, Is.EqualTo(200f));
            Assert.That(GameDisplaySettings.BlackLevelNits, Is.EqualTo(0.0005f));
        }

        [Test]
        public void LegacyIntegerPeakSettingMigrates()
        {
            PlayerPrefs.SetInt(GameDisplaySettings.PeakBrightnessKey, 1000);
            GameDisplaySettings.ResetStatics();

            Assert.That(GameDisplaySettings.PeakBrightnessNits, Is.EqualTo(1000f));
        }

        [Test]
        public void TenBitSwapchainFormatsAreRecognized()
        {
            Assert.That(GameDisplaySettings.IsTenBitFormat("A2B10G10R10_UNormPack32"), Is.True);
            Assert.That(GameDisplaySettings.IsTenBitFormat("R10G10B10A2_UNorm"), Is.True);
            Assert.That(GameDisplaySettings.IsTenBitFormat("R16G16B16A16_SFloat"), Is.False);
            Assert.That(GameDisplaySettings.IsTenBitFormat("R8G8B8A8_UNorm"), Is.False);
        }

        [Test]
        public void RuntimeDriverAppliesCalibratedValuesToHighestPriorityGlobalVolume()
        {
            GameObject root = new("HDR Display Driver Test");
            try
            {
                var driver = root.AddComponent<GameDisplaySettings.HdrDisplayDriver>();
                driver.Awake();
                driver.Apply(false, true, _ => { });

                Volume volume = root.GetComponent<Volume>();
                Assert.That(volume, Is.Not.Null);
                Assert.That(volume.isGlobal, Is.True);
                Assert.That(volume.priority, Is.EqualTo(float.MaxValue));
                Assert.That(volume.enabled, Is.True);
                Assert.That(volume.profile.TryGet(out Tonemapping tonemapping), Is.True);
                Assert.That(
                    volume.profile.TryGet(out Bloom _),
                    Is.False,
                    "HDR inherits the scene's bloom rather than overriding it"
                );
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(colorAdjustments.active, Is.True);
                Assert.That(colorAdjustments.saturation.value, Is.EqualTo(12f));
                Assert.That(colorAdjustments.contrast.value, Is.EqualTo(17f));
                Assert.That(
                    colorAdjustments.postExposure.overrideState,
                    Is.False,
                    "exposure stays with the scene; the HDR grade only reshapes it"
                );
                Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));
                Assert.That(tonemapping.acesPreset.value, Is.EqualTo(HDRACESPreset.ACES1000Nits));
                Assert.That(tonemapping.detectPaperWhite.value, Is.False);
                Assert.That(tonemapping.paperWhite.value, Is.EqualTo(200f));
                Assert.That(tonemapping.detectBrightnessLimits.value, Is.False);
                Assert.That(tonemapping.minNits.value, Is.EqualTo(0.0005f));
                Assert.That(tonemapping.maxNits.value, Is.EqualTo(600f));

                GameDisplaySettings.BeginHdrCalibrationPreview();
                Assert.That(tonemapping.detectBrightnessLimits.value, Is.True);
                GameDisplaySettings.EndHdrCalibrationPreview();
                Assert.That(tonemapping.detectBrightnessLimits.value, Is.False);

                GameDisplaySettings.SetCalibration(775f, 225f, 0.003f);
                Assert.That(tonemapping.maxNits.value, Is.EqualTo(775f));
                Assert.That(tonemapping.paperWhite.value, Is.EqualTo(225f));
                Assert.That(tonemapping.minNits.value, Is.EqualTo(0.003f));
                Assert.That(tonemapping.acesPreset.value, Is.EqualTo(HDRACESPreset.ACES1000Nits));

                GameDisplaySettings.SetCalibration(1200f, 225f, 0.003f);
                Assert.That(
                    tonemapping.acesPreset.value,
                    Is.EqualTo(HDRACESPreset.ACES2000Nits),
                    "a peak the 1000 nit shoulder cannot reach moves up a preset"
                );
                GameDisplaySettings.SetHdrEnabled(false);
                Assert.That(volume.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private readonly struct SavedPreference
        {
            private readonly bool existed;
            private readonly bool wasFloat;
            private readonly float floatValue;
            private readonly int intValue;

            private SavedPreference(bool existed, bool wasFloat, float floatValue, int intValue)
            {
                this.existed = existed;
                this.wasFloat = wasFloat;
                this.floatValue = floatValue;
                this.intValue = intValue;
            }

            internal static SavedPreference Capture(string key)
            {
                if (!PlayerPrefs.HasKey(key))
                    return default;

                float value = PlayerPrefs.GetFloat(key, float.NaN);
                return float.IsNaN(value)
                    ? new SavedPreference(true, false, 0f, PlayerPrefs.GetInt(key))
                    : new SavedPreference(true, true, value, 0);
            }

            internal void Restore(string key)
            {
                if (!existed)
                    return;
                if (wasFloat)
                    PlayerPrefs.SetFloat(key, floatValue);
                else
                    PlayerPrefs.SetInt(key, intValue);
            }
        }
    }
}
