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
