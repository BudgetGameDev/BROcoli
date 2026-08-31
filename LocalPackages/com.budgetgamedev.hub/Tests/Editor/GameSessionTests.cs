using System.Collections.Generic;
using System.Text.RegularExpressions;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// The two transitions between the launcher and a game. Every refusal has to be
    /// reported and survivable: a game that cannot be opened must leave the player
    /// in the launcher rather than in a half-configured session.
    /// </summary>
    public sealed class GameSessionTests
    {
        private readonly HubTestGames games = new();
        private readonly List<string> loaded = new();
        private string previousLastPlayed;

        [SetUp]
        public void RecordScenesInsteadOfLoadingThem()
        {
            previousLastPlayed = GameSession.LastPlayedId;
            PlayerPrefs.SetString(GameSession.LastPlayedKey, string.Empty);
            GameSession.ResetSessionState();
            loaded.Clear();
            GameSession.SceneLoader = loaded.Add;
        }

        [TearDown]
        public void RestoreSessionState()
        {
            games.DestroyAll();
            GameSession.ResetSessionState();
            GameAudioSettings.Configure(null, null);
            Time.timeScale = 1f;
            PlayerPrefs.SetString(GameSession.LastPlayedKey, previousLastPlayed);
        }

        [Test]
        public void LaunchingNothingIsReportedAndRefused()
        {
            LogAssert.Expect(LogType.Error, new Regex("null game"));

            Assert.That(GameSession.Launch(null), Is.False);

            Assert.That(loaded, Is.Empty);
            Assert.That(GameSession.Active, Is.Null);
        }

        [Test]
        public void AGameWithNoMainMenuSceneIsReportedAndRefused()
        {
            GameDefinition game = games.Add("broken");
            LogAssert.Expect(LogType.Error, new Regex("no main menu scene"));

            Assert.That(GameSession.Launch(game), Is.False);

            Assert.That(GameSession.Active, Is.Null);
        }

        [Test]
        public void ASceneMissingFromTheBuildIsReportedAndRefused()
        {
            GameDefinition game = games.Add("alpha", "Never_Built_Scene");
            LogAssert.Expect(LogType.Error, new Regex("not in the build"));

            Assert.That(GameSession.Launch(game), Is.False);

            Assert.That(loaded, Is.Empty, "a missing scene must not blank the screen");
            Assert.That(GameSession.Active, Is.Null, "the refused game must not become active");
        }

        [Test]
        public void LaunchingAGameAppliesItsSetupAndOpensItsMainMenu()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameDefinition game = games.Add("alpha", scene);
            HubTestGames.Set(game, "mixerResourcePath", "Alpha/Mixer");
            Time.timeScale = 0f;

            Assert.That(GameSession.Launch(game), Is.True);

            Assert.That(GameSession.Active, Is.SameAs(game));
            Assert.That(GameSession.LastPlayedId, Is.EqualTo("alpha"));
            Assert.That(GameAudioSettings.MixerResourcePath, Is.EqualTo("Alpha/Mixer"));
            Assert.That(GameAudioSettings.MenuSceneName, Is.EqualTo(scene));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a game must not start paused");
            Assert.That(loaded, Is.EqualTo(new[] { scene }));
        }

        [Test]
        public void AnExplicitSceneIsTrimmedAndUsedInsteadOfTheMainMenu()
        {
            string menu = HubTestScenes.At(0);
            string other = HubTestScenes.At(1);
            Assume.That(other, Is.Not.Null, "this needs a second scene in the build");
            GameDefinition game = games.Add("alpha", menu, other);

            Assert.That(GameSession.Launch(game, $"  {other}  "), Is.True);

            Assert.That(loaded, Is.EqualTo(new[] { other }));
            Assert.That(
                GameAudioSettings.MenuSceneName,
                Is.EqualTo(menu),
                "the game's own menu scene still decides where ambience is muted"
            );
        }

        [Test]
        public void ReturningToTheLauncherClearsThePerGameSetup()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameDefinition game = games.Add("alpha", scene);
            HubTestGames.Set(game, "mixerResourcePath", "Alpha/Mixer");
            GameSession.Launch(game);
            loaded.Clear();
            Time.timeScale = 0f;

            GameSession.ReturnToLauncher();

            Assert.That(GameSession.Active, Is.Null);
            Assert.That(GameAudioSettings.MixerResourcePath, Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a paused game must not freeze the hub");
            Assert.That(loaded, Is.EqualTo(new[] { GameSession.LauncherSceneName }));
        }

        [Test]
        public void ANewRunForgetsTheRunningGameButNotThePreselection()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameSession.Launch(games.Add("alpha", scene));

            GameSession.ResetSessionState();

            Assert.That(GameSession.Active, Is.Null, "a new run starts in the launcher");
            Assert.That(
                GameSession.LastPlayedId,
                Is.EqualTo("alpha"),
                "the preselection is meant to survive between runs"
            );
        }

        [Test]
        public void NothingPlayedYetPreselectsNothing()
        {
            PlayerPrefs.DeleteKey(GameSession.LastPlayedKey);

            Assert.That(GameSession.LastPlayedId, Is.Empty);
        }
    }
}
