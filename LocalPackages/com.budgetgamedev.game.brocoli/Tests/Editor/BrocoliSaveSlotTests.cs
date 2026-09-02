using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class BrocoliSaveSlotTests
    {
        private readonly Dictionary<string, string> backup = new();
        private int backedUpActiveSlot;

        /// <summary>
        /// These tests write real save slots, so the machine's own runs are put
        /// aside for the duration and handed back afterwards.
        /// </summary>
        [SetUp]
        public void TakeSavesAside()
        {
            backup.Clear();
            foreach (string key in StorageKeys())
            {
                if (PlayerPrefs.HasKey(key))
                    backup[key] = PlayerPrefs.GetString(key);
                PlayerPrefs.DeleteKey(key);
            }

            backedUpActiveSlot = PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1);
            PlayerPrefs.DeleteKey(BrocoliSaveSystem.ActiveSlotKey);
        }

        [TearDown]
        public void GiveSavesBack()
        {
            foreach (string key in StorageKeys())
                PlayerPrefs.DeleteKey(key);

            foreach (KeyValuePair<string, string> entry in backup)
                PlayerPrefs.SetString(entry.Key, entry.Value);

            PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, backedUpActiveSlot);
            PlayerPrefs.Save();
        }

        [Test]
        public void TenRunsFitAndTheEleventhIsRefused()
        {
            for (int run = 0; run < BrocoliSaveSystem.MaxSaves; run++)
            {
                Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True, $"run {run}");
                BrocoliSaveSystem.Save(CreateValidSave());
            }

            Assert.That(BrocoliSaveSystem.LoadAll(), Has.Count.EqualTo(BrocoliSaveSystem.MaxSaves));
            Assert.That(BrocoliSaveSystem.CanCreateSave, Is.False);
            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.False);
        }

        [Test]
        public void DeletingARunFreesItsSlotForANewOne()
        {
            for (int run = 0; run < BrocoliSaveSystem.MaxSaves; run++)
            {
                BrocoliSaveSystem.BeginNewGame(false);
                BrocoliSaveSystem.Save(CreateValidSave());
            }

            BrocoliSaveSystem.DeleteSave(4);

            Assert.That(BrocoliSaveSystem.CanCreateSave, Is.True);
            Assert.That(BrocoliSaveSystem.BeginNewGame(false), Is.True);
            Assert.That(BrocoliSaveSystem.ActiveSlot, Is.EqualTo(4));
        }

        [Test]
        public void SavesAreListedMostRecentlyPlayedFirst()
        {
            WriteSave(0, DateTime.UtcNow.AddHours(-2));
            WriteSave(1, DateTime.UtcNow.AddMinutes(-5));
            WriteSave(2, DateTime.UtcNow.AddDays(-3));

            List<BrocoliRunSave> saves = BrocoliSaveSystem.LoadAll();

            Assert.That(saves.ConvertAll(save => save.slot), Is.EqualTo(new[] { 1, 0, 2 }));
        }

        [Test]
        public void ResumingARunMovesItToTheTopOfTheList()
        {
            WriteSave(0, DateTime.UtcNow.AddHours(-2));
            WriteSave(1, DateTime.UtcNow.AddMinutes(-5));

            Assert.That(BrocoliSaveSystem.BeginContinue(0), Is.True);

            Assert.That(BrocoliSaveSystem.ActiveSlot, Is.EqualTo(0));
            Assert.That(BrocoliSaveSystem.LoadAll()[0].slot, Is.EqualTo(0));
            Assert.That(BrocoliSaveSystem.TryGetPendingContinue(out _), Is.True);
            BrocoliSaveSystem.FinishContinue();
        }

        [Test]
        public void DyingDropsOnlyTheRunBeingPlayed()
        {
            WriteSave(0, DateTime.UtcNow.AddHours(-2));
            WriteSave(1, DateTime.UtcNow.AddMinutes(-5));
            BrocoliSaveSystem.BeginContinue(1);
            BrocoliSaveSystem.FinishContinue();

            BrocoliSaveSystem.DeleteActiveSave();

            Assert.That(
                BrocoliSaveSystem.LoadAll().ConvertAll(save => save.slot),
                Is.EqualTo(new[] { 0 })
            );
            Assert.That(BrocoliSaveSystem.ActiveSlot, Is.EqualTo(-1));

            // Dying again with nothing being played costs nobody their run.
            BrocoliSaveSystem.DeleteActiveSave();
            Assert.That(
                BrocoliSaveSystem.LoadAll().ConvertAll(save => save.slot),
                Is.EqualTo(new[] { 0 })
            );
        }

        [Test]
        public void ARunFromBeforeSaveSlotsIsCarriedIntoOne()
        {
            BrocoliRunSave legacy = CreateValidSave();
            legacy.version = 1;
            legacy.game.score = 4321;
            PlayerPrefs.SetString(
                BrocoliSaveSystem.LegacySaveKey,
                BrocoliSaveSystem.Serialize(legacy)
            );

            List<BrocoliRunSave> saves = BrocoliSaveSystem.LoadAll();

            Assert.That(saves, Has.Count.EqualTo(1));
            Assert.That(saves[0].game.score, Is.EqualTo(4321));
            Assert.That(saves[0].version, Is.EqualTo(BrocoliRunSave.CurrentVersion));
            Assert.That(saves[0].savedAtTicks, Is.GreaterThan(0L));
            Assert.That(PlayerPrefs.HasKey(BrocoliSaveSystem.LegacySaveKey), Is.False);
        }

        [Test]
        public void AnUnreadableSlotIsDiscardedRatherThanListed()
        {
            WriteSave(0, DateTime.UtcNow);
            PlayerPrefs.SetString(BrocoliSaveSystem.SlotKey(1), "{not json");

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                "[Autosave] Discarding an unreadable checkpoint in slot 1."
            );
            List<BrocoliRunSave> saves = BrocoliSaveSystem.LoadAll();

            Assert.That(saves, Has.Count.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(BrocoliSaveSystem.SlotKey(1)), Is.False);
        }

        [Test]
        public void ARunWrittenByAnOlderSchemaIsUpgradedRatherThanDropped()
        {
            BrocoliRunSave older = CreateValidSave();
            older.version = 1;
            older.savedAtTicks = 0L;
            older.game.score = 777;
            PlayerPrefs.SetString(BrocoliSaveSystem.SlotKey(2), BrocoliSaveSystem.Serialize(older));

            List<BrocoliRunSave> saves = BrocoliSaveSystem.LoadAll();

            Assert.That(saves, Has.Count.EqualTo(1));
            Assert.That(saves[0].game.score, Is.EqualTo(777));
            Assert.That(saves[0].slot, Is.EqualTo(2));
            Assert.That(saves[0].version, Is.EqualTo(BrocoliRunSave.CurrentVersion));
            Assert.That(saves[0].savedAtTicks, Is.GreaterThan(0L));
        }

        [Test]
        public void ARunWrittenByANewerBuildIsLeftAloneRatherThanDeleted()
        {
            BrocoliRunSave newer = CreateValidSave();
            newer.slot = 0;
            newer.savedAtTicks = DateTime.UtcNow.Ticks;
            newer.version = BrocoliRunSave.CurrentVersion + 1;
            PlayerPrefs.SetString(BrocoliSaveSystem.SlotKey(0), BrocoliSaveSystem.Serialize(newer));

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                "[Autosave] Slot 0 was written by a newer build and is being left alone."
            );
            List<BrocoliRunSave> saves = BrocoliSaveSystem.LoadAll();

            Assert.That(saves, Is.Empty);
            Assert.That(PlayerPrefs.HasKey(BrocoliSaveSystem.SlotKey(0)), Is.True);
        }

        [Test]
        public void RunsStoredUnderTheOldKeyLayoutAreCarriedOver()
        {
            BrocoliRunSave stored = CreateValidSave();
            stored.slot = 3;
            stored.savedAtTicks = DateTime.UtcNow.AddHours(-1).Ticks;
            stored.game.score = 555;
            PlayerPrefs.SetString(
                BrocoliSaveSystem.InterimSlotKeyPrefix + 3,
                BrocoliSaveSystem.Serialize(stored)
            );

            List<BrocoliRunSave> saves = BrocoliSaveSystem.LoadAll();

            Assert.That(saves, Has.Count.EqualTo(1));
            Assert.That(saves[0].game.score, Is.EqualTo(555));
            Assert.That(saves[0].slot, Is.EqualTo(3));
            Assert.That(PlayerPrefs.HasKey(BrocoliSaveSystem.SlotKey(3)), Is.True);
            Assert.That(PlayerPrefs.HasKey(BrocoliSaveSystem.InterimSlotKeyPrefix + 3), Is.False);
        }

        private static void WriteSave(int slot, DateTime savedAtUtc)
        {
            BrocoliRunSave save = CreateValidSave();
            save.slot = slot;
            save.savedAtTicks = savedAtUtc.Ticks;
            PlayerPrefs.SetString(
                BrocoliSaveSystem.SlotKey(slot),
                BrocoliSaveSystem.Serialize(save)
            );
            PlayerPrefs.Save();
        }

        private static IEnumerable<string> StorageKeys()
        {
            for (int slot = 0; slot < BrocoliSaveSystem.MaxSaves; slot++)
            {
                yield return BrocoliSaveSystem.SlotKey(slot);
                yield return BrocoliSaveSystem.InterimSlotKeyPrefix + slot;
            }

            yield return BrocoliSaveSystem.LegacySaveKey;
        }

        private static BrocoliRunSave CreateValidSave()
        {
            return new BrocoliRunSave
            {
                playerPosition = new Vector3(3f, 0f, -7f),
                player = new BrocoliPlayerSave
                {
                    health = 75f,
                    maxHealth = 100f,
                    attackSpeed = 0.6f,
                    damage = 8f,
                    movementSpeed = 4f,
                    experience = 12f,
                    maxExperience = 30f,
                    level = 3f,
                    detectionRadius = 12f,
                    sprayRange = 4f,
                    sprayWidth = 20f,
                    sprayDamageMultiplier = 1f,
                    critChance = 5f,
                    critDamage = 1.5f,
                    dodgeChance = 0f,
                    armor = 0f,
                    healthRegen = 0f,
                    lifeSteal = 0f,
                },
                game = new BrocoliGameStateSave
                {
                    score = 120,
                    gameTime = 90f,
                    enemiesKilled = 7,
                },
                dungeon = new BrocoliDungeonSave { seed = 987, roomsVisited = 5 },
            };
        }
    }
}
