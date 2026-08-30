using System;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Owns the player's resumable BROcoli runs. Each run lives in one of
    /// <see cref="MaxSaves"/> slots and is rewritten in place as it is played, so a
    /// player holds several runs at once and a full set has to be pruned by hand
    /// rather than silently losing its oldest entry.
    ///
    /// Saves outlive the build that wrote them. Changing what a checkpoint holds
    /// means bumping <see cref="BrocoliRunSave.CurrentVersion"/> and teaching
    /// <see cref="TryUpgrade"/> to carry the older shape forward - never changing
    /// the storage key, and never widening what counts as unreadable. A checkpoint
    /// written by a build newer than this one is left untouched rather than
    /// deleted, so running an older build for a while costs nothing.
    /// </summary>
    internal static class BrocoliSaveSystem
    {
        /// <summary>Runs a player can keep at once; a new one needs a free slot.</summary>
        internal const int MaxSaves = 10;

        // No version in the key: the schema version lives inside the payload, where
        // an upgrade can act on it. A versioned key would orphan every save the
        // day the schema moves.
        private const string SlotKeyPrefix = "Brocoli.Save.";
        internal const string ActiveSlotKey = "Brocoli.Save.ActiveSlot";

        // Storage layouts this replaced, read once and carried into the current one
        // so nobody loses a run to an update: a single checkpoint under its own key,
        // and the first slotted layout that put the schema version in the key.
        internal const string LegacySaveKey = "Brocoli.Autosave.v1";
        internal const string InterimSlotKeyPrefix = "Brocoli.Save.v2.";

        private const int SlotlessVersion = 1;

        private const string ControlPreferenceKey = "ShowVirtualController";

        private static BrocoliRunSave pendingContinue;

        /// <summary>The slot the running game checkpoints into; -1 when none is claimed.</summary>
        internal static int ActiveSlot
        {
            get => PlayerPrefs.GetInt(ActiveSlotKey, -1);
            private set
            {
                PlayerPrefs.SetInt(ActiveSlotKey, value);
                PlayerPrefs.Save();
            }
        }

        internal static bool HasAnySave => LoadAll().Count > 0;

        /// <summary>Whether another run can be started without deleting one first.</summary>
        internal static bool CanCreateSave => FindFreeSlot() >= 0;

        /// <summary>Every readable save, most recently played first.</summary>
        internal static List<BrocoliRunSave> LoadAll()
        {
            MigrateOlderLayouts();

            var saves = new List<BrocoliRunSave>();
            for (int slot = 0; slot < MaxSaves; slot++)
            {
                if (TryLoad(slot, out BrocoliRunSave save))
                    saves.Add(save);
            }

            saves.Sort((first, second) => second.savedAtTicks.CompareTo(first.savedAtTicks));
            return saves;
        }

        /// <summary>Claims a free slot for a fresh run. False when all of them are taken.</summary>
        internal static bool BeginNewGame(bool mobileControls)
        {
            int slot = FindFreeSlot();
            if (slot < 0)
                return false;

            pendingContinue = null;
            DeleteSlotKey(slot);
            ActiveSlot = slot;
            SetControlPreference(mobileControls);
            return true;
        }

        /// <summary>Arms the given slot's checkpoint for the dungeon scene to restore.</summary>
        internal static bool BeginContinue(int slot)
        {
            if (!TryLoad(slot, out BrocoliRunSave save))
                return false;

            pendingContinue = save;
            ActiveSlot = slot;
            SetControlPreference(save.mobileControls);

            // Loading counts as playing: the list reorders now rather than waiting
            // for the run's first checkpoint five seconds into the dungeon.
            Touch(save);
            return true;
        }

        internal static bool TryGetPendingContinue(out BrocoliRunSave save)
        {
            save = pendingContinue;
            return save != null;
        }

        internal static void FinishContinue()
        {
            pendingContinue = null;
        }

        /// <summary>Writes the live run into its slot, stamping it as the newest.</summary>
        internal static void Save(BrocoliRunSave save)
        {
            if (save == null)
                return;

            int slot = ClaimSlot();
            if (slot < 0)
            {
                WarnSlotsFull();
                return;
            }

            save.slot = slot;
            save.savedAtTicks = DateTime.UtcNow.Ticks;
            if (!IsValid(save))
            {
                Debug.LogWarning("[Autosave] Refused to write an invalid run checkpoint.");
                return;
            }

            Write(save);
        }

        internal static bool TryLoad(int slot, out BrocoliRunSave save)
        {
            save = null;
            if (slot < 0 || slot >= MaxSaves)
                return false;

            string key = SlotKey(slot);
            if (!PlayerPrefs.HasKey(key))
                return false;

            switch (Read(PlayerPrefs.GetString(key), slot, out save))
            {
                case ReadResult.Ok:
                    return true;

                case ReadResult.FromANewerBuild:
                    // Left where it is: whatever wrote it can still read it, and the
                    // player only has to open the newer build to get the run back.
                    Debug.LogWarning(
                        $"[Autosave] Slot {slot} was written by a newer build and is being left alone."
                    );
                    return false;

                default:
                    Debug.LogWarning(
                        $"[Autosave] Discarding an unreadable checkpoint in slot {slot}."
                    );
                    DeleteSlotKey(slot);
                    return false;
            }
        }

        /// <summary>Frees a slot. The player has to do this to make room for a new run.</summary>
        internal static void DeleteSave(int slot)
        {
            if (pendingContinue != null && pendingContinue.slot == slot)
                pendingContinue = null;

            DeleteSlotKey(slot);
            if (ActiveSlot == slot)
                ActiveSlot = -1;
        }

        /// <summary>Drops the run being played, which is what dying costs.</summary>
        internal static void DeleteActiveSave()
        {
            int slot = ActiveSlot;
            if (slot >= 0)
                DeleteSave(slot);
        }

        internal static string Serialize(BrocoliRunSave save) => JsonUtility.ToJson(save);

        internal static bool TryDeserialize(string json, out BrocoliRunSave save)
        {
            return Read(json, -1, out save) == ReadResult.Ok;
        }

        private enum ReadResult
        {
            Ok,
            Unreadable,

            /// <summary>Written by a build that knows a schema this one does not.</summary>
            FromANewerBuild,
        }

        /// <summary>
        /// Parses a stored checkpoint, brings it up to the current schema and checks
        /// it over. <paramref name="slot"/> is the slot it was found in, so a save
        /// from before slots existed knows where it now lives; pass -1 to keep
        /// whatever the payload already claims.
        /// </summary>
        private static ReadResult Read(string json, int slot, out BrocoliRunSave save)
        {
            save = null;
            if (string.IsNullOrWhiteSpace(json))
                return ReadResult.Unreadable;

            try
            {
                save = JsonUtility.FromJson<BrocoliRunSave>(json);
            }
            catch (ArgumentException)
            {
                save = null;
                return ReadResult.Unreadable;
            }

            if (save == null)
                return ReadResult.Unreadable;

            if (save.version > BrocoliRunSave.CurrentVersion)
            {
                save = null;
                return ReadResult.FromANewerBuild;
            }

            if (slot >= 0)
                save.slot = slot;

            if (TryUpgrade(save) && IsValid(save))
                return ReadResult.Ok;

            save = null;
            return ReadResult.Unreadable;
        }

        /// <summary>
        /// Walks a checkpoint forward one schema version at a time. Every bump of
        /// <see cref="BrocoliRunSave.CurrentVersion"/> adds a step here; that is what
        /// keeps a player's runs across an update.
        /// </summary>
        private static bool TryUpgrade(BrocoliRunSave save)
        {
            if (save.version == SlotlessVersion)
            {
                // Slots and timestamps arrived together. The run keeps the slot it
                // was read from and is dated now, so it sorts as the newest thing
                // the player touched rather than as undated.
                save.savedAtTicks = DateTime.UtcNow.Ticks;
                save.version = 2;
            }

            return save.version == BrocoliRunSave.CurrentVersion;
        }

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
