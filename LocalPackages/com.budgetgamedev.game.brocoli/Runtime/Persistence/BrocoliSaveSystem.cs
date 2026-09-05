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
    internal static partial class BrocoliSaveSystem
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

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
            GameplayDiagnostics.Record("save.checkpointed");
#endif
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
            if (slot < 0)
                return;

            DeleteSave(slot);

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && GAME_AUTOPLAY)
            GameplayDiagnostics.Record("save.dropped");
#endif
        }

        internal static string Serialize(BrocoliRunSave save) => JsonUtility.ToJson(save);

        internal static bool TryDeserialize(string json, out BrocoliRunSave save)
        {
            return Read(json, -1, out save) == ReadResult.Ok;
        }

        internal static bool TryDeserialize(
            string json,
            System.Func<string, BrocoliRunSave> deserialize,
            out BrocoliRunSave save
        ) => Read(json, -1, deserialize, out save) == ReadResult.Ok;

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
            return Read(json, slot, JsonUtility.FromJson<BrocoliRunSave>, out save);
        }

        private static ReadResult Read(
            string json,
            int slot,
            System.Func<string, BrocoliRunSave> deserialize,
            out BrocoliRunSave save
        )
        {
            save = null;
            if (string.IsNullOrWhiteSpace(json))
                return ReadResult.Unreadable;

            try
            {
                save = deserialize(json);
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
    }
}
