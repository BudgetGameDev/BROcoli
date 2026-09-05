using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// The picker itself: what it lists, what it highlights, and what Select does.
    /// The launcher is the one screen every build has to be able to open, so an
    /// awkward registry -- no games, a game with no scenes, a game with no blurb --
    /// has to produce a usable screen rather than an exception.
    /// </summary>
    public sealed class GameLauncherTests : GameLauncherFixture
    {
        [Test]
        public void NoInstalledGamesExplainsItselfAndHidesSelect()
        {
            GameLauncher launcher = LauncherListing();

            Assert.That(launcher.EmptyLabel.gameObject.activeSelf, Is.True);
            Assert.That(launcher.EmptyLabel.text, Does.Contain("manifest.json"));
            Assert.That(
                launcher.SelectButton.gameObject.activeSelf,
                Is.False,
                "a Select button with nothing to select is a dead control"
            );
            Assert.That(launcher.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void EveryRegisteredGameGetsARow()
        {
            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", "Beta_Menu")
            );

            Assert.That(launcher.Entries.Count, Is.EqualTo(2));
            Assert.That(launcher.EmptyLabel.gameObject.activeSelf, Is.False);
            Assert.That(launcher.SelectButton.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ARowCarriesTheGamesOwnNameAndBlurb()
        {
            GameDefinition game = Games.Named("alpha", "Alpha Quest", "Alpha_Menu");
            HubTestGames.Set(game, "description", "A short blurb.");

            GameLauncher launcher = LauncherListing(game);
            Transform row = launcher.Entries[0].Button.transform;

            Assert.That(launcher.Entries[0].Label.text, Is.EqualTo("Alpha Quest"));
            Assert.That(
                row.Find("Description").GetComponent<Text>().text,
                Is.EqualTo("A short blurb.")
            );
        }

        [Test]
        public void AGameWithNoBlurbGetsNoDescriptionRow()
        {
            GameLauncher launcher = LauncherListing(Games.Add("alpha", "Alpha_Menu"));

            Assert.That(
                launcher.Entries[0].Button.transform.Find("Description"),
                Is.Null,
                "an empty blurb must not leave a gap in the row"
            );
        }

        [Test]
        public void AGamesIconIsShownBesideItsName()
        {
            GameDefinition game = Games.Add("alpha", "Alpha_Menu");
            Sprite icon = NewIcon();
            HubTestGames.Set(game, "icon", icon);

            GameLauncher launcher = LauncherListing(game);
            Transform row = launcher.Entries[0].Button.transform;

            Assert.That(row.Find("Icon"), Is.Not.Null);
            Assert.That(row.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(icon));
        }

        [Test]
        public void AGameWithNoMainMenuIsListedButCannotBeStarted()
        {
            GameLauncher launcher = LauncherListing(Games.Add("broken"));
            Transform row = launcher.Entries[0].Button.transform;

            Assert.That(
                launcher.Entries[0].Button.interactable,
                Is.False,
                "a broken entry stays visible so the setup mistake is obvious"
            );
            Assert.That(
                row.Find("Description").GetComponent<Text>().text,
                Does.Contain("Unavailable")
            );
            Assert.That(launcher.SelectButton.interactable, Is.False);
        }

        [Test]
        public void TheGamePlayedLastIsHighlighted()
        {
            PlayerPrefs.SetString(GameSession.LastPlayedKey, "beta");

            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", "Beta_Menu")
            );

            Assert.That(launcher.SelectedIndex, Is.EqualTo(1));
            Assert.That(
                launcher.Entries[1].Background.color,
                Is.Not.EqualTo(launcher.Entries[0].Background.color),
                "the highlighted row must look different"
            );
        }

        [Test]
        public void AGameThatIsNoLongerInstalledFallsBackToTheFirstPlayableOne()
        {
            PlayerPrefs.SetString(GameSession.LastPlayedKey, "removed");

            GameLauncher launcher = LauncherListing(
                Games.Add("broken"),
                Games.Add("beta", "Beta_Menu")
            );

            Assert.That(launcher.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void WithNothingPlayableTheFirstRowIsHighlightedAnyway()
        {
            GameLauncher launcher = LauncherListing(Games.Add("broken"), Games.Add("also-broken"));

            Assert.That(launcher.SelectedIndex, Is.EqualTo(0));
            Assert.That(launcher.SelectButton.interactable, Is.False);
        }

        [Test]
        public void ClickingARowHighlightsIt()
        {
            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", "Beta_Menu")
            );

            launcher.Entries[1].Button.onClick.Invoke();

            Assert.That(launcher.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void HighlightingNothingDisablesSelectAndClearsEveryRow()
        {
            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", "Beta_Menu")
            );
            Color idle = launcher.Entries[1].Background.color;

            launcher.Select(-1);

            Assert.That(launcher.SelectButton.interactable, Is.False);
            Assert.That(
                launcher.Entries[0].Background.color,
                Is.EqualTo(idle),
                "the previously highlighted row must go back to looking idle"
            );
        }

        [Test]
        public void SelectStartsTheHighlightedGame()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameDefinition game = Games.Add("alpha", scene);
            GameLauncher launcher = LauncherListing(game);

            launcher.SelectButton.onClick.Invoke();

            Assert.That(LoadedScenes, Is.EqualTo(new[] { scene }));
            Assert.That(GameSession.Active, Is.SameAs(game));
        }

        [Test]
        public void SelectDoesNothingWithAnEmptyList()
        {
            GameLauncher launcher = LauncherListing();

            launcher.LaunchSelected();

            Assert.That(LoadedScenes, Is.Empty);
        }

        [Test]
        public void SelectRefusesAGameWithNoMainMenu()
        {
            GameLauncher launcher = LauncherListing(Games.Add("broken"));

            launcher.LaunchSelected();

            Assert.That(LoadedScenes, Is.Empty, "an unplayable row must not report an error");
        }

        [Test]
        public void StartBuildsThePickerFromTheInstalledGames()
        {
            GameLauncher launcher = NewLauncher();

            StartLauncher(launcher);

            Assert.That(launcher.SelectButton, Is.Not.Null, "the picker must have been built");
            Assert.That(
                launcher.Entries.Count,
                Is.EqualTo(GameCatalog.All.Count),
                "the launcher lists whatever the build actually ships"
            );
            Assert.That(LoadedScenes, Is.Empty);
        }
    }
}
