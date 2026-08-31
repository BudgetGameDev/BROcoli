using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the two decisions the mixer routing rests on: when the ambience bus is
    /// muted, and which loose AudioSource counts as ambience rather than an effect.
    /// </summary>
    public sealed class GameAudioSettingsRoutingTests : GameAudioSettingsFixture
    {
        private static readonly string[] AmbienceWords =
        {
            "ambient",
            "ambience",
            "music",
            "wind",
            "lava",
            "nature",
            "rain",
            "umhv",
        };

        private readonly List<AudioClip> clips = new();

        [TearDown]
        public void DestroyClips()
        {
            foreach (AudioClip clip in clips)
            {
                if (clip != null)
                    Object.DestroyImmediate(clip);
            }

            clips.Clear();
        }

        [Test]
        public void AGameWithNoMenuSceneNeverSuppressesItsAmbience()
        {
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.False);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience(null), Is.False);

            GameAudioSettings.Configure(null, string.Empty);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience(string.Empty), Is.False);
        }

        [Test]
        public void OnlyTheConfiguredMenuSceneSilencesTheAmbience()
        {
            GameAudioSettings.Configure("Example/ExampleMixer", "MainMenu");

            Assert.That(GameAudioSettings.ShouldSuppressAmbience("MainMenu"), Is.True);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.False);
            Assert.That(
                GameAudioSettings.ShouldSuppressAmbience("mainmenu"),
                Is.False,
                "Scene names are matched exactly, as SceneManager reports them."
            );
        }

        [Test]
        public void AnOpenPauseMenuSilencesTheAmbienceInEveryScene()
        {
            GameAudioSettings.Configure(null, "MainMenu");
            GameAudioSettings.SetPauseMenuOpen(true);

            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.True);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("MainMenu"), Is.True);

            GameAudioSettings.SetPauseMenuOpen(false);
            Assert.That(GameAudioSettings.ShouldSuppressAmbience("Gameplay"), Is.False);
        }

        [Test]
        public void EveryAmbienceWordInASourceNameRoutesItToTheAmbienceBus()
        {
            foreach (string word in AmbienceWords)
            {
                AudioSource source = NewSource($"{word}Loop_01");
                Assert.That(GameAudioSettings.IsAmbience(source), Is.True, $"'{word}' missed.");
            }
        }

        [Test]
        public void TheAmbienceMatchIgnoresCase()
        {
            Assert.That(GameAudioSettings.IsAmbience(NewSource("Howling WIND")), Is.True);
        }

        [Test]
        public void AnEffectSourceStaysOnTheEffectBus()
        {
            Assert.That(GameAudioSettings.IsAmbience(NewSource("Footsteps")), Is.False);
            Assert.That(GameAudioSettings.IsAmbience(NewSource("Explosion", "Boom")), Is.False);
            Assert.That(GameAudioSettings.IsAmbience(NewSource("Hit", "Thud", "Player")), Is.False);
        }

        [Test]
        public void AClipNameAloneIsEnoughToCountAsAmbience()
        {
            AudioSource source = NewSource("Emitter", "ForestRainLoop");

            Assert.That(GameAudioSettings.IsAmbience(source), Is.True);
        }

        [Test]
        public void AParentNameAloneIsEnoughToCountAsAmbience()
        {
            AudioSource source = NewSource("Emitter", null, "MusicHolder");

            Assert.That(GameAudioSettings.IsAmbience(source), Is.True);
        }

        [Test]
        public void ShippedMixerAppliesVolumesAndRoutesLooseSources()
        {
            GameAudioSettings.Configure("Brocoli/Audio/BrocoliAudioMixer", "Brocoli_MainMenu");
            GameAudioSettings settings = NewSettings();
            settings.Awake();

            AudioSource ambience = NewSource("Ambient coverage loop");
            AudioSource effect = NewSource("Coverage impact");
            Invoke(settings, "RouteAllSources");
            Assert.That(ambience.outputAudioMixerGroup, Is.Not.Null);
            Assert.That(effect.outputAudioMixerGroup, Is.Not.Null);
            Assert.That(
                ambience.outputAudioMixerGroup,
                Is.Not.SameAs(effect.outputAudioMixerGroup)
            );

            Invoke(settings, "RouteAllSources");
            GameAudioSettings.SetPauseMenuOpen(true);
            Invoke(settings, "ApplyMixerVolumes");
            GameAudioSettings.SetPauseMenuOpen(false);

            LogAssert.Expect(
                LogType.Error,
                "[Audio Settings] Mixer group 'Coverage Missing Group' was not found."
            );
            Assert.That(Invoke(settings, "FindGroup", "Coverage Missing Group"), Is.Null);
        }

        private AudioSource NewSource(string name, string clipName = null, string parentName = null)
        {
            GameObject host = Track(new GameObject(name));
            if (parentName != null)
                host.transform.SetParent(Track(new GameObject(parentName)).transform);

            AudioSource source = host.AddComponent<AudioSource>();
            if (clipName != null)
            {
                AudioClip clip = AudioClip.Create(clipName, 64, 1, 8000, false);
                clips.Add(clip);
                source.clip = clip;
            }

            return source;
        }

        private static object Invoke(object target, string method, params object[] arguments) =>
            target
                .GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, arguments);
    }
}
