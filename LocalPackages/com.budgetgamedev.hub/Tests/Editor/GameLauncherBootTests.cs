using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// Booting straight into a configured scene. The rule that matters is that this
    /// happens at most once per run: a player who leaves a booted game would
    /// otherwise be thrown back into it and could never reach the game list.
    /// </summary>
    public sealed class GameLauncherBootTests : GameLauncherFixture
    {
        [Test]
        public void NoConfiguredSceneShowsThePicker()
        {
            Assert.That(GameLauncher.TryBootConfiguredGame(string.Empty), Is.False);
            Assert.That(LoadedScenes, Is.Empty);
        }

        [Test]
        public void AConfiguredSceneOpensItsOwningGameExactlyOnce()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameDefinition game = Games.Add("alpha", scene);
            GameCatalog.cached = Games.All;

            Assert.That(GameLauncher.TryBootConfiguredGame(scene), Is.True);
            Assert.That(GameSession.Active, Is.SameAs(game), "the owner's setup is applied");
            Assert.That(LoadedScenes, Is.EqualTo(new[] { scene }));

            Assert.That(
                GameLauncher.TryBootConfiguredGame(scene),
                Is.False,
                "booting twice would make the game list unreachable"
            );
            Assert.That(LoadedScenes.Count, Is.EqualTo(1));
        }

        [Test]
        public void AConfiguredSceneNoGameClaimsStillOpens()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameCatalog.cached = System.Array.Empty<GameDefinition>();
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(scene)));

            Assert.That(GameLauncher.TryBootConfiguredGame(scene), Is.True);

            Assert.That(LoadedScenes, Is.EqualTo(new[] { scene }));
            Assert.That(GameSession.Active, Is.Null, "there is no per-game setup to apply");
        }

        [Test]
        public void ASceneMissingFromTheBuildFallsBackToThePicker()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Renamed_Scene"));

            Assert.That(GameLauncher.TryBootConfiguredGame("Renamed_Scene"), Is.False);

            Assert.That(LoadedScenes, Is.Empty, "a stale config must not strand the build");
        }

        [Test]
        public void BootingResetsTheTimeScale()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameCatalog.cached = System.Array.Empty<GameDefinition>();
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(scene)));
            Time.timeScale = 0f;

            GameLauncher.TryBootConfiguredGame(scene);

            Assert.That(Time.timeScale, Is.EqualTo(1f), "a booted scene must not start frozen");
        }
    }
}
