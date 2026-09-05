using System;
using System.Collections.Generic;
using System.Reflection;
using BudgetGameDev.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// The launcher must open whatever happens, so every unusable startup value
    /// has to fall back to the picker rather than fail.
    /// </summary>
    public sealed class LauncherStartupTests
    {
        private readonly List<GameDefinition> games = new();

        /// <summary>
        /// Builds a definition by setting its backing fields directly. Going
        /// through SerializedObject would fire OnValidate, which rebuilds
        /// sceneNames from the editor-only SceneAsset references and would wipe
        /// exactly the values under test.
        /// </summary>
        private GameDefinition Game(string id, params string[] scenes)
        {
            var game = ScriptableObject.CreateInstance<GameDefinition>();
            game.name = id;
            Set(game, "id", id);
            Set(game, "mainMenuSceneName", scenes.Length > 0 ? scenes[0] : string.Empty);
            Set(game, "sceneNames", scenes);
            games.Add(game);
            return game;
        }

        private static void Set(GameDefinition game, string field, object value) =>
            typeof(GameDefinition)
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(game, value);

        [TearDown]
        public void DestroyGames()
        {
            foreach (GameDefinition game in games)
                UnityEngine.Object.DestroyImmediate(game);
            games.Clear();
        }

        private static Func<string, bool> InBuild(params string[] scenes) =>
            scene => Array.IndexOf(scenes, scene) >= 0;

        [Test]
        public void NoConfiguredSceneShowsThePicker()
        {
            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                null,
                new List<GameDefinition>(),
                InBuild("Anything")
            );

            Assert.That(plan.ShowsPicker, Is.True);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void BlankConfiguredSceneShowsThePicker(string configured)
        {
            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                configured,
                new List<GameDefinition>(),
                InBuild("Anything")
            );

            Assert.That(plan.ShowsPicker, Is.True);
        }

        [Test]
        public void SceneMissingFromTheBuildIsIgnored()
        {
            GameDefinition brocoli = Game("brocoli", "Brocoli_MainMenu_Common");
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("Renamed_Scene")
            );

            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                "Renamed_Scene",
                new List<GameDefinition> { brocoli },
                InBuild("Brocoli_MainMenu_Common")
            );

            Assert.That(plan.ShowsPicker, Is.True, "an unknown scene must not strand the build");
        }

        [Test]
        public void ConfiguredSceneBootsAndCarriesItsOwningGame()
        {
            GameDefinition brocoli = Game(
                "brocoli",
                "Brocoli_MainMenu_Common",
                "Brocoli_Dungeon_Common"
            );

            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                "Brocoli_Dungeon_Common",
                new List<GameDefinition> { brocoli },
                InBuild("Brocoli_MainMenu_Common", "Brocoli_Dungeon_Common")
            );

            Assert.That(plan.ShowsPicker, Is.False);
            Assert.That(plan.SceneName, Is.EqualTo("Brocoli_Dungeon_Common"));
            Assert.That(plan.Game, Is.SameAs(brocoli), "the owner supplies the per-game setup");
        }

        [Test]
        public void OwnerMatchIgnoresCaseAndSurroundingSpace()
        {
            GameDefinition brocoli = Game("brocoli", "Brocoli_MainMenu_Common");

            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                "  brocoli_mainmenu_common  ",
                new List<GameDefinition> { brocoli },
                InBuild("brocoli_mainmenu_common")
            );

            Assert.That(plan.SceneName, Is.EqualTo("brocoli_mainmenu_common"));
            Assert.That(plan.Game, Is.SameAs(brocoli));
        }

        [Test]
        public void SceneNoGameClaimsStillBootsWithoutAnOwner()
        {
            LauncherStartup.Plan plan = LauncherStartup.Resolve(
                "Standalone_Sandbox",
                new List<GameDefinition>(),
                InBuild("Standalone_Sandbox")
            );

            Assert.That(plan.ShowsPicker, Is.False);
            Assert.That(plan.Game, Is.Null);
        }
    }
}
