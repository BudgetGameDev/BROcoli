using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Step = BudgetGameDev.Games.Brocoli.AutoplaySaveJourneyDirector.Step;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The journey is the only part of the harness that ever leaves the dungeon, so
    /// it is the only part that tests the menu's save list, the continue path, and
    /// what a death costs. These drive it against the real save system with a stand-in
    /// only for the two scenes, which is what makes "the second character did not
    /// overwrite the first" a statement about the save slots rather than about a fake.
    /// </summary>
    public sealed partial class AutoplaySaveJourneyTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Pumps allowed before a journey that is going nowhere is called stuck.</summary>
        private const int Frames = 400;

        private readonly Dictionary<string, string> backup = new();
        private int backedUpActiveSlot;

        private GameObject host;
        private AutoplaySaveJourneyDirector director;
        private string scene;
        private BrocoliRunSave live;

        /// <summary>
        /// These write real save slots, so the machine's own runs are put aside for
        /// the duration and handed back afterwards.
        /// </summary>
        [SetUp]
        public void TakeSavesAside()
        {
            backup.Clear();
            for (int slot = 0; slot < BrocoliSaveSystem.MaxSaves; slot++)
            {
                string key = BrocoliSaveSystem.SlotKey(slot);
                if (PlayerPrefs.HasKey(key))
                    backup[key] = PlayerPrefs.GetString(key);
                PlayerPrefs.DeleteKey(key);
            }

            backedUpActiveSlot = PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1);
            PlayerPrefs.DeleteKey(BrocoliSaveSystem.ActiveSlotKey);
            SetAutoplayActive(true);
            AutoplayFeatureLog.Reset();
        }

        [TearDown]
        public void GiveSavesBack()
        {
            if (host != null)
                Object.DestroyImmediate(host);
            host = null;
            director = null;

            SetAutoplayActive(false);
            AutoplayFeatureLog.Reset();
            BrocoliSaveSystem.FinishContinue();

            for (int slot = 0; slot < BrocoliSaveSystem.MaxSaves; slot++)
                PlayerPrefs.DeleteKey(BrocoliSaveSystem.SlotKey(slot));
            foreach (KeyValuePair<string, string> entry in backup)
                PlayerPrefs.SetString(entry.Key, entry.Value);

            PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, backedUpActiveSlot);
            PlayerPrefs.Save();
        }

        [Test]
        public void TwoCharactersAreMadeResumedAndOnlyTheOneDiedInIsLost()
        {
            StartJourney();
            ExpectTheJourneyToFinish();

            Pump();

            Assert.That(director.Current, Is.EqualTo(Step.Done));
            Assert.That(
                AutoplayFeatureLog.Count(AutoplayFeatures.SaveResumed),
                Is.EqualTo(2),
                "both characters were resumed and both came back as themselves"
            );
            Assert.That(
                AutoplayFeatureLog.Reached(AutoplayFeatures.SaveCheckpointed),
                Is.True,
                "the runs checkpointed themselves rather than being written by the harness"
            );
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.SaveSlotsIndependent), Is.True);
            Assert.That(AutoplayFeatureLog.Reached(AutoplayFeatures.SaveDropped), Is.True);
            Assert.That(
                AutoplayFeatureLog.Reached(AutoplayFeatures.SaveSurvivedAnotherRunsDeath),
                Is.True
            );

            List<BrocoliRunSave> left = BrocoliSaveSystem.LoadAll();
            Assert.That(
                left,
                Has.Count.EqualTo(1),
                "the run that died is gone and the other is not"
            );
            Assert.That(left[0].dungeon.seed, Is.EqualTo(FirstSeed));
        }

        [Test]
        public void EveryStepTheJourneyOwesIsReachedInOrder()
        {
            StartJourney();
            ExpectTheJourneyToFinish();
            var seen = new List<Step>();

            for (int frame = 0; frame < Frames && director.Current != Step.Done; frame++)
            {
                if (seen.Count == 0 || seen[seen.Count - 1] != director.Current)
                    seen.Add(director.Current);
                director.RunStep();
            }

            Assert.That(
                seen,
                Is.EqualTo(
                    new[]
                    {
                        Step.WalkFirstRun,
                        Step.LeaveFirstRun,
                        Step.ResumeFirstRun,
                        Step.VerifyFirstRun,
                        Step.ParkFirstRun,
                        Step.StartSecondRun,
                        Step.WalkSecondRun,
                        Step.LeaveSecondRun,
                        Step.ResumeSecondRun,
                        Step.VerifySecondRun,
                        Step.DieOnPurpose,
                    }
                )
            );
        }

        [Test]
        public void TheSlotsTheJourneyClaimedAreHandedBackAndThePlayersAreLeftAlone()
        {
            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
            BrocoliSaveSystem.Save(FreshRun(PlayersOwnSeed));
            int playersSlot = BrocoliSaveSystem.ActiveSlot;
            PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, playersSlot);

            StartJourney();
            ExpectTheJourneyToFinish();
            Pump();
            Assert.That(BrocoliSaveSystem.LoadAll(), Has.Count.EqualTo(2), "before the hand-back");

            // The session director puts the player's own pointer back on its way out.
            // Whichever order the two are torn down in, the hand-back must leave the
            // pointer exactly as it found it rather than clearing it again.
            PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, playersSlot);
            director.FreeClaimedSlots();

            List<BrocoliRunSave> left = BrocoliSaveSystem.LoadAll();
            Assert.That(left, Has.Count.EqualTo(1));
            Assert.That(left[0].dungeon.seed, Is.EqualTo(PlayersOwnSeed));
            Assert.That(left[0].slot, Is.EqualTo(playersSlot));
            Assert.That(
                PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1),
                Is.EqualTo(playersSlot),
                "freeing the run's own slots did not clear the pointer along with them"
            );
        }

        [Test]
        public void TheJourneyIsInertOutsideAnAutoplayRun()
        {
            StartJourney();
            SetAutoplayActive(false);

            for (int frame = 0; frame < Frames; frame++)
                Invoke(director, "Update");

            Assert.That(director.Current, Is.EqualTo(Step.WalkFirstRun));
        }
    }
}
