using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class BrocoliSaveSlotTests
    {
        [Test]
        public void SaveEntryGuardsAndFullSlotWarningAreCovered()
        {
            Assert.That(BrocoliSaveSystem.HasAnySave, Is.False);
            BrocoliSaveSystem.Save(null);

            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
            BrocoliRunSave invalid = CreateValidSave();
            invalid.player.health = float.NaN;
            LogAssert.Expect(
                LogType.Warning,
                "[Autosave] Refused to write an invalid run checkpoint."
            );
            BrocoliSaveSystem.Save(invalid);

            for (int slot = 0; slot < BrocoliSaveSystem.MaxSaves; slot++)
                WriteSave(slot, DateTime.UtcNow.AddMinutes(-slot));
            PlayerPrefs.DeleteKey(BrocoliSaveSystem.ActiveSlotKey);
            typeof(BrocoliSaveSystem)
                .GetField("warnedSlotsFull", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, false);
            LogAssert.Expect(
                LogType.Warning,
                "[Autosave] Every save slot is taken and this run claimed none, so it is not "
                    + "being checkpointed. Delete a save from the menu to free one."
            );
            BrocoliSaveSystem.Save(CreateValidSave());

            BrocoliRunSave nullableBoosts = CreateValidSave();
            nullableBoosts.player.temporaryBoosts = null;
            Assert.That(BrocoliSaveSystem.IsValid(nullableBoosts), Is.True);
        }

        [Test]
        public void DeletingAnArmedContinueClearsIt()
        {
            WriteSave(2, DateTime.UtcNow);
            Assert.That(BrocoliSaveSystem.BeginContinue(2), Is.True);
            BrocoliSaveSystem.DeleteSave(2);
            Assert.That(BrocoliSaveSystem.TryGetPendingContinue(out _), Is.False);
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void MainMenuLaunchPoliciesLoadValidRunsAndReportSlotExhaustion()
        {
            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
            BrocoliSaveSystem.Save(CreateValidSave());
            int slot = BrocoliSaveSystem.ActiveSlot;
            GameObject host = new("Coverage Main Menu Launch");
            try
            {
                MainMenu menu = host.AddComponent<MainMenu>();
                int loads = 0;
                Assert.That(menu.LoadSave(slot, () => loads++), Is.True);
                BrocoliSaveSystem.FinishContinue();
                Assert.That(MainMenu.LaunchNewDungeon(true, () => loads++), Is.True);
                Assert.That(loads, Is.EqualTo(2));

                for (int index = 0; index < BrocoliSaveSystem.MaxSaves; index++)
                    WriteSave(index, DateTime.UtcNow.AddMinutes(-index));
                Assert.That(MainMenu.LaunchNewDungeon(false, () => loads++), Is.False);
                Assert.That(loads, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
