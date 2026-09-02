using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Exercises checkpointing against the live run. Autoplay deliberately keeps the
    /// autosave controller switched off -- a throwaway bot run must not claim one of
    /// the player's ten slots -- which would otherwise leave the entire persistence
    /// path untested by the harness that exists to test everything.
    ///
    /// So the probe writes only into a slot that was already empty, deletes it again,
    /// and restores the two preferences it had to move. When every slot is taken it
    /// falls back to a serialize/parse round trip rather than evicting a real run.
    /// </summary>
    internal static class AutoplaySaveProbe
    {
        private const string ControlPreferenceKey = "ShowVirtualController";

        internal delegate bool CaptureRun(out BrocoliRunSave save);

        internal static bool TryRoundTrip() => TryRoundTrip(BrocoliAutosaveController.TryCapture);

        internal static bool TryRoundTrip(CaptureRun capture)
        {
            if (!capture(out BrocoliRunSave live))
                return false;

            int activeSlot = PlayerPrefs.GetInt(BrocoliSaveSystem.ActiveSlotKey, -1);
            int controls = PlayerPrefs.GetInt(ControlPreferenceKey, 0);
            try
            {
                return BrocoliSaveSystem.BeginNewGame(live.mobileControls)
                    ? ThroughFreeSlot(live)
                    : ThroughSerialization(live);
            }
            finally
            {
                PlayerPrefs.SetInt(BrocoliSaveSystem.ActiveSlotKey, activeSlot);
                PlayerPrefs.SetInt(ControlPreferenceKey, controls);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The real path: write, read back, and free the slot again.</summary>
        private static bool ThroughFreeSlot(BrocoliRunSave live)
        {
            int slot = BrocoliSaveSystem.ActiveSlot;
            BrocoliSaveSystem.Save(live);
            bool restored =
                BrocoliSaveSystem.TryLoad(slot, out BrocoliRunSave loaded) && Matches(live, loaded);
            BrocoliSaveSystem.DeleteSave(slot);
            return restored;
        }

        private static bool ThroughSerialization(BrocoliRunSave live)
        {
            string json = BrocoliSaveSystem.Serialize(live);
            return BrocoliSaveSystem.TryDeserialize(json, out BrocoliRunSave loaded)
                && Matches(live, loaded);
        }

        /// <summary>
        /// Checks the fields a resumed run is actually rebuilt from. A checkpoint that
        /// round-trips its schema but loses the dungeon seed is a corrupt save that
        /// only shows up as a different dungeon on load.
        /// </summary>
        internal static bool Matches(BrocoliRunSave written, BrocoliRunSave read)
        {
            if (written == null || read == null || read.player == null || read.dungeon == null)
                return false;

            return Mathf.Approximately(written.player.health, read.player.health)
                && Mathf.Approximately(written.player.level, read.player.level)
                && Mathf.Approximately(written.player.experience, read.player.experience)
                && written.dungeon.seed == read.dungeon.seed
                && written.dungeon.roomsVisited == read.dungeon.roomsVisited
                && written.game.enemiesKilled == read.game.enemiesKilled
                && Vector3.Distance(written.playerPosition, read.playerPosition) < 0.01f;
        }
    }
}
