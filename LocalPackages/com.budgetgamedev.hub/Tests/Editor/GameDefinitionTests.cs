using NUnit.Framework;
using UnityEditor;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// A game's registry entry. Every field is optional except the entry point, and
    /// the fallbacks exist so a half-filled asset still lists something a human can
    /// recognise instead of a blank row.
    /// </summary>
    public sealed class GameDefinitionTests
    {
        private readonly HubTestGames games = new();

        [TearDown]
        public void DestroyGames() => games.DestroyAll();

        [Test]
        public void ABlankIdAndNameFallBackToTheAssetName()
        {
            GameDefinition game = games.Add("brocoli", "Brocoli_MainMenu");
            HubTestGames.Set(game, "id", "   ");
            HubTestGames.Set(game, "displayName", string.Empty);

            Assert.That(game.Id, Is.EqualTo("brocoli"), "the asset name is the fallback id");
            Assert.That(game.DisplayName, Is.EqualTo("brocoli"));
        }

        [Test]
        public void AuthoredValuesAreReportedAsWritten()
        {
            GameDefinition game = games.Named("alpha", "Alpha Quest", "Alpha_Menu", "Alpha_Level");
            HubTestGames.Set(game, "description", "A short blurb.");
            HubTestGames.Set(game, "sortOrder", 7);
            HubTestGames.Set(game, "mixerResourcePath", "Alpha/Mixer");

            Assert.That(game.Id, Is.EqualTo("alpha"));
            Assert.That(game.DisplayName, Is.EqualTo("Alpha Quest"));
            Assert.That(game.Description, Is.EqualTo("A short blurb."));
            Assert.That(game.SortOrder, Is.EqualTo(7));
            Assert.That(game.MixerResourcePath, Is.EqualTo("Alpha/Mixer"));
            Assert.That(game.MainMenuSceneName, Is.EqualTo("Alpha_Menu"));
            Assert.That(game.SceneNames, Is.EqualTo(new[] { "Alpha_Menu", "Alpha_Level" }));
            Assert.That(game.Icon, Is.Null, "an icon is optional");
            Assert.That(game.IsPlayable, Is.True);
        }

        [Test]
        public void AnEntryWithNoMainMenuSceneIsNotPlayable()
        {
            GameDefinition game = games.Add("broken");

            Assert.That(
                game.IsPlayable,
                Is.False,
                "the launcher lists it, disabled, so the setup mistake is visible"
            );
        }

        [Test]
        public void ValidatingRebuildsTheSceneListFromTheDraggedAssets()
        {
            SceneAsset menu = SceneAt(0);
            SceneAsset extra = SceneAt(1);
            Assume.That(extra, Is.Not.Null, "this needs two scenes in the build");
            GameDefinition game = games.Add("alpha", "Stale_Scene");
            HubTestGames.Set(game, "mainMenuScene", menu);
            HubTestGames.Set(game, "additionalScenes", new[] { extra, menu, null });

            game.OnValidate();

            Assert.That(game.MainMenuSceneName, Is.EqualTo(menu.name));
            Assert.That(
                game.SceneNames,
                Is.EqualTo(new[] { menu.name, extra.name }),
                "the main menu leads, and a scene listed twice appears once"
            );
            Assert.That(game.MainMenuScene, Is.SameAs(menu));
            Assert.That(game.AdditionalScenes.Length, Is.EqualTo(3), "empty slots are tolerated");
        }

        [Test]
        public void ValidatingWithNoDraggedSceneLeavesNothingToLaunch()
        {
            GameDefinition game = games.Add("alpha", "Stale_Scene");
            HubTestGames.Set(game, "id", string.Empty);
            game.name = "Alpha";

            game.OnValidate();

            Assert.That(
                game.Id,
                Is.EqualTo("alpha"),
                "a blank id is filled in from the asset name"
            );
            Assert.That(game.MainMenuSceneName, Is.Empty, "a stale name must not survive");
            Assert.That(game.SceneNames, Is.Empty);
            Assert.That(game.IsPlayable, Is.False);
        }

        private static SceneAsset SceneAt(int index)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            return index < scenes.Length
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[index].path)
                : null;
        }
    }
}
