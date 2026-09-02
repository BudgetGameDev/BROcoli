using System.IO;
using BudgetGameDev.Hub.Editor;
using NUnit.Framework;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// The rule deciding whether the editor's opening scene is a placeholder worth
    /// replacing with the launcher, and that the launcher is findable at all.
    /// </summary>
    public sealed class LauncherSceneDefaultTests
    {
        [Test]
        public void AnUntouchedEmptySceneIsReplaced()
        {
            Assert.That(LauncherSceneDefault.ShouldOpenLauncher(1, string.Empty, false), Is.True);
            Assert.That(LauncherSceneDefault.ShouldOpenLauncher(1, null, false), Is.True);
        }

        [Test]
        public void ARestoredSceneIsLeftAlone()
        {
            Assert.That(
                LauncherSceneDefault.ShouldOpenLauncher(
                    1,
                    "Packages/com.budgetgamedev.game.brocoli/Scenes/Brocoli_Dungeon_Common.unity",
                    false
                ),
                Is.False,
                "reopening the editor must not discard the scene someone was working in"
            );
        }

        [Test]
        public void AnEditedButUnsavedSceneIsLeftAlone()
        {
            Assert.That(
                LauncherSceneDefault.ShouldOpenLauncher(1, string.Empty, true),
                Is.False,
                "an unsaved scene with edits in it is work, not a placeholder"
            );
        }

        [Test]
        public void AMultiSceneSetupIsLeftAlone()
        {
            Assert.That(
                LauncherSceneDefault.ShouldOpenLauncher(2, string.Empty, false),
                Is.False,
                "a second loaded scene means the setup was arranged on purpose"
            );
        }

        [Test]
        public void TheLauncherSceneIsFound()
        {
            string launcher = HubBuildScenes.FindLauncherScene();

            Assert.That(launcher, Is.Not.Null, "the editor has nothing to open without it");
            Assert.That(
                Path.GetFileNameWithoutExtension(launcher),
                Is.EqualTo(GameSession.LauncherSceneName)
            );
        }
    }
}
