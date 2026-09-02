using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Step = BudgetGameDev.Games.Brocoli.AutoplaySaveJourneyDirector.Step;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// A scene load takes frames and a restore finishes a frame after the scene does,
    /// so most of what the journey does is wait. These are the waits: a step that has
    /// asked for something and not had it yet has to look different from the same step
    /// having been answered wrongly.
    /// </summary>
    public sealed partial class AutoplaySaveJourneyTests
    {
        [Test]
        public void TheJourneyWaitsForTheGameInsteadOfRacingIt()
        {
            StartJourney();

            // A run with nothing written for it has nothing to come back to, so the
            // walk stays where it is rather than leaving for a menu it could not
            // return from.
            director.World.Checkpoint = () => { };
            Pump();
            Assert.That(director.Current, Is.EqualTo(Step.WalkFirstRun));
            WireWorld();

            PumpUntil(Step.VerifyFirstRun);
            WaitOnARestoreThatHasNotFinished();
            PumpUntil(Step.ParkFirstRun);
            WaitOnAMenuThatHasNotLoaded();
            PumpUntil(Step.StartSecondRun);
            WaitOnADungeonThatHasNotLoaded();

            ExpectTheJourneyToFinish();
            PumpUntil(Step.DieOnPurpose);
            WaitOnADeathThatHasNotReachedTheSave();

            Pump();
            Assert.That(director.Current, Is.EqualTo(Step.Done));
        }

        /// <summary>
        /// The dungeon is not back yet; then it is back but still rebuilding the run;
        /// then the run is back but the player is not in it.
        /// </summary>
        private void WaitOnARestoreThatHasNotFinished()
        {
            scene = AutoplaySessionDirector.MenuScene;
            director.RunStep();

            scene = AutoplaySessionDirector.DungeonScene;
            Assert.That(BrocoliSaveSystem.BeginContinue(0), Is.True, "a checkpoint still pending");
            director.RunStep();
            BrocoliSaveSystem.FinishContinue();

            director.World.CaptureLiveRun = (out BrocoliRunSave save) =>
            {
                save = null;
                return false;
            };
            director.RunStep();

            Assert.That(director.Current, Is.EqualTo(Step.VerifyFirstRun));
            WireWorld();
        }

        private void WaitOnAMenuThatHasNotLoaded()
        {
            director.World.QuitToMenu = () => true; // pressed; the menu is still coming
            director.RunStep();
            director.RunStep();
            Assert.That(director.Current, Is.EqualTo(Step.ParkFirstRun));

            CheckpointTheLiveRun();
            scene = AutoplaySessionDirector.MenuScene;
            WireWorld();
        }

        private void WaitOnADungeonThatHasNotLoaded()
        {
            director.World.StartAnotherRun = () => true;
            director.RunStep();
            director.RunStep();
            Assert.That(director.Current, Is.EqualTo(Step.StartSecondRun));

            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
            live = FreshRun(SecondSeed);
            scene = AutoplaySessionDirector.DungeonScene;
            WireWorld();
        }

        /// <summary>The blow landed; the save it costs is dropped a moment later.</summary>
        private void WaitOnADeathThatHasNotReachedTheSave()
        {
            director.World.Die = () => true;
            director.RunStep();
            director.RunStep();
            Assert.That(director.Current, Is.EqualTo(Step.DieOnPurpose));

            BrocoliSaveSystem.DeleteActiveSave();
        }

        [Test]
        public void ASecondCharacterThatClaimedNoSlotIsReported()
        {
            StartJourney();
            PumpUntil(Step.StartSecondRun);
            director.World.StartAnotherRun = () =>
            {
                PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, -1);
                scene = AutoplaySessionDirector.DungeonScene;
                return true;
            };

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because the second run claimed no save "
                    + "slot."
            );
            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
        }

        [Test]
        public void AFirstCharacterTheSecondOneDeletedIsReported()
        {
            StartJourney();
            PumpUntil(Step.VerifySecondRun);
            director.RunStep(); // the second character came back as itself
            BrocoliSaveSystem.DeleteSave(0);

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because starting and playing a second "
                    + "run emptied slot 0."
            );
            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
        }

        [Test]
        public void AFirstCharacterTheSecondOneWroteOverIsReported()
        {
            StartJourney();
            PumpUntil(Step.VerifySecondRun);
            director.RunStep();
            OverwriteSlot(0, SecondSeed);

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because the run parked in slot 0 came "
                    + "back with a different level (2 became 6)."
            );
            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
        }

        [Test]
        public void ASurvivingCharacterTheDeathChangedIsReported()
        {
            StartJourney();
            PumpUntil(Step.DieOnPurpose);
            director.World.Die = () =>
            {
                BrocoliSaveSystem.DeleteActiveSave();
                OverwriteSlot(0, SecondSeed);
                return true;
            };

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because the run that survived the death "
                    + "has a different level (2 became 6)."
            );
            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
        }

        [Test]
        public void TheJourneyDrivesItselfFromTheOrdinaryUpdateLoop()
        {
            StartJourney();

            for (int frame = 0; frame < Frames && director.Current == Step.WalkFirstRun; frame++)
                Invoke(director, "Update");

            Assert.That(director.Current, Is.EqualTo(Step.LeaveFirstRun));
        }

        [Test]
        public void QuittingTheApplicationHandsTheClaimedSlotsBackToo()
        {
            StartJourney();
            Assert.That(BrocoliSaveSystem.LoadAll(), Is.Empty, "nothing is written yet");
            CheckpointTheLiveRun();
            Assert.That(BrocoliSaveSystem.LoadAll(), Has.Count.EqualTo(1));

            Invoke(director, "OnApplicationQuit");

            Assert.That(BrocoliSaveSystem.LoadAll(), Is.Empty);
        }

        /// <summary>
        /// A step's press against a game that has nothing to press. The journey reads
        /// these as "not yet" and tries again, because that is what every one of them
        /// is for the frames a scene takes to load.
        /// </summary>
        [Test]
        public void PressesAgainstAScreenThatIsNotThereReportThatTheyDidNotLand()
        {
            StartJourney();

            Assert.That((bool)Call(director, "PressNewRun"), Is.False, "no menu to start one in");
            Assert.That((bool)Call(director, "PressPlayOnRun", 0), Is.False, "no save list");
            Assert.That((bool)Call(director, "PressQuitToMenu"), Is.False, "no pause menu");
            Assert.That((bool)Call(director, "TakeAFatalHit"), Is.False, "no player to lose");

            GameObject bare = new("Coverage Player") { tag = "Player" };
            try
            {
                Assert.That(
                    (bool)Call(director, "TakeAFatalHit"),
                    Is.False,
                    "a player object with no health to take is nothing to hit"
                );
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        /// <summary>Puts a different character in a slot, the way a lost save reads.</summary>
        private static void OverwriteSlot(int slot, int seed)
        {
            BrocoliRunSave replacement = FreshRun(seed);
            replacement.slot = slot;
            replacement.savedAtTicks = System.DateTime.UtcNow.Ticks;
            PlayerPrefs.SetString(
                BrocoliSaveSystem.SlotKey(slot),
                BrocoliSaveSystem.Serialize(replacement)
            );
            PlayerPrefs.Save();
        }

        private void PumpUntil(Step step)
        {
            for (int frame = 0; frame < Frames && director.Current != step; frame++)
                director.RunStep();

            Assert.That(director.Current, Is.EqualTo(step), $"the journey reached {step}");
        }
    }
}
