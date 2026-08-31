using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the cached shared source the UI plays through: it is built once, every
    /// sound reuses it, and the reset seam lets a new session rebuild it.
    /// </summary>
    public sealed class ProceduralUIAudioPlaybackTests
    {
        [SetUp]
        public void ClearCachedAudio()
        {
            ProceduralUIAudio.Reset();
        }

        [TearDown]
        public void DropCachedAudio()
        {
            ProceduralUIAudio.Reset();
        }

        [Test]
        public void PrewarmingBuildsTheSharedSourceAndEveryClip()
        {
            ProceduralUIAudio.PrewarmAll();

            AudioSource source = ProceduralUIAudio.sharedAudioSource;
            Assert.That(source, Is.Not.Null, "Pre-warming created no source.");
            Assert.That(source.gameObject.name, Is.EqualTo("UIAudio"));
            Assert.That(source.playOnAwake, Is.False, "The shared source would fire on load.");
            Assert.That(source.spatialBlend, Is.EqualTo(0f), "UI sound must not be positional.");
            Assert.That(
                source.ignoreListenerPause,
                Is.True,
                "The menus must still click while the game is paused."
            );

            Assert.That(ProceduralUIAudio.hoverClip.name, Is.EqualTo("UIHover"));
            Assert.That(ProceduralUIAudio.selectClip.name, Is.EqualTo("UISelect"));
            Assert.That(ProceduralUIAudio.levelUpSelectClip.name, Is.EqualTo("UILevelUpSelect"));
        }

        [Test]
        public void PrewarmingTwiceRegeneratesNothing()
        {
            ProceduralUIAudio.PrewarmAll();
            AudioSource source = ProceduralUIAudio.sharedAudioSource;
            AudioClip hover = ProceduralUIAudio.hoverClip;

            ProceduralUIAudio.PrewarmAll();

            Assert.That(ProceduralUIAudio.sharedAudioSource, Is.SameAs(source));
            Assert.That(ProceduralUIAudio.hoverClip, Is.SameAs(hover));
        }

        [Test]
        public void EverySoundSharesOneSourceAndItsOwnClip()
        {
            ProceduralUIAudio.PlayHover();
            AudioSource source = ProceduralUIAudio.sharedAudioSource;
            Assert.That(source, Is.Not.Null, "Playing a sound created no source.");

            ProceduralUIAudio.PlaySelect();
            ProceduralUIAudio.PlayLevelUpSelect();

            Assert.That(ProceduralUIAudio.sharedAudioSource, Is.SameAs(source));
            Assert.That(
                ProceduralUIAudio.selectClip,
                Is.Not.SameAs(ProceduralUIAudio.hoverClip),
                "Select and hover share one clip."
            );
            Assert.That(
                ProceduralUIAudio.levelUpSelectClip,
                Is.Not.SameAs(ProceduralUIAudio.selectClip),
                "Level-up and select share one clip."
            );
        }

        [Test]
        public void PlayingASoundFirstBuildsTheCache()
        {
            ProceduralUIAudio.PlaySelect();
            Assert.That(ProceduralUIAudio.selectClip, Is.Not.Null);

            ProceduralUIAudio.Reset();
            Assert.That(ProceduralUIAudio.selectClip, Is.Null, "Reset kept a stale clip.");

            ProceduralUIAudio.PlayLevelUpSelect();
            Assert.That(
                ProceduralUIAudio.selectClip,
                Is.Not.Null,
                "The first sound after a reset must rebuild the whole cache."
            );
        }

        [Test]
        public void ResetDestroysTheHostObjectSoTheNextCallRebuildsIt()
        {
            ProceduralUIAudio.PrewarmAll();
            AudioSource source = ProceduralUIAudio.sharedAudioSource;

            ProceduralUIAudio.Reset();

            Assert.That(ProceduralUIAudio.sharedAudioSource, Is.Null);
            Assert.That(ProceduralUIAudio.hoverClip, Is.Null);
            Assert.That(ProceduralUIAudio.levelUpSelectClip, Is.Null);
            Assert.That(source == null, Is.True, "The old UIAudio object outlived the reset.");

            ProceduralUIAudio.PrewarmAll();
            Assert.That(ProceduralUIAudio.sharedAudioSource, Is.Not.Null);
            Assert.That(
                ProceduralUIAudio.sharedAudioSource,
                Is.Not.SameAs(source),
                "A destroyed source was handed out again."
            );
        }

        [Test]
        public void ResettingTwiceIsHarmless()
        {
            ProceduralUIAudio.Reset();
            ProceduralUIAudio.Reset();

            Assert.That(ProceduralUIAudio.sharedAudioSource, Is.Null);
        }
    }
}
