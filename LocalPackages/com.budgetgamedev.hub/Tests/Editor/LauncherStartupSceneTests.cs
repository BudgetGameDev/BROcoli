using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// Build-list membership, which is the question a configured startup scene is
    /// actually judged on, and the registry entries that must not break the search
    /// for the scene's owner.
    /// </summary>
    public sealed class LauncherStartupSceneTests
    {
        private readonly HubTestGames games = new();

        [TearDown]
        public void DestroyGames() => games.DestroyAll();

        [Test]
        public void ABlankNameIsNeverInTheBuild()
        {
            Assert.That(LauncherStartup.IsSceneInBuild(null), Is.False);
            Assert.That(LauncherStartup.IsSceneInBuild(string.Empty), Is.False);
            Assert.That(LauncherStartup.IsSceneInBuild("   "), Is.False);
        }

        [Test]
        public void ABuiltSceneIsFoundIgnoringCaseAndSurroundingSpace()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");

            Assert.That(
                LauncherStartup.IsSceneInBuild($"  {scene.ToUpperInvariant()}  "),
                Is.True,
                "a hand-typed config value must not have to match the file exactly"
            );
        }

        [Test]
        public void ASceneOutsideTheBuildIsNotFound()
        {
            Assert.That(LauncherStartup.IsSceneInBuild("Never_Built_Scene"), Is.False);
        }

        [Test]
        public void WithNoWayToCheckTheBuildTheLauncherShowsThePicker()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Some_Scene"));

            LauncherStartup.Plan plan = LauncherStartup.Resolve("Some_Scene", null, null);

            Assert.That(plan.ShowsPicker, Is.True);
            Assert.That(plan.SceneName, Is.Null);
        }

        [Test]
        public void BrokenRegistryEntriesAreSkippedWhenLookingForTheOwner()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameDefinition sceneless = games.Add("ghost");
            HubTestGames.Set(sceneless, "sceneNames", null);

            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                scene,
                new List<GameDefinition> { null, sceneless },
                LauncherStartup.IsSceneInBuild
            );

            Assert.That(plan.ShowsPicker, Is.False, "the scene still opens");
            Assert.That(plan.Game, Is.Null, "a half-filled entry must not claim it or throw");
        }
    }
}
