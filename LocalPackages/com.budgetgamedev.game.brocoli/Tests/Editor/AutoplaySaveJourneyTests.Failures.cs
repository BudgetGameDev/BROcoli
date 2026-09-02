using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Step = BudgetGameDev.Games.Brocoli.AutoplaySaveJourneyDirector.Step;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// What the journey does when the game underneath it is broken. Each of these is
    /// a defect the harness exists to name out loud rather than to survive quietly:
    /// a second character written over the first, a resume that came back as somebody
    /// else, a death that took both runs with it.
    /// </summary>
    public sealed partial class AutoplaySaveJourneyTests
    {
        [Test]
        public void ASecondCharacterThatLandsInTheFirstOnesSlotIsReported()
        {
            StartJourney();

            // A menu that handed out the slot the first run is already holding.
            director.World.StartAnotherRun = () =>
            {
                live = FreshRun(SecondSeed);
                scene = AutoplaySessionDirector.DungeonScene;
                return true;
            };

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because the second run claimed slot 0, "
                    + "which the first run holds."
            );
            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
            Assert.That(
                AutoplayFeatureLog.Reached(AutoplayFeatures.SaveSlotsIndependent),
                Is.False
            );
        }

        [Test]
        public void AResumeThatComesBackAsSomeoneElseIsReported()
        {
            StartJourney();

            // A continue that rebuilt the dungeon from the wrong seed, which on screen
            // is a resumed run that opens in a dungeon the player has never seen.
            director.World.ResumeRun = slot =>
            {
                Assert.That(BrocoliSaveSystem.BeginContinue(slot), Is.True);
                BrocoliSaveSystem.TryGetPendingContinue(out BrocoliRunSave resumed);
                live = Clone(resumed);
                live.dungeon.seed += 1;
                BrocoliSaveSystem.FinishContinue();
                scene = AutoplaySessionDirector.DungeonScene;
                return true;
            };

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because the resumed run came back with "
                    + $"a different dungeon ({FirstSeed} became {FirstSeed + 1})."
            );
            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.SaveResumed), Is.False);
        }

        [Test]
        public void ADeathThatTakesTheOtherCharacterTooIsReported()
        {
            StartJourney();
            director.World.Die = () =>
            {
                // A death that deleted the slots rather than the run being played.
                for (int slot = 0; slot < BrocoliSaveSystem.MaxSaves; slot++)
                    BrocoliSaveSystem.DeleteSave(slot);
                return true;
            };

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because dying in slot 1 also took the "
                    + "run in slot 0."
            );
            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
            Assert.That(
                AutoplayFeatureLog.Reached(AutoplayFeatures.SaveSurvivedAnotherRunsDeath),
                Is.False
            );
        }

        [Test]
        public void AJourneyWithNowhereToPutTwoRunsSaysSoRatherThanEvictingOne()
        {
            for (int run = 0; run < BrocoliSaveSystem.MaxSaves - 1; run++)
            {
                Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
                BrocoliSaveSystem.Save(FreshRun(FirstSeed + run));
            }

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because the journey needs two free save "
                    + "slots and found 1; delete a run from the menu, or run this tier on a "
                    + "fresh profile."
            );
            BuildDirector();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
            Assert.That(
                BrocoliSaveSystem.LoadAll(),
                Has.Count.EqualTo(BrocoliSaveSystem.MaxSaves - 1),
                "the runs that were already there are still there"
            );
        }

        [Test]
        public void AStepThatNeverFinishesIsReportedRatherThanWaitedOnForever()
        {
            StartJourney();
            Set(director, "stepDeadline", -1f);

            LogAssert.Expect(
                LogType.Error,
                "[Autoplay] Abandoning the save journey because WalkFirstRun did not finish "
                    + "within 90s."
            );
            Invoke(director, "Update");

            Assert.That(director.Current, Is.EqualTo(Step.Done));
        }

        [Test]
        public void AResumeIsJudgedOnEverythingARunIsRebuiltFrom()
        {
            BrocoliRunSave written = FreshRun(FirstSeed);

            Assert.That(
                AutoplaySaveJourneyDirector.ResumedFrom(written, Clone(written), out _),
                Is.True
            );
            AssertResumeDiffers(written, save => save.player.level += 1f, "level");
            AssertResumeDiffers(written, save => save.player.experience += 5f, "experience");
            AssertResumeDiffers(written, save => save.dungeon.seed += 1, "dungeon");
            AssertResumeDiffers(written, save => save.dungeon.roomsVisited += 1, "rooms visited");
            AssertResumeDiffers(written, save => save.game.enemiesKilled += 1, "enemies killed");
            AssertResumeDiffers(
                written,
                save => save.player.health = save.player.maxHealth,
                "health"
            );
            AssertResumeDiffers(
                written,
                save => save.playerPosition += Vector3.right * 8f,
                "position"
            );

            Assert.That(
                AutoplaySaveJourneyDirector.ResumedFrom(written, null, out _),
                Is.False,
                "a resume that produced nothing at all is not a resume"
            );
            Assert.That(AutoplaySaveJourneyDirector.ResumedFrom(null, written, out _), Is.False);
        }

        [Test]
        public void ATickOfPlayBetweenTheRestoreAndTheCheckIsNotALostSave()
        {
            BrocoliRunSave written = FreshRun(FirstSeed);
            BrocoliRunSave settled = Clone(written);
            settled.player.health -= 0.5f;
            settled.playerPosition += new Vector3(0.2f, 0f, -0.2f);

            Assert.That(AutoplaySaveJourneyDirector.ResumedFrom(written, settled, out _), Is.True);
        }

        private static void AssertResumeDiffers(
            BrocoliRunSave written,
            System.Action<BrocoliRunSave> drift,
            string named
        )
        {
            BrocoliRunSave drifted = Clone(written);
            drift(drifted);

            Assert.That(
                AutoplaySaveJourneyDirector.ResumedFrom(written, drifted, out string difference),
                Is.False,
                named
            );
            Assert.That(difference, Does.Contain(named));
        }
    }
}
