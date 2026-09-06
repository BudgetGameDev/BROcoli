using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonAmbienceIntegrationTests
    {
        [Test]
        public void PlayerOwnsExactlyOneSoundscapeAndDeathDisablesIt()
        {
            var player = new GameObject("Dungeon ambience ownership test");
            player.SetActive(false);
            try
            {
                var audio = player.AddComponent<PlayerAudioHandler>();
                audio.EnsureDungeonAmbience();
                audio.EnsureDungeonAmbience();
                var soundscapes = player.GetComponentsInChildren<ProceduralDungeonAmbience>(true);
                Assert.That(soundscapes.Length, Is.EqualTo(1));
                Assert.That(soundscapes[0].transform.parent, Is.EqualTo(player.transform));
                Assert.That(soundscapes[0].enabled, Is.True);
                audio.StopAllAmbient();
                Assert.That(soundscapes[0].enabled, Is.False);
                audio.EnsureDungeonAmbience();
                Assert.That(
                    soundscapes[0].enabled,
                    Is.False,
                    "Initialization cannot restart the death ambience."
                );
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
