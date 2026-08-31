using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// Discovery and ordering of the installed games. The catalog is scanned from
    /// Resources rather than listed anywhere, so the invariants it has to hold up --
    /// a stable order, and colliding ids being visible -- are checked here against a
    /// known set as well as against whatever this build actually ships.
    /// </summary>
    public sealed class GameCatalogTests
    {
        private readonly HubTestGames games = new();

        [TearDown]
        public void RestoreTheRealCatalog()
        {
            GameCatalog.Invalidate();
            games.DestroyAll();
        }

        [Test]
        public void GamesAreOrderedBySortOrderThenDisplayName()
        {
            GameDefinition zebra = games.Named("zebra", "Zebra");
            GameDefinition apple = games.Named("apple", "apple");
            GameDefinition first = games.Named("first", "Anything");
            HubTestGames.Set(first, "sortOrder", -1);

            GameDefinition[] ordered = GameCatalog.Order(new[] { zebra, apple, first });

            Assert.That(
                Array.ConvertAll(ordered, game => game.Id),
                Is.EqualTo(new[] { "first", "apple", "zebra" }),
                "sort order wins, and ties fall back to display name ignoring case"
            );
        }

        [Test]
        public void CollidingIdsAreReportedRatherThanSilentlyMerged()
        {
            GameDefinition first = games.Named("twin", "First", "First_Menu");
            GameDefinition second = games.Named("TWIN", "Second", "Second_Menu");
            LogAssert.Expect(LogType.Error, new Regex("Duplicate game id"));

            GameDefinition[] ordered = GameCatalog.Order(new[] { first, second });

            Assert.That(
                ordered.Length,
                Is.EqualTo(2),
                "the collision is reported; neither entry is dropped"
            );
        }

        [Test]
        public void TheCatalogIsScannedOnceAndRescannedAfterInvalidating()
        {
            GameCatalog.Invalidate();
            IReadOnlyList<GameDefinition> first = GameCatalog.All;

            Assert.That(GameCatalog.All, Is.SameAs(first), "the Resources scan must not repeat");

            GameCatalog.Invalidate();

            Assert.That(
                GameCatalog.All,
                Is.Not.SameAs(first),
                "a newly imported game has to be able to show up"
            );
        }

        [Test]
        public void FindMatchesTheIdIgnoringCase()
        {
            GameCatalog.cached = new[] { games.Add("alpha", "Alpha_Menu") };

            Assert.That(GameCatalog.Find("ALPHA"), Is.Not.Null);
            Assert.That(GameCatalog.Find("missing"), Is.Null);
        }

        [Test]
        public void EveryShippedGameHasItsOwnId()
        {
            GameCatalog.Invalidate();

            Assert.That(
                GameCatalog.All.Select(game => game.Id).ToArray(),
                Is.Unique,
                "two games sharing an id would share save keys and the last-played record"
            );
        }
    }
}
