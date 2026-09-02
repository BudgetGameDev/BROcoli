using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The two directors are what make a run more than combat. These cover the paths
    /// a healthy scene never takes: a probe against a system that is not there, a
    /// probe that throws, and a main menu that never shows up.
    /// </summary>
    public sealed class AutoplayDirectorTests
    {
        private const BindingFlags Members =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        private readonly List<GameObject> hosts = new();

        [SetUp]
        public void StartDriving() => SetAutoplayActive(true);

        [TearDown]
        public void DestroyHosts()
        {
            SetAutoplayActive(false);
            foreach (GameObject host in hosts)
                Object.DestroyImmediate(host);
            hosts.Clear();
        }

        private static void SetAutoplayActive(bool active) =>
            typeof(AutoplayController)
                .GetField("<IsActive>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, active);

        private T Create<T>(string name)
            where T : Component
        {
            GameObject host = new(name);
            hosts.Add(host);
            return host.AddComponent<T>();
        }

        [Test]
        public void ProbesAgainstAbsentSystemsDoNothingRatherThanThrow()
        {
            AutoplayFeatureDirector director = Create<AutoplayFeatureDirector>("Coverage probes");

            Invoke(director, "Update"); // before Start there is nothing to run
            Invoke(director, "Start");
            Invoke(director, "Update"); // still inside the warm-up

            Assert.That(director.CompletedSweeps, Is.Zero);
            foreach (string probe in ProbeNames)
                Invoke(director, probe);
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.InventoryOpened), Is.False);
        }

        [Test]
        public void TheSweepRunsEveryProbeAndThenRests()
        {
            AutoplayFeatureDirector director = Create<AutoplayFeatureDirector>("Coverage sweep");
            Invoke(director, "Start");

            // One extra step past the probe list is the wrap that closes a sweep.
            for (int step = 0; step <= ProbeNames.Length + 1; step++)
            {
                Set(director, "nextProbeTime", 0f);
                Invoke(director, "Update");
            }

            Assert.That(director.CompletedSweeps, Is.EqualTo(1));
        }

        [Test]
        public void AProbeThatThrowsIsReportedAndTheSweepCarriesOn()
        {
            LogAssert.Expect(LogType.Error, "[Autoplay] Feature probe failed: coverage");

            typeof(AutoplayFeatureDirector)
                .GetMethod("RunProbe", Members)
                .Invoke(
                    null,
                    new object[] { (Action)(() => throw new InvalidOperationException("coverage")) }
                );
        }

        [Test]
        public void AMissingMainMenuEventuallyGivesUpAndEntersTheDungeon()
        {
            AutoplaySessionDirector director = Create<AutoplaySessionDirector>("Coverage session");
            var loaded = new List<string>();
            director.LoadScene = loaded.Add;
            Invoke(director, "Start");

            Invoke(director, "Update"); // still inside the timeout, so nothing happens
            Assert.That(loaded, Is.Empty);

            SetAutoplayActive(false);
            Set(director, "deadline", -1f);
            Invoke(director, "Update");
            Assert.That(loaded, Is.Empty, "the director is inert outside an autoplay run");
            SetAutoplayActive(true);

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Entering the dungeon directly because the main menu never appeared."
            );
            Set(director, "deadline", -1f);
            Invoke(director, "Update");

            Assert.That(loaded, Is.EqualTo(new[] { AutoplaySessionDirector.DungeonScene }));
        }

        [Test]
        public void AMenuThatAcceptsPlayButNeverLoadsIsAlsoReported()
        {
            AutoplaySessionDirector director = Create<AutoplaySessionDirector>("Coverage press");
            var loaded = new List<string>();
            var pressed = new List<MainMenu>();
            director.LoadScene = loaded.Add;
            director.PressPlay = pressed.Add;
            MainMenu menu = Create<MainMenu>("Coverage main menu");
            Invoke(director, "Start");

            Invoke(director, "Update");

            Assert.That(pressed, Is.EqualTo(new[] { menu }));
            Assert.That(loaded, Is.Empty);

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Entering the dungeon directly because the main menu accepted Play "
                    + "but never loaded the dungeon."
            );
            Set(director, "deadline", -1f);
            Invoke(director, "Update");

            Assert.That(loaded, Is.EqualTo(new[] { AutoplaySessionDirector.DungeonScene }));
        }

        [Test]
        public void ThePreferencesTheDirectorMovedAreOnlyPutBackOnce()
        {
            const int owned = 4;
            int original = PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1);
            try
            {
                PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, owned);
                AutoplaySessionDirector director = Create<AutoplaySessionDirector>(
                    "Coverage preferences"
                );
                Invoke(director, "Start");
                PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, 9);

                director.RestorePreferences();
                Assert.That(
                    PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1),
                    Is.EqualTo(owned)
                );

                PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, 9);
                director.RestorePreferences();
                Assert.That(
                    PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1),
                    Is.EqualTo(9),
                    "a second restore must not undo whatever came after the first"
                );
            }
            finally
            {
                PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, original);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void TheEntrySceneFollowsWhetherTheTierDrivesTheMenus()
        {
            var loaded = new List<string>();
            AutoplayController straightIn = Create<AutoplayController>("Coverage entry");
            Set(straightIn, "_config", new AutoplayConfig { DriveMenus = false });

            straightIn.EnterGame(loaded.Add);

            Assert.That(loaded, Is.EqualTo(new[] { AutoplaySessionDirector.DungeonScene }));
            Assert.That(
                straightIn.GetComponent<AutoplaySessionDirector>(),
                Is.Null,
                "no menu to drive means no session director"
            );

            loaded.Clear();
            AutoplayController throughMenus = Create<AutoplayController>("Coverage menu entry");
            Set(throughMenus, "_config", new AutoplayConfig { DriveMenus = true });

            throughMenus.EnterGame(loaded.Add);

            Assert.That(loaded, Is.EqualTo(new[] { AutoplaySessionDirector.MenuScene }));
            Assert.That(throughMenus.GetComponent<AutoplaySessionDirector>(), Is.Not.Null);
            Assert.That(
                throughMenus.GetComponent<AutoplaySaveJourneyDirector>(),
                Is.Null,
                "walking the menus is not the same as making and losing a character"
            );

            loaded.Clear();
            AutoplayController journey = Create<AutoplayController>("Coverage journey entry");
            Set(
                journey,
                "_config",
                new AutoplayConfig { DriveMenus = true, ExerciseSaveJourney = true }
            );

            journey.EnterGame(loaded.Add);

            Assert.That(
                journey.GetComponent<AutoplaySaveJourneyDirector>(),
                Is.Not.Null,
                "the journey is stood up with the run, before the menu claims a save slot"
            );
        }

        private static readonly string[] ProbeNames =
        {
            "OpenInventory",
            "NavigateInventory",
            "EquipInventoryItem",
            "OpenMap",
            "PanMap",
            "CloseOverlay",
            "OpenPauseMenu",
            "OpenPauseSettings",
            "ResumeFromPause",
            "ProbeSaveRoundTrip",
        };

        private static object Invoke(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, Members).Invoke(target, arguments);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Members).SetValue(target, value);
    }
}
