using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// Moving the highlight without a mouse. The repeat clock is passed in rather
    /// than read from Time, so the hold-to-repeat behaviour can be checked without
    /// waiting on it.
    /// </summary>
    public sealed class GameLauncherNavigationTests : GameLauncherFixture
    {
        private GameLauncher ThreeGames() =>
            LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", "Beta_Menu"),
                Games.Add("gamma", "Gamma_Menu")
            );

        [Test]
        public void HoldingADirectionMovesOnceThenRepeats()
        {
            GameLauncher launcher = ThreeGames();

            launcher.HandleNavigation(-1f, 0f);
            Assert.That(launcher.SelectedIndex, Is.EqualTo(1), "a fresh press moves at once");

            launcher.HandleNavigation(-1f, 0.2f);
            Assert.That(launcher.SelectedIndex, Is.EqualTo(1), "held too briefly to repeat");

            launcher.HandleNavigation(-1f, 0.4f);
            Assert.That(launcher.SelectedIndex, Is.EqualTo(2), "the repeat delay has passed");

            launcher.HandleNavigation(-1f, 0.5f);
            Assert.That(
                launcher.SelectedIndex,
                Is.EqualTo(2),
                "the repeat interval gates the rest"
            );

            launcher.HandleNavigation(-1f, 0.56f);
            Assert.That(launcher.SelectedIndex, Is.EqualTo(0), "and it wraps past the last row");
        }

        [Test]
        public void ReleasingLetsTheNextPressMoveImmediately()
        {
            GameLauncher launcher = ThreeGames();

            launcher.HandleNavigation(1f, 0f);
            Assert.That(launcher.SelectedIndex, Is.EqualTo(2), "up wraps past the first row");

            launcher.HandleNavigation(0f, 0.05f);
            launcher.HandleNavigation(1f, 0.1f);

            Assert.That(
                launcher.SelectedIndex,
                Is.EqualTo(1),
                "a new press must not be throttled by the previous hold"
            );
        }

        [Test]
        public void MovingSkipsGamesThatCannotBeStarted()
        {
            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("broken"),
                Games.Add("gamma", "Gamma_Menu")
            );

            launcher.MoveSelection(1);

            Assert.That(launcher.SelectedIndex, Is.EqualTo(2), "the unplayable row is passed over");
        }

        [Test]
        public void MovingWithNothingPlayableStaysPut()
        {
            GameLauncher launcher = LauncherListing(Games.Add("broken"), Games.Add("also-broken"));

            launcher.MoveSelection(1);

            Assert.That(launcher.SelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void MovingInAnEmptyListDoesNothing()
        {
            GameLauncher launcher = LauncherListing();

            launcher.MoveSelection(1);

            Assert.That(launcher.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void MovingNowhereDoesNothing()
        {
            GameLauncher launcher = ThreeGames();

            launcher.MoveSelection(0);

            Assert.That(launcher.SelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void TheLauncherSilencesNavigationEventsAndHandsThemBack()
        {
            EventSystem eventSystem = NewEventSystem();
            GameLauncher launcher = NewLauncher();

            launcher.SuppressEventSystemNavigation(eventSystem);
            Assert.That(
                eventSystem.sendNavigationEvents,
                Is.False,
                "one confirm press must not also be submitted by the UI module"
            );

            launcher.OnDestroy();

            Assert.That(
                eventSystem.sendNavigationEvents,
                Is.True,
                "the launcher must not leave navigation off behind it"
            );
        }

        [Test]
        public void AnEventSystemThatAlreadySilencedNavigationIsLeftAsItWas()
        {
            EventSystem eventSystem = NewEventSystem();
            eventSystem.sendNavigationEvents = false;
            GameLauncher launcher = NewLauncher();

            launcher.SuppressEventSystemNavigation(eventSystem);
            launcher.OnDestroy();

            Assert.That(
                eventSystem.sendNavigationEvents,
                Is.False,
                "the launcher must only undo what it did itself"
            );
        }

        [Test]
        public void NoEventSystemAtAllIsNotAProblem()
        {
            EventSystem eventSystem = NewEventSystem();
            GameLauncher launcher = NewLauncher();

            launcher.SuppressEventSystemNavigation(null);
            launcher.OnDestroy();

            Assert.That(eventSystem.sendNavigationEvents, Is.True, "nothing was silenced");
        }

        [Test]
        public void ARowAboveTheViewportScrollsBackDown()
        {
            float correction = GameLauncher.ScrollCorrection(Row(120f, 200f), Viewport);

            Assert.That(correction, Is.EqualTo(-100f));
        }

        [Test]
        public void ARowBelowTheViewportScrollsBackUp()
        {
            float correction = GameLauncher.ScrollCorrection(Row(-300f, -200f), Viewport);

            Assert.That(correction, Is.EqualTo(200f));
        }

        [Test]
        public void ARowAlreadyInViewIsLeftWhereItIs()
        {
            Assert.That(GameLauncher.ScrollCorrection(Row(-50f, 50f), Viewport), Is.EqualTo(0f));
        }

        [Test]
        public void HighlightingARowOutOfViewScrollsItIn()
        {
            GameLauncher launcher = ThreeGames();
            FreezeLayout(launcher);
            RectTransform content = launcher.ListScroll.content;
            ((RectTransform)launcher.Entries[2].Button.transform).anchoredPosition = new Vector2(
                0f,
                -5000f
            );

            launcher.Select(2);

            Assert.That(
                content.anchoredPosition.y,
                Is.GreaterThan(0f),
                "the list must scroll to keep controller selection visible"
            );
        }

        [Test]
        public void HighlightingAVisibleRowLeavesTheScrollAlone()
        {
            GameLauncher launcher = ThreeGames();
            FreezeLayout(launcher);
            RectTransform content = launcher.ListScroll.content;

            launcher.Select(0);

            Assert.That(content.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        private static Rect Viewport => new(-50f, -100f, 100f, 200f);

        private static Bounds Row(float bottom, float top) =>
            new(new Vector3(0f, (bottom + top) * 0.5f, 0f), new Vector3(1f, top - bottom, 1f));
    }
}
