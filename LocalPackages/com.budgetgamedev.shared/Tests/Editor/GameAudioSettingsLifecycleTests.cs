using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the bootstrap, the singleton rules and the Unity messages of the audio
    /// settings component. Edit mode never sends those messages itself, so each test
    /// calls them directly on a component it built.
    /// </summary>
    public sealed class GameAudioSettingsLifecycleTests : GameAudioSettingsFixture
    {
        [Test]
        public void BootstrapCreatesOnePersistentHostAndKeepsIt()
        {
            GameAudioSettings.Bootstrap();
            GameAudioSettings created = GameAudioSettings.instance;
            Assert.That(created, Is.Not.Null, "Bootstrap did not create the host object.");
            Track(created.gameObject);
            Assert.That(created.gameObject.name, Is.EqualTo("Game Audio Settings"));
            Assert.That(
                GameAudioSettings.MasterVolume,
                Is.EqualTo(GameAudioSettings.DefaultMasterVolume).Within(1e-6f),
                "Bootstrap must leave the volumes loaded before the first scene."
            );

            GameAudioSettings.Bootstrap();

            Assert.That(GameAudioSettings.instance, Is.SameAs(created), "A second host appeared.");
        }

        [Test]
        public void AwakeAdoptsTheComponentAsTheSingleton()
        {
            GameAudioSettings settings = NewSettings();

            settings.Awake();

            Assert.That(GameAudioSettings.instance, Is.SameAs(settings));
            Assert.That(
                GameAudioSettings.AmbienceVolume,
                Is.EqualTo(GameAudioSettings.DefaultAmbienceVolume).Within(1e-6f)
            );
        }

        [Test]
        public void AwakeReportsAMixerTheGameDidNotShip()
        {
            GameAudioSettings.Configure("Nope/Mixer", null);
            LogAssert.Expect(LogType.Error, "[Audio Settings] Missing Resources/Nope/Mixer.mixer");

            GameAudioSettings settings = NewSettings();
            settings.Awake();

            // The component still owns the singleton: a missing mixer must not take the
            // volume store down with it.
            Assert.That(GameAudioSettings.instance, Is.SameAs(settings));
            GameAudioSettings.SetSfxVolume(0.5f);
            Assert.That(GameAudioSettings.SfxVolume, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void ASecondComponentDoesNotStealTheSingleton()
        {
            GameAudioSettings first = NewSettings();
            first.Awake();
            GameAudioSettings duplicate = NewSettings();

            duplicate.Awake();

            Assert.That(GameAudioSettings.instance, Is.SameAs(first));
            Assert.That(duplicate == null, Is.True, "The duplicate did not remove itself.");
        }

        [Test]
        public void TheSourceScanIsThrottledBetweenFrames()
        {
            GameAudioSettings settings = NewSettings();
            settings.Awake();

            settings.nextSourceScan = float.MaxValue;
            settings.LateUpdate();
            Assert.That(
                settings.nextSourceScan,
                Is.EqualTo(float.MaxValue),
                "A scan ran before its due time."
            );

            settings.nextSourceScan = float.MinValue;
            settings.LateUpdate();
            Assert.That(
                settings.nextSourceScan,
                Is.GreaterThan(float.MinValue),
                "A due scan did not schedule the next one."
            );
        }

        [Test]
        public void LoadingASceneLetsTheAmbienceBackIn()
        {
            GameAudioSettings settings = NewSettings();
            settings.Awake();
            GameAudioSettings.SetPauseMenuOpen(true);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.True);

            settings.HandleSceneLoaded(default, LoadSceneMode.Single);

            Assert.That(AudioListener.pause, Is.False, "A loaded scene stayed paused.");
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.False);
        }

        [Test]
        public void PausingWithoutAHostStillSilencesTheListener()
        {
            // The pause menu can open before any component exists, as it does when a
            // scene is opened straight from the editor.
            GameAudioSettings.SetPauseMenuOpen(true);

            Assert.That(AudioListener.pause, Is.True);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.True);

            GameAudioSettings.SetPauseMenuOpen(false);
            Assert.That(AudioListener.pause, Is.False);
        }

        [Test]
        public void DestroyingTheHostLeavesTheSettingsUsable()
        {
            GameAudioSettings settings = NewSettings();
            settings.Awake();

            settings.OnDestroy();

            // Only the scene hook goes; the persisted values and the pause state are
            // static and outlive any single host.
            Assert.That(GameAudioSettings.instance, Is.SameAs(settings));
            GameAudioSettings.SetAmbienceVolume(0.75f);
            Assert.That(GameAudioSettings.AmbienceVolume, Is.EqualTo(0.75f).Within(1e-6f));
        }

        [Test]
        public void DestroyingAComponentThatNeverBecameTheHostChangesNothing()
        {
            GameAudioSettings settings = NewSettings();
            settings.Awake();
            GameAudioSettings other = NewSettings();

            other.OnDestroy();

            Assert.That(GameAudioSettings.instance, Is.SameAs(settings));
        }

        [Test]
        public void ResettingTheStaticsForgetsTheHostAndThePause()
        {
            GameAudioSettings settings = NewSettings();
            settings.Awake();
            GameAudioSettings.SetPauseMenuOpen(true);

            GameAudioSettings.ResetStatics();

            Assert.That(GameAudioSettings.instance, Is.Null);
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.False);
        }
    }
}
