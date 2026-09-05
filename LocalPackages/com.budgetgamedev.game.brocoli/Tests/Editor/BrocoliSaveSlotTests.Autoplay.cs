using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The autoplay save probe writes real checkpoints, so it shares this fixture's
    /// habit of putting the machine's own runs aside for the duration.
    /// </summary>
    public sealed partial class BrocoliSaveSlotTests
    {
        [Test]
        public void TheSaveProbeNeedsARunWorthCheckpointing()
        {
            Assert.That(BrocoliSaveProbeCapture(null), Is.False);
        }

        [Test]
        public void TheSaveProbeRoundTripsThroughAFreeSlotAndLeavesNoTrace()
        {
            const int unrelatedSlot = 7;
            PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, unrelatedSlot);

            Assert.That(BrocoliSaveProbeCapture(CreateValidSave()), Is.True);

            Assert.That(
                PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1),
                Is.EqualTo(unrelatedSlot),
                "the probe put the active slot pointer back"
            );
            Assert.That(BrocoliSaveSystem.LoadAll(), Is.Empty, "the probe freed its own slot");
        }

        [Test]
        public void TheSaveProbeRefusesToEvictARunWhenEverySlotIsTaken()
        {
            for (int run = 0; run < BrocoliSaveSystem.MaxSaves; run++)
            {
                Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
                BrocoliSaveSystem.Save(CreateValidSave());
            }

            Assert.That(
                BrocoliSaveProbeCapture(CreateValidSave()),
                Is.True,
                "it falls back to a serialization round trip"
            );
            Assert.That(BrocoliSaveSystem.LoadAll(), Has.Count.EqualTo(BrocoliSaveSystem.MaxSaves));
        }

        [Test]
        public void ACheckpointThatLosesRunStateDoesNotCountAsARoundTrip()
        {
            BrocoliRunSave written = CreateValidSave();
            BrocoliRunSave drifted = CreateValidSave();
            drifted.dungeon.seed = written.dungeon.seed + 1;

            Assert.That(AutoplaySaveProbe.Matches(written, written), Is.True);
            Assert.That(AutoplaySaveProbe.Matches(written, drifted), Is.False);
            Assert.That(AutoplaySaveProbe.Matches(null, written), Is.False);
            Assert.That(AutoplaySaveProbe.Matches(written, null), Is.False);
            Assert.That(
                AutoplaySaveProbe.Matches(written, new BrocoliRunSave { player = null }),
                Is.False
            );
        }

        /// <summary>
        /// The probe exists because autoplay switches checkpointing off. The one run
        /// that needs it back is the save journey, whose whole subject is the slots --
        /// and which hands back every slot it claimed on its way out.
        /// </summary>
        [Test]
        public void CheckpointingIsOffForAnOrdinaryBotRunAndOnForTheSaveJourney()
        {
            try
            {
                AutoplayController.InjectDiagnostics();
                SetAutoplayFlag("IsActive", true);
                SetAutoplayFlag("CheckpointsEnabled", false);
                BrocoliAutosaveController.EnsurePresent();
                Assert.That(
                    Object.FindAnyObjectByType<BrocoliAutosaveController>(),
                    Is.Null,
                    "a throwaway run must not claim one of the player's ten slots"
                );

                SetAutoplayFlag("CheckpointsEnabled", true);
                BrocoliAutosaveController.EnsurePresent();
                BrocoliAutosaveController checkpointing =
                    Object.FindAnyObjectByType<BrocoliAutosaveController>();
                Assert.That(
                    checkpointing,
                    Is.Not.Null,
                    "the journey is the one run that has to write real checkpoints"
                );
                Object.DestroyImmediate(checkpointing.gameObject);
            }
            finally
            {
                SetAutoplayFlag("IsActive", false);
                SetAutoplayFlag("CheckpointsEnabled", false);
                GameplayDiagnostics.AllowCheckpoint = null;
            }
        }

        private static void SetAutoplayFlag(string property, bool value) =>
            typeof(AutoplayController)
                .GetField(
                    $"<{property}>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic
                )
                .SetValue(null, value);

        /// <summary>Runs the probe against a fixed capture result.</summary>
        private static bool BrocoliSaveProbeCapture(BrocoliRunSave captured) =>
            AutoplaySaveProbe.TryRoundTrip(
                (out BrocoliRunSave save) =>
                {
                    save = captured;
                    return captured != null;
                }
            );
    }
}
