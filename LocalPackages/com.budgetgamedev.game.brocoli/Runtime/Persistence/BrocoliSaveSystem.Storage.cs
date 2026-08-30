using System;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal static partial class BrocoliSaveSystem
    {
        internal static bool IsValid(BrocoliRunSave save)
        {
            if (
                save == null
                || save.version != BrocoliRunSave.CurrentVersion
                || save.slot < 0
                || save.slot >= MaxSaves
                || save.savedAtTicks < 0
                || save.player == null
                || save.game == null
                || save.dungeon == null
                || save.dungeon.seed == 0
                || !IsFinite(save.playerPosition)
            )
            {
                return false;
            }

            BrocoliPlayerSave player = save.player;
            return IsPositive(player.health)
                && IsPositive(player.maxHealth)
                && player.health <= player.maxHealth + 0.01f
                && IsPositive(player.attackSpeed)
                && IsFinite(player.damage)
                // A run that cannot be moved cannot be played: resuming one strands
                // the player in the spawn room with nothing to do but reload.
                && IsPositive(player.movementSpeed)
                && IsFiniteNonNegative(player.experience)
                && IsPositive(player.maxExperience)
                && IsPositive(player.level)
                && IsFinite(player.detectionRadius)
                && IsFinite(player.sprayRange)
                && IsFiniteNonNegative(player.sprayWidth)
                && IsPositive(player.sprayDamageMultiplier)
                && IsFiniteNonNegative(player.critChance)
                && IsFinite(player.critDamage)
                && IsFiniteNonNegative(player.dodgeChance)
                && IsFinite(player.armor)
                && IsFinite(player.healthRegen)
                && IsFiniteNonNegative(player.lifeSteal)
                && HasValidBoosts(player)
                && save.game.score >= 0
                && IsFiniteNonNegative(save.game.gameTime)
                && save.game.enemiesKilled >= 0
                && save.dungeon.roomsVisited >= 0;
        }

        /// <summary>
        /// Moves runs stored the way an older build stored them into the current
        /// keys. Runs once per layout: each source key is cleared as it is read.
        /// </summary>
        private static void MigrateOlderLayouts()
        {
            MigrateInterimSlotKeys();
            MigrateSingleCheckpointKey();
        }

        private static void MigrateInterimSlotKeys()
        {
            bool moved = false;
            for (int slot = 0; slot < MaxSaves; slot++)
            {
                string key = InterimSlotKeyPrefix + slot;
                if (!PlayerPrefs.HasKey(key))
                    continue;

                string json = PlayerPrefs.GetString(key);
                PlayerPrefs.DeleteKey(key);
                moved = true;

                string destination = SlotKey(slot);
                if (
                    !PlayerPrefs.HasKey(destination)
                    && Read(json, slot, out BrocoliRunSave save) == ReadResult.Ok
                )
                {
                    PlayerPrefs.SetString(destination, Serialize(save));
                }
            }

            // The save list is read every time the panel refreshes, so the flush only
            // happens on the one run that actually finds something to move.
            if (moved)
                PlayerPrefs.Save();
        }

        private static void MigrateSingleCheckpointKey()
        {
            if (!PlayerPrefs.HasKey(LegacySaveKey))
                return;

            string json = PlayerPrefs.GetString(LegacySaveKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);

            int slot = FindFreeSlot();
            if (slot >= 0 && Read(json, slot, out BrocoliRunSave save) == ReadResult.Ok)
            {
                Write(save);
                ActiveSlot = slot;
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// The slot the current run owns. A run reached without the menu - the dungeon
        /// scene played straight from the editor - claims a free one on its first
        /// checkpoint rather than writing over somebody's save.
        /// </summary>
        private static int ClaimSlot()
        {
            int slot = ActiveSlot;
            if (slot >= 0 && slot < MaxSaves)
                return slot;

            slot = FindFreeSlot();
            if (slot < 0)
                return -1;

            ActiveSlot = slot;
            return slot;
        }

        private static int FindFreeSlot()
        {
            for (int slot = 0; slot < MaxSaves; slot++)
            {
                if (!TryLoad(slot, out _))
                    return slot;
            }

            return -1;
        }

        private static void Touch(BrocoliRunSave save)
        {
            save.savedAtTicks = DateTime.UtcNow.Ticks;
            Write(save);
        }

        private static void Write(BrocoliRunSave save)
        {
            PlayerPrefs.SetString(SlotKey(save.slot), Serialize(save));
            PlayerPrefs.Save();
        }

        private static void DeleteSlotKey(int slot)
        {
            string key = SlotKey(slot);
            if (!PlayerPrefs.HasKey(key))
                return;

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        internal static string SlotKey(int slot) => SlotKeyPrefix + slot;

        private static bool warnedSlotsFull;

        private static void WarnSlotsFull()
        {
            if (warnedSlotsFull)
                return;

            warnedSlotsFull = true;
            Debug.LogWarning(
                "[Autosave] Every save slot is taken and this run claimed none, so it is not "
                    + "being checkpointed. Delete a save from the menu to free one."
            );
        }

        private static bool HasValidBoosts(BrocoliPlayerSave player)
        {
            if (player.temporaryBoosts == null)
                return true;

            foreach (BrocoliTemporaryBoostSave boost in player.temporaryBoosts)
            {
                if (
                    boost == null
                    || !Enum.IsDefined(typeof(TemporaryBoostType), boost.type)
                    || !IsFinite(boost.amount)
                    || !IsPositive(boost.remainingTime)
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetControlPreference(bool mobileControls)
        {
            PlayerPrefs.SetInt(ControlPreferenceKey, mobileControls ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static bool IsPositive(float value) => value > 0f && IsFinite(value);

        private static bool IsFiniteNonNegative(float value) => value >= 0f && IsFinite(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
