using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the persisted three-bus volumes: their defaults, clamping, the
    /// PlayerPrefs round trip and the change notification the settings UI listens to.
    /// </summary>
    public sealed class GameAudioSettingsVolumeTests : GameAudioSettingsFixture
    {
        private int changeCount;

        private void CountChanges() => changeCount++;

        [SetUp]
        public void ForgetEarlierNotifications()
        {
            // NUnit reuses one fixture instance for every test in the class.
            changeCount = 0;
        }

        [Test]
        public void UntouchedVolumesReadBackAsTheDocumentedDefaults()
        {
            Assert.That(
                GameAudioSettings.MasterVolume,
                Is.EqualTo(GameAudioSettings.DefaultMasterVolume).Within(1e-6f)
            );
            Assert.That(
                GameAudioSettings.AmbienceVolume,
                Is.EqualTo(GameAudioSettings.DefaultAmbienceVolume).Within(1e-6f)
            );
            Assert.That(
                GameAudioSettings.SfxVolume,
                Is.EqualTo(GameAudioSettings.DefaultSfxVolume).Within(1e-6f)
            );
            Assert.That(GameAudioSettings.DefaultAmbienceVolume, Is.LessThan(1f));
        }

        [Test]
        public void SavedVolumesAreReadOnceAndThenCached()
        {
            PlayerPrefs.SetFloat(MasterKey, 0.25f);
            PlayerPrefs.SetFloat(AmbienceKey, 0.5f);
            PlayerPrefs.SetFloat(SfxKey, 0.75f);
            GameAudioSettings.ResetStatics();

            Assert.That(GameAudioSettings.MasterVolume, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(GameAudioSettings.AmbienceVolume, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(GameAudioSettings.SfxVolume, Is.EqualTo(0.75f).Within(1e-6f));

            // A later edit to the store is not re-read; the statics are the source of
            // truth once loaded, which is what keeps the per-frame mixer push cheap.
            PlayerPrefs.SetFloat(MasterKey, 0.1f);
            Assert.That(GameAudioSettings.MasterVolume, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void EachSetterStoresItsOwnKeyAndNotifiesOnce()
        {
            GameAudioSettings.ValuesChanged += CountChanges;
            try
            {
                GameAudioSettings.SetMasterVolume(0.4f);
                GameAudioSettings.SetAmbienceVolume(0.6f);
                GameAudioSettings.SetSfxVolume(0.8f);

                Assert.That(changeCount, Is.EqualTo(3));
                Assert.That(GameAudioSettings.MasterVolume, Is.EqualTo(0.4f).Within(1e-6f));
                Assert.That(GameAudioSettings.AmbienceVolume, Is.EqualTo(0.6f).Within(1e-6f));
                Assert.That(GameAudioSettings.SfxVolume, Is.EqualTo(0.8f).Within(1e-6f));
                Assert.That(PlayerPrefs.GetFloat(MasterKey), Is.EqualTo(0.4f).Within(1e-6f));
                Assert.That(PlayerPrefs.GetFloat(AmbienceKey), Is.EqualTo(0.6f).Within(1e-6f));
                Assert.That(PlayerPrefs.GetFloat(SfxKey), Is.EqualTo(0.8f).Within(1e-6f));
            }
            finally
            {
                GameAudioSettings.ValuesChanged -= CountChanges;
            }
        }

        [Test]
        public void SettingTheValueItAlreadyHasChangesNothing()
        {
            GameAudioSettings.SetSfxVolume(0.3f);
            GameAudioSettings.ValuesChanged += CountChanges;
            try
            {
                GameAudioSettings.SetSfxVolume(0.3f);
                Assert.That(changeCount, Is.Zero, "A no-op write still notified listeners.");
                Assert.That(GameAudioSettings.SfxVolume, Is.EqualTo(0.3f).Within(1e-6f));
            }
            finally
            {
                GameAudioSettings.ValuesChanged -= CountChanges;
            }
        }

        [Test]
        public void VolumesAreClampedToTheZeroToOneRange()
        {
            GameAudioSettings.SetMasterVolume(4f);
            GameAudioSettings.SetAmbienceVolume(-2f);

            Assert.That(GameAudioSettings.MasterVolume, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(GameAudioSettings.AmbienceVolume, Is.Zero);
            Assert.That(PlayerPrefs.GetFloat(AmbienceKey), Is.Zero);
        }

        [Test]
        public void ResetToDefaultsRestoresEveryBusAndPersistsIt()
        {
            GameAudioSettings.SetMasterVolume(0.1f);
            GameAudioSettings.SetAmbienceVolume(0.9f);
            GameAudioSettings.SetSfxVolume(0.2f);

            GameAudioSettings.ValuesChanged += CountChanges;
            try
            {
                GameAudioSettings.ResetToDefaults();
            }
            finally
            {
                GameAudioSettings.ValuesChanged -= CountChanges;
            }

            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(
                GameAudioSettings.MasterVolume,
                Is.EqualTo(GameAudioSettings.DefaultMasterVolume).Within(1e-6f)
            );
            Assert.That(
                GameAudioSettings.AmbienceVolume,
                Is.EqualTo(GameAudioSettings.DefaultAmbienceVolume).Within(1e-6f)
            );
            Assert.That(
                GameAudioSettings.SfxVolume,
                Is.EqualTo(GameAudioSettings.DefaultSfxVolume).Within(1e-6f)
            );
            Assert.That(
                PlayerPrefs.GetFloat(AmbienceKey),
                Is.EqualTo(GameAudioSettings.DefaultAmbienceVolume).Within(1e-6f)
            );
        }

        [Test]
        public void ResettingTheStaticsDropsEveryListener()
        {
            GameAudioSettings.ValuesChanged += CountChanges;
            GameAudioSettings.ResetStatics();

            GameAudioSettings.SetSfxVolume(0.42f);

            Assert.That(changeCount, Is.Zero, "A listener survived the subsystem reset.");
        }

        [Test]
        public void SilenceAndFullScaleMapOntoTheMixersDecibelRange()
        {
            Assert.That(GameAudioSettings.LinearToDecibels(1f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(GameAudioSettings.LinearToDecibels(0.5f), Is.EqualTo(-6.02f).Within(0.01f));
            Assert.That(GameAudioSettings.LinearToDecibels(0.1f), Is.EqualTo(-20f).Within(0.01f));
            Assert.That(GameAudioSettings.LinearToDecibels(0f), Is.EqualTo(-80f).Within(1e-4f));
            Assert.That(
                GameAudioSettings.LinearToDecibels(0.0001f),
                Is.EqualTo(-80f).Within(1e-4f)
            );
        }

        [Test]
        public void TheDecibelCurveNeverFallsAsTheSliderRises()
        {
            float previous = float.NegativeInfinity;
            for (int step = 0; step <= 20; step++)
            {
                float linear = step / 20f;
                float decibels = GameAudioSettings.LinearToDecibels(linear);
                Assert.That(decibels, Is.GreaterThanOrEqualTo(previous), $"{linear} dips.");
                Assert.That(decibels, Is.LessThanOrEqualTo(0f));
                previous = decibels;
            }
        }

        [Test]
        public void ConfiguringTheHostGamePublishesItsMixerAndMenuScene()
        {
            GameAudioSettings.Configure("Example/ExampleMixer", "ExampleMenu");

            Assert.That(GameAudioSettings.MixerResourcePath, Is.EqualTo("Example/ExampleMixer"));
            Assert.That(GameAudioSettings.MenuSceneName, Is.EqualTo("ExampleMenu"));
        }
    }
}
