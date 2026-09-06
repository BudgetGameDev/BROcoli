using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class BrocoliSaveSlotTests
    {
        [Test]
        public void ReadinessBlocksAllRunMutationsWithoutChangingActiveSlot()
        {
            var original = CreateValidSave();
            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
            BrocoliSaveSystem.Save(original);
            int slot = BrocoliSaveSystem.ActiveSlot;
            string payload = PlayerPrefs.GetString(BrocoliSaveSystem.SlotKey(slot));
            BrocoliSaveSystem.BeginReadOnlyRun();
            try
            {
                var replacement = CreateValidSave();
                replacement.game.score = 999;
                BrocoliSaveSystem.Save(replacement);
                BrocoliSaveSystem.DeleteSave(slot);
                BrocoliSaveSystem.DeleteActiveSave();
                Assert.That(BrocoliSaveSystem.BeginNewGame(true), Is.False);
                Assert.That(BrocoliSaveSystem.BeginContinue(slot), Is.False);
                Assert.That(BrocoliSaveSystem.ActiveSlot, Is.EqualTo(-1));
                Assert.That(PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey), Is.EqualTo(slot));
                Assert.That(PlayerPrefs.GetString(BrocoliSaveSystem.SlotKey(slot)), Is.EqualTo(payload));
            }
            finally { BrocoliSaveSystem.EndReadOnlyRun(); }
            Assert.That(BrocoliSaveSystem.ActiveSlot, Is.EqualTo(slot));
        }

        [Test]
        public void ReadinessDoesNotMigrateOrDeleteUnreadableSaves()
        {
            PlayerPrefs.SetString(BrocoliSaveSystem.LegacySaveKey, "legacy-sentinel");
            PlayerPrefs.SetString(BrocoliSaveSystem.InterimSlotKeyPrefix + 2, "interim-sentinel");
            PlayerPrefs.SetString(BrocoliSaveSystem.SlotKey(3), "unreadable-sentinel");
            BrocoliSaveSystem.BeginReadOnlyRun();
            try
            {
                BrocoliSaveSystem.LoadAll();
                Assert.That(PlayerPrefs.GetString(BrocoliSaveSystem.LegacySaveKey), Is.EqualTo("legacy-sentinel"));
                Assert.That(PlayerPrefs.GetString(BrocoliSaveSystem.InterimSlotKeyPrefix + 2), Is.EqualTo("interim-sentinel"));
                Assert.That(PlayerPrefs.GetString(BrocoliSaveSystem.SlotKey(3)), Is.EqualTo("unreadable-sentinel"));
            }
            finally { BrocoliSaveSystem.EndReadOnlyRun(); }
        }

        [Test]
        public void RestoringInMemoryCheckpointKeepsGuardUntilExplicitCompletion()
        {
            var original = CreateValidSave();
            BrocoliSaveSystem.BeginReadOnlyRun();
            try
            {
                BrocoliSaveSystem.RestoreReadOnlyCheckpoint(original);
                Assert.That(BrocoliSaveSystem.TryGetPendingContinue(out var restored), Is.True);
                Assert.That(restored, Is.SameAs(original));
                BrocoliSaveSystem.FinishContinue();
                BrocoliSaveSystem.Save(original);
                Assert.That(BrocoliSaveSystem.ReadOnlyRun, Is.True);
                Assert.That(PlayerPrefs.HasKey(BrocoliSaveSystem.SlotKey(0)), Is.False);
            }
            finally { BrocoliSaveSystem.EndReadOnlyRun(); }
            Assert.That(BrocoliSaveSystem.TryGetPendingContinue(out _), Is.False);
        }
    }
}
