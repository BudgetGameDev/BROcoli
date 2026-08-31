using NUnit.Framework;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// What the launcher does with one sampled frame of input.
    /// </summary>
    /// <remarks>
    /// The devices themselves are not driven here. A confirm press is a transition
    /// between two frames, and edit mode has no frames, so the launcher samples the
    /// devices in one place and decides from plain values everywhere else -- and it
    /// is those values that are supplied below.
    /// </remarks>
    public sealed class GameLauncherInputTests : GameLauncherFixture
    {
        [Test]
        public void WithNoDevicesTheSampledFrameIsIdle()
        {
            GameLauncher.NavigationInput input = GameLauncher.ReadDevices(null, null);

            Assert.That(input.Up, Is.False);
            Assert.That(input.Down, Is.False);
            Assert.That(input.Stick, Is.EqualTo(0f));
            Assert.That(input.Submit, Is.False);
            Assert.That(
                GameLauncher.NavigationAxis(input),
                Is.EqualTo(0f),
                "an unplugged machine must not drift through the list"
            );
        }

        [Test]
        public void KeyboardUpAndDownDriveTheAxis()
        {
            Assert.That(GameLauncher.NavigationAxis(Frame(up: true)), Is.EqualTo(1f));
            Assert.That(GameLauncher.NavigationAxis(Frame(down: true)), Is.EqualTo(-1f));
        }

        [Test]
        public void UpWinsWhenBothKeysAreHeld()
        {
            Assert.That(
                GameLauncher.NavigationAxis(Frame(up: true, down: true)),
                Is.EqualTo(1f),
                "two opposite keys must settle on one direction, not oscillate"
            );
        }

        [Test]
        public void AControllerPastHalfwayOverridesTheKeyboard()
        {
            Assert.That(
                GameLauncher.NavigationAxis(Frame(up: true, stick: -0.9f)),
                Is.EqualTo(-1f)
            );
            Assert.That(GameLauncher.NavigationAxis(Frame(stick: 0.7f)), Is.EqualTo(1f));
        }

        [Test]
        public void ARestingControllerLeavesTheKeyboardAlone()
        {
            Assert.That(
                GameLauncher.NavigationAxis(Frame(up: true, stick: 0.2f)),
                Is.EqualTo(1f),
                "stick drift must not cancel a held key"
            );
            Assert.That(GameLauncher.NavigationAxis(Frame(stick: -0.4f)), Is.EqualTo(0f));
        }

        [Test]
        public void AnIdleFrameChangesNothing()
        {
            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", "Beta_Menu")
            );

            launcher.Apply(Frame(), 0f);

            Assert.That(launcher.SelectedIndex, Is.EqualTo(0));
            Assert.That(LoadedScenes, Is.Empty);
        }

        [Test]
        public void PressingDownMovesTheHighlight()
        {
            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", "Beta_Menu")
            );

            launcher.Apply(Frame(down: true), 0f);

            Assert.That(launcher.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmingStartsTheHighlightedGame()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameLauncher launcher = LauncherListing(Games.Add("alpha", scene));

            launcher.Apply(Frame(submit: true), 0f);

            Assert.That(LoadedScenes, Is.EqualTo(new[] { scene }));
        }

        [Test]
        public void OneFrameCanBothMoveAndConfirm()
        {
            string scene = HubTestScenes.First();
            Assume.That(scene, Is.Not.Null, "the build must contain at least one scene");
            GameLauncher launcher = LauncherListing(
                Games.Add("alpha", "Alpha_Menu"),
                Games.Add("beta", scene)
            );

            launcher.Apply(Frame(down: true, submit: true), 0f);

            Assert.That(launcher.SelectedIndex, Is.EqualTo(1));
            Assert.That(
                LoadedScenes,
                Is.EqualTo(new[] { scene }),
                "the move is applied before the confirm, so it starts the new row"
            );
        }

        [Test]
        public void ReadingTheLiveDevicesLeavesAnUntouchedLauncherAlone()
        {
            GameLauncher launcher = LauncherListing(Games.Add("alpha", "Alpha_Menu"));

            launcher.Update();

            Assert.That(launcher.SelectedIndex, Is.EqualTo(0));
            Assert.That(LoadedScenes, Is.Empty);
        }

        private static GameLauncher.NavigationInput Frame(
            bool up = false,
            bool down = false,
            float stick = 0f,
            bool submit = false
        ) => new(up, down, stick, submit);
    }
}
