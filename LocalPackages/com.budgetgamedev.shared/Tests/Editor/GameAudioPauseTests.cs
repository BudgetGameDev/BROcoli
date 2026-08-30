using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class GameAudioPauseTests
    {
        [TearDown]
        public void TearDown()
        {
            GameAudioSettings.SetPauseMenuOpen(false);
        }

        [Test]
        public void PauseMenuSuspendsAndRestoresGameAudio()
        {
            GameAudioSettings.SetPauseMenuOpen(true);
            Assert.That(AudioListener.pause, Is.True);

            GameAudioSettings.SetPauseMenuOpen(false);
            Assert.That(AudioListener.pause, Is.False);
        }
    }
}
