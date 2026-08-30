using BudgetGameDev.Hub;
using BudgetGameDev.Hub.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Hub.Tests
{
    /// <summary>
    /// The config format, and the committed file itself. A bad value is
    /// recoverable at runtime -- the launcher ignores it -- but silently ignoring
    /// a scene someone meant to boot is its own bug, so it is caught here.
    /// </summary>
    public sealed class LauncherConfigTests
    {
        [Test]
        public void EmptyAndCommentOnlyTextConfigureNothing()
        {
            Assert.That(LauncherConfig.Parse(string.Empty).StartupScene, Is.Empty);
            Assert.That(
                LauncherConfig.Parse("# startupScene = Brocoli_Dungeon\n\n   \n").StartupScene,
                Is.Empty,
                "a commented-out setting must stay off"
            );
        }

        [Test]
        public void SettingIsReadAndTrimmed()
        {
            Assert.That(
                LauncherConfig.Parse("  startupScene =  Brocoli_Dungeon  ").StartupScene,
                Is.EqualTo("Brocoli_Dungeon")
            );
        }

        [Test]
        public void TrailingCommentIsNotPartOfTheValue()
        {
            Assert.That(
                LauncherConfig
                    .Parse("startupScene = Brocoli_Dungeon # boot the dungeon")
                    .StartupScene,
                Is.EqualTo("Brocoli_Dungeon")
            );
        }

        [Test]
        public void CarriageReturnsAreTrimmed()
        {
            Assert.That(
                LauncherConfig.Parse("startupScene = Brocoli_Dungeon\r\n").StartupScene,
                Is.EqualTo("Brocoli_Dungeon"),
                "a file saved with CRLF endings must behave the same"
            );
        }

        [Test]
        public void UnknownKeyIsReportedAndSkipped()
        {
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("startScene")
            );

            Assert.That(LauncherConfig.Parse("startScene = Typo").StartupScene, Is.Empty);
        }

        [Test]
        public void MalformedLineIsReportedAndSkipped()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("expected"));

            Assert.That(
                LauncherConfig.Parse("startupScene Brocoli_Dungeon").StartupScene,
                Is.Empty
            );
        }

        /// <summary>
        /// The launcher can only read a copy under Assets/, so the root file
        /// reaching the player depends entirely on this sync running.
        /// </summary>
        [Test]
        public void SyncMirrorsTheRootFileIntoResources()
        {
            LauncherConfigSync.Sync();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string root = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath)!.FullName,
                LauncherConfigSync.SourcePath
            );
            Assert.That(System.IO.File.Exists(root), Is.True, "the authored config must exist");

            var mirrored = Resources.Load<TextAsset>(LauncherConfig.ResourceName);
            Assert.That(mirrored, Is.Not.Null, "a player reads this copy, not the root file");
            Assert.That(
                mirrored.text,
                Does.EndWith(System.IO.File.ReadAllText(root)),
                "the copy must carry the root file's content verbatim after its header"
            );
        }

        [Test]
        public void CommittedConfigLoads()
        {
            Assert.That(LauncherConfig.Load(), Is.Not.Null);
        }

        [Test]
        public void CommittedStartupSceneIsEmptyOrInTheBuild()
        {
            string configured = LauncherConfig.Load().StartupScene;
            if (string.IsNullOrWhiteSpace(configured))
                Assert.Pass("No startup scene configured; the launcher shows its game list.");

            string[] built = System.Array.ConvertAll(
                EditorBuildSettings.scenes,
                scene => System.IO.Path.GetFileNameWithoutExtension(scene.path)
            );

            Assert.That(
                built,
                Contains.Item(configured.Trim()),
                $"LauncherConfig names '{configured}', which is not a scene in the build. "
                    + "Fix the name or clear it; the launcher would ignore it at runtime."
            );
        }
    }
}
